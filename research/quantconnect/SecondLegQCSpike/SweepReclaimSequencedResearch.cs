using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string SweepSeqLevels = "PDH,PDL,ORH,ORL,SWING_H,SWING_L";
        private HashSet<string> _sweepSeqLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int SweepSeqMinTicks = 1;
        private int SweepSeqMaxTicks = 16;
        private int SweepSeqReclaimBars = 3;
        private int SweepSeqMinSignalMinutes = 15;
        private int SweepSeqMaxSignalMinutes = 300;
        private int SweepSeqStopBufferTicks = 2;
        private int SweepSeqEntryOffsetTicks = 1;
        private int SweepSeqTriggerExpiryBars = 4;
        private double SweepSeqMinReclaimClosePct = 0.55;
        private double SweepSeqMinReclaimBodyPct = 0.0;
        private double SweepSeqMinDisplacementAtr = 0.0;
        private bool SweepSeqOneTradePerLevel = true;
        private string SweepSeqEntryType = "stop";

        private DateTime _sweepSeqSessionDate = DateTime.MinValue;
        private DateTime _sweepSeqEntryExpiry = DateTime.MinValue;
        private DateTime _sweepSeqTradeExpiry = DateTime.MinValue;
        private readonly Dictionary<string, SweepSeqProbe> _sweepSeqProbes = new Dictionary<string, SweepSeqProbe>();
        private readonly HashSet<string> _sweepSeqTradedLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private SweepSeqPending _sweepSeqPending = new SweepSeqPending();

        private bool IsSweepReclaimSequencedMode()
        {
            return string.Equals(EntryMode, "sweepreclaimsequenced", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "sweepseq", StringComparison.OrdinalIgnoreCase);
        }

        private void ConfigureSweepReclaimSequencedResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsSweepReclaimSequencedMode())
                return;

            SweepSeqLevels = TextParameter("sweepSeqLevels", SweepSeqLevels);
            _sweepSeqLevels = TextSetParameter("sweepSeqLevels");
            if (_sweepSeqLevels.Count == 0)
            {
                foreach (string token in SweepSeqLevels.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    _sweepSeqLevels.Add(token.Trim().ToUpperInvariant());
            }

            SweepSeqMinTicks = Math.Max(1, IntParameter("sweepSeqMinTicks", SweepSeqMinTicks));
            SweepSeqMaxTicks = Math.Max(SweepSeqMinTicks, IntParameter("sweepSeqMaxTicks", SweepSeqMaxTicks));
            SweepSeqReclaimBars = Math.Max(1, IntParameter("sweepSeqReclaimBars", SweepSeqReclaimBars));
            SweepSeqMinSignalMinutes = Math.Max(0, IntParameter("sweepSeqMinSignalMinutes", SweepSeqMinSignalMinutes));
            SweepSeqMaxSignalMinutes = Math.Max(SweepSeqMinSignalMinutes, IntParameter("sweepSeqMaxSignalMinutes", SweepSeqMaxSignalMinutes));
            SweepSeqStopBufferTicks = Math.Max(0, IntParameter("sweepSeqStopBufferTicks", SweepSeqStopBufferTicks));
            SweepSeqEntryOffsetTicks = Math.Max(0, IntParameter("sweepSeqEntryOffsetTicks", SweepSeqEntryOffsetTicks));
            SweepSeqTriggerExpiryBars = Math.Max(1, IntParameter("sweepSeqTriggerExpiryBars", SweepSeqTriggerExpiryBars));
            SweepSeqMinReclaimClosePct = Clamp(DoubleParameter("sweepSeqMinReclaimClosePct", SweepSeqMinReclaimClosePct), 0.0, 0.95);
            SweepSeqMinReclaimBodyPct = Clamp(DoubleParameter("sweepSeqMinReclaimBodyPct", SweepSeqMinReclaimBodyPct), 0.0, 0.95);
            SweepSeqMinDisplacementAtr = Math.Max(0.0, DoubleParameter("sweepSeqMinDisplacementAtr", SweepSeqMinDisplacementAtr));
            SweepSeqOneTradePerLevel = BoolParameter("sweepSeqOneTradePerLevel", SweepSeqOneTradePerLevel);
            SweepSeqEntryType = TextParameter("sweepSeqEntryType", SweepSeqEntryType).ToLowerInvariant();
            if (SweepSeqEntryType != "limit")
                SweepSeqEntryType = "stop";

            _tradeExportKey = $"{ProjectId}/sweepseq_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_levels_{TextKeyToken(_sweepSeqLevels)}_entry_{SweepSeqEntryType}_min_{SweepSeqMinTicks}_max_{SweepSeqMaxTicks}_reclaim_{SweepSeqReclaimBars}_close_{ParamToken(SweepSeqMinReclaimClosePct)}_body_{ParamToken(SweepSeqMinReclaimBodyPct)}_disp_{ParamToken(SweepSeqMinDisplacementAtr)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void TrySweepReclaimSequencedSignal(BarSnapshot bar)
        {
            ResetSweepSeqSessionIfNeeded(bar);

            if (_virtualTrade.IsActive || _sweepSeqPending.IsActive)
                return;

            int minutes = MinutesFromOpen(bar);
            if (minutes < SweepSeqMinSignalMinutes || minutes > SweepSeqMaxSignalMinutes)
                return;

            foreach (var level in BuildStructureLevels(bar))
            {
                if (!_sweepSeqLevels.Contains(level.Label))
                    continue;
                if (SweepSeqOneTradePerLevel && _sweepSeqTradedLevels.Contains(level.Label))
                    continue;
                if (TryTrackSweepSeqLevel(bar, level))
                    return;
            }
        }

        private void ProcessSweepReclaimSequencedExecution(TradeBar input)
        {
            if (_virtualTrade.IsActive)
            {
                ObserveSweepSeqTrade(input);
                return;
            }

            if (!_sweepSeqPending.IsActive)
                return;

            if (input.EndTime <= _sweepSeqPending.SignalTime)
                return;

            if (input.EndTime > _sweepSeqEntryExpiry)
            {
                ExpireSweepSeqPending(input.EndTime);
                return;
            }

            bool invalidated = _sweepSeqPending.Bias == Bias.Long
                ? (double)input.Low <= _sweepSeqPending.Stop
                : (double)input.High >= _sweepSeqPending.Stop;
            if (invalidated)
            {
                Block("SweepSeqPreFillInvalid");
                ExpireSweepSeqPending(input.EndTime);
                return;
            }

            bool touched = _sweepSeqPending.EntryType == "limit"
                ? LimitEntryTouched(input)
                : StopEntryTouched(input);
            if (!touched)
                return;

            StartSweepSeqTrade(input);
        }

        private void ResetSweepSeqSessionIfNeeded(BarSnapshot bar)
        {
            if (_sweepSeqSessionDate == bar.EndTime.Date)
                return;

            _sweepSeqSessionDate = bar.EndTime.Date;
            _sweepSeqProbes.Clear();
            _sweepSeqTradedLevels.Clear();
            _sweepSeqPending = new SweepSeqPending();
        }

        private bool TryTrackSweepSeqLevel(BarSnapshot bar, StructureLevel level)
        {
            if (level.Kind == StructureKind.Resistance)
                return TryTrackSweepSeqResistance(bar, level);
            return TryTrackSweepSeqSupport(bar, level);
        }

        private bool TryTrackSweepSeqResistance(BarSnapshot bar, StructureLevel level)
        {
            double depthTicks = (bar.High - level.Price) / TickSize;
            string key = level.Label;
            if (depthTicks >= SweepSeqMinTicks && depthTicks <= SweepSeqMaxTicks)
                _sweepSeqProbes[key] = new SweepSeqProbe(level.Label, level.Price, Bias.Short, bar.Index, bar.High);

            if (!_sweepSeqProbes.TryGetValue(key, out SweepSeqProbe probe) || probe.Bias != Bias.Short)
                return false;
            if (bar.Index - probe.SweepBar > SweepSeqReclaimBars || depthTicks > SweepSeqMaxTicks)
            {
                _sweepSeqProbes.Remove(key);
                return false;
            }
            if (bar.Close >= level.Price || !PassesSweepSeqReclaimBar(bar, Bias.Short))
                return false;

            double stop = RoundToTick(probe.SweepExtreme + SweepSeqStopBufferTicks * TickSize);
            double entry = SweepSeqEntryType == "limit"
                ? RoundToTick(level.Price)
                : RoundToTick(bar.Low - SweepSeqEntryOffsetTicks * TickSize);
            return ArmSweepSeqPending(bar, probe, entry, stop);
        }

        private bool TryTrackSweepSeqSupport(BarSnapshot bar, StructureLevel level)
        {
            double depthTicks = (level.Price - bar.Low) / TickSize;
            string key = level.Label;
            if (depthTicks >= SweepSeqMinTicks && depthTicks <= SweepSeqMaxTicks)
                _sweepSeqProbes[key] = new SweepSeqProbe(level.Label, level.Price, Bias.Long, bar.Index, bar.Low);

            if (!_sweepSeqProbes.TryGetValue(key, out SweepSeqProbe probe) || probe.Bias != Bias.Long)
                return false;
            if (bar.Index - probe.SweepBar > SweepSeqReclaimBars || depthTicks > SweepSeqMaxTicks)
            {
                _sweepSeqProbes.Remove(key);
                return false;
            }
            if (bar.Close <= level.Price || !PassesSweepSeqReclaimBar(bar, Bias.Long))
                return false;

            double stop = RoundToTick(probe.SweepExtreme - SweepSeqStopBufferTicks * TickSize);
            double entry = SweepSeqEntryType == "limit"
                ? RoundToTick(level.Price)
                : RoundToTick(bar.High + SweepSeqEntryOffsetTicks * TickSize);
            return ArmSweepSeqPending(bar, probe, entry, stop);
        }

        private bool PassesSweepSeqReclaimBar(BarSnapshot bar, Bias bias)
        {
            if (!SideAllowed(bias))
                return false;
            if (BodyPct(bar) < SweepSeqMinReclaimBodyPct)
                return false;
            double closePct = CloseLocationPct(bar);
            if (bias == Bias.Long && closePct < SweepSeqMinReclaimClosePct)
                return false;
            if (bias == Bias.Short && closePct > 1.0 - SweepSeqMinReclaimClosePct)
                return false;
            if (SweepSeqMinDisplacementAtr > 0.0)
            {
                double range = bar.High - bar.Low;
                if (bar.Atr <= 0.0 || range < SweepSeqMinDisplacementAtr * bar.Atr)
                    return false;
            }
            return true;
        }

        private bool ArmSweepSeqPending(BarSnapshot bar, SweepSeqProbe probe, double entry, double stop)
        {
            double risk = Math.Abs(entry - stop);
            if (risk <= TickSize * 0.5)
            {
                Block("SweepSeqRiskTooSmall");
                _sweepSeqProbes.Remove(probe.Label);
                return false;
            }

            _sweepSeqPending = new SweepSeqPending
            {
                Bias = probe.Bias,
                SignalBar = bar.Index,
                SignalTime = bar.EndTime,
                Entry = entry,
                Stop = stop,
                EntryType = SweepSeqEntryType,
                Structure = $"SSEQ_{probe.Label}_{SweepSeqEntryType.ToUpperInvariant()}",
                LevelLabel = probe.Label,
                LevelPrice = probe.LevelPrice,
                SweepExtreme = probe.SweepExtreme,
                AtrAtSignal = bar.Atr,
                SignalHour = bar.EndTime.Hour,
                MinutesFromOpen = MinutesFromOpen(bar),
                SignalClosePct = CloseLocationPct(bar),
                SignalBodyPct = BodyPct(bar),
                AtrRatio = bar.AtrRatio,
                SlopeAtrPct = bar.SlopeAtrPct
            };
            _sweepSeqEntryExpiry = bar.EndTime.AddMinutes(SweepSeqTriggerExpiryBars * BarMinutes);

            _armedSignals++;
            if (probe.Bias == Bias.Long)
                _longArmed++;
            else
                _shortArmed++;
            MonthStats month = GetMonth(bar.EndTime);
            month.Armed++;
            if (probe.Bias == Bias.Long)
                month.LongArmed++;
            else
                month.ShortArmed++;

            _sweepSeqProbes.Remove(probe.Label);
            Debug($"SWEEP_SEQ_ARM time={bar.EndTime:yyyy-MM-dd HH:mm} side={probe.Bias} level={probe.Label} entryType={SweepSeqEntryType} entry={entry:0.00} stop={stop:0.00} sweep={probe.SweepExtreme:0.00}");
            return true;
        }

        private bool StopEntryTouched(TradeBar input)
        {
            return _sweepSeqPending.Bias == Bias.Long
                ? (double)input.High >= _sweepSeqPending.Entry
                : (double)input.Low <= _sweepSeqPending.Entry;
        }

        private bool LimitEntryTouched(TradeBar input)
        {
            return _sweepSeqPending.Bias == Bias.Long
                ? (double)input.Low <= _sweepSeqPending.Entry
                : (double)input.High >= _sweepSeqPending.Entry;
        }

        private void StartSweepSeqTrade(TradeBar input)
        {
            double risk = Math.Abs(_sweepSeqPending.Entry - _sweepSeqPending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("SweepSeqRiskTooSmall");
                _sweepSeqPending = new SweepSeqPending();
                return;
            }
            if (_sweepSeqPending.AtrAtSignal > 0.0 && risk / _sweepSeqPending.AtrAtSignal > MaxStopAtrMultiple)
            {
                Block("SweepSeqStopTooWide");
                _sweepSeqPending = new SweepSeqPending();
                return;
            }

            _triggeredSignals++;
            GetMonth(input.EndTime).Triggered++;
            _virtualTrades++;
            _sweepSeqTradedLevels.Add(_sweepSeqPending.LevelLabel);
            _sweepSeqTradeExpiry = input.EndTime.AddMinutes(MaxOutcomeBars * BarMinutes);
            _virtualTrade = new VirtualTrade
            {
                TradeId = _virtualTrades,
                Bias = _sweepSeqPending.Bias,
                Entry = _sweepSeqPending.Entry,
                Stop = _sweepSeqPending.Stop,
                TouchProbePrice = _sweepSeqPending.Bias == Bias.Long ? _sweepSeqPending.Entry + TouchProbeR * risk : _sweepSeqPending.Entry - TouchProbeR * risk,
                TargetPrice = _sweepSeqPending.Bias == Bias.Long ? _sweepSeqPending.Entry + ProfitTargetR * risk : _sweepSeqPending.Entry - ProfitTargetR * risk,
                TargetR = ProfitTargetR,
                TriggerBar = _bars.Count,
                ExpiryBar = _bars.Count + MaxOutcomeBars,
                SignalTime = _sweepSeqPending.SignalTime,
                TriggerTime = input.EndTime,
                Structure = _sweepSeqPending.Structure,
                RiskDollars = riskPerContract,
                Quantity = quantity,
                AtrAtPlan = _sweepSeqPending.AtrAtSignal,
                StopAtrMultiple = _sweepSeqPending.AtrAtSignal > 0.0 ? risk / _sweepSeqPending.AtrAtSignal : 0.0,
                RoomToStructureR = Math.Abs(_sweepSeqPending.Entry - _sweepSeqPending.LevelPrice) / Math.Max(risk, TickSize),
                SignalHour = _sweepSeqPending.SignalHour,
                MinutesFromOpen = _sweepSeqPending.MinutesFromOpen,
                SignalClosePct = _sweepSeqPending.SignalClosePct,
                SignalBodyPct = _sweepSeqPending.SignalBodyPct,
                AtrRatio = _sweepSeqPending.AtrRatio,
                SlopeAtrPct = _sweepSeqPending.SlopeAtrPct,
                MaxAdverseR = 0.0,
                MaxFavorableR = 0.0
            };

            Debug($"SWEEP_SEQ_OPEN time={input.EndTime:yyyy-MM-dd HH:mm:ss} side={_virtualTrade.Bias} entry={_virtualTrade.Entry:0.00} stop={_virtualTrade.Stop:0.00} target={_virtualTrade.TargetPrice:0.00} setup={_virtualTrade.Structure}");
            _sweepSeqPending = new SweepSeqPending();
        }

        private void ObserveSweepSeqTrade(TradeBar input)
        {
            if (input.EndTime <= _virtualTrade.TriggerTime)
                return;

            BarSnapshot bar = SequencedSnapshot(input);
            bool stopHit = _virtualTrade.Bias == Bias.Long ? bar.Low <= _virtualTrade.Stop : bar.High >= _virtualTrade.Stop;
            bool touchProbeHit = _virtualTrade.Bias == Bias.Long ? bar.High >= _virtualTrade.TouchProbePrice : bar.Low <= _virtualTrade.TouchProbePrice;
            bool targetHit = _virtualTrade.Bias == Bias.Long ? bar.High >= _virtualTrade.TargetPrice : bar.Low <= _virtualTrade.TargetPrice;
            UpdateExcursion(bar);

            if (touchProbeHit && !_virtualTrade.TouchedProbe)
            {
                _virtualTrade.TouchedProbe = true;
                _touchOneR++;
            }

            if (TryCloseAmbiguousOutcome(bar, stopHit, targetHit))
                return;

            if (stopHit)
            {
                CloseVirtualTrade(bar, "Stop", -1.0);
                return;
            }

            if (targetHit)
            {
                CloseVirtualTrade(bar, "Target", _virtualTrade.TargetR);
                return;
            }

            if (input.EndTime > _sweepSeqTradeExpiry || input.EndTime.TimeOfDay >= new TimeSpan(15, 55, 0))
            {
                double risk = Math.Max(TickSize, Math.Abs(_virtualTrade.Entry - _virtualTrade.Stop));
                double openR = _virtualTrade.Bias == Bias.Long
                    ? ((double)input.Close - _virtualTrade.Entry) / risk
                    : (_virtualTrade.Entry - (double)input.Close) / risk;
                CloseVirtualTrade(bar, "Timeout", openR);
            }
        }

        private void ExpireSweepSeqPending(DateTime time)
        {
            _expiredSignals++;
            GetMonth(time).Expired++;
            _sweepSeqPending = new SweepSeqPending();
        }

        private sealed class SweepSeqProbe
        {
            public SweepSeqProbe(string label, double levelPrice, Bias bias, int sweepBar, double sweepExtreme)
            {
                Label = label;
                LevelPrice = levelPrice;
                Bias = bias;
                SweepBar = sweepBar;
                SweepExtreme = sweepExtreme;
            }

            public string Label;
            public double LevelPrice;
            public Bias Bias;
            public int SweepBar;
            public double SweepExtreme;
        }

        private sealed class SweepSeqPending
        {
            public Bias Bias = Bias.Neutral;
            public int SignalBar = -1;
            public DateTime SignalTime;
            public double Entry;
            public double Stop;
            public string EntryType = string.Empty;
            public string Structure = string.Empty;
            public string LevelLabel = string.Empty;
            public double LevelPrice;
            public double SweepExtreme;
            public double AtrAtSignal;
            public int SignalHour;
            public int MinutesFromOpen;
            public double SignalClosePct;
            public double SignalBodyPct;
            public double AtrRatio;
            public double SlopeAtrPct;
            public bool IsActive => SignalBar >= 0 && Bias != Bias.Neutral;
        }
    }
}
