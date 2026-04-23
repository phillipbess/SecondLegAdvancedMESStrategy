using System;
using NinjaTrader.Data;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            if (!_hostShellReady || marketDataUpdate == null)
                return;

            if (State != State.Realtime)
                return;

            DateTime nowUtc = System.DateTime.UtcNow;
            if (_lastMarketDataUtc != DateTime.MinValue
                && (nowUtc - _lastMarketDataUtc).TotalSeconds > ReconnectGapThresholdSeconds)
            {
                _awaitingReconnectGrace = true;
            }

            _lastMarketDataUtc = nowUtc;

            if (_awaitingReconnectGrace && marketDataUpdate.MarketDataType == MarketDataType.Last)
                BeginReconnectGrace("MarketDataResume");

            // Keep the signal brain bar-stable while still borrowing a thin
            // tick-responsive lane from the Mancini runtime model for protection
            // maintenance and cancel queue pumping.
            if (marketDataUpdate.MarketDataType == MarketDataType.Last
                && marketDataUpdate.Price > 0.0
                && Position.Quantity != 0)
            {
                UpdateManagedTradeProtection(marketDataUpdate.Price);
                ValidateStopQuantity("OnMarketData.Last");
            }

            if (marketDataUpdate.MarketDataType == MarketDataType.Last)
                EvaluateRealtimeReconnectGrace("OnMarketData.Last");

            PumpExitOps("OnMarketData");
            ContinueFlattenProtocol("OnMarketData");
            ProcessCancellationQueue();
        }
    }
}
