using System;
using System.Globalization;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string OpeningAuctionModel = "both";
        private int OpeningAuctionConfirmBars = 2;
        private int OpeningAuctionFailureBars = 3;
        private int OpeningAuctionMinSignalMinutes = 0;
        private int OpeningAuctionMaxSignalMinutes = 90;
        private double OpeningAuctionMinRangeAtr = 0.0;
        private double OpeningAuctionMaxRangeAtr = 999.0;
        private double OpeningAuctionMinRoomR = -1.0;
        private double OpeningAuctionMaxRoomR = -1.0;
        private int OpeningAuctionStopBufferTicks = 2;
        private bool OpeningAuctionOneTradePerDay = true;

        private DateTime _oaSessionDate = DateTime.MinValue;
        private bool _oaTradedToday;
        private int _oaLongAcceptCount;
        private int _oaShortAcceptCount;
        private Bias _oaBreakBias = Bias.Neutral;
        private int _oaBreakBar = -1;
        private double _oaBreakExtreme = double.NaN;
        private OpeningAuctionPending _oaPending = new OpeningAuctionPending();

        private void ConfigureOpeningAuctionResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsOpeningAuctionMode())
                return;

            OpeningAuctionModel = TextParameter("openingAuctionModel", OpeningAuctionModel).ToLowerInvariant();
            OpeningAuctionConfirmBars = Math.Max(1, IntParameter("openingAuctionConfirmBars", OpeningAuctionConfirmBars));
            OpeningAuctionFailureBars = Math.Max(1, IntParameter("openingAuctionFailureBars", OpeningAuctionFailureBars));
            OpeningAuctionMinSignalMinutes = Math.Max(0, IntParameter("openingAuctionMinSignalMinutes", OpeningAuctionMinSignalMinutes));
            OpeningAuctionMaxSignalMinutes = Math.Max(Math.Max(OpeningRangeMinutes, OpeningAuctionMinSignalMinutes), IntParameter("openingAuctionMaxSignalMinutes", OpeningAuctionMaxSignalMinutes));
            OpeningAuctionMinRangeAtr = Math.Max(0.0, DoubleParameter("openingAuctionMinRangeAtr", OpeningAuctionMinRangeAtr));
            OpeningAuctionMaxRangeAtr = Math.Max(OpeningAuctionMinRangeAtr, DoubleParameter("openingAuctionMaxRangeAtr", OpeningAuctionMaxRangeAtr));
            OpeningAuctionMinRoomR = DoubleParameter("openingAuctionMinRoomR", OpeningAuctionMinRoomR);
            OpeningAuctionMaxRoomR = DoubleParameter("openingAuctionMaxRoomR", OpeningAuctionMaxRoomR);
            OpeningAuctionStopBufferTicks = Math.Max(0, IntParameter("openingAuctionStopBufferTicks", OpeningAuctionStopBufferTicks));
            OpeningAuctionOneTradePerDay = BoolParameter("openingAuctionOneTradePerDay", OpeningAuctionOneTradePerDay);
            _tradeExportKey = $"{ProjectId}/opening_auction_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_model_{OpeningAuctionModel}_confirm_{OpeningAuctionConfirmBars}_fail_{OpeningAuctionFailureBars}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}_or_{OpeningRangeMinutes}_sig_{OpeningAuctionMinSignalMinutes}-{OpeningAuctionMaxSignalMinutes}_range_{ParamToken(OpeningAuctionMinRangeAtr)}-{ParamToken(OpeningAuctionMaxRangeAtr)}_room_{ParamToken(OpeningAuctionMinRoomR)}-{ParamToken(OpeningAuctionMaxRoomR)}.csv";
        }

        private bool IsOpeningAuctionMode()
        {
            return string.Equals(EntryMode, "openauction", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "openingauction", StringComparison.OrdinalIgnoreCase);
        }

        private void TryOpeningAuctionResearch(BarSnapshot bar)
        {
            ResetOpeningAuctionSessionIfNeeded(bar);

            if (_virtualTrade.IsActive)
            {
                ObserveVirtualTrade(bar);
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_oaPending.IsActive)
            {
                StartOpeningAuctionTrade(bar, _oaPending);
                _oaPending = new OpeningAuctionPending();
                return;
            }

            if (!_openingRangeComplete || double.IsNaN(_openingRangeHigh) || double.IsNaN(_openingRangeLow))
                return;
            if (OpeningAuctionOneTradePerDay && _oaTradedToday)
                return;

            int minutes = MinutesFromOpen(bar);
            if (minutes <= OpeningRangeMinutes || minutes < OpeningAuctionMinSignalMinutes || minutes > OpeningAuctionMaxSignalMinutes)
                return;

            double openingRange = _openingRangeHigh - _openingRangeLow;
            double rangeAtr = bar.Atr > 0.0 ? openingRange / bar.Atr : 0.0;
            if (rangeAtr < OpeningAuctionMinRangeAtr || rangeAtr > OpeningAuctionMaxRangeAtr)
            {
                Block("OaRangeFilter");
                return;
            }

            TrackOpeningAuctionBreaks(bar);
            if (TryArmOpeningAuctionFailure(bar, openingRange))
                return;
            TryArmOpeningAuctionAcceptance(bar, openingRange);
        }

        private void ResetOpeningAuctionSessionIfNeeded(BarSnapshot bar)
        {
            if (_oaSessionDate == bar.EndTime.Date)
                return;

            _oaSessionDate = bar.EndTime.Date;
            _oaTradedToday = false;
            _oaLongAcceptCount = 0;
            _oaShortAcceptCount = 0;
            _oaBreakBias = Bias.Neutral;
            _oaBreakBar = -1;
            _oaBreakExtreme = double.NaN;
            _oaPending = new OpeningAuctionPending();
        }

        private void TrackOpeningAuctionBreaks(BarSnapshot bar)
        {
            if (bar.High > _openingRangeHigh && (_oaBreakBias != Bias.Long || bar.High > _oaBreakExtreme))
            {
                _oaBreakBias = Bias.Long;
                _oaBreakBar = bar.Index;
                _oaBreakExtreme = bar.High;
            }
            if (bar.Low < _openingRangeLow && (_oaBreakBias != Bias.Short || bar.Low < _oaBreakExtreme))
            {
                _oaBreakBias = Bias.Short;
                _oaBreakBar = bar.Index;
                _oaBreakExtreme = bar.Low;
            }
        }

        private bool TryArmOpeningAuctionFailure(BarSnapshot bar, double openingRange)
        {
            if (OpeningAuctionModel != "failed" && OpeningAuctionModel != "both")
                return false;
            if (_oaBreakBias == Bias.Neutral || bar.Index - _oaBreakBar > OpeningAuctionFailureBars)
                return false;

            bool reclaimedInside = bar.Close < _openingRangeHigh && bar.Close > _openingRangeLow;
            if (!reclaimedInside)
                return false;

            if (_oaBreakBias == Bias.Long)
                return ArmOpeningAuctionSignal(bar, Bias.Short, _oaBreakExtreme + OpeningAuctionStopBufferTicks * TickSize, "OA_FAIL_ORH", openingRange);
            return ArmOpeningAuctionSignal(bar, Bias.Long, _oaBreakExtreme - OpeningAuctionStopBufferTicks * TickSize, "OA_FAIL_ORL", openingRange);
        }

        private void TryArmOpeningAuctionAcceptance(BarSnapshot bar, double openingRange)
        {
            if (OpeningAuctionModel != "accepted" && OpeningAuctionModel != "both")
                return;

            if (bar.Close > _openingRangeHigh)
            {
                _oaLongAcceptCount++;
                _oaShortAcceptCount = 0;
                if (_oaLongAcceptCount >= OpeningAuctionConfirmBars)
                    ArmOpeningAuctionSignal(bar, Bias.Long, _openingRangeHigh - OpeningAuctionStopBufferTicks * TickSize, "OA_ACCEPT_ORH", openingRange);
                return;
            }
            if (bar.Close < _openingRangeLow)
            {
                _oaShortAcceptCount++;
                _oaLongAcceptCount = 0;
                if (_oaShortAcceptCount >= OpeningAuctionConfirmBars)
                    ArmOpeningAuctionSignal(bar, Bias.Short, _openingRangeLow + OpeningAuctionStopBufferTicks * TickSize, "OA_ACCEPT_ORL", openingRange);
                return;
            }

            _oaLongAcceptCount = 0;
            _oaShortAcceptCount = 0;
        }

        private bool ArmOpeningAuctionSignal(BarSnapshot bar, Bias bias, double stop, string label, double openingRange)
        {
            if (!SideAllowed(bias))
                return false;

            _oaPending = new OpeningAuctionPending
            {
                Bias = bias,
                SignalBar = bar.Index,
                SignalTime = bar.EndTime,
                Stop = RoundToTick(stop),
                Structure = label,
                OpeningRangeR = openingRange,
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

        private void StartOpeningAuctionTrade(BarSnapshot bar, OpeningAuctionPending pending)
        {
            if (!pending.IsActive)
                return;

            double entry = RoundToTick(bar.Open);
            double risk = Math.Abs(entry - pending.Stop);
            double riskPerContract = risk * PointValue;
            double quantity = riskPerContract > 0.0 ? Math.Floor(RiskPerTrade / riskPerContract) : 0.0;
            if (risk <= 0.0 || quantity <= 0.0)
            {
                Block("OaRiskTooSmall");
                return;
            }
            if (bar.Atr > 0.0 && risk / bar.Atr > MaxStopAtrMultiple)
            {
                Block("OaStopTooWide");
                return;
            }
            double roomToStructureR = pending.OpeningRangeR / Math.Max(risk, TickSize);
            if (OpeningAuctionMinRoomR >= 0.0 && roomToStructureR < OpeningAuctionMinRoomR)
            {
                Block("OaRoomTooSmall");
                return;
            }
            if (OpeningAuctionMaxRoomR >= 0.0 && roomToStructureR > OpeningAuctionMaxRoomR)
            {
                Block("OaRoomTooLarge");
                return;
            }

            _triggeredSignals++;
            GetMonth(bar.EndTime).Triggered++;
            _oaTradedToday = true;
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
                RoomToStructureR = roomToStructureR,
                SignalHour = pending.SignalHour,
                MinutesFromOpen = pending.MinutesFromOpen,
                SignalClosePct = pending.SignalClosePct,
                SignalBodyPct = pending.SignalBodyPct,
                AtrRatio = pending.AtrRatio,
                SlopeAtrPct = pending.SlopeAtrPct,
                MaxAdverseR = 0.0,
                MaxFavorableR = 0.0
            };

            Debug($"OA_TRADE_OPEN time={bar.EndTime:yyyy-MM-dd HH:mm} side={_virtualTrade.Bias} entry={entry:0.00} stop={pending.Stop:0.00} label={pending.Structure} orRange={pending.OpeningRangeR:0.00}");
            ObserveVirtualTrade(bar);
        }

        private sealed class OpeningAuctionPending
        {
            public Bias Bias = Bias.Neutral;
            public int SignalBar = -1;
            public DateTime SignalTime;
            public double Stop;
            public string Structure = string.Empty;
            public double OpeningRangeR;
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
