using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private bool _closedBarWasFirstBarOfSession;
        private bool _currentBarWasFirstBarOfSession;
        private bool _closedBarSessionFlagsPrimed;

        private void ResetClosedBarSessionFlags()
        {
            _closedBarWasFirstBarOfSession = false;
            _currentBarWasFirstBarOfSession = false;
            _closedBarSessionFlagsPrimed = false;
        }

        private void AdvanceClosedBarSessionFlags()
        {
            if (Calculate == Calculate.OnBarClose || Bars == null)
                return;

            bool currentBarIsFirstBarOfSession = Bars != null && Bars.IsFirstBarOfSession;
            if (!_closedBarSessionFlagsPrimed)
            {
                _currentBarWasFirstBarOfSession = currentBarIsFirstBarOfSession;
                _closedBarWasFirstBarOfSession = false;
                _closedBarSessionFlagsPrimed = true;
                return;
            }

            if (IsFirstTickOfBar)
                _closedBarWasFirstBarOfSession = _currentBarWasFirstBarOfSession;

            _currentBarWasFirstBarOfSession = currentBarIsFirstBarOfSession;
        }

        private int ClosedBarBarsAgo()
        {
            return Calculate == Calculate.OnBarClose ? 0 : 1;
        }

        private int ResolveClosedBarBarsAgo(int barsAgoOffset = 0)
        {
            int barsAgo = ClosedBarBarsAgo() + Math.Max(0, barsAgoOffset);
            return Math.Min(CurrentBar, barsAgo);
        }

        private DateTime ClosedBarTime(int barsAgoOffset = 0)
        {
            return Time[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private int ClosedBarIndex(int barsAgoOffset = 0)
        {
            return Math.Max(0, CurrentBar - ResolveClosedBarBarsAgo(barsAgoOffset));
        }

        private int ClosedBarCount()
        {
            return ClosedBarIndex() + 1;
        }

        private double ClosedBarOpen(int barsAgoOffset = 0)
        {
            return Open[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private double ClosedBarHigh(int barsAgoOffset = 0)
        {
            return High[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private double ClosedBarLow(int barsAgoOffset = 0)
        {
            return Low[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private double ClosedBarClose(int barsAgoOffset = 0)
        {
            return Close[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private double ClosedBarFastEma(int barsAgoOffset = 0)
        {
            return _emaFast[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private double ClosedBarSlowEma(int barsAgoOffset = 0)
        {
            return _emaSlow[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private double ClosedBarAtrValue(int barsAgoOffset = 0)
        {
            return _atr[ResolveClosedBarBarsAgo(barsAgoOffset)];
        }

        private int ClosedBarTimeHhmm(int barsAgoOffset = 0)
        {
            return ToTime(ClosedBarTime(barsAgoOffset)) / 100;
        }

        private bool ClosedBarIsFirstBarOfSession()
        {
            if (Calculate == Calculate.OnBarClose)
                return Bars != null && Bars.IsFirstBarOfSession;

            return _closedBarWasFirstBarOfSession;
        }
    }
}
