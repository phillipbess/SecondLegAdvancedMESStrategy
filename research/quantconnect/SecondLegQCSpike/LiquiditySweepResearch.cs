using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string LiquiditySweepLevels = "pdh,pdl,orh,orl";
        private int LiquiditySweepMinTicks = 2;
        private int LiquiditySweepMaxTicks = 12;
        private int LiquiditySweepReclaimBars = 3;
        private int LiquiditySweepMinSignalMinutes = 15;
        private int LiquiditySweepMaxSignalMinutes = 300;
        private int LiquiditySweepStopBufferTicks = 2;
        private bool LiquiditySweepOneTradePerLevel = true;

        private DateTime _lsSessionDate = DateTime.MinValue;
        private readonly Dictionary<string, LiquiditySweepProbe> _lsProbes = new Dictionary<string, LiquiditySweepProbe>();
        private readonly HashSet<string> _lsTradedLevels = new HashSet<string>();
        private LiquiditySweepPending _lsPending = new LiquiditySweepPending();

        private bool IsLiquiditySweepMode()
        {
            return string.Equals(EntryMode, "liquiditysweep", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "liqsweep", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "sweepreclaim", StringComparison.OrdinalIgnoreCase);
        }

        private void ConfigureLiquiditySweepResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsLiquiditySweepMode())
                return;

            LiquiditySweepLevels = TextParameter("liquiditySweepLevels", LiquiditySweepLevels).ToLowerInvariant();
            LiquiditySweepMinTicks = Math.Max(1, IntParameter("liquiditySweepMinTicks", LiquiditySweepMinTicks));
            LiquiditySweepMaxTicks = Math.Max(LiquiditySweepMinTicks, IntParameter("liquiditySweepMaxTicks", LiquiditySweepMaxTicks));
            LiquiditySweepReclaimBars = Math.Max(1, IntParameter("liquiditySweepReclaimBars", LiquiditySweepReclaimBars));
            LiquiditySweepMinSignalMinutes = Math.Max(0, IntParameter("liquiditySweepMinSignalMinutes", LiquiditySweepMinSignalMinutes));
            LiquiditySweepMaxSignalMinutes = Math.Max(LiquiditySweepMinSignalMinutes, IntParameter("liquiditySweepMaxSignalMinutes", LiquiditySweepMaxSignalMinutes));
            LiquiditySweepStopBufferTicks = Math.Max(0, IntParameter("liquiditySweepStopBufferTicks", LiquiditySweepStopBufferTicks));
            LiquiditySweepOneTradePerLevel = BoolParameter("liquiditySweepOneTradePerLevel", LiquiditySweepOneTradePerLevel);
            string levelsToken = LiquiditySweepLevels.Replace(",", "-").Replace(" ", string.Empty);
            _tradeExportKey = $"{ProjectId}/liquidity_sweep_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_levels_{levelsToken}_min_{LiquiditySweepMinTicks}_max_{LiquiditySweepMaxTicks}_reclaim_{LiquiditySweepReclaimBars}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void TryLiquiditySweepResearch(BarSnapshot bar)
        {
            ResetLiquiditySweepSessionIfNeeded(bar);

            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_lsPending.IsActive)
            {
                StartLiquiditySweepTrade(bar, _lsPending);
                _lsPending = new LiquiditySweepPending();
                return;
            }

            int minutes = MinutesFromOpen(bar);
            if (minutes < LiquiditySweepMinSignalMinutes || minutes > LiquiditySweepMaxSignalMinutes)
                return;

            foreach (var level in BuildStructureLevels(bar))
            {
                string key = level.Label.ToLowerInvariant();
                if (!LiquiditySweepLevels.Contains(key))
                    continue;
                if (LiquiditySweepOneTradePerLevel && _lsTradedLevels.Contains(level.Label))
                    continue;
                if (TryTrackLiquiditySweep(bar, level))
                    return;
            }
        }

        private void ResetLiquiditySweepSessionIfNeeded(BarSnapshot bar)
        {
            if (_lsSessionDate == bar.EndTime.Date)
                return;

            _lsSessionDate = bar.EndTime.Date;
            _lsProbes.Clear();
            _lsTradedLevels.Clear();
            _lsPending = new LiquiditySweepPending();
        }

        private bool TryTrackLiquiditySweep(BarSnapshot bar, StructureLevel level)
        {
            if (level.Kind == StructureKind.Resistance)
                return TryTrackResistanceSweep(bar, level);
            return TryTrackSupportSweep(bar, level);
        }

        private bool TryTrackResistanceSweep(BarSnapshot bar, StructureLevel level)
        {
            double depthTicks = (bar.High - level.Price) / TickSize;
            if (depthTicks >= LiquiditySweepMinTicks && depthTicks <= LiquiditySweepMaxTicks)
                _lsProbes[level.Label] = new LiquiditySweepProbe(level.Label, level.Price, Bias.Short, bar.Index, bar.High);

            if (!_lsProbes.TryGetValue(level.Label, out LiquiditySweepProbe probe) || probe.Bias != Bias.Short)
                return false;
            if (bar.Index - probe.SweepBar > LiquiditySweepReclaimBars || depthTicks > LiquiditySweepMaxTicks)
            {
                _lsProbes.Remove(level.Label);
                return false;
            }
            if (bar.Close >= level.Price)
                return false;

            return ArmLiquiditySweepSignal(bar, probe, probe.SweepExtreme + LiquiditySweepStopBufferTicks * TickSize);
        }

        private bool TryTrackSupportSweep(BarSnapshot bar, StructureLevel level)
        {
            double depthTicks = (level.Price - bar.Low) / TickSize;
            if (depthTicks >= LiquiditySweepMinTicks && depthTicks <= LiquiditySweepMaxTicks)
                _lsProbes[level.Label] = new LiquiditySweepProbe(level.Label, level.Price, Bias.Long, bar.Index, bar.Low);

            if (!_lsProbes.TryGetValue(level.Label, out LiquiditySweepProbe probe) || probe.Bias != Bias.Long)
                return false;
            if (bar.Index - probe.SweepBar > LiquiditySweepReclaimBars || depthTicks > LiquiditySweepMaxTicks)
            {
                _lsProbes.Remove(level.Label);
                return false;
            }
            if (bar.Close <= level.Price)
                return false;

            return ArmLiquiditySweepSignal(bar, probe, probe.SweepExtreme - LiquiditySweepStopBufferTicks * TickSize);
        }

        private bool ArmLiquiditySweepSignal(BarSnapshot bar, LiquiditySweepProbe probe, double stop)
        {
            if (!SideAllowed(probe.Bias))
                return false;

            _lsPending = new LiquiditySweepPending
            {
                Bias = probe.Bias,
                SignalBar = bar.Index,
                SignalTime = bar.EndTime,
                Stop = RoundToTick(stop),
                Structure = $"LSR_{probe.Label}",
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
            _lsProbes.Remove(probe.Label);
            return true;
        }

        private void StartLiquiditySweepTrade(BarSnapshot bar, LiquiditySweepPending pending)
        {
            double entry = RoundToTick(bar.Open);
            double risk = Math.Abs(entry - pending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("LsRiskTooSmall");
                return;
            }
            if (bar.Atr > 0.0 && risk / bar.Atr > MaxStopAtrMultiple)
            {
                Block("LsStopTooWide");
                return;
            }

            _triggeredSignals++;
            GetMonth(bar.EndTime).Triggered++;
            _virtualTrades++;
            _lsTradedLevels.Add(pending.Structure.Replace("LSR_", string.Empty));
            _virtualTrade = new VirtualTrade
            {
                TradeId = _virtualTrades,
                Bias = pending.Bias,
                Entry = entry,
                Stop = pending.Stop,
                TouchProbePrice = pending.Bias == Bias.Long ? entry + TouchProbeR * risk : entry - TouchProbeR * risk,
                TargetPrice = pending.Bias == Bias.Long ? entry + ProfitTargetR * risk : entry - ProfitTargetR * risk,
                TargetR = ProfitTargetR,
                TriggerBar = bar.Index,
                ExpiryBar = bar.Index + MaxOutcomeBars,
                SignalTime = pending.SignalTime,
                TriggerTime = bar.EndTime,
                Structure = pending.Structure,
                RiskDollars = riskPerContract,
                Quantity = quantity,
                AtrAtPlan = pending.AtrAtSignal,
                StopAtrMultiple = bar.Atr > 0.0 ? risk / bar.Atr : 0.0,
                RoomToStructureR = Math.Abs(entry - pending.LevelPrice) / Math.Max(risk, TickSize),
                SignalHour = pending.SignalHour,
                MinutesFromOpen = pending.MinutesFromOpen,
                SignalClosePct = pending.SignalClosePct,
                SignalBodyPct = pending.SignalBodyPct,
                AtrRatio = pending.AtrRatio,
                SlopeAtrPct = pending.SlopeAtrPct,
                MaxAdverseR = 0.0,
                MaxFavorableR = 0.0
            };
            ObserveVirtualTrade(bar);
        }

        private sealed class LiquiditySweepProbe
        {
            public LiquiditySweepProbe(string label, double levelPrice, Bias bias, int sweepBar, double sweepExtreme)
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

        private sealed class LiquiditySweepPending
        {
            public Bias Bias = Bias.Neutral;
            public int SignalBar = -1;
            public DateTime SignalTime;
            public double Stop;
            public string Structure = string.Empty;
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
