using System;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private int BrooksMeasureMinutes = 60;
        private int BrooksMaxSignalMinutes = 180;
        private int BrooksMinStrongBars = 3;
        private int BrooksMaxPullbackBars = 12;
        private double BrooksMinMoveAtr = 0.75;
        private double BrooksStrongBodyPct = 0.45;
        private double BrooksStrongClosePct = 0.65;
        private double BrooksMinPullbackAtr = 0.25;
        private double BrooksMaxRetrace = 0.65;
        private double BrooksStopBufferTicks = 2.0;
        private bool BrooksRequireEmaSide = true;

        private bool _btfoMeasured;
        private bool _btfoPullbackSeen;
        private int _btfoStrongBullBars;
        private int _btfoStrongBearBars;
        private int _btfoPullbackStartBar = -1;
        private int _btfoCounterStrongBars;
        private Bias _btfoBias = Bias.Neutral;
        private double _btfoHigh = double.NaN;
        private double _btfoLow = double.NaN;
        private double _btfoPullbackHigh = double.NaN;
        private double _btfoPullbackLow = double.NaN;

        private void ConfigureBrooksTrendPullbackResearch(DateTime startDate, DateTime endDate)
        {
            if (MomentumResearchMode != "brooks")
                return;

            BrooksMeasureMinutes = Math.Max(OpeningRangeMinutes, IntParameter("brooksMeasureMinutes", BrooksMeasureMinutes));
            BrooksMaxSignalMinutes = Math.Max(BrooksMeasureMinutes, IntParameter("brooksMaxSignalMinutes", BrooksMaxSignalMinutes));
            BrooksMinStrongBars = Math.Max(1, IntParameter("brooksMinStrongBars", BrooksMinStrongBars));
            BrooksMaxPullbackBars = Math.Max(1, IntParameter("brooksMaxPullbackBars", BrooksMaxPullbackBars));
            BrooksMinMoveAtr = Math.Max(0.0, DoubleParameter("brooksMinMoveAtr", BrooksMinMoveAtr));
            BrooksStrongBodyPct = Clamp(DoubleParameter("brooksStrongBodyPct", BrooksStrongBodyPct), 0.1, 0.95);
            BrooksStrongClosePct = Clamp(DoubleParameter("brooksStrongClosePct", BrooksStrongClosePct), 0.5, 0.95);
            BrooksMinPullbackAtr = Math.Max(0.0, DoubleParameter("brooksMinPullbackAtr", BrooksMinPullbackAtr));
            BrooksMaxRetrace = Clamp(DoubleParameter("brooksMaxRetrace", BrooksMaxRetrace), 0.1, 1.5);
            BrooksStopBufferTicks = Math.Max(0.0, DoubleParameter("brooksStopBufferTicks", BrooksStopBufferTicks));
            BrooksRequireEmaSide = BoolParameter("brooksRequireEmaSide", BrooksRequireEmaSide);
            _tradeExportKey = $"{ProjectId}/brooks_tfo_export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_bar_{BarMinutes}_side_{SideFilter}_measure_{BrooksMeasureMinutes}_maxsig_{BrooksMaxSignalMinutes}_move_{ParamToken(BrooksMinMoveAtr)}_strong_{BrooksMinStrongBars}_pb_{ParamToken(BrooksMinPullbackAtr)}_retr_{ParamToken(BrooksMaxRetrace)}_target_{ParamToken(ProfitTargetR)}_hold_{MaxOutcomeBars}.csv";
        }

        private void ResetBrooksTrendPullbackSession()
        {
            _btfoMeasured = false;
            _btfoPullbackSeen = false;
            _btfoStrongBullBars = 0;
            _btfoStrongBearBars = 0;
            _btfoPullbackStartBar = -1;
            _btfoCounterStrongBars = 0;
            _btfoBias = Bias.Neutral;
            _btfoHigh = double.NaN;
            _btfoLow = double.NaN;
            _btfoPullbackHigh = double.NaN;
            _btfoPullbackLow = double.NaN;
        }

        private void TryBrooksTrendPullbackResearch(BarSnapshot bar)
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

            TrackBrooksOpeningContext(bar, minutes);
            if (!_btfoMeasured)
                return;

            if (_btfoBias == Bias.Neutral || _odTradedToday && OpeningDriveOneTradePerDay)
                return;
            if (minutes <= BrooksMeasureMinutes || minutes > BrooksMaxSignalMinutes)
                return;
            if (!SideAllowed(_btfoBias))
                return;

            TrackBrooksPullbackAndSignal(bar);
        }

        private void TrackBrooksOpeningContext(BarSnapshot bar, int minutes)
        {
            if (_btfoMeasured)
                return;

            _btfoHigh = double.IsNaN(_btfoHigh) ? bar.High : Math.Max(_btfoHigh, bar.High);
            _btfoLow = double.IsNaN(_btfoLow) ? bar.Low : Math.Min(_btfoLow, bar.Low);
            if (IsStrongBar(bar, Bias.Long))
                _btfoStrongBullBars++;
            if (IsStrongBar(bar, Bias.Short))
                _btfoStrongBearBars++;
            if (minutes < BrooksMeasureMinutes)
                return;

            double moveAtr = (bar.Close - _odSessionOpen) / Math.Max(bar.Atr, TickSize);
            bool emaLongOk = !BrooksRequireEmaSide || bar.Close >= bar.EmaFast;
            bool emaShortOk = !BrooksRequireEmaSide || bar.Close <= bar.EmaFast;
            if (moveAtr >= BrooksMinMoveAtr && _btfoStrongBullBars >= BrooksMinStrongBars && emaLongOk)
                _btfoBias = Bias.Long;
            else if (moveAtr <= -BrooksMinMoveAtr && _btfoStrongBearBars >= BrooksMinStrongBars && emaShortOk)
                _btfoBias = Bias.Short;
            else
                Block("BrooksNoTfo");

            _odHigh = _btfoHigh;
            _odLow = _btfoLow;
            _btfoMeasured = true;
        }

        private void TrackBrooksPullbackAndSignal(BarSnapshot bar)
        {
            if (_bars.Count < 2)
                return;

            BarSnapshot previous = PreviousBar();
            if (_btfoBias == Bias.Long)
                TrackBrooksLongPullback(bar, previous);
            else
                TrackBrooksShortPullback(bar, previous);
        }

        private void TrackBrooksLongPullback(BarSnapshot bar, BarSnapshot previous)
        {
            if (!_btfoPullbackSeen)
            {
                if (_btfoHigh - bar.Low < BrooksMinPullbackAtr * Math.Max(bar.Atr, TickSize))
                    return;
                _btfoPullbackSeen = true;
                _btfoPullbackStartBar = bar.Index;
                _btfoPullbackHigh = bar.High;
                _btfoPullbackLow = bar.Low;
            }

            _btfoPullbackHigh = Math.Max(_btfoPullbackHigh, bar.High);
            _btfoPullbackLow = Math.Min(_btfoPullbackLow, bar.Low);
            if (IsStrongBar(bar, Bias.Short))
                _btfoCounterStrongBars++;
            if (!PassesBrooksPullbackShape(bar, Bias.Long))
                return;

            if (bar.Close > previous.High)
                ArmOpeningDriveSignal(bar, Bias.Long, _btfoPullbackLow - BrooksStopBufferTicks * TickSize, "BROOKS_TFO_LONG");
        }

        private void TrackBrooksShortPullback(BarSnapshot bar, BarSnapshot previous)
        {
            if (!_btfoPullbackSeen)
            {
                if (bar.High - _btfoLow < BrooksMinPullbackAtr * Math.Max(bar.Atr, TickSize))
                    return;
                _btfoPullbackSeen = true;
                _btfoPullbackStartBar = bar.Index;
                _btfoPullbackHigh = bar.High;
                _btfoPullbackLow = bar.Low;
            }

            _btfoPullbackHigh = Math.Max(_btfoPullbackHigh, bar.High);
            _btfoPullbackLow = Math.Min(_btfoPullbackLow, bar.Low);
            if (IsStrongBar(bar, Bias.Long))
                _btfoCounterStrongBars++;
            if (!PassesBrooksPullbackShape(bar, Bias.Short))
                return;

            if (bar.Close < previous.Low)
                ArmOpeningDriveSignal(bar, Bias.Short, _btfoPullbackHigh + BrooksStopBufferTicks * TickSize, "BROOKS_TFO_SHORT");
        }

        private bool PassesBrooksPullbackShape(BarSnapshot bar, Bias bias)
        {
            int bars = bar.Index - _btfoPullbackStartBar + 1;
            if (bars > BrooksMaxPullbackBars)
            {
                Block("BrooksPullbackTooLong");
                return false;
            }
            if (_btfoCounterStrongBars > 1)
            {
                Block("BrooksPullbackTooStrong");
                return false;
            }

            double impulse = bias == Bias.Long
                ? Math.Max(TickSize, _btfoHigh - _odSessionOpen)
                : Math.Max(TickSize, _odSessionOpen - _btfoLow);
            double retrace = bias == Bias.Long
                ? (_btfoHigh - _btfoPullbackLow) / impulse
                : (_btfoPullbackHigh - _btfoLow) / impulse;
            if (retrace > BrooksMaxRetrace)
            {
                Block("BrooksPullbackTooDeep");
                return false;
            }

            if (BrooksRequireEmaSide && bias == Bias.Long && bar.Close < bar.EmaFast)
            {
                Block("BrooksLostEma");
                return false;
            }
            if (BrooksRequireEmaSide && bias == Bias.Short && bar.Close > bar.EmaFast)
            {
                Block("BrooksLostEma");
                return false;
            }
            return true;
        }

        private bool IsStrongBar(BarSnapshot bar, Bias bias)
        {
            double closePct = CloseLocationPct(bar);
            double bodyPct = BodyPct(bar);
            if (bias == Bias.Long)
                return bar.Close > bar.Open && bodyPct >= BrooksStrongBodyPct && closePct >= BrooksStrongClosePct;
            return bar.Close < bar.Open && bodyPct >= BrooksStrongBodyPct && closePct <= 1.0 - BrooksStrongClosePct;
        }
    }
}
