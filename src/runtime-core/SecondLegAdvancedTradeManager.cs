#region Using declarations
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;
#endregion

// ============================================================================
// SecondLegAdvancedMESStrategy - TradeManager (first-port adaptation)
//
// Intent:
// - Keep this as a mechanical donor port under src/runtime-core only.
// - Keep host assumptions narrow and documented here instead of requiring
//   shell edits during the first import pass.
// - Stay compile-oriented until the execution/order-maintenance lanes land.
//
// Host assumptions for later wiring:
// 1. The host strategy will eventually forward relevant execution fills into
//    RuntimeTradeManager.OnExecutionUpdate(...).
// 2. Entry-order identity may need a stricter host predicate once final signal
//    naming is frozen for this strategy family.
// 3. TradeManagerSafeCancelOrder must be replaced by the future runtime-core
//    order-maintenance/safe-cancel authority before live cleanup flows rely on
//    CancelActiveOrders(...).
// ============================================================================

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private SecondLegTradeManager _tradeStateManager;

        internal SecondLegTradeManager TradeStateManager
        {
            get { return _tradeStateManager ?? (_tradeStateManager = new SecondLegTradeManager(this)); }
        }

        /// <summary>
        /// First-pass trade state tracker imported from the donor runtime.
        /// This class intentionally avoids broad behavioral changes.
        /// </summary>
        internal sealed class SecondLegTradeManager
        {
            private readonly SecondLegAdvancedMESStrategy strategy;
            private readonly List<Order> activeOrders = new List<Order>();

            public SecondLegTradeManager(SecondLegAdvancedMESStrategy strategy)
            {
                this.strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            }

            public double EntryPrice { get; set; }
            public DateTime EntryTime { get; set; }
            public double PeakProfitUsd { get; set; }
            public double MaxDrawdownUsd { get; set; }
            public MarketPosition EntryDirection { get; private set; }
            public double StopPrice { get; set; }

            public void OnExecutionUpdate(Execution execution, double price)
            {
                if (execution == null)
                    return;

                if (strategy.TradeManagerIsPrimaryEntryOrder(execution.Order))
                {
                    EntryPrice = strategy.Position.AveragePrice > 0 ? strategy.Position.AveragePrice : price;

                    bool firstEntryFillForTrade = EntryTime == DateTime.MinValue;
                    if (firstEntryFillForTrade)
                    {
                        EntryTime = execution.Time;
                        PeakProfitUsd = 0;
                        MaxDrawdownUsd = 0;
                    }

                    EntryDirection = execution.MarketPosition;
                    strategy.TradeManagerPrint(
                        string.Format(
                            "[TradeManager] Entry filled: {0} @ {1:F2} | AvgEntry: {2:F2}",
                            EntryDirection,
                            price,
                            EntryPrice));
                }

                // StopPrice stays host-owned until the exit-authority port lands.
                UpdatePeakAndDrawdown(priceForCalculation: price);
            }

            public void UpdatePeakAndDrawdown(double priceForCalculation = 0.0, double? knownCurrentPnL = null)
            {
                if (strategy.Position == null || strategy.Position.Quantity == 0)
                {
                    if (strategy.TradeManagerDebugMode)
                        strategy.TradeManagerWriteDebugLog("PEAK PROFIT UPDATE SKIPPED | Position is null or flat");
                    return;
                }

                // Donor behavior: prefer the supplied price, then fall back to the current bar.
                double priceToUse = priceForCalculation > 0.0 ? priceForCalculation : strategy.Close[0];

                double currentPnL = knownCurrentPnL ?? 0.0;
                if (!knownCurrentPnL.HasValue)
                {
                    try
                    {
                        currentPnL = strategy.Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, priceToUse);

                        if (double.IsNaN(currentPnL) || double.IsInfinity(currentPnL))
                        {
                            currentPnL = 0.0;
                            strategy.TradeManagerWriteDebugLog(
                                "PEAK PROFIT UPDATE ERROR | Invalid PnL calculation result | Setting to 0 for safety");
                        }
                    }
                    catch (Exception ex)
                    {
                        currentPnL = 0.0;
                        strategy.TradeManagerWriteDebugLog(
                            string.Format(
                                "PEAK PROFIT UPDATE ERROR | Exception in PnL calculation: {0} | Setting to 0 for safety",
                                ex.Message));
                    }
                }

                double previousPeak = PeakProfitUsd;
                double cumulativePnL = currentPnL;

                if (cumulativePnL > PeakProfitUsd)
                {
                    PeakProfitUsd = cumulativePnL;
                    strategy.TradeManagerWriteDebugLog(
                        string.Format(
                            "SIMPLE PEAK PROFIT UPDATE | Previous: ${0:F2} | New: ${1:F2} | Gain: ${2:F2} | UnrealizedPnL: ${3:F2} | Position: {4} contracts | Price: {5:F2}",
                            previousPeak,
                            PeakProfitUsd,
                            PeakProfitUsd - previousPeak,
                            currentPnL,
                            strategy.Position.Quantity,
                            priceToUse));
                }

                if (cumulativePnL < MaxDrawdownUsd)
                    MaxDrawdownUsd = cumulativePnL;

                if (strategy.TradeManagerDebugMode || cumulativePnL > previousPeak)
                {
                    strategy.TradeManagerWriteDebugLog(
                        string.Format(
                            "SIMPLE PEAK PROFIT TRACKING | UnrealizedPnL: ${0:F2} | CumulativePnL: ${1:F2} | PeakProfit: ${2:F2} | MaxDrawdown: ${3:F2} | Position: {4} | EntryPrice: {5:F2} | CurrentPrice: {6:F2}",
                            currentPnL,
                            cumulativePnL,
                            PeakProfitUsd,
                            MaxDrawdownUsd,
                            strategy.Position.Quantity,
                            EntryPrice,
                            priceToUse));
                }
            }

            public void CancelActiveOrders(bool includeEntryOrders = true)
            {
                for (int i = activeOrders.Count - 1; i >= 0; i--)
                {
                    Order trackedOrder = activeOrders[i];
                    if (trackedOrder == null)
                        continue;

                    if (!includeEntryOrders && strategy.TradeManagerIsPrimaryEntryOrder(trackedOrder))
                        continue;

                    if (trackedOrder.OrderState == OrderState.Working || trackedOrder.OrderState == OrderState.Accepted)
                        strategy.TradeManagerSafeCancelOrder(trackedOrder, "TradeManager_Cleanup");
                }

                if (includeEntryOrders)
                {
                    activeOrders.Clear();
                    return;
                }

                activeOrders.RemoveAll(order =>
                    order == null
                    || !strategy.TradeManagerIsPrimaryEntryOrder(order)
                    || order.OrderState == OrderState.Filled
                    || order.OrderState == OrderState.Cancelled
                    || order.OrderState == OrderState.Rejected);
            }

            public void AddActiveOrder(Order order)
            {
                if (order == null)
                    return;

                RemoveStaleOrders();

                if (!activeOrders.Any(o => o != null && o.Id == order.Id))
                    activeOrders.Add(order);
            }

            public void RemoveActiveOrder(Order order)
            {
                if (order != null)
                    activeOrders.Remove(order);
            }

            private void RemoveStaleOrders()
            {
                activeOrders.RemoveAll(order =>
                    order == null
                    || order.OrderState == OrderState.Filled
                    || order.OrderState == OrderState.Cancelled
                    || order.OrderState == OrderState.Rejected);
            }
        }

        // Minimal host hooks kept local so the strategy shell does not need edits yet.
        // These are intentionally small; later runtime-core ports can replace the
        // heuristics/no-op surfaces with final order identity and debug plumbing.
        internal bool TradeManagerDebugMode
        {
            get { return false; }
        }

        internal void TradeManagerWriteDebugLog(string message)
        {
            WriteDebugLog($"[TRADE_MANAGER] {message}");
        }

        internal void TradeManagerPrint(string message)
        {
            Print(message);
        }

        internal bool TradeManagerIsPrimaryEntryOrder(Order order)
        {
            return IsPrimaryEntryOrder(order);
        }

        internal void TradeManagerSafeCancelOrder(Order order, string reason)
        {
            if (order == null)
                return;

            SafeCancelOrder(order, reason);
        }
    }
}
