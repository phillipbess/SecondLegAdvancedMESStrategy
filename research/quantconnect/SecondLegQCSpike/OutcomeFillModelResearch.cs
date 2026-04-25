using System;

namespace QuantConnect.Algorithm.CSharp
{
    public partial class SecondLegQCSpike
    {
        private bool TryCloseAmbiguousOutcome(BarSnapshot bar, bool stopHit, bool targetHit)
        {
            if (!stopHit || !targetHit)
                return false;

            Block("SameBarBoth");
            string policy = TextParameter("sameBarPolicy", "stopfirst").ToLowerInvariant();
            if (policy == "targetfirst")
                CloseVirtualTrade(bar, "Target", _virtualTrade.TargetR);
            else
                CloseVirtualTrade(bar, "Stop", -1.0);
            return true;
        }
    }
}
