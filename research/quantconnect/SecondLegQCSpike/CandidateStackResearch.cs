using System;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private string CandidateStackOpeningSide = "both";
        private string CandidateStackAfternoonSide = "long";

        private bool IsCandidateStackMode()
        {
            return string.Equals(EntryMode, "candidatestack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "amoarstack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(EntryMode, "oaramstack", StringComparison.OrdinalIgnoreCase);
        }

        private void ConfigureCandidateStackResearch(DateTime startDate, DateTime endDate)
        {
            if (!IsCandidateStackMode())
                return;

            CandidateStackOpeningSide = TextParameter("candidateStackOpeningSide", CandidateStackOpeningSide).ToLowerInvariant();
            CandidateStackAfternoonSide = TextParameter("candidateStackAfternoonSide", CandidateStackAfternoonSide).ToLowerInvariant();

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

            AfternoonMomentumMeasureMinutes = Math.Max(OpeningRangeMinutes, IntParameter("afternoonMomentumMeasureMinutes", AfternoonMomentumMeasureMinutes));
            AfternoonMomentumStartMinutes = Math.Max(AfternoonMomentumMeasureMinutes, IntParameter("afternoonMomentumStartMinutes", AfternoonMomentumStartMinutes));
            AfternoonMomentumEndMinutes = Math.Max(AfternoonMomentumStartMinutes, IntParameter("afternoonMomentumEndMinutes", AfternoonMomentumEndMinutes));
            AfternoonMomentumMinMoveAtr = Math.Max(0.0, DoubleParameter("afternoonMomentumMinMoveAtr", AfternoonMomentumMinMoveAtr));
            AfternoonMomentumStopAtr = Math.Max(0.25, DoubleParameter("afternoonMomentumStopAtr", AfternoonMomentumStopAtr));

            _tradeExportKey = $"{ProjectId}/candidate_stack_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_oa_{CandidateStackOpeningSide}_am_{CandidateStackAfternoonSide}_or_{OpeningRangeMinutes}_oaRange_{ParamToken(OpeningAuctionMinRangeAtr)}-{ParamToken(OpeningAuctionMaxRangeAtr)}_oaRoom_{ParamToken(OpeningAuctionMinRoomR)}-{ParamToken(OpeningAuctionMaxRoomR)}_amMove_{ParamToken(AfternoonMomentumMinMoveAtr)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void TryCandidateStackResearch(BarSnapshot bar)
        {
            ResetOpeningAuctionSessionIfNeeded(bar);
            ResetOpeningDriveSessionIfNeeded(bar);

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
                if (_virtualTrade.IsActive)
                    return;
            }

            if (_odPending.IsActive)
            {
                StartOpeningDriveTrade(bar, _odPending);
                _odPending = new OpeningDrivePending();
                if (_virtualTrade.IsActive)
                    return;
            }

            string originalSide = SideFilter;
            try
            {
                int minutes = MinutesFromOpen(bar);
                if (minutes <= OpeningAuctionMaxSignalMinutes)
                {
                    SideFilter = CandidateStackOpeningSide;
                    TryOpeningAuctionResearch(bar);
                    if (_oaPending.IsActive || _virtualTrade.IsActive)
                        return;
                }

                SideFilter = CandidateStackAfternoonSide;
                TryAfternoonMomentumResearch(bar);
            }
            finally
            {
                SideFilter = originalSide;
            }
        }
    }
}
