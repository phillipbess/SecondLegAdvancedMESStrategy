using System;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private int AfternoonCompressionMeasureMinutes = 60;
        private int AfternoonCompressionStartMinutes = 180;
        private int AfternoonCompressionEndMinutes = 300;
        private int AfternoonCompressionBreakoutEndMinutes = 360;
        private double AfternoonCompressionMinMorningMoveAtr = 0.5;
        private double AfternoonCompressionMaxBoxAtr = 1.0;
        private double AfternoonCompressionMinBoxBars = 6.0;
        private double AfternoonCompressionStopAtr = 0.75;
        private double AfternoonCompressionStopBufferTicks = 2.0;
        private bool AfternoonCompressionLongOnly = true;

        private bool _acBiasCaptured;
        private bool _acBoxComplete;
        private Bias _acBias = Bias.Neutral;
        private double _acBoxHigh = double.NaN;
        private double _acBoxLow = double.NaN;
        private double _acBoxAtr = double.NaN;
        private int _acBoxBars;

        private void ConfigureAfternoonCompressionResearch(DateTime startDate, DateTime endDate)
        {
            if (MomentumResearchMode != "compression")
                return;

            AfternoonCompressionMeasureMinutes = Math.Max(OpeningRangeMinutes, IntParameter("afternoonCompressionMeasureMinutes", AfternoonCompressionMeasureMinutes));
            AfternoonCompressionStartMinutes = Math.Max(AfternoonCompressionMeasureMinutes, IntParameter("afternoonCompressionStartMinutes", AfternoonCompressionStartMinutes));
            AfternoonCompressionEndMinutes = Math.Max(AfternoonCompressionStartMinutes + BarMinutes, IntParameter("afternoonCompressionEndMinutes", AfternoonCompressionEndMinutes));
            AfternoonCompressionBreakoutEndMinutes = Math.Max(AfternoonCompressionEndMinutes, IntParameter("afternoonCompressionBreakoutEndMinutes", AfternoonCompressionBreakoutEndMinutes));
            AfternoonCompressionMinMorningMoveAtr = Math.Max(0.0, DoubleParameter("afternoonCompressionMinMorningMoveAtr", AfternoonCompressionMinMorningMoveAtr));
            AfternoonCompressionMaxBoxAtr = Math.Max(0.1, DoubleParameter("afternoonCompressionMaxBoxAtr", AfternoonCompressionMaxBoxAtr));
            AfternoonCompressionMinBoxBars = Math.Max(1.0, DoubleParameter("afternoonCompressionMinBoxBars", AfternoonCompressionMinBoxBars));
            AfternoonCompressionStopAtr = Math.Max(0.0, DoubleParameter("afternoonCompressionStopAtr", AfternoonCompressionStopAtr));
            AfternoonCompressionStopBufferTicks = Math.Max(0.0, DoubleParameter("afternoonCompressionStopBufferTicks", AfternoonCompressionStopBufferTicks));
            AfternoonCompressionLongOnly = BoolParameter("afternoonCompressionLongOnly", AfternoonCompressionLongOnly);
            _tradeExportKey = $"{ProjectId}/afternoon_compression_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_measure_{AfternoonCompressionMeasureMinutes}_box_{AfternoonCompressionStartMinutes}_{AfternoonCompressionEndMinutes}_move_{ParamToken(AfternoonCompressionMinMorningMoveAtr)}_maxbox_{ParamToken(AfternoonCompressionMaxBoxAtr)}_stopatr_{ParamToken(AfternoonCompressionStopAtr)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void ResetAfternoonCompressionSession()
        {
            _acBiasCaptured = false;
            _acBoxComplete = false;
            _acBias = Bias.Neutral;
            _acBoxHigh = double.NaN;
            _acBoxLow = double.NaN;
            _acBoxAtr = double.NaN;
            _acBoxBars = 0;
        }

        private void TryAfternoonCompressionResearch(BarSnapshot bar)
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

            CaptureAfternoonCompressionBias(bar, minutes);
            TrackAfternoonCompressionBox(bar, minutes);
            TryArmAfternoonCompressionBreakout(bar, minutes);
        }

        private void CaptureAfternoonCompressionBias(BarSnapshot bar, int minutes)
        {
            if (_acBiasCaptured || minutes < AfternoonCompressionMeasureMinutes)
                return;

            double moveAtr = (bar.Close - _odSessionOpen) / Math.Max(bar.Atr, TickSize);
            if (moveAtr >= AfternoonCompressionMinMorningMoveAtr)
                _acBias = Bias.Long;
            else if (!AfternoonCompressionLongOnly && moveAtr <= -AfternoonCompressionMinMorningMoveAtr)
                _acBias = Bias.Short;
            else
                Block("AcWeakMorning");
            _acBiasCaptured = true;
        }

        private void TrackAfternoonCompressionBox(BarSnapshot bar, int minutes)
        {
            if (!_acBiasCaptured || _acBias == Bias.Neutral || _acBoxComplete)
                return;
            if (minutes < AfternoonCompressionStartMinutes || minutes > AfternoonCompressionEndMinutes)
                return;

            _acBoxHigh = double.IsNaN(_acBoxHigh) ? bar.High : Math.Max(_acBoxHigh, bar.High);
            _acBoxLow = double.IsNaN(_acBoxLow) ? bar.Low : Math.Min(_acBoxLow, bar.Low);
            _acBoxAtr = double.IsNaN(_acBoxAtr) ? bar.Atr : (_acBoxAtr * _acBoxBars + bar.Atr) / Math.Max(1, _acBoxBars + 1);
            _acBoxBars++;

            if (minutes >= AfternoonCompressionEndMinutes)
                _acBoxComplete = true;
        }

        private void TryArmAfternoonCompressionBreakout(BarSnapshot bar, int minutes)
        {
            if (!_acBoxComplete || _acBias == Bias.Neutral || _odTradedToday && OpeningDriveOneTradePerDay)
                return;
            if (minutes <= AfternoonCompressionEndMinutes || minutes > AfternoonCompressionBreakoutEndMinutes)
                return;
            if (!SideAllowed(_acBias))
                return;
            if (_acBoxBars < AfternoonCompressionMinBoxBars || double.IsNaN(_acBoxHigh) || double.IsNaN(_acBoxLow))
            {
                Block("AcBoxTooShort");
                return;
            }

            double boxRange = _acBoxHigh - _acBoxLow;
            if (boxRange <= 0.0 || boxRange / Math.Max(_acBoxAtr, TickSize) > AfternoonCompressionMaxBoxAtr)
            {
                Block("AcBoxTooWide");
                return;
            }

            _odHigh = _acBoxHigh;
            _odLow = _acBoxLow;
            if (_acBias == Bias.Long && bar.Close > _acBoxHigh)
            {
                double stop = AfternoonCompressionStopAtr > 0.0
                    ? bar.Close - AfternoonCompressionStopAtr * bar.Atr
                    : _acBoxLow - AfternoonCompressionStopBufferTicks * TickSize;
                ArmOpeningDriveSignal(bar, Bias.Long, stop, "AC_BREAK_LONG");
            }
            else if (_acBias == Bias.Short && bar.Close < _acBoxLow)
            {
                double stop = AfternoonCompressionStopAtr > 0.0
                    ? bar.Close + AfternoonCompressionStopAtr * bar.Atr
                    : _acBoxHigh + AfternoonCompressionStopBufferTicks * TickSize;
                ArmOpeningDriveSignal(bar, Bias.Short, stop, "AC_BREAK_SHORT");
            }
            else
                Block("AcNoBreakout");
        }
    }
}
