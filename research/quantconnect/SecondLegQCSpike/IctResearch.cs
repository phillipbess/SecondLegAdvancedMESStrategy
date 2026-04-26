using System;
using System.Globalization;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string IctModel = "silverbullet";
        private string IctLiquiditySet = "swing,or,pd";
        private int IctWindowStartMinutes = 30;
        private int IctWindowEndMinutes = 90;
        private int IctLiquidityLookbackBars = 20;
        private int IctMssLookbackBars = 5;
        private int IctMaxBarsAfterSweep = 12;
        private int IctMaxBarsAfterFvg = 8;
        private int IctStopBufferTicks = 2;
        private double IctMinSweepTicks = 1.0;
        private double IctMinDisplacementAtr = 0.50;
        private double IctMinFvgTicks = 1.0;
        private double IctEntryFvgPct = 0.50;
        private bool IctOneTradePerDay = true;

        private DateTime _ictSessionDate = DateTime.MinValue;
        private bool _ictTradedToday;
        private IctSweep _ictSweep = new IctSweep();
        private IctPending _ictPending = new IctPending();

        private void ConfigureIctResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsIctMode())
                return;

            IctModel = TextParameter("ictModel", IctModel).ToLowerInvariant();
            IctLiquiditySet = TextParameter("ictLiquiditySet", IctLiquiditySet).ToLowerInvariant();
            IctWindowStartMinutes = Math.Max(0, IntParameter("ictWindowStartMinutes", DefaultIctWindowStart()));
            IctWindowEndMinutes = Math.Max(IctWindowStartMinutes, IntParameter("ictWindowEndMinutes", DefaultIctWindowEnd()));
            IctLiquidityLookbackBars = Math.Max(3, IntParameter("ictLiquidityLookbackBars", IctLiquidityLookbackBars));
            IctMssLookbackBars = Math.Max(2, IntParameter("ictMssLookbackBars", IctMssLookbackBars));
            IctMaxBarsAfterSweep = Math.Max(1, IntParameter("ictMaxBarsAfterSweep", IctMaxBarsAfterSweep));
            IctMaxBarsAfterFvg = Math.Max(1, IntParameter("ictMaxBarsAfterFvg", IctMaxBarsAfterFvg));
            IctStopBufferTicks = Math.Max(0, IntParameter("ictStopBufferTicks", IctStopBufferTicks));
            IctMinSweepTicks = Math.Max(0.0, DoubleParameter("ictMinSweepTicks", IctMinSweepTicks));
            IctMinDisplacementAtr = Math.Max(0.0, DoubleParameter("ictMinDisplacementAtr", IctMinDisplacementAtr));
            IctMinFvgTicks = Math.Max(0.0, DoubleParameter("ictMinFvgTicks", IctMinFvgTicks));
            IctEntryFvgPct = Clamp(DoubleParameter("ictEntryFvgPct", IctEntryFvgPct), 0.0, 1.0);
            IctOneTradePerDay = BoolParameter("ictOneTradePerDay", IctOneTradePerDay);
            string exportPrefix = IsIctSequencedMode() ? "ictseq_export" : "ict_export";
            _tradeExportKey = $"{ProjectId}/{exportPrefix}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_model_{IctModel}_liq_{TextKey(IctLiquiditySet)}_win_{IctWindowStartMinutes}-{IctWindowEndMinutes}_disp_{ParamToken(IctMinDisplacementAtr)}_fvg_{ParamToken(IctMinFvgTicks)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private bool IsIctMode()
        {
            return IsIctSequencedMode()
                || string.Equals(EntryMode, "ict", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "ictsilverbullet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "ictjudas", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "ict2022", StringComparison.OrdinalIgnoreCase);
        }

        private int DefaultIctWindowStart()
        {
            if (IctModel == "judas")
                return 0;
            if (IctModel == "2022")
                return 0;
            return 30;
        }

        private int DefaultIctWindowEnd()
        {
            if (IctModel == "judas")
                return 45;
            if (IctModel == "2022")
                return 300;
            return 90;
        }

        private void TryIctResearch(BarSnapshot bar)
        {
            ResetIctSessionIfNeeded(bar);

            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_ictPending.IsActive)
            {
                TryStartIctTrade(bar);
                return;
            }

            if (IctOneTradePerDay && _ictTradedToday)
                return;

            int minutes = MinutesFromOpen(bar);
            if (minutes < IctWindowStartMinutes || minutes > IctWindowEndMinutes)
                return;

            if (_ictSweep.IsActive && bar.Index - _ictSweep.Bar > IctMaxBarsAfterSweep)
                _ictSweep = new IctSweep();

            if (!_ictSweep.IsActive)
            {
                TryCaptureIctSweep(bar);
                return;
            }

            TryCaptureIctDisplacementAndFvg(bar);
        }

        private void ResetIctSessionIfNeeded(BarSnapshot bar)
        {
            if (_ictSessionDate == bar.EndTime.Date)
                return;

            _ictSessionDate = bar.EndTime.Date;
            _ictTradedToday = false;
            _ictSweep = new IctSweep();
            _ictPending = new IctPending();
        }

        private void TryCaptureIctSweep(BarSnapshot bar)
        {
            if (_bars.Count <= IctLiquidityLookbackBars + 1)
                return;

            var prior = _bars.Take(_bars.Count - 1).Reverse().Take(IctLiquidityLookbackBars).ToList();
            double swingHigh = prior.Max(x => x.High);
            double swingLow = prior.Min(x => x.Low);
            double sweepBuffer = IctMinSweepTicks * TickSize;

            if (IctLiquiditySet.Contains("swing"))
            {
                TryRecordIctLevelSweep(bar, swingHigh, StructureKind.Resistance, "SWING_H", sweepBuffer);
                TryRecordIctLevelSweep(bar, swingLow, StructureKind.Support, "SWING_L", sweepBuffer);
            }

            if (IctLiquiditySet.Contains("or") && _openingRangeComplete)
            {
                TryRecordIctLevelSweep(bar, _openingRangeHigh, StructureKind.Resistance, "ORH", sweepBuffer);
                TryRecordIctLevelSweep(bar, _openingRangeLow, StructureKind.Support, "ORL", sweepBuffer);
            }

            if (IctLiquiditySet.Contains("pd"))
            {
                TryRecordIctLevelSweep(bar, _priorRthHigh, StructureKind.Resistance, "PDH", sweepBuffer);
                TryRecordIctLevelSweep(bar, _priorRthLow, StructureKind.Support, "PDL", sweepBuffer);
            }
        }

        private void TryRecordIctLevelSweep(BarSnapshot bar, double level, StructureKind kind, string label, double buffer)
        {
            if (double.IsNaN(level) || _ictSweep.IsActive)
                return;

            bool sweptHigh = kind == StructureKind.Resistance && bar.High >= level + buffer;
            bool sweptLow = kind == StructureKind.Support && bar.Low <= level - buffer;
            if (!sweptHigh && !sweptLow)
                return;

            _ictSweep = new IctSweep
            {
                Bias = sweptHigh ? Bias.Short : Bias.Long,
                Bar = bar.Index,
                Time = bar.EndTime,
                Level = level,
                Extreme = sweptHigh ? bar.High : bar.Low,
                Label = label
            };
        }

        private void TryCaptureIctDisplacementAndFvg(BarSnapshot bar)
        {
            if (_bars.Count < Math.Max(IctMssLookbackBars + 2, 3))
                return;

            var prior = _bars.Take(_bars.Count - 1).Reverse().Take(IctMssLookbackBars).ToList();
            double priorHigh = prior.Max(x => x.High);
            double priorLow = prior.Min(x => x.Low);
            double body = Math.Abs(bar.Close - bar.Open);
            bool enoughDisplacement = bar.Atr <= 0.0 || body >= IctMinDisplacementAtr * bar.Atr;
            if (!enoughDisplacement)
                return;

            bool mss = _ictSweep.Bias == Bias.Long
                ? bar.Close > priorHigh
                : bar.Close < priorLow;
            if (!mss)
                return;

            if (!TryGetIctFvg(bar, _ictSweep.Bias, out double fvgLow, out double fvgHigh))
                return;

            double entry = RoundToTick(fvgLow + (fvgHigh - fvgLow) * IctEntryFvgPct);
            double stop = _ictSweep.Bias == Bias.Long
                ? _ictSweep.Extreme - IctStopBufferTicks * TickSize
                : _ictSweep.Extreme + IctStopBufferTicks * TickSize;

            _ictPending = new IctPending
            {
                Bias = _ictSweep.Bias,
                SignalBar = bar.Index,
                SignalTime = bar.EndTime,
                ExpiryBar = bar.Index + IctMaxBarsAfterFvg,
                Entry = entry,
                Stop = RoundToTick(stop),
                FvgLow = fvgLow,
                FvgHigh = fvgHigh,
                Structure = $"ICT_{IctModel}_{_ictSweep.Label}_FVG",
                AtrAtSignal = bar.Atr,
                SignalHour = bar.EndTime.Hour,
                MinutesFromOpen = MinutesFromOpen(bar),
                SignalClosePct = CloseLocationPct(bar),
                SignalBodyPct = BodyPct(bar),
                AtrRatio = bar.AtrRatio,
                SlopeAtrPct = bar.SlopeAtrPct
            };

            _armedSignals++;
            MonthStats month = GetMonth(bar.EndTime);
            month.Armed++;
            if (_ictPending.Bias == Bias.Long)
            {
                _longArmed++;
                month.LongArmed++;
            }
            else
            {
                _shortArmed++;
                month.ShortArmed++;
            }
        }

        private bool TryGetIctFvg(BarSnapshot bar, Bias bias, out double fvgLow, out double fvgHigh)
        {
            fvgLow = double.NaN;
            fvgHigh = double.NaN;
            BarSnapshot twoBack = _bars[_bars.Count - 3];
            double minGap = IctMinFvgTicks * TickSize;

            if (bias == Bias.Long && bar.Low > twoBack.High + minGap)
            {
                fvgLow = twoBack.High;
                fvgHigh = bar.Low;
                return true;
            }
            if (bias == Bias.Short && bar.High < twoBack.Low - minGap)
            {
                fvgLow = bar.High;
                fvgHigh = twoBack.Low;
                return true;
            }
            return false;
        }

        private void TryStartIctTrade(BarSnapshot bar)
        {
            if (bar.Index > _ictPending.ExpiryBar)
            {
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                _expiredSignals++;
                GetMonth(bar.EndTime).Expired++;
                return;
            }

            bool touched = _ictPending.Bias == Bias.Long
                ? bar.Low <= _ictPending.Entry
                : bar.High >= _ictPending.Entry;
            if (!touched)
                return;

            if (!SideAllowed(_ictPending.Bias))
            {
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                return;
            }

            double risk = Math.Abs(_ictPending.Entry - _ictPending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("IctRiskTooSmall");
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                return;
            }
            if (bar.Atr > 0.0 && risk / bar.Atr > MaxStopAtrMultiple)
            {
                Block("IctStopTooWide");
                _ictPending = new IctPending();
                _ictSweep = new IctSweep();
                return;
            }

            _triggeredSignals++;
            GetMonth(bar.EndTime).Triggered++;
            _ictTradedToday = true;
            _virtualTrades++;
            _virtualTrade = new VirtualTrade
            {
                TradeId = _virtualTrades,
                Bias = _ictPending.Bias,
                Entry = _ictPending.Entry,
                Stop = _ictPending.Stop,
                TouchProbePrice = _ictPending.Bias == Bias.Long ? _ictPending.Entry + TouchProbeR * risk : _ictPending.Entry - TouchProbeR * risk,
                TargetPrice = _ictPending.Bias == Bias.Long ? _ictPending.Entry + ProfitTargetR * risk : _ictPending.Entry - ProfitTargetR * risk,
                TargetR = ProfitTargetR,
                TriggerBar = bar.Index,
                ExpiryBar = bar.Index + MaxOutcomeBars,
                SignalTime = _ictPending.SignalTime,
                TriggerTime = bar.EndTime,
                Structure = _ictPending.Structure,
                RiskDollars = riskPerContract,
                Quantity = quantity,
                AtrAtPlan = _ictPending.AtrAtSignal,
                StopAtrMultiple = bar.Atr > 0.0 ? risk / bar.Atr : 0.0,
                SignalHour = _ictPending.SignalHour,
                MinutesFromOpen = _ictPending.MinutesFromOpen,
                SignalClosePct = _ictPending.SignalClosePct,
                SignalBodyPct = _ictPending.SignalBodyPct,
                AtrRatio = _ictPending.AtrRatio,
                SlopeAtrPct = _ictPending.SlopeAtrPct,
                MaxAdverseR = 0.0,
                MaxFavorableR = 0.0
            };

            Debug($"ICT_TRADE_OPEN time={bar.EndTime:yyyy-MM-dd HH:mm} model={IctModel} side={_virtualTrade.Bias} entry={_virtualTrade.Entry:0.00} stop={_virtualTrade.Stop:0.00} setup={_virtualTrade.Structure}");
            _ictPending = new IctPending();
            _ictSweep = new IctSweep();
            ObserveVirtualTrade(bar);
        }

        private static string TextKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "none"
                : value.Replace(",", "-").Replace(" ", string.Empty);
        }

        private sealed class IctSweep
        {
            public Bias Bias = Bias.Neutral;
            public int Bar = -1;
            public DateTime Time;
            public double Level;
            public double Extreme;
            public string Label = string.Empty;
            public bool IsActive => Bar >= 0 && Bias != Bias.Neutral;
        }

        private sealed class IctPending
        {
            public Bias Bias = Bias.Neutral;
            public int SignalBar = -1;
            public DateTime SignalTime;
            public int ExpiryBar;
            public double Entry;
            public double Stop;
            public double FvgLow;
            public double FvgHigh;
            public string Structure = string.Empty;
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
