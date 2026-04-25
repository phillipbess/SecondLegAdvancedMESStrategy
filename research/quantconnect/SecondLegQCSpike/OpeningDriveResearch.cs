using System;
using System.Globalization;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private double OpeningDriveMinRangeAtr = 0.8;
        private double OpeningDriveClosePct = 0.7;
        private double OpeningDrivePullbackAtr = 0.25;
        private int OpeningDriveMaxSignalMinutes = 180;
        private int OpeningDriveStopBufferTicks = 2;
        private bool OpeningDriveOneTradePerDay = true;
        private int AfternoonMomentumMeasureMinutes = 60;
        private int AfternoonMomentumStartMinutes = 300;
        private int AfternoonMomentumEndMinutes = 345;
        private double AfternoonMomentumMinMoveAtr = 1.0;
        private double AfternoonMomentumStopAtr = 0.75;
        private string MomentumResearchMode = string.Empty;

        private DateTime _odSessionDate = DateTime.MinValue;
        private bool _odRangeCaptured;
        private bool _odTradedToday;
        private Bias _odBias = Bias.Neutral;
        private double _odHigh = double.NaN;
        private double _odLow = double.NaN;
        private double _odPullbackExtreme = double.NaN;
        private bool _odPullbackSeen;
        private double _odSessionOpen = double.NaN;
        private OpeningDrivePending _odPending = new OpeningDrivePending();

        private bool IsOpeningDriveMode()
        {
            return string.Equals(EntryMode, "openingdrive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "drivepullback", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "odpullback", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "afternoonmomentum", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "afternooncompression", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "compressionbreakout", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brookstfo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brookstrendpullback", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brooksopenreversal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brooksor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brookslabelpack", StringComparison.OrdinalIgnoreCase);
        }

        private void ConfigureOpeningDriveResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsOpeningDriveMode())
                return;

            OpeningDriveMinRangeAtr = Math.Max(0.0, DoubleParameter("openingDriveMinRangeAtr", OpeningDriveMinRangeAtr));
            OpeningDriveClosePct = Clamp(DoubleParameter("openingDriveClosePct", OpeningDriveClosePct), 0.5, 0.95);
            OpeningDrivePullbackAtr = Math.Max(0.0, DoubleParameter("openingDrivePullbackAtr", OpeningDrivePullbackAtr));
            OpeningDriveMaxSignalMinutes = Math.Max(OpeningRangeMinutes, IntParameter("openingDriveMaxSignalMinutes", OpeningDriveMaxSignalMinutes));
            OpeningDriveStopBufferTicks = Math.Max(0, IntParameter("openingDriveStopBufferTicks", OpeningDriveStopBufferTicks));
            OpeningDriveOneTradePerDay = BoolParameter("openingDriveOneTradePerDay", OpeningDriveOneTradePerDay);
            AfternoonMomentumMeasureMinutes = Math.Max(OpeningRangeMinutes, IntParameter("afternoonMomentumMeasureMinutes", AfternoonMomentumMeasureMinutes));
            AfternoonMomentumStartMinutes = Math.Max(AfternoonMomentumMeasureMinutes, IntParameter("afternoonMomentumStartMinutes", AfternoonMomentumStartMinutes));
            AfternoonMomentumEndMinutes = Math.Max(AfternoonMomentumStartMinutes, IntParameter("afternoonMomentumEndMinutes", AfternoonMomentumEndMinutes));
            AfternoonMomentumMinMoveAtr = Math.Max(0.0, DoubleParameter("afternoonMomentumMinMoveAtr", AfternoonMomentumMinMoveAtr));
            AfternoonMomentumStopAtr = Math.Max(0.25, DoubleParameter("afternoonMomentumStopAtr", AfternoonMomentumStopAtr));
            MomentumResearchMode = string.Equals(EntryMode, "afternooncompression", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "compressionbreakout", StringComparison.OrdinalIgnoreCase)
                    ? "compression"
                    : string.Empty;
            if (string.Equals(EntryMode, "brookstfo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brookstrendpullback", StringComparison.OrdinalIgnoreCase))
                MomentumResearchMode = "brooks";
            if (string.Equals(EntryMode, "brooksopenreversal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "brooksor", StringComparison.OrdinalIgnoreCase))
                MomentumResearchMode = "brooksor";
            if (string.Equals(EntryMode, "brookslabelpack", StringComparison.OrdinalIgnoreCase))
                MomentumResearchMode = "labelpack";
            ConfigureAfternoonCompressionResearch(startDate, endDate);
            ConfigureBrooksTrendPullbackResearch(startDate, endDate);
            ConfigureBrooksOpeningReversalResearch(startDate, endDate);
            ConfigureBrooksLabelPackResearch(startDate, endDate);
            if (MomentumResearchMode != "compression" && MomentumResearchMode != "brooks" && MomentumResearchMode != "brooksor" && MomentumResearchMode != "labelpack")
                _tradeExportKey = $"{ProjectId}/opening_drive_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_or_{OpeningRangeMinutes}_minatr_{ParamToken(OpeningDriveMinRangeAtr)}_close_{ParamToken(OpeningDriveClosePct)}_pb_{ParamToken(OpeningDrivePullbackAtr)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void TryOpeningDriveResearch(BarSnapshot bar)
        {
            ResetOpeningDriveSessionIfNeeded(bar);

            if (string.Equals(EntryMode, "afternoonmomentum", StringComparison.OrdinalIgnoreCase))
            {
                TryAfternoonMomentumResearch(bar);
                return;
            }
            if (MomentumResearchMode == "compression")
            {
                TryAfternoonCompressionResearch(bar);
                return;
            }
            if (MomentumResearchMode == "brooks")
            {
                TryBrooksTrendPullbackResearch(bar);
                return;
            }
            if (MomentumResearchMode == "brooksor")
            {
                TryBrooksOpeningReversalResearch(bar);
                return;
            }
            if (MomentumResearchMode == "labelpack")
            {
                TryBrooksLabelPackResearch(bar);
                return;
            }

            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_odPending.IsActive)
            {
                StartOpeningDriveTrade(bar, _odPending);
                _odPending = new OpeningDrivePending();
                return;
            }

            if (!_openingRangeComplete || double.IsNaN(_openingRangeHigh) || double.IsNaN(_openingRangeLow))
                return;

            CaptureOpeningDriveBias(bar);
            if (_odBias == Bias.Neutral || OpeningDriveOneTradePerDay && _odTradedToday)
                return;

            int minutes = MinutesFromOpen(bar);
            if (minutes <= OpeningRangeMinutes || minutes > OpeningDriveMaxSignalMinutes)
                return;

            TrackOpeningDrivePullback(bar);
        }

        private void ResetOpeningDriveSessionIfNeeded(BarSnapshot bar)
        {
            if (_odSessionDate == bar.EndTime.Date)
                return;

            _odSessionDate = bar.EndTime.Date;
            _odRangeCaptured = false;
            _odTradedToday = false;
            _odBias = Bias.Neutral;
            _odHigh = double.NaN;
            _odLow = double.NaN;
            _odPullbackExtreme = double.NaN;
            _odPullbackSeen = false;
            _odSessionOpen = bar.Open;
            _odPending = new OpeningDrivePending();
            ResetAfternoonCompressionSession();
            ResetBrooksTrendPullbackSession();
            ResetBrooksOpeningReversalSession();
            ResetBrooksLabelPackSession();
        }

        private void TryAfternoonMomentumResearch(BarSnapshot bar)
        {
            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_odPending.IsActive)
            {
                StartOpeningDriveTrade(bar, _odPending);
                _odPending = new OpeningDrivePending();
                return;
            }

            int minutes = MinutesFromOpen(bar);
            if (double.IsNaN(_odSessionOpen) || bar.Atr <= 0.0)
                return;

            if (!_odRangeCaptured && minutes >= AfternoonMomentumMeasureMinutes)
            {
                double moveAtr = (bar.Close - _odSessionOpen) / Math.Max(bar.Atr, TickSize);
                if (moveAtr >= AfternoonMomentumMinMoveAtr)
                    _odBias = Bias.Long;
                else if (moveAtr <= -AfternoonMomentumMinMoveAtr)
                    _odBias = Bias.Short;
                else
                    Block("AmWeakMorning");
                _odRangeCaptured = true;
            }

            if (_odBias == Bias.Neutral || _odTradedToday && OpeningDriveOneTradePerDay)
                return;
            if (minutes < AfternoonMomentumStartMinutes || minutes > AfternoonMomentumEndMinutes)
                return;
            if (!SideAllowed(_odBias))
                return;

            _odHigh = _openingRangeHigh;
            _odLow = _openingRangeLow;
            if (_odBias == Bias.Long && _openingRangeComplete && bar.Close > _openingRangeHigh)
                ArmOpeningDriveSignal(bar, Bias.Long, bar.Close - AfternoonMomentumStopAtr * bar.Atr, "AM_CONT_LONG");
            else if (_odBias == Bias.Short && _openingRangeComplete && bar.Close < _openingRangeLow)
                ArmOpeningDriveSignal(bar, Bias.Short, bar.Close + AfternoonMomentumStopAtr * bar.Atr, "AM_CONT_SHORT");
        }

        private void CaptureOpeningDriveBias(BarSnapshot bar)
        {
            if (_odRangeCaptured)
                return;

            double range = _openingRangeHigh - _openingRangeLow;
            if (bar.Atr <= 0.0 || range / bar.Atr < OpeningDriveMinRangeAtr || range <= 0.0)
            {
                Block("OdWeakDrive");
                _odRangeCaptured = true;
                return;
            }

            double closePct = (bar.Close - _openingRangeLow) / Math.Max(range, TickSize);
            if (closePct >= OpeningDriveClosePct)
                _odBias = Bias.Long;
            else if (closePct <= 1.0 - OpeningDriveClosePct)
                _odBias = Bias.Short;
            else
                Block("OdNoDriveClose");

            _odHigh = _openingRangeHigh;
            _odLow = _openingRangeLow;
            _odRangeCaptured = true;
        }

        private void TrackOpeningDrivePullback(BarSnapshot bar)
        {
            if (_bars.Count < 2)
                return;

            BarSnapshot previous = PreviousBar();
            if (_odBias == Bias.Long)
            {
                if (!_odPullbackSeen && _odHigh - bar.Low >= OpeningDrivePullbackAtr * Math.Max(bar.Atr, TickSize))
                {
                    _odPullbackSeen = true;
                    _odPullbackExtreme = bar.Low;
                }
                if (_odPullbackSeen)
                {
                    _odPullbackExtreme = Math.Min(_odPullbackExtreme, bar.Low);
                    if (bar.Close > previous.High)
                        ArmOpeningDriveSignal(bar, Bias.Long, _odPullbackExtreme - OpeningDriveStopBufferTicks * TickSize, "OD_PULLBACK_LONG");
                }
                return;
            }

            if (!_odPullbackSeen && bar.High - _odLow >= OpeningDrivePullbackAtr * Math.Max(bar.Atr, TickSize))
            {
                _odPullbackSeen = true;
                _odPullbackExtreme = bar.High;
            }
            if (_odPullbackSeen)
            {
                _odPullbackExtreme = Math.Max(_odPullbackExtreme, bar.High);
                if (bar.Close < previous.Low)
                    ArmOpeningDriveSignal(bar, Bias.Short, _odPullbackExtreme + OpeningDriveStopBufferTicks * TickSize, "OD_PULLBACK_SHORT");
            }
        }

        private bool ArmOpeningDriveSignal(BarSnapshot bar, Bias bias, double stop, string label)
        {
            if (!SideAllowed(bias))
                return false;

            _odPending = new OpeningDrivePending
            {
                Bias = bias,
                SignalBar = bar.Index,
                SignalTime = bar.EndTime,
                Stop = RoundToTick(stop),
                Structure = label,
                AtrAtSignal = bar.Atr,
                SignalHour = bar.EndTime.Hour,
                MinutesFromOpen = MinutesFromOpen(bar),
                SignalClosePct = CloseLocationPct(bar),
                SignalBodyPct = BodyPct(bar),
                AtrRatio = bar.AtrRatio,
                SlopeAtrPct = bar.SlopeAtrPct
            };

            _armedSignals++;
            if (bias == Bias.Long)
                _longArmed++;
            else
                _shortArmed++;
            MonthStats month = GetMonth(bar.EndTime);
            month.Armed++;
            if (bias == Bias.Long)
                month.LongArmed++;
            else
                month.ShortArmed++;
            return true;
        }

        private void StartOpeningDriveTrade(BarSnapshot bar, OpeningDrivePending pending)
        {
            double entry = RoundToTick(bar.Open);
            double risk = Math.Abs(entry - pending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("OdRiskTooSmall");
                return;
            }
            if (bar.Atr > 0.0 && risk / bar.Atr > MaxStopAtrMultiple)
            {
                Block("OdStopTooWide");
                return;
            }

            _triggeredSignals++;
            GetMonth(bar.EndTime).Triggered++;
            _odTradedToday = true;
            _virtualTrades++;
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
                RoomToStructureR = (_odHigh - _odLow) / Math.Max(risk, TickSize),
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

        private sealed class OpeningDrivePending
        {
            public Bias Bias = Bias.Neutral;
            public int SignalBar = -1;
            public DateTime SignalTime;
            public double Stop;
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
