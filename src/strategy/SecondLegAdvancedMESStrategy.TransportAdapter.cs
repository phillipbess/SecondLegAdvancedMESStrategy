using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private enum TransportResultKind
        {
            None = 0,
            Submitted = 1,
            Requested = 2,
            Changed = 3,
            Failed = 4,
        }

        private readonly struct TransportResult
        {
            internal TransportResult(
                TransportResultKind kind,
                Order order,
                string signalName,
                string oco,
                string failureReason)
            {
                Kind = kind;
                Order = order;
                SignalName = signalName ?? string.Empty;
                Oco = oco ?? string.Empty;
                FailureReason = failureReason ?? string.Empty;
            }

            internal TransportResultKind Kind { get; }
            internal Order Order { get; }
            internal string SignalName { get; }
            internal string Oco { get; }
            internal string FailureReason { get; }
            internal bool IsSuccess =>
                Kind == TransportResultKind.Submitted
                || Kind == TransportResultKind.Requested
                || Kind == TransportResultKind.Changed;
            internal bool IsPendingAck => Kind == TransportResultKind.Requested;

            internal static TransportResult Submitted(Order order, string signalName, string oco = "")
            {
                return new TransportResult(TransportResultKind.Submitted, order, signalName, oco, string.Empty);
            }

            internal static TransportResult Requested(Order order, string signalName, string oco = "")
            {
                return new TransportResult(TransportResultKind.Requested, order, signalName, oco, string.Empty);
            }

            internal static TransportResult Changed(Order order, string signalName, string oco = "")
            {
                return new TransportResult(TransportResultKind.Changed, order, signalName, oco, string.Empty);
            }

            internal static TransportResult Failed(string failureReason)
            {
                return new TransportResult(TransportResultKind.Failed, null, string.Empty, string.Empty, failureReason);
            }
        }

        private static bool IsFinitePrice(double price)
        {
            return !double.IsNaN(price) && !double.IsInfinity(price);
        }

        private static int NormalizePositiveQuantity(int quantity)
        {
            if (quantity == int.MinValue)
                return 0;

            return Math.Abs(quantity);
        }

        private static bool IsMutableWorkingOrderState(OrderState orderState)
        {
            switch (orderState)
            {
                case OrderState.Submitted:
                case OrderState.Accepted:
                case OrderState.TriggerPending:
                case OrderState.Working:
                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveOrderSignalName(Order order, string fallbackSignalName)
        {
            if (order == null)
                return fallbackSignalName ?? string.Empty;

            if (!string.IsNullOrEmpty(order.Name))
                return order.Name;

            if (!string.IsNullOrEmpty(order.FromEntrySignal))
                return order.FromEntrySignal;

            return fallbackSignalName ?? string.Empty;
        }

        private static string ResolveOrderOco(Order order, string fallbackOco)
        {
            if (order != null && !string.IsNullOrEmpty(order.Oco))
                return order.Oco;

            return fallbackOco ?? string.Empty;
        }

        private TransportResult SubmitPrimaryEntryBridge(PlannedEntry plannedEntry)
        {
            if (plannedEntry == null || !plannedEntry.IsValid)
                return TransportResult.Failed("entry-plan-not-ready");

            int quantity = NormalizePositiveQuantity(plannedEntry.Quantity);
            if (quantity <= 0)
                return TransportResult.Failed("entry-invalid-qty");

            if (!IsFinitePrice(plannedEntry.EntryPrice) || plannedEntry.EntryPrice <= 0.0)
                return TransportResult.Failed("entry-invalid-entry-price");

            OrderAction action = plannedEntry.Bias == SecondLegBias.Short
                ? OrderAction.SellShort
                : OrderAction.Buy;
            try
            {
                Order submittedOrder = SubmitOrderUnmanaged(
                    0,
                    action,
                    OrderType.StopMarket,
                    quantity,
                    0,
                    plannedEntry.EntryPrice,
                    string.Empty,
                    plannedEntry.SignalName);

                if (submittedOrder == null)
                    return TransportResult.Failed("entry-submit-null");

                return TransportResult.Submitted(
                    submittedOrder,
                    ResolveOrderSignalName(submittedOrder, plannedEntry.SignalName));
            }
            catch (Exception ex)
            {
                return TransportResult.Failed($"entry-submit-exception:{ex.GetType().Name}");
            }
        }

        private TransportResult EnsureProtectiveStopBridge(
            string signalName,
            double stopPrice,
            int quantity,
            string oco)
        {
            int effectiveQuantity = NormalizePositiveQuantity(quantity);
            if (effectiveQuantity <= 0)
                return TransportResult.Failed("protective-invalid-qty");

            if (!IsFinitePrice(stopPrice) || stopPrice <= 0.0)
                return TransportResult.Failed("protective-invalid-stop-price");

            if (!TryResolveProtectiveStopAction(out OrderAction action))
                return TransportResult.Failed("protective-missing-side");

            string stopSignalName = BuildProtectiveStopSignalName(signalName);
            string effectiveOco = string.IsNullOrEmpty(oco) ? NewExitOco() : oco;
            try
            {
                Order submittedOrder = SubmitOrderUnmanaged(
                    0,
                    action,
                    OrderType.StopMarket,
                    effectiveQuantity,
                    0,
                    stopPrice,
                    effectiveOco,
                    stopSignalName);

                if (submittedOrder == null)
                    return TransportResult.Failed("protective-submit-null");

                return TransportResult.Submitted(
                    submittedOrder,
                    ResolveOrderSignalName(submittedOrder, stopSignalName),
                    ResolveOrderOco(submittedOrder, effectiveOco));
            }
            catch (Exception ex)
            {
                return TransportResult.Failed($"protective-submit-exception:{ex.GetType().Name}");
            }
        }

        private TransportResult ChangeProtectiveStopBridge(Order workingStop, int quantity, double stopPrice)
        {
            if (workingStop == null)
                return TransportResult.Failed("protective-change-missing-order");

            int effectiveQuantity = NormalizePositiveQuantity(quantity);
            if (effectiveQuantity <= 0)
                return TransportResult.Failed("protective-change-invalid-qty");

            if (!IsFinitePrice(stopPrice) || stopPrice <= 0.0)
                return TransportResult.Failed("protective-change-invalid-stop-price");

            if (!TryResolveProtectiveStopAction(out OrderAction expectedAction))
                return TransportResult.Failed("protective-change-missing-side");

            if (workingStop.OrderType != OrderType.StopMarket)
                return TransportResult.Failed($"protective-change-invalid-type:{workingStop.OrderType}");

            if (workingStop.OrderAction != expectedAction)
                return TransportResult.Failed($"protective-change-invalid-side:{workingStop.OrderAction}");

            if (!IsMutableWorkingOrderState(workingStop.OrderState))
                return TransportResult.Failed($"protective-change-invalid-state:{workingStop.OrderState}");

            try
            {
                ChangeOrder(workingStop, effectiveQuantity, 0, stopPrice);
                return TransportResult.Requested(
                    workingStop,
                    ResolveOrderSignalName(workingStop, workingStop.Name ?? string.Empty),
                    ResolveOrderOco(workingStop, workingStop.Oco ?? string.Empty));
            }
            catch (Exception ex)
            {
                return TransportResult.Failed($"protective-change-exception:{ex.GetType().Name}");
            }
        }

        private TransportResult SubmitFlattenBridge(string fromEntrySignal, int quantity)
        {
            quantity = NormalizePositiveQuantity(quantity);
            if (quantity <= 0)
                return TransportResult.Failed("flatten-qty-zero");

            int livePositionQuantity = NormalizePositiveQuantity(Position.Quantity);
            if (livePositionQuantity == 0)
                return TransportResult.Failed("flatten-flat-position");

            quantity = livePositionQuantity;

            OrderAction action = Position.Quantity > 0
                ? OrderAction.Sell
                : OrderAction.BuyToCover;
            string signalName = BuildFlattenSignalName(fromEntrySignal);
            try
            {
                Order submittedOrder = SubmitOrderUnmanaged(0, action, OrderType.Market, quantity, 0, 0, string.Empty, signalName);
                if (submittedOrder == null)
                    return TransportResult.Failed("flatten-submit-null");

                return TransportResult.Submitted(
                    submittedOrder,
                    ResolveOrderSignalName(submittedOrder, signalName),
                    ResolveOrderOco(submittedOrder, string.Empty));
            }
            catch (Exception ex)
            {
                return TransportResult.Failed($"flatten-submit-exception:{ex.GetType().Name}");
            }
        }

        private bool TryResolveProtectiveStopAction(out OrderAction action)
        {
            action = OrderAction.BuyToCover;

            if (Position.Quantity > 0 || entryPositionSide == MarketPosition.Long)
            {
                action = OrderAction.Sell;
                return true;
            }

            if (Position.Quantity < 0 || entryPositionSide == MarketPosition.Short)
            {
                action = OrderAction.BuyToCover;
                return true;
            }

            return false;
        }

        private OrderAction ResolveProtectiveStopAction()
        {
            return TryResolveProtectiveStopAction(out OrderAction action)
                ? action
                : OrderAction.BuyToCover;
        }

        private string BuildProtectiveStopSignalName(string signalName)
        {
            string ownedSignal = !string.IsNullOrEmpty(signalName)
                ? signalName
                : (!string.IsNullOrEmpty(_activeEntrySignal) ? _activeEntrySignal : currentTradeID);
            return $"StopLoss_PrimaryEntry|{ownedSignal}";
        }

        private string BuildFlattenSignalName(string fromEntrySignal)
        {
            string ownedSignal = !string.IsNullOrEmpty(fromEntrySignal)
                ? fromEntrySignal
                : (!string.IsNullOrEmpty(_activeEntrySignal) ? _activeEntrySignal : currentTradeID);
            return $"{SecondLegNameTokens.SafetyFlatten}|{ownedSignal}";
        }

        private bool TryGetOwnedProtectiveSignal(Order order, out string ownedSignal)
        {
            ownedSignal = string.Empty;
            if (order == null)
                return false;

            string orderName = order.Name ?? string.Empty;
            const string prefix = "StopLoss_PrimaryEntry|";
            if (!orderName.StartsWith(prefix, System.StringComparison.Ordinal))
                return false;

            ownedSignal = orderName.Substring(prefix.Length);
            return !string.IsNullOrEmpty(ownedSignal);
        }
    }
}
