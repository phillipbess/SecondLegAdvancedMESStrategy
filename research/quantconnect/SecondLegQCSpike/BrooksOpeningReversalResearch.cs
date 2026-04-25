using System;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private int BrooksOrMaxSignalMinutes = 90;
        private double BrooksOrMinSweepTicks = 2.0;
        private double BrooksOrStopBufferTicks = 2.0;
        private string BrooksOrLevels = "prior,or";
        private bool _borArmedToday;

        private void ConfigureBrooksOpeningReversalResearch(DateTime startDate, DateTime endDate)
        {
            if (MomentumResearchMode != "brooksor")
                return;

            BrooksOrMaxSignalMinutes = Math.Max(OpeningRangeMinutes, IntParameter("brooksOrMaxSignalMinutes", BrooksOrMaxSignalMinutes));
            BrooksOrMinSweepTicks = Math.Max(0.0, DoubleParameter("brooksOrMinSweepTicks", BrooksOrMinSweepTicks));
            BrooksOrStopBufferTicks = Math.Max(0.0, DoubleParameter("brooksOrStopBufferTicks", BrooksOrStopBufferTicks));
            BrooksOrLevels = TextParameter("brooksOrLevels", BrooksOrLevels).ToLowerInvariant();
            _tradeExportKey = $"{ProjectId}/brooks_open_reversal_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_levels_{BrooksOrLevels.Replace(",", "-")}_maxsig_{BrooksOrMaxSignalMinutes}_sweep_{ParamToken(BrooksOrMinSweepTicks)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void ResetBrooksOpeningReversalSession()
        {
            _borArmedToday = false;
        }

        private void TryBrooksOpeningReversalResearch(BarSnapshot bar)
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
            if (minutes <= 0 || minutes > BrooksOrMaxSignalMinutes || _borArmedToday)
                return;

            _odHigh = _sessionHigh;
            _odLow = _sessionLow;
            if (BrooksOrLevels.Contains("prior") && TryBrooksPriorLevelReversal(bar))
                return;
            if (BrooksOrLevels.Contains("or") && _openingRangeComplete)
                TryBrooksOpeningRangeReversal(bar);
        }

        private bool TryBrooksPriorLevelReversal(BarSnapshot bar)
        {
            double sweep = BrooksOrMinSweepTicks * TickSize;
            if (!double.IsNaN(_priorRthLow)
                && _sessionLow <= _priorRthLow - sweep
                && bar.Close > _priorRthLow
                && SideAllowed(Bias.Long))
            {
                _borArmedToday = ArmOpeningDriveSignal(bar, Bias.Long, _sessionLow - BrooksOrStopBufferTicks * TickSize, "BROOKS_OR_PDL_LONG");
                return _borArmedToday;
            }

            if (!double.IsNaN(_priorRthHigh)
                && _sessionHigh >= _priorRthHigh + sweep
                && bar.Close < _priorRthHigh
                && SideAllowed(Bias.Short))
            {
                _borArmedToday = ArmOpeningDriveSignal(bar, Bias.Short, _sessionHigh + BrooksOrStopBufferTicks * TickSize, "BROOKS_OR_PDH_SHORT");
                return _borArmedToday;
            }

            return false;
        }

        private bool TryBrooksOpeningRangeReversal(BarSnapshot bar)
        {
            double sweep = BrooksOrMinSweepTicks * TickSize;
            if (_sessionLow <= _openingRangeLow - sweep
                && bar.Close > _openingRangeLow
                && SideAllowed(Bias.Long))
            {
                _borArmedToday = ArmOpeningDriveSignal(bar, Bias.Long, _sessionLow - BrooksOrStopBufferTicks * TickSize, "BROOKS_OR_ORL_LONG");
                return _borArmedToday;
            }

            if (_sessionHigh >= _openingRangeHigh + sweep
                && bar.Close < _openingRangeHigh
                && SideAllowed(Bias.Short))
            {
                _borArmedToday = ArmOpeningDriveSignal(bar, Bias.Short, _sessionHigh + BrooksOrStopBufferTicks * TickSize, "BROOKS_OR_ORH_SHORT");
                return _borArmedToday;
            }

            return false;
        }
    }
}
