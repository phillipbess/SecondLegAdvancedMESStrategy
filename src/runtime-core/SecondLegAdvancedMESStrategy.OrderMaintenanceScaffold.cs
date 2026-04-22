using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    internal enum OrderMaintenanceCancellationScope
    {
        AllOrders = 0,
        ExitOrdersOnly = 1,
    }

    internal sealed class OrderMaintenanceState
    {
        public IList<Order> WorkingOrders { get; } = new List<Order>();

        public Queue<(Order Order, string Tag, DateTime EnqueuedAtUtc)> CancelQueue { get; } =
            new Queue<(Order Order, string Tag, DateTime EnqueuedAtUtc)>();

        public IDictionary<string, int> CancelAttempts { get; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public IDictionary<string, DateTime> CancelNextAtUtc { get; } =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
    }

    internal sealed class OrderMaintenanceHostContract
    {
        public Action<string> WriteDebugLog { get; set; }
        public Action<string> WriteRiskLog { get; set; }
        public Action<string> TriggerFlatten { get; set; }
        public Action<Order> CancelOrderRequest { get; set; }
        public Action<Order> NullOrderReference { get; set; }
        public Func<Order, bool> CanCancelOrder { get; set; }
        public Func<Order, bool> IsPrimaryEntryOrder { get; set; }
        public Func<Order, bool> IsProtectiveStopOrder { get; set; }
        public Func<int> PositionQuantity { get; set; }
        public Func<bool> HasCoverageNow { get; set; }

        public bool IsActivationReady
        {
            get
            {
                return CancelOrderRequest != null
                    && CanCancelOrder != null
                    && IsPrimaryEntryOrder != null
                    && IsProtectiveStopOrder != null
                    && PositionQuantity != null;
            }
        }

        public string DescribeMissingActivationHooks()
        {
            List<string> missing = new List<string>();

            if (CancelOrderRequest == null)
                missing.Add(nameof(CancelOrderRequest));

            if (CanCancelOrder == null)
                missing.Add(nameof(CanCancelOrder));

            if (IsPrimaryEntryOrder == null)
                missing.Add(nameof(IsPrimaryEntryOrder));

            if (IsProtectiveStopOrder == null)
                missing.Add(nameof(IsProtectiveStopOrder));

            if (PositionQuantity == null)
                missing.Add(nameof(PositionQuantity));

            return missing.Count == 0 ? string.Empty : string.Join(", ", missing);
        }
    }

    internal sealed class OrderMaintenanceScaffold
    {
        private const int DefaultMaxAttemptsPerPass = 8;
        private readonly OrderMaintenanceHostContract _host;

        public OrderMaintenanceScaffold(OrderMaintenanceHostContract host)
        {
            _host = host ?? new OrderMaintenanceHostContract();
        }

        public bool EnqueueOrderCancellation(OrderMaintenanceState state, Order order, string tag)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (order == null)
                return false;

            if (_host.CanCancelOrder != null && !_host.CanCancelOrder(order))
            {
                Debug(
                    $"[ORDER_MAINT][CANCEL_QUEUE] skip non-cancellable order | order={order.Name} state={order.OrderState} tag={tag}");
                return false;
            }

            string orderId = order.OrderId;
            if (!string.IsNullOrEmpty(orderId))
            {
                if (!state.CancelAttempts.ContainsKey(orderId))
                    state.CancelAttempts[orderId] = 0;

                if (!state.CancelNextAtUtc.ContainsKey(orderId))
                    state.CancelNextAtUtc[orderId] = DateTime.UtcNow;
            }

            state.CancelQueue.Enqueue((order, tag ?? string.Empty, DateTime.UtcNow));
            Debug(
                $"[ORDER_MAINT][CANCEL_QUEUE] enqueued | order={order.Name} state={order.OrderState} tag={tag} size={state.CancelQueue.Count}");
            return true;
        }

        public int ProcessCancellationQueue(OrderMaintenanceState state, int maxAttemptsPerPass = DefaultMaxAttemptsPerPass)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (state.CancelQueue.Count == 0)
                return 0;

            if (!_host.IsActivationReady)
            {
                Debug(
                    "[ORDER_MAINT][TODO] Host shell must wire cancel hooks before activation | missing="
                    + _host.DescribeMissingActivationHooks());
                return 0;
            }

            List<(Order Order, string Tag, DateTime EnqueuedAtUtc)> requeue =
                new List<(Order Order, string Tag, DateTime EnqueuedAtUtc)>();
            List<Order> workingSnapshot = SnapshotWorkingOrders(state);
            int processed = 0;

            while (state.CancelQueue.Count > 0 && maxAttemptsPerPass-- > 0)
            {
                (Order Order, string Tag, DateTime EnqueuedAtUtc) item = state.CancelQueue.Dequeue();
                processed++;

                DateTime now = DateTime.UtcNow;
                string orderId = item.Order?.OrderId;

                if ((now - item.EnqueuedAtUtc).TotalSeconds > 3.0)
                {
                    int attempts = 0;
                    if (!string.IsNullOrEmpty(orderId))
                        state.CancelAttempts.TryGetValue(orderId, out attempts);

                    Debug(
                        $"[ORDER_MAINT][CANCEL_QUEUE] timeout drop | order={item.Order?.Name} tag={item.Tag} age={(now - item.EnqueuedAtUtc).TotalSeconds:F1}s");

                    if (attempts >= 6 && AtRiskNow(state))
                    {
                        Risk(
                            $"[ORDER_MAINT][CANCEL_ESCALATE] attempts={attempts} | timeout-before-attempt7 | requesting flatten");
                        _host.TriggerFlatten?.Invoke("CancelFail_Escalate");
                    }

                    ClearCancelMetadata(state, orderId);
                    continue;
                }

                Order freshOrder = RehydrateWorkingOrder(workingSnapshot, orderId);
                if (freshOrder == null && _host.CanCancelOrder(item.Order))
                    freshOrder = item.Order;

                if (freshOrder == null)
                {
                    Debug($"[ORDER_MAINT][CANCEL_QUEUE] terminal drop | order={item.Order?.Name} tag={item.Tag}");
                    ClearCancelMetadata(state, orderId);
                    continue;
                }

                if (!_host.CanCancelOrder(freshOrder))
                {
                    Debug(
                        $"[ORDER_MAINT][CANCEL_QUEUE] drop non-cancellable refresh | order={freshOrder.Name} state={freshOrder.OrderState} tag={item.Tag}");
                    ClearCancelMetadata(state, orderId);
                    continue;
                }

                if (!string.IsNullOrEmpty(orderId)
                    && state.CancelNextAtUtc.TryGetValue(orderId, out DateTime nextAt)
                    && now < nextAt)
                {
                    requeue.Add(item);
                    continue;
                }

                try
                {
                    _host.CancelOrderRequest(freshOrder);
                    Debug(
                        $"[ORDER_MAINT][CANCEL_QUEUE] cancel requested | order={freshOrder.Name} state={freshOrder.OrderState} tag={item.Tag}");
                    ClearCancelMetadata(state, orderId);
                }
                catch (Exception ex)
                {
                    int attempts = 0;
                    if (!string.IsNullOrEmpty(orderId))
                    {
                        state.CancelAttempts.TryGetValue(orderId, out attempts);
                        attempts = Math.Max(0, attempts) + 1;
                        state.CancelAttempts[orderId] = attempts;

                        int backoffMs = (int)Math.Min(2000, 150 * Math.Pow(2, Math.Max(0, attempts - 1)));
                        int jitterMs = (int)(now.Ticks % 100);
                        state.CancelNextAtUtc[orderId] = now.AddMilliseconds(backoffMs + jitterMs);

                        if (attempts >= 6 && AtRiskNow(state))
                        {
                            Risk(
                                $"[ORDER_MAINT][CANCEL_ESCALATE] attempts={attempts} | retry ceiling hit | requesting flatten");
                            _host.TriggerFlatten?.Invoke("CancelFail_Escalate");
                        }
                    }

                    Debug(
                        $"[ORDER_MAINT][CANCEL_QUEUE] cancel failed requeue | order={item.Order?.Name} tag={item.Tag} attempts={attempts} err={ex.Message}");
                    requeue.Add(item);
                }
            }

            foreach ((Order Order, string Tag, DateTime EnqueuedAtUtc) item in requeue)
                state.CancelQueue.Enqueue(item);

            return processed;
        }

        public bool SafeCancelOrder(OrderMaintenanceState state, Order order, string context = "")
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (order == null)
                return false;

            if (_host.CanCancelOrder != null && !_host.CanCancelOrder(order))
                return false;

            return EnqueueOrderCancellation(state, order, context);
        }

        public int CancelAllWorkingOrders(
            OrderMaintenanceState state,
            string reason,
            OrderMaintenanceCancellationScope scope = OrderMaintenanceCancellationScope.AllOrders)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            int enqueued = 0;
            foreach (Order order in SnapshotWorkingOrders(state))
            {
                if (order == null)
                    continue;

                if (_host.CanCancelOrder != null && !_host.CanCancelOrder(order))
                    continue;

                bool includeOrder = scope == OrderMaintenanceCancellationScope.AllOrders;
                if (!includeOrder)
                    includeOrder = _host.IsPrimaryEntryOrder == null || !_host.IsPrimaryEntryOrder(order);

                if (!includeOrder)
                    continue;

                if (EnqueueOrderCancellation(state, order, $"{reason}|{order.Name}"))
                    enqueued++;
            }

            Debug(
                $"[ORDER_MAINT] cancel-all queued | reason={reason} scope={scope} count={enqueued}");
            return enqueued;
        }

        public void TrackWorkingOrder(OrderMaintenanceState state, Order order, OrderState orderState)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (order == null || !SecondLegOrderStateExtensions.IsWorkingLike(orderState))
                return;

            RemoveWorkingOrder(state, order);
            state.WorkingOrders.Add(order);
        }

        public void ReleaseTerminalOrder(OrderMaintenanceState state, Order order)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (order == null)
                return;

            RemoveWorkingOrder(state, order);
            ClearCancelMetadata(state, order.OrderId);
            _host.NullOrderReference?.Invoke(order);
        }

        public Order FindWorkingExitForRole(OrderMaintenanceState state, SecondLegOrderRole role)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            foreach (Order order in SnapshotWorkingOrders(state))
            {
                if (order == null || !SecondLegOrderStateExtensions.IsWorkingLike(order.OrderState))
                    continue;

                if (role == SecondLegOrderRole.StopLoss
                    && _host.IsProtectiveStopOrder != null
                    && _host.IsProtectiveStopOrder(order))
                {
                    return order;
                }
            }

            return null;
        }

        public int WorkingExitCount(OrderMaintenanceState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            int count = 0;
            foreach (Order order in SnapshotWorkingOrders(state))
            {
                if (order == null || !SecondLegOrderStateExtensions.IsWorkingLike(order.OrderState))
                    continue;

                bool isEntryOrder = _host.IsPrimaryEntryOrder != null && _host.IsPrimaryEntryOrder(order);
                if (!isEntryOrder)
                    count++;
            }

            return count;
        }

        public bool HasCoverageNow(OrderMaintenanceState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (_host.HasCoverageNow != null)
                return _host.HasCoverageNow();

            return FindWorkingExitForRole(state, SecondLegOrderRole.StopLoss) != null;
        }

        private bool AtRiskNow(OrderMaintenanceState state)
        {
            int positionQuantity = _host.PositionQuantity != null ? _host.PositionQuantity() : 0;
            return positionQuantity != 0 && !HasCoverageNow(state);
        }

        private static void ClearCancelMetadata(OrderMaintenanceState state, string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return;

            state.CancelAttempts.Remove(orderId);
            state.CancelNextAtUtc.Remove(orderId);
        }

        private static List<Order> SnapshotWorkingOrders(OrderMaintenanceState state)
        {
            return new List<Order>(state.WorkingOrders ?? Array.Empty<Order>());
        }

        private static Order RehydrateWorkingOrder(List<Order> workingSnapshot, string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return null;

            for (int i = 0; i < workingSnapshot.Count; i++)
            {
                Order candidate = workingSnapshot[i];
                if (candidate?.OrderId == orderId)
                    return candidate;
            }

            return null;
        }

        private static void RemoveWorkingOrder(OrderMaintenanceState state, Order order)
        {
            string orderId = order.OrderId;
            for (int i = state.WorkingOrders.Count - 1; i >= 0; i--)
            {
                Order candidate = state.WorkingOrders[i];
                if (candidate == null)
                {
                    state.WorkingOrders.RemoveAt(i);
                    continue;
                }

                if (!string.IsNullOrEmpty(orderId))
                {
                    if (candidate.OrderId == orderId)
                        state.WorkingOrders.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(candidate, order))
                    state.WorkingOrders.RemoveAt(i);
            }
        }

        private void Debug(string message)
        {
            _host.WriteDebugLog?.Invoke(message);
        }

        private void Risk(string message)
        {
            _host.WriteRiskLog?.Invoke(message);
        }
    }
}
