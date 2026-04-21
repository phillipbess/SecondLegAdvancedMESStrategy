using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        // M1 host-shell contract for the donor runtime-core port.
        // This file intentionally defines the shared runtime surface before the
        // heavier donor partials are imported.

        private readonly List<Order> _workingOrders = new List<Order>();
        private readonly object _exitOpQueueLock = new object();
        private readonly object _cancelQueueLock = new object();

        private readonly Queue<Action> _exitOpQueue = new Queue<Action>();
        private readonly Queue<(Order Order, string Tag, DateTime EnqueuedAt)> _cancelQueue =
            new Queue<(Order Order, string Tag, DateTime EnqueuedAt)>();

        private readonly Dictionary<string, int> _cancelAttempts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _cancelNextAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private const double ReconnectGapThresholdSeconds = 15.0;
        private const int FlattenRepriceTicks = 10;
        private const int FlattenAwaitCancelWindowMs = 1000;
        private const int SubmissionAuthorityStopSubmitCooldownMs = 3000;

        private ExitFlowState _exitState = ExitFlowState.Flat;
        private bool _exitOpBusy;
        private DateTime _exitOpPendingUntil = DateTime.MinValue;
        private int _exitEpoch;
        private DateTime? _flattenAwaitCancelsUntil;
        private DateTime? _flattenPostSweepUntil;
        private bool _flattenMarketSubmitted;

        private bool isFinalizingTrade;
        private bool suppressAllOrderSubmissions;
        private bool tradeOpen;
        private bool controllerStopPlaced;
        private bool autoDisabled;
        private bool _globalKillSwitch;
        private bool stateRestorationInProgress;

        private DateTime _lastStopStateChangeAt = DateTime.MinValue;
        private DateTime _coverageGraceUntil = DateTime.MinValue;
        private DateTime _stopFillLikelyUntilUtc = DateTime.MinValue;
        private DateTime _lastOcoResubmitAt = DateTime.MinValue;
        private DateTime _lastMarketDataUtc = DateTime.MinValue;
        private bool _awaitingReconnectGrace;

        private string currentTradeID = string.Empty;
        private string _activeTradeId = string.Empty;
        private string _currentExitOco = string.Empty;
        private string _activeEntrySignal = string.Empty;
        private string _currentEntryTag = string.Empty;
        private string _recoveryResolution = string.Empty;
        private int _ocoCounter;
        private Order _nativeProtectiveStopOrder;
        private Order _nativeProtectiveReplaceFromOrder;
        private Order _nativeProtectiveReplaceToOrder;
        private string _nativeProtectiveReplaceOco = string.Empty;
        private Order _nativeFlattenOrder;

        private bool stateRestored;
        private string stateFilePath = string.Empty;

        private MarketPosition entryPositionSide = MarketPosition.Flat;
        private DateTime entryFillTime = DateTime.MinValue;
        private double entryFillPrice;
        private int entryQuantity;
        private double entryPrice;
        private double avgEntryPrice;
        private int initialPositionSize;
        private double tradeRiskPerContract;
        private double initialStopPrice;
        private double initialTradeRisk;

        private bool dailyStopHit;
        private bool dailyLossHit;
        private bool unknownExitEconomicsBlock;

        private double workingStopPrice;
        private bool hasWorkingStop;
        private double currentControllerStopPrice;

        private readonly SubmissionAuthorityState _submissionAuthorityState = new SubmissionAuthorityState();
        private readonly Dictionary<string, string> _submissionRetryCorrelationBySignal =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _submissionAuthorityEmitOnce =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);

        private RuntimeTradeManager tradeManager;
        private ExitController exitCtl;
        private RuntimeSnapshotScaffold _lastRuntimeSnapshot;
        private SubmissionAuthorityScaffold _submissionAuthority;
        private readonly OrderMaintenanceState _orderMaintenanceState = new OrderMaintenanceState();
        private OrderMaintenanceScaffold _orderMaintenance;

        private bool _stopSubmitInFlight;
        private bool _stopSubmissionPending;
        private DateTime _lastStopSubmitAtUtc = DateTime.MinValue;
        private DateTime _lastStopSubmissionAtUtc = DateTime.MinValue;

        private void InitializeRuntimeCoreIfNeeded()
        {
            tradeManager ??= new RuntimeTradeManager(this);
            exitCtl ??= new ExitController(this);
            _ = OrderMaintenance;
        }

        private void BeginAtomicFinalization(string reason)
        {
            SubmissionAuthorityState state = SnapshotSubmissionAuthorityState();
            SubmissionAuthority.BeginAtomicFinalization(state, reason ?? string.Empty);
            ApplySubmissionAuthorityState(state);
        }

        private bool MaySubmitOrders(string context = "")
        {
            SubmissionAuthorityState state = SnapshotSubmissionAuthorityState();
            bool maySubmit = SubmissionAuthority.MaySubmitOrders(state, context ?? string.Empty);
            ApplySubmissionAuthorityState(state);
            return maySubmit;
        }

        private void TriggerFlatten(string reason)
        {
            if (_exitState == ExitFlowState.Flattening || _exitState == ExitFlowState.PostSweep)
            {
                WriteDebugLog($"[FLATTEN] duplicate ignored | reason={reason}");
                return;
            }

            BeginAtomicFinalization(reason);
            _exitState = ExitFlowState.Flattening;
            _flattenInFlight = true;
            _flattenMarketSubmitted = false;
            _flattenRequestCount++;
            WriteDebugEvent("FLATTEN", "phase=requested", $"reason={reason}");
            WriteTradeContextLog(
                "FLATTEN_REQUEST",
                $"qty={Math.Abs(Position.Quantity)}",
                $"reason={reason}",
                $"state={_exitState}");
            RepriceWorkingProtectiveOrdersForFlatten("TriggerFlatten");
            CancelAllWorkingOrders(reason ?? "TriggerFlatten");

            if (_entryOrder != null && CanCancelOrderForOrderMaintenance(_entryOrder))
                SafeCancelOrder(_entryOrder, reason ?? "TriggerFlatten");

            _flattenAwaitCancelsUntil = DateTime.UtcNow.AddMilliseconds(FlattenAwaitCancelWindowMs);
            _flattenPostSweepUntil = null;
            ContinueFlattenProtocol(reason ?? "TriggerFlatten");
        }

        private void ContinueFlattenProtocol(string context = "")
        {
            bool hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();
            bool hasWorkingExits = WorkingResidualExitCount() > 0;

            if (_exitState == ExitFlowState.Flattening)
            {
                if (Position.Quantity == 0 && !hasWorkingEntry && !hasWorkingExits)
                {
                    CompleteFlattenProtocol(context, "Flattening.NoLiveExposure");
                    return;
                }

                if (_flattenAwaitCancelsUntil.HasValue && DateTime.UtcNow >= _flattenAwaitCancelsUntil.Value)
                {
                    if (!_flattenMarketSubmitted && Position.Quantity != 0)
                    {
                        int flattenQty = Math.Abs(Position.Quantity);
                        string fromEntrySignal = !string.IsNullOrEmpty(_activeEntrySignal)
                            ? _activeEntrySignal
                            : PlannedEntrySignalName();
                        ExitController.FlattenSubmitResult flattenSubmitResult =
                            exitCtl != null
                                ? exitCtl.SubmitFlattenMarket(flattenQty, fromEntrySignal, context ?? "TriggerFlatten")
                                : ExitController.FlattenSubmitResult.Failed;
                        if (flattenSubmitResult == ExitController.FlattenSubmitResult.Submitted)
                        {
                            SetFlattenRecoveryState(false, false, $"{context}.FlattenSubmit");
                            WriteTradeContextLog(
                                "FLATTEN_SUBMIT",
                                $"signal={fromEntrySignal}",
                                $"qty={flattenQty}",
                                $"context={context}",
                                $"result={flattenSubmitResult}");
                            RefreshRuntimeSnapshot($"{context}.FlattenSubmit");
                            return;
                        }

                        _flattenMarketSubmitted = false;
                        SetFlattenRecoveryState(true, false, $"{context}.FlattenFailed");
                        _flattenAwaitCancelsUntil = DateTime.UtcNow.AddMilliseconds(250);
                        WriteTradeContextLog(
                            "FLATTEN_SUBMIT",
                            $"signal={fromEntrySignal}",
                            $"qty={flattenQty}",
                            $"context={context}",
                            $"result={flattenSubmitResult}");
                        RefreshRuntimeSnapshot($"{context}.FlattenFailed");
                        return;
                    }

                    if (!_flattenMarketSubmitted && Position.Quantity == 0)
                    {
                        _flattenMarketSubmitted = true;
                        _flattenPostSweepUntil = DateTime.UtcNow.AddSeconds(3);
                        _exitState = ExitFlowState.PostSweep;
                    }
                }
            }

            if (_exitState != ExitFlowState.PostSweep)
                return;

            if (hasWorkingEntry || hasWorkingExits)
                CancelResidualWorkingOrdersForFlattenSweep();

            bool postSweepExpired = _flattenPostSweepUntil.HasValue && DateTime.UtcNow >= _flattenPostSweepUntil.Value;
            if ((Position.Quantity == 0 && !hasWorkingEntry && !hasWorkingExits) || postSweepExpired)
                CompleteFlattenProtocol(context, postSweepExpired ? "PostSweep.Timeout" : "PostSweep.Clean");
        }

        private void CompleteFlattenProtocol(string context, string completionReason)
        {
            if (_entryPending || _hasWorkingEntry)
            {
                _entryPending = false;
                _hasWorkingEntry = false;
                _entryOrder = null;
            }

            _flattenInFlight = false;
            _flattenMarketSubmitted = false;
            _flattenAwaitCancelsUntil = null;
            _flattenPostSweepUntil = null;
            isFinalizingTrade = false;
            suppressAllOrderSubmissions = false;
            SetFlattenRecoveryState(false, false, $"{context}.{completionReason}");
            _exitState = ExitFlowState.Flat;
            WriteTradeContextLog(
                "FLATTEN_COMPLETE",
                $"reason={completionReason}",
                $"context={context}",
                $"qty={Math.Abs(Position.Quantity)}");
            RefreshRuntimeSnapshot($"{context}.{completionReason}");
        }

        private void EnqueueEnsureProtectiveExit(double price, int quantity, string tag, string reason, int delayMs = 0)
        {
            if (isFinalizingTrade)
            {
                bool isProtective = !string.IsNullOrEmpty(tag)
                    && tag.IndexOf("StopLoss", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!(isProtective && Position.Quantity != 0))
                {
                    WriteDebugEvent("EXIT_OP_DROP", $"reason={reason}", "phase=finalizing");
                    return;
                }

                WriteDebugEvent("EXIT_OP_CARVEOUT", $"reason={reason}", "phase=finalizing", $"qty={Position.Quantity}");
            }

            string label = $"ENSURE_PROTECTIVE {reason}";
            Action ensureProtective = () =>
            {
                if (Position.Quantity == 0)
                {
                    if (TryBuildProtectiveRetry(reason, out int attempt, out int maxAttempts, out int nextDelayMs, out string nextReason))
                    {
                        WriteDebugEvent(
                            "EXIT_OP_RETRY",
                            $"attempt={attempt}",
                            $"maxAttempts={maxAttempts}",
                            $"reason={reason}",
                            $"retryInMs={nextDelayMs}");
                        EnqueueEnsureProtectiveExit(price, quantity, tag, nextReason, nextDelayMs);
                    }
                    else
                    {
                        WriteDebugEvent("EXIT_OP_TIMEOUT", $"reason={reason}", "outcome=defer-watchdog");
                    }
                    return;
                }

                exitCtl?.EnsureProtectiveExit(price, quantity, tag, reason);
            };

            if (delayMs > 0)
                EnqueueExitOp(label, ensureProtective, delayMs);
            else
                EnqueueExitOp(label, ensureProtective);
        }

        private void EnqueueExitOp(string label, Action op)
        {
            if (op == null)
                return;

            string safeLabel = string.IsNullOrWhiteSpace(label) ? "unknown" : label.Trim();
            Action wrapped = () =>
            {
                WriteDebugEvent("EXIT_OP_BEGIN", $"label={safeLabel}");
                try
                {
                    op();
                }
                finally
                {
                    WriteDebugEvent("EXIT_OP_END", $"label={safeLabel}");
                }
            };

            lock (_exitOpQueueLock)
            {
                _exitOpQueue.Enqueue(wrapped);
            }

            WriteDebugEvent("EXIT_OP_ENQ", $"label={safeLabel}");
            PumpExitOps(label ?? string.Empty);
        }

        private void EnqueueExitOp(string label, Action op, int delayMs)
        {
            if (op == null)
                return;

            string safeLabel = string.IsNullOrWhiteSpace(label) ? "unknown" : label.Trim();
            if (delayMs <= 0)
            {
                EnqueueExitOp(safeLabel, op);
                return;
            }

            DateTime executeAt = DateTime.UtcNow.AddMilliseconds(delayMs);
            Action delayed = null;
            delayed = () =>
            {
                if (DateTime.UtcNow < executeAt)
                {
                    _exitOpPendingUntil = executeAt.AddMilliseconds(25);
                    lock (_exitOpQueueLock)
                    {
                        _exitOpQueue.Enqueue(delayed);
                    }
                    return;
                }

                _exitOpPendingUntil = DateTime.MinValue;
                op();
            };

            _exitOpPendingUntil = executeAt.AddMilliseconds(25);
            lock (_exitOpQueueLock)
            {
                _exitOpQueue.Enqueue(delayed);
            }

            WriteDebugEvent("EXIT_OP_ENQ", $"label={safeLabel}", $"delayMs={delayMs}");
            PumpExitOps(label ?? string.Empty);
        }

        private void PumpExitOps(string context = "")
        {
            if (_exitOpBusy)
                return;

            while (true)
            {
                Action op = null;
                lock (_exitOpQueueLock)
                {
                    if (_exitOpQueue.Count == 0)
                    {
                        if (_exitOpPendingUntil != DateTime.MinValue && DateTime.UtcNow >= _exitOpPendingUntil)
                        {
                            WriteDebugEvent("EXIT_OP_TIMEOUT_RELEASE", "reason=pending-window-expired");
                            _exitOpPendingUntil = DateTime.MinValue;
                        }
                        return;
                    }

                    op = _exitOpQueue.Dequeue();
                    _exitOpBusy = true;
                }

                try
                {
                    op?.Invoke();
                }
                catch (Exception ex)
                {
                    WriteDebugEvent("EXIT_OP_ERROR", $"ctx={context}", $"msg={ex.Message}");
                }
                finally
                {
                    _exitOpBusy = false;
                }
            }
        }

        private bool TryBuildProtectiveRetry(
            string reason,
            out int attempt,
            out int maxAttempts,
            out int nextDelayMs,
            out string nextReason)
        {
            attempt = 0;
            maxAttempts = 0;
            nextDelayMs = 0;
            nextReason = reason ?? string.Empty;

            string currentReason = reason ?? string.Empty;
            bool entryCoverageReason =
                currentReason.IndexOf("EntryFill", StringComparison.OrdinalIgnoreCase) >= 0
                || currentReason.IndexOf("EntryPartFill", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!entryCoverageReason)
                return false;

            Match retryMatch = Regex.Match(currentReason, "\\|RETRY(\\d+)/(\\d+)");
            if (retryMatch.Success)
            {
                attempt = int.TryParse(retryMatch.Groups[1].Value, out int parsedAttempt) ? parsedAttempt + 1 : 2;
                maxAttempts = int.TryParse(retryMatch.Groups[2].Value, out int parsedMaxAttempts) ? parsedMaxAttempts : 5;
            }
            else
            {
                attempt = 1;
                maxAttempts = 5;
            }

            if (attempt > maxAttempts)
                return false;

            nextDelayMs = Math.Min(30 * (1 << (attempt - 1)), 300);
            nextReason = $"{currentReason}|RETRY{attempt}/{maxAttempts}";
            return true;
        }

        private bool IsExitMutateSuppressed(string reason)
        {
            return _exitState != ExitFlowState.Live && Position.Quantity == 0;
        }

        private bool CanMutateProtectiveStop(Order order, string context)
        {
            return order != null && Position.Quantity != 0;
        }

        private double RuntimeCurrentBidSafe()
        {
            double bid = GetCurrentBid();
            if (bid > 0.0)
                return bid;

            if (CurrentBar >= 0)
                return Close[0];

            return 0.0;
        }

        private double RuntimeBestAsk()
        {
            double ask = GetCurrentAsk();
            if (ask > 0.0)
                return ask;

            if (CurrentBar >= 0)
                return Close[0];

            return 0.0;
        }

        private void RepriceWorkingProtectiveOrdersForFlatten(string context)
        {
            foreach (Order order in _workingOrders)
            {
                if (order == null
                    || !OrderStateExtensions.IsWorkingLike(order.OrderState)
                    || !order.IsProtectiveStop()
                    || !CanMutateProtectiveStop(order, context))
                {
                    continue;
                }

                double repriceAway = order.OrderAction switch
                {
                    OrderAction.Sell or OrderAction.SellShort => RuntimeCurrentBidSafe() - (FlattenRepriceTicks * TickSize),
                    OrderAction.Buy or OrderAction.BuyToCover => RuntimeBestAsk() + (FlattenRepriceTicks * TickSize),
                    _ => Position.Quantity > 0
                        ? RuntimeCurrentBidSafe() - (FlattenRepriceTicks * TickSize)
                        : RuntimeBestAsk() + (FlattenRepriceTicks * TickSize),
                };

                if (repriceAway <= 0.0)
                    continue;

                double rounded = Instrument.MasterInstrument.RoundToTickSize(repriceAway);
                exitCtl?.ChangeUnmanaged(order, rounded, "FLATTEN_REPRICE");
                WriteDebugLog($"[FLATTEN_REPRICE] {order.Name} repriced away to {rounded:F2} ctx={context}");
            }
        }

        private void BeginReconnectGrace(string context)
        {
            _awaitingReconnectGrace = false;
            _orphanedOrdersScanComplete = false;
            WriteDebugEvent(
                "RECONNECT_GRACE",
                "phase=begin",
                $"ctx={context}",
                $"qty={Math.Abs(Position.Quantity)}",
                $"avg={Position.AveragePrice:F2}");
            WriteTradeContextLog(
                "RECOVERY_RECONNECT_GRACE",
                $"ctx={context}",
                $"qty={Math.Abs(Position.Quantity)}",
                $"avg={Position.AveragePrice:F2}");
            SetReconnectObservationState(false, Math.Abs(Position.Quantity), Position.AveragePrice);
            RefreshRuntimeSnapshot($"{context}.ReconnectGrace");
        }

        private void EvaluateRealtimeReconnectGrace(string context)
        {
            if (State != State.Realtime || _orphanedOrdersScanComplete || stateRestorationInProgress)
                return;

            int liveQty = Math.Abs(Position.Quantity);
            bool durableLiveTradeContext = HasDurableLiveTradeContext();
            if (!durableLiveTradeContext && liveQty == 0)
            {
                WriteDebugLog($"[RECONNECT_OBSERVATION] flat | ctx={context}");
                WriteTradeContextLog("RECOVERY_RECONNECT_OUTCOME", $"ctx={context}", "outcome=flat", "qty=0");
                SetReconnectObservationState(true, 0, 0.0);
                RefreshRuntimeSnapshot($"{context}.ReconnectFlat");
                return;
            }

            RebuildWorkingOrderTruthFromBroker($"{context}.ReconnectObservation");
            RestoreProtectiveReplaceLineageFromBroker($"{context}.ReconnectObservation");
            SetReconnectObservationState(true, liveQty, Position.AveragePrice);

            if (liveQty == 0)
            {
                WriteDebugLog($"[RECONNECT_OBSERVATION] no-live-qty | ctx={context}");
                WriteTradeContextLog("RECOVERY_RECONNECT_OUTCOME", $"ctx={context}", "outcome=no-live-qty", "qty=0");
                RefreshRuntimeSnapshot($"{context}.ReconnectNoLiveQty");
                return;
            }

            bool hasOwnedCoverage = HasWorkingProtectiveCoverage() && SumWorkingProtectiveCoverageQty() >= liveQty;
            if (hasOwnedCoverage)
            {
                _explicitCoverageLossPending = false;
                _protectiveCoverageAmbiguous = false;
                WriteDebugEvent("RECONNECT_OBSERVATION", "outcome=covered-owned", $"ctx={context}", $"qty={liveQty}");
                WriteTradeContextLog("RECOVERY_RECONNECT_OUTCOME", $"ctx={context}", "outcome=covered-owned", $"qty={liveQty}");
                SetProtectiveCoverageDisposition("covered-owned", $"{context}.ReconnectCovered");
                SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                SetRecoveryResolution("covered-owned", $"{context}.ReconnectCovered");
                RefreshRuntimeSnapshot($"{context}.ReconnectCovered");
                return;
            }

            if (ReconcileCompatibleBrokerCoverageForRecovery($"{context}.ReconnectObservation", liveQty))
            {
                WriteDebugEvent("RECONNECT_OBSERVATION", "outcome=compatible-broker-coverage", $"ctx={context}", $"qty={liveQty}");
                WriteTradeContextLog("RECOVERY_RECONNECT_OUTCOME", $"ctx={context}", "outcome=compatible-broker-coverage", $"qty={liveQty}");
                SetRecoveryHoldState(true, false, false, false, true, "compatible-broker-coverage");
                SetRecoveryResolution("compatible-broker-coverage", $"{context}.ReconnectCompatible");
                RefreshRuntimeSnapshot($"{context}.ReconnectCompatible");
                return;
            }

            _explicitCoverageLossPending = true;
            WriteDebugEvent("RECONNECT_OBSERVATION", "outcome=pending-owned", $"ctx={context}", $"qty={liveQty}");
            WriteTradeContextLog("RECOVERY_RECONNECT_OUTCOME", $"ctx={context}", "outcome=pending-owned", $"qty={liveQty}");
            SetProtectiveCoverageDisposition("pending-owned", $"{context}.ReconnectPending");
            ValidateStopQuantity($"{context}.ReconnectPending");
            RefreshRuntimeSnapshot($"{context}.ReconnectPending");
        }

        private void BindPrimaryEntryTransportHandle(Order order, string context)
        {
            if (order == null)
                return;

            _entryOrder = order;
            BindRuntimeOrderHandle(order, context);
        }

        private void BindProtectiveTransportHandle(Order order, string oco, string context)
        {
            if (order == null)
                return;

            bool replaceTarget =
                !string.IsNullOrEmpty(_nativeProtectiveReplaceOco)
                && !MatchesOrderHandle(_nativeProtectiveReplaceFromOrder, order)
                && (string.Equals(order.Oco ?? string.Empty, _nativeProtectiveReplaceOco, StringComparison.Ordinal)
                    || string.Equals(oco ?? string.Empty, _nativeProtectiveReplaceOco, StringComparison.Ordinal));

            if (replaceTarget)
            {
                _nativeProtectiveReplaceToOrder = order;
                SyncProtectiveReplaceLineageState("replace-target-bound", context);
                WriteTradeLifecycleLog(
                    "TRANSPORT_BIND",
                    order,
                    $"context={context}",
                    "transportRole=protective-replace-target",
                    $"oco={_nativeProtectiveReplaceOco}");
                WriteDebugEvent("TRANSPORT_BIND", $"ctx={context}", "role=protective-replace-target", $"oco={_nativeProtectiveReplaceOco}");
                CancelProtectiveReplaceSourceIfReady(context);
            }

            _nativeProtectiveStopOrder = order;
            if (!string.IsNullOrEmpty(oco))
                _currentExitOco = oco;

            BindRuntimeOrderHandle(order, context);
        }

        private void BindFlattenTransportHandle(Order order, string context)
        {
            if (order == null)
                return;

            _nativeFlattenOrder = order;
            _flattenMarketSubmitted = true;
            if (_exitState == ExitFlowState.Flattening)
            {
                _flattenPostSweepUntil = DateTime.UtcNow.AddSeconds(3);
                _exitState = ExitFlowState.PostSweep;
            }
            BindRuntimeOrderHandle(order, context);
        }

        private Order ResolveBoundProtectiveStopHandle()
        {
            if (_nativeProtectiveStopOrder != null
                && OrderStateExtensions.IsWorkingLike(_nativeProtectiveStopOrder.OrderState))
            {
                return _nativeProtectiveStopOrder;
            }

            return FindWorkingExitForRole(OrderRole.StopLoss);
        }

        private void BindRuntimeOrderHandle(Order order, string context)
        {
            if (order == null || !OrderStateExtensions.IsWorkingLike(order.OrderState))
                return;

            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            OrderMaintenance.TrackWorkingOrder(state, order, order.OrderState);
            ApplyOrderMaintenanceState(state);
            WriteTradeLifecycleLog(
                "TRANSPORT_BIND",
                order,
                $"context={context}",
                $"name={order.Name}",
                $"workingState={order.OrderState}");
            WriteDebugEvent("TRANSPORT_BIND", $"ctx={context}", $"name={order.Name}", $"state={order.OrderState}");
        }

        private bool MatchesOrderHandle(Order left, Order right)
        {
            if (left == null || right == null)
                return false;

            if (ReferenceEquals(left, right))
                return true;

            if (!string.IsNullOrEmpty(left.OrderId) && string.Equals(left.OrderId, right.OrderId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrEmpty(left.Id) && string.Equals(left.Id, right.Id, StringComparison.Ordinal))
                return true;

            return false;
        }

        private static bool MatchesTransportIdentity(Order order, string identity)
        {
            if (order == null || string.IsNullOrEmpty(identity))
                return false;

            return string.Equals(order.OrderId ?? string.Empty, identity, StringComparison.Ordinal)
                || string.Equals(order.Id ?? string.Empty, identity, StringComparison.Ordinal)
                || string.Equals(order.Name ?? string.Empty, identity, StringComparison.Ordinal);
        }

        private static string ResolveTransportOrderIdentity(Order order)
        {
            if (order == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(order.OrderId))
                return order.OrderId;

            if (!string.IsNullOrEmpty(order.Id))
                return order.Id;

            return order.Name ?? string.Empty;
        }

        private void SyncProtectiveReplaceLineageState(
            string disposition,
            string context,
            string reason = "",
            DateTime? startedAtUtc = null)
        {
            SetProtectiveReplaceLineageState(
                disposition,
                context,
                reason,
                ResolveTransportOrderIdentity(_nativeProtectiveReplaceFromOrder),
                ResolveTransportOrderIdentity(_nativeProtectiveReplaceToOrder),
                _nativeProtectiveReplaceOco,
                startedAtUtc ?? _protectiveReplaceStartedAtUtc);
        }

        private void PreserveRestoredRecoveryHoldState()
        {
            SetRecoveryHoldState(
                _orphanAdoptionPending,
                _protectiveReplacePending,
                _protectiveReplaceFailurePending,
                _protectiveReplaceRejected,
                _adoptDeferPending,
                _adoptDeferReason);

            if (HasPendingRecoveryResolution())
                return;

            _recoveryResolution = string.Empty;

            if (_protectiveReplacePending)
                SetRecoveryResolution("replace-pending", "PreserveRestoredRecoveryHoldState");
            else if (_protectiveReplaceFailurePending)
                SetRecoveryResolution("replace-failed", "PreserveRestoredRecoveryHoldState");
            else if (_protectiveReplaceRejected)
                SetRecoveryResolution("replace-rejected", "PreserveRestoredRecoveryHoldState");
            else if (_orphanAdoptionPending || _adoptDeferPending || _protectiveCoverageAmbiguous)
                SetRecoveryResolution("compatible-broker-coverage", "PreserveRestoredRecoveryHoldState");
            else if (_explicitCoverageLossPending)
                SetRecoveryResolution("restore-missing-stop", "PreserveRestoredRecoveryHoldState");
            else
                SetRecoveryResolution("restore-live-position", "PreserveRestoredRecoveryHoldState");
        }

        private void SetRecoveryResolution(string resolution, string context)
        {
            _recoveryResolution = resolution ?? string.Empty;
            WriteDebugLog($"[HARNESS_STATE] recoveryResolution={_recoveryResolution} ctx={context}");
            WriteTradeContextLog("RECOVERY_RESOLUTION", $"resolution={_recoveryResolution}", $"ctx={context}");
        }

        private void ClearRestoreFlatPresentationState(string context)
        {
            _protectiveCoverageAmbiguous = false;
            SetProtectiveCoverageDisposition(string.Empty, context);
            SetProtectiveReplaceLineageState(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                DateTime.MinValue);
        }

        private bool HasPendingRecoveryResolution()
        {
            return string.Equals(_recoveryResolution, "restore-live-position", StringComparison.Ordinal)
                || string.Equals(_recoveryResolution, "compatible-broker-coverage", StringComparison.Ordinal)
                || string.Equals(_recoveryResolution, "restore-missing-stop", StringComparison.Ordinal)
                || string.Equals(_recoveryResolution, "pending-owned", StringComparison.Ordinal)
                || string.Equals(_recoveryResolution, "replace-pending", StringComparison.Ordinal)
                || string.Equals(_recoveryResolution, "replace-failed", StringComparison.Ordinal)
                || string.Equals(_recoveryResolution, "replace-rejected", StringComparison.Ordinal);
        }

        private void ReleaseBoundTransportHandle(Order order, string context)
        {
            if (order == null)
                return;

            bool releasedReplaceSource = false;
            bool releasedReplaceTarget = false;
            string releasedReplaceSourceId = string.Empty;
            string releasedReplaceTargetId = string.Empty;

            if (MatchesOrderHandle(_nativeProtectiveReplaceFromOrder, order))
            {
                releasedReplaceSourceId = ResolveTransportOrderIdentity(_nativeProtectiveReplaceFromOrder);
                _nativeProtectiveReplaceFromOrder = null;
                releasedReplaceSource = true;
            }

            if (MatchesOrderHandle(_nativeProtectiveReplaceToOrder, order))
            {
                releasedReplaceTargetId = ResolveTransportOrderIdentity(_nativeProtectiveReplaceToOrder);
                _nativeProtectiveReplaceToOrder = null;
                releasedReplaceTarget = true;
            }

            if (MatchesOrderHandle(_nativeProtectiveStopOrder, order))
            {
                _nativeProtectiveStopOrder = null;
                WriteTradeLifecycleLog("TRANSPORT_RELEASE", order, $"context={context}", "transportRole=protective");
                WriteDebugEvent("TRANSPORT_RELEASE", $"ctx={context}", "role=protective");
            }

            if (MatchesOrderHandle(_nativeFlattenOrder, order))
            {
                _nativeFlattenOrder = null;
                WriteTradeLifecycleLog("TRANSPORT_RELEASE", order, $"context={context}", "transportRole=flatten");
                WriteDebugEvent("TRANSPORT_RELEASE", $"ctx={context}", "role=flatten");
            }

            if (releasedReplaceSource)
            {
                if (_nativeProtectiveReplaceToOrder != null
                    && OrderStateExtensions.IsWorkingLike(_nativeProtectiveReplaceToOrder.OrderState))
                {
                    FinalizeProtectiveReplaceLineage($"{context}.SourceReleased");
                }
                else
                {
                    FailProtectiveReplaceLineage(
                        $"{context}.SourceReleasedWithoutTarget",
                        releasedReplaceSourceId,
                        releasedReplaceTargetId);
                }
            }

            if (releasedReplaceTarget)
                FailProtectiveReplaceLineage(
                    $"{context}.TargetReleased",
                    releasedReplaceSourceId,
                    releasedReplaceTargetId);
        }

        private void BeginProtectiveReplaceLineage(Order existingOrder, string newOco, string context, string reason)
        {
            _nativeProtectiveReplaceFromOrder = existingOrder;
            _nativeProtectiveReplaceToOrder = null;
            _nativeProtectiveReplaceOco = newOco ?? string.Empty;
            SetRecoveryHoldState(false, true, false, false, false, string.Empty);
            SetProtectiveCoverageDisposition("replace-pending", context);
            SetRecoveryResolution("replace-pending", context);
            SyncProtectiveReplaceLineageState("replace-pending", context, reason, DateTime.UtcNow);
            WriteTradeContextLog(
                "PROTECTIVE_REPLACE_BEGIN",
                $"context={context}",
                $"reason={reason}",
                $"sourceOrder={(existingOrder?.Name ?? string.Empty)}",
                $"sourceOrderId={ResolveTransportOrderIdentity(existingOrder)}",
                $"oco={_nativeProtectiveReplaceOco}");
            WriteDebugEvent("PROTECTIVE_REPLACE_BEGIN", $"ctx={context}", $"from={existingOrder?.Name ?? string.Empty}", $"oco={_nativeProtectiveReplaceOco}");
        }

        private void FinalizeProtectiveReplaceLineage(string context)
        {
            if (_nativeProtectiveReplaceFromOrder == null && _nativeProtectiveReplaceToOrder == null && string.IsNullOrEmpty(_nativeProtectiveReplaceOco))
                return;

            if (_nativeProtectiveReplaceToOrder != null)
                _nativeProtectiveStopOrder = _nativeProtectiveReplaceToOrder;

            _nativeProtectiveReplaceFromOrder = null;
            _nativeProtectiveReplaceToOrder = null;
            _nativeProtectiveReplaceOco = string.Empty;
            SetRecoveryHoldState(false, false, false, false, false, string.Empty);
            SetProtectiveCoverageDisposition("covered-owned", context);
            SetRecoveryResolution("covered-owned", context);
            SetProtectiveReplaceLineageState(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                DateTime.MinValue);
            WriteTradeContextLog("PROTECTIVE_REPLACE_END", $"context={context}", "disposition=covered-owned");
            WriteDebugEvent("PROTECTIVE_REPLACE_END", $"ctx={context}", "disposition=covered-owned");
        }

        private void FailProtectiveReplaceLineage(
            string context,
            string fallbackSourceOrderId = "",
            string fallbackTargetOrderId = "")
        {
            string sourceOrderId = !string.IsNullOrEmpty(fallbackSourceOrderId)
                ? fallbackSourceOrderId
                : ResolveTransportOrderIdentity(_nativeProtectiveReplaceFromOrder);
            string targetOrderId = !string.IsNullOrEmpty(fallbackTargetOrderId)
                ? fallbackTargetOrderId
                : ResolveTransportOrderIdentity(_nativeProtectiveReplaceToOrder);
            string replaceOco = _nativeProtectiveReplaceOco;
            bool oldCoverageStillWorking =
                _nativeProtectiveReplaceFromOrder != null
                && OrderStateExtensions.IsWorkingLike(_nativeProtectiveReplaceFromOrder.OrderState);

            if (oldCoverageStillWorking)
                _nativeProtectiveStopOrder = _nativeProtectiveReplaceFromOrder;

            _nativeProtectiveReplaceToOrder = null;
            _nativeProtectiveReplaceOco = string.Empty;
            if (!oldCoverageStillWorking)
                _nativeProtectiveReplaceFromOrder = null;

            SetRecoveryHoldState(false, false, !oldCoverageStillWorking, _protectiveReplaceRejected, false, string.Empty);
            SetProtectiveCoverageDisposition(oldCoverageStillWorking ? "covered-owned" : "replace-failed", context);
            SetRecoveryResolution(oldCoverageStillWorking ? "covered-owned" : "replace-failed", context);
            SetProtectiveReplaceLineageState(
                oldCoverageStillWorking ? "replace-reverted" : "replace-failed",
                context,
                _protectiveReplaceReason,
                sourceOrderId,
                targetOrderId,
                replaceOco,
                _protectiveReplaceStartedAtUtc);
            RecoverProtectiveReplaceCoverageIfNeeded(context);
            WriteTradeContextLog(
                "PROTECTIVE_REPLACE_FAIL",
                $"context={context}",
                $"oldCoverageStillWorking={oldCoverageStillWorking}",
                $"sourceOrderId={sourceOrderId}",
                $"targetOrderId={targetOrderId}",
                $"oco={replaceOco}");
            WriteDebugEvent("PROTECTIVE_REPLACE_FAIL", $"ctx={context}", $"oldCoverageStillWorking={oldCoverageStillWorking}");
        }

        private void CancelProtectiveReplaceSourceIfReady(string context)
        {
            if (_nativeProtectiveReplaceFromOrder == null
                || _nativeProtectiveReplaceToOrder == null
                || !OrderStateExtensions.IsWorkingLike(_nativeProtectiveReplaceToOrder.OrderState))
            {
                return;
            }

            if (!CanCancelOrderForOrderMaintenance(_nativeProtectiveReplaceFromOrder))
                return;

            SafeCancelOrder(_nativeProtectiveReplaceFromOrder, $"PROTECTIVE_REPLACE_COMMIT|{context}");
            SyncProtectiveReplaceLineageState("replace-commit-pending", context);
            WriteTradeContextLog(
                "PROTECTIVE_REPLACE_COMMIT",
                $"context={context}",
                $"old={_nativeProtectiveReplaceFromOrder.Name}",
                $"new={_nativeProtectiveReplaceToOrder.Name}");
            WriteDebugEvent("PROTECTIVE_REPLACE_COMMIT", $"ctx={context}", $"old={_nativeProtectiveReplaceFromOrder.Name}", $"new={_nativeProtectiveReplaceToOrder.Name}");
        }

        private void RecoverProtectiveReplaceCoverageIfNeeded(string context)
        {
            int liveQty = Math.Abs(Position.Quantity);
            if (liveQty <= 0 || HasWorkingProtectiveCoverage())
                return;

            double candidateStop = currentControllerStopPrice > 0.0
                ? currentControllerStopPrice
                : (workingStopPrice > 0.0 ? workingStopPrice : initialStopPrice);
            if (candidateStop <= 0.0)
                return;

            DateTime nowUtc = DateTime.UtcNow;
            _coverageGraceUntil = nowUtc.AddMilliseconds(500);
            _lastStopStateChangeAt = nowUtc;
            SetProtectiveCoverageDisposition("replace-recovering", context);
            SetRecoveryResolution("pending-owned", context);
            EnqueueEnsureProtectiveExit(
                candidateStop,
                liveQty,
                "StopLoss_PrimaryEntry",
                $"ProtectiveReplaceRecovery|{context}");
            RefreshRuntimeSnapshot($"{context}.ReplaceRecoveryQueued");
        }

        private void RestoreProtectiveReplaceLineageFromBroker(string context)
        {
            bool hasReplaceLineage =
                _protectiveReplacePending
                || _protectiveReplaceFailurePending
                || _protectiveReplaceRejected
                || !string.IsNullOrEmpty(_protectiveReplaceDisposition)
                || !string.IsNullOrEmpty(_protectiveReplaceSourceOrderId)
                || !string.IsNullOrEmpty(_protectiveReplaceTargetOrderId)
                || !string.IsNullOrEmpty(_protectiveReplaceOco);
            if (!hasReplaceLineage)
                return;

            Order rebuiltStop = ResolveBoundProtectiveStopHandle();
            if (rebuiltStop == null)
            {
                if (_protectiveReplacePending && string.IsNullOrEmpty(_protectiveReplaceOco))
                {
                    SetProtectiveReplaceLineageState(
                        "replace-stale",
                        $"{context}.MissingReplaceCorrelation",
                        _protectiveReplaceReason,
                        _protectiveReplaceSourceOrderId,
                        _protectiveReplaceTargetOrderId,
                        _protectiveReplaceOco,
                        _protectiveReplaceStartedAtUtc);
                }

                return;
            }

            if (!string.IsNullOrEmpty(_protectiveReplaceOco)
                && string.Equals(rebuiltStop.Oco ?? string.Empty, _protectiveReplaceOco, StringComparison.Ordinal))
            {
                _nativeProtectiveReplaceToOrder = rebuiltStop;
                FinalizeProtectiveReplaceLineage($"{context}.RecoveredTarget");
                RefreshRuntimeSnapshot($"{context}.RecoveredTarget");
                return;
            }

            if (MatchesTransportIdentity(rebuiltStop, _protectiveReplaceSourceOrderId))
            {
                _nativeProtectiveReplaceFromOrder = rebuiltStop;
                FailProtectiveReplaceLineage($"{context}.RecoveredSource");
                RefreshRuntimeSnapshot($"{context}.RecoveredSource");
                return;
            }

            if (_protectiveReplacePending)
            {
                FailProtectiveReplaceLineage($"{context}.RecoveredMismatchedCoverage");
                RefreshRuntimeSnapshot($"{context}.RecoveredMismatchedCoverage");
            }
        }

        private bool IsProtectiveReplaceSource(Order order)
        {
            return MatchesOrderHandle(_nativeProtectiveReplaceFromOrder, order);
        }

        private bool IsProtectiveReplaceTarget(Order order)
        {
            return MatchesOrderHandle(_nativeProtectiveReplaceToOrder, order)
                || (!string.IsNullOrEmpty(_nativeProtectiveReplaceOco)
                    && order != null
                    && string.Equals(order.Oco ?? string.Empty, _nativeProtectiveReplaceOco, StringComparison.Ordinal));
        }

        private Order FindWorkingExitForRole(OrderRole role)
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            return OrderMaintenance.FindWorkingExitForRole(state, role);
        }

        private string NewExitOco()
        {
            _ocoCounter++;
            _currentExitOco = $"SLA_OCO_{_ocoCounter:D4}";
            return _currentExitOco;
        }

        private int WorkingExitCount()
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            return OrderMaintenance.WorkingExitCount(state);
        }

        private int WorkingResidualExitCount()
        {
            int count = 0;
            foreach (Order order in _workingOrders)
            {
                if (order == null || !OrderStateExtensions.IsWorkingLike(order.OrderState))
                    continue;

                if (IsPrimaryEntryOrder(order) || IsStrategyFlattenOrder(order))
                    continue;

                count++;
            }

            return count;
        }

        private bool HasCoverageNow()
        {
            return HasEffectiveProtectiveCoverage();
        }

        private bool HasEffectiveProtectiveCoverage()
        {
            if (HasWorkingProtectiveCoverage())
                return true;

            return IsProtectiveCoveragePending(DateTime.UtcNow);
        }

        private bool IsOurStrategyExitOrder(Order order)
        {
            if (order == null)
                return false;

            if (TryGetOwnedProtectiveSignal(order, out string ownedSignal))
                return MatchesOwnedPrimaryEntrySignal(ownedSignal);

            return MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);
        }

        private bool IsStrategyFlattenOrder(Order order)
        {
            if (order == null || !order.IsFlattenLike())
                return false;

            if (TryGetOwnedFlattenSignal(order, out string ownedSignal))
                return MatchesOwnedPrimaryEntrySignal(ownedSignal);

            return MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);
        }

        private bool TryGetOwnedFlattenSignal(Order order, out string ownedSignal)
        {
            ownedSignal = string.Empty;
            if (order == null)
                return false;

            string orderName = order.Name ?? string.Empty;
            string prefix = NameTokens.SafetyFlatten + "|";
            if (!orderName.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            ownedSignal = orderName.Substring(prefix.Length);
            return !string.IsNullOrEmpty(ownedSignal);
        }

        private bool IsOrderInCurrentSession(Order order)
        {
            return order != null && order.Time.Date == _sessionAnchor.Date;
        }

        private IEnumerable<Order> EnumerateBrokerWorkingOrders()
        {
            if (Account?.Orders == null)
                yield break;

            foreach (Order order in Account.Orders)
            {
                if (order == null || order.Instrument != Instrument)
                    continue;

                if (!OrderStateExtensions.IsWorkingLike(order.OrderState))
                    continue;

                yield return order;
            }
        }

        private bool IsCompatibleBrokerProtectiveStopOrder(Order order, int liveQty)
        {
            if (order == null || liveQty <= 0)
                return false;

            if (order.IsFlattenLike())
                return false;

            bool stopLike = order.IsProtectiveStop();
            if (!stopLike)
            {
                bool stopType = order.OrderType == OrderType.StopMarket || order.OrderType == OrderType.StopLimit;
                string orderName = order.Name ?? string.Empty;
                stopLike = stopType && orderName.IndexOf("stop", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (!stopLike)
                return false;

            return order.OrderAction == ResolveProtectiveStopAction()
                && Math.Max(0, order.Quantity) > 0;
        }

        private void RebuildWorkingOrderTruthFromBroker(string context)
        {
            var rebuiltOrders = new List<Order>();
            var seenOrderIds = new HashSet<string>(StringComparer.Ordinal);
            Order rebuiltEntry = null;
            Order rebuiltProtectiveStop = null;
            int preservedProtectiveCount = 0;

            foreach (Order order in EnumerateBrokerWorkingOrders())
            {
                bool ownedEntry = TradeManagerIsPrimaryEntryOrder(order) || IsTrackedPrimaryEntryOrder(order);
                bool ownedProtective = IsOurStrategyExitOrder(order) && IsCompatibleBrokerProtectiveStopOrder(order, Math.Abs(Position.Quantity));
                if (!ownedEntry && !ownedProtective)
                    continue;

                string orderId = order.OrderId ?? string.Empty;
                if (!string.IsNullOrEmpty(orderId) && !seenOrderIds.Add(orderId))
                    continue;

                rebuiltOrders.Add(order);

                if (ownedEntry && rebuiltEntry == null)
                    rebuiltEntry = order;

                if (ownedProtective)
                {
                    preservedProtectiveCount++;
                    if (rebuiltProtectiveStop == null)
                        rebuiltProtectiveStop = order;

                    if (string.IsNullOrEmpty(_currentExitOco) && !string.IsNullOrEmpty(order.Oco))
                        _currentExitOco = order.Oco;
                }
            }

            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            SyncWorkingOrderState(state.WorkingOrders, rebuiltOrders);
            ApplyOrderMaintenanceState(state);

            if (rebuiltEntry != null)
            {
                _entryOrder = rebuiltEntry;
                _hasWorkingEntry = true;
                _entryPending = false;
                ClearEntrySubmitInFlight($"RestoreRebuild.{context}");
                TradeStateManager.AddActiveOrder(rebuiltEntry);
            }
            else if (!HasWorkingPrimaryEntryForActiveSignal())
            {
                _entryOrder = null;
                _hasWorkingEntry = false;
            }

            _preservedProtectiveOrderCount = preservedProtectiveCount;
            if (rebuiltProtectiveStop != null)
            {
                _nativeProtectiveReplaceFromOrder = null;
                _nativeProtectiveReplaceToOrder = null;
                _nativeProtectiveReplaceOco = string.Empty;
                _nativeProtectiveStopOrder = rebuiltProtectiveStop;
                controllerStopPlaced = true;
                hasWorkingStop = true;
                workingStopPrice = rebuiltProtectiveStop.StopPrice;
                currentControllerStopPrice = rebuiltProtectiveStop.StopPrice;
                _protectiveCoverageAmbiguous = false;
                SetProtectiveCoverageDisposition("covered-owned", $"RestoreRebuild.{context}");
                SetRecoveryResolution("covered-owned", $"RestoreRebuild.{context}");
            }
            else if (!IsProtectiveCoveragePending(DateTime.UtcNow))
            {
                RecalculateProtectiveCoverageState();
            }

            WriteRiskEvent(
                "ORPHAN_CHECK",
                $"ctx={context}",
                $"rebuilt={rebuiltOrders.Count}",
                $"preservedStops={preservedProtectiveCount}",
                $"hasEntry={(rebuiltEntry != null)}");
            WriteDebugLog(
                $"[RESTORE_REBUILD] ctx={context} rebuilt={rebuiltOrders.Count} preservedStops={preservedProtectiveCount} hasEntry={(rebuiltEntry != null)}");
        }

        private int SumWorkingProtectiveCoverageQty()
        {
            int total = 0;
            foreach (Order order in _workingOrders)
            {
                if (!IsWorkingProtectiveStopOrder(order))
                    continue;

                total += Math.Max(0, order.Quantity);
            }

            return total;
        }

        private bool ReconcileCompatibleBrokerCoverageForRecovery(string context, int quantity)
        {
            int liveQty = Math.Max(0, Math.Abs(quantity));
            if (liveQty == 0)
                return false;

            RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();
            bool hasCompatibleCoverage =
                coverage.CompatibleProtectiveOrderCount > 0
                && coverage.WorkingProtectiveOrderQuantityTotal >= liveQty
                && coverage.OwnedProtectiveOrderCount == 0;
            if (!hasCompatibleCoverage)
                return false;

            controllerStopPlaced = false;
            hasWorkingStop = false;
            _protectiveCoverageAmbiguous = true;
            SetProtectiveCoverageDisposition("compatible-unattributed", context ?? "CompatibleCoverage");
            SetRecoveryResolution("compatible-broker-coverage", context ?? "CompatibleCoverage");
            WriteRiskEvent(
                "ADOPT",
                $"ctx={context}",
                "outcome=compatible-broker-coverage",
                $"qty={liveQty}");
            RefreshRuntimeSnapshot($"CompatibleCoverage.{context}");
            return true;
        }

        private void LogCoverageState(string context)
        {
            WriteRiskEvent(
                "COVERAGE_STATE",
                $"ctx={context}",
                $"covered={HasCoverageNow()}",
                $"pending={IsProtectiveCoveragePending(DateTime.UtcNow)}",
                $"qty={Position.Quantity}",
                $"workingStopQty={SumWorkingProtectiveCoverageQty()}");
        }

        private bool IsProtectiveCoveragePending(DateTime nowUtc)
        {
            if (_coverageGraceUntil != DateTime.MinValue && nowUtc < _coverageGraceUntil)
                return true;

            if (_lastStopStateChangeAt != DateTime.MinValue
                && (nowUtc - _lastStopStateChangeAt).TotalMilliseconds < 500)
            {
                return true;
            }

            if (!_stopSubmissionPending && !_stopSubmitInFlight)
                return false;

            DateTime pendingSinceUtc = _lastStopSubmissionAtUtc > _lastStopSubmitAtUtc
                ? _lastStopSubmissionAtUtc
                : _lastStopSubmitAtUtc;
            if (pendingSinceUtc == DateTime.MinValue)
                return true;

            return (nowUtc - pendingSinceUtc).TotalMilliseconds < 500;
        }

        private void CancelAllWorkingOrders(string reason)
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            int enqueued = OrderMaintenance.CancelAllWorkingOrders(state, reason);
            ApplyOrderMaintenanceState(state);
            WriteDebugLog($"[CANCEL_ALL] reason={reason} queued={enqueued}");
        }

        private void CancelResidualWorkingOrdersForFlattenSweep()
        {
            var seenOrderIds = new HashSet<string>(StringComparer.Ordinal);
            int swept = 0;

            foreach (Order order in _workingOrders)
            {
                if (order == null
                    || !OrderStateExtensions.IsWorkingLike(order.OrderState)
                    || IsStrategyFlattenOrder(order)
                    || !CanCancelOrderForOrderMaintenance(order))
                {
                    continue;
                }

                string orderId = order.OrderId ?? string.Empty;
                if (!string.IsNullOrEmpty(orderId) && seenOrderIds.Contains(orderId))
                    continue;

                SafeCancelOrder(order, "FlattenSweep");
                swept++;
                if (!string.IsNullOrEmpty(orderId))
                    seenOrderIds.Add(orderId);
            }

            WriteRiskEvent(
                "ORPHAN_SWEEP",
                "ctx=FlattenSweep",
                $"queued={swept}",
                $"workingExits={WorkingResidualExitCount()}");
        }

        private void EnqueueOrderCancellation(Order order, string tag)
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            OrderMaintenance.EnqueueOrderCancellation(state, order, tag);
            ApplyOrderMaintenanceState(state);
        }

        private void ProcessCancellationQueue()
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            OrderMaintenance.ProcessCancellationQueue(state);
            ApplyOrderMaintenanceState(state);
        }

        private bool SafeCancelOrder(Order order, string context = "")
        {
            OrderMaintenanceState state = SnapshotOrderMaintenanceState();
            bool queued = OrderMaintenance.SafeCancelOrder(state, order, context);
            ApplyOrderMaintenanceState(state);
            return queued;
        }

        private void NullOrderReference(Order order)
        {
            RemoveWorkingOrderReference(order);
        }

        private void SetStateRestorationInProgress(bool enabled, string context)
        {
            stateRestorationInProgress = enabled;
            WriteDebugLog($"[STATE_RESTORE] {(enabled ? "ON" : "OFF")} ctx={context}");
        }

        private void ResetTradeState()
        {
            WriteOmHealthSummary("ResetTradeState");
            currentTradeID = string.Empty;
            _activeTradeId = string.Empty;
            _activeEntrySignal = string.Empty;
            _currentEntryTag = string.Empty;
            _currentExitOco = string.Empty;
            entryPositionSide = MarketPosition.Flat;
            entryFillTime = DateTime.MinValue;
            entryFillPrice = 0.0;
            entryQuantity = 0;
            entryPrice = 0.0;
            avgEntryPrice = 0.0;
            initialPositionSize = 0;
            tradeRiskPerContract = 0.0;
            initialStopPrice = 0.0;
            initialTradeRisk = 0.0;
            controllerStopPlaced = false;
            tradeOpen = false;
            _lastStopStateChangeAt = DateTime.MinValue;
            _coverageGraceUntil = DateTime.MinValue;
            _stopSubmitInFlight = false;
            _stopSubmissionPending = false;
            _lastStopSubmitAtUtc = DateTime.MinValue;
            _lastStopSubmissionAtUtc = DateTime.MinValue;
            _nativeProtectiveStopOrder = null;
            _nativeProtectiveReplaceFromOrder = null;
            _nativeProtectiveReplaceToOrder = null;
            _nativeProtectiveReplaceOco = string.Empty;
            _nativeFlattenOrder = null;
            ClearEntrySubmitInFlight("ResetTradeState");
            isFinalizingTrade = false;
            suppressAllOrderSubmissions = false;
            _flattenInFlight = false;
            _submissionRetryCorrelationBySignal.Clear();
            _submissionAuthorityEmitOnce.Clear();
            _bestFavorablePrice = 0.0;
            _simpleTrailArmed = false;
            ResetTradeStateScaffold();
            ResetRuntimeScenarioState();
            ResetOmCompatibilityTracking();
            _exitState = ExitFlowState.Flat;
            RefreshRuntimeSnapshot("ResetTradeState");
        }

        private void SaveStrategyState()
        {
            CapturePersistedTradeIdentity("SaveStrategyState");
            if (!ShouldPersistStrategyState())
            {
                RefreshRuntimeSnapshot("SaveStrategyState.Skipped");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(stateFilePath))
                    InitializeStatePersistencePath();

                string stateDirectory = Path.GetDirectoryName(stateFilePath) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(stateDirectory))
                    Directory.CreateDirectory(stateDirectory);

                var stateLines = new List<string>();
                AppendStrategyStateHeaderScaffold(stateLines);
                AppendDurableStateSnapshotScaffold(stateLines);
                AppendTradeScopedStateSnapshotScaffold(stateLines);

                string tempFilePath = stateFilePath + ".tmp";
                File.WriteAllLines(tempFilePath, stateLines);
                if (File.Exists(stateFilePath))
                    File.Replace(tempFilePath, stateFilePath, null);
                else
                    File.Move(tempFilePath, stateFilePath);
            }
            catch (Exception ex)
            {
                WriteDebugEvent("STATE_SAVE_ERROR", $"msg={ex.Message}");
            }

            RefreshRuntimeSnapshot("SaveStrategyState");
        }

        private void RestoreStrategyState()
        {
            if (!ShouldPersistStrategyState())
            {
                RefreshRuntimeSnapshot("RestoreStrategyState.Skipped");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(stateFilePath))
                    InitializeStatePersistencePath();

                if (!File.Exists(stateFilePath))
                {
                    RefreshRuntimeSnapshot("RestoreStrategyState.Missing");
                    return;
                }

                if (!TryParseStateEntries(File.ReadAllLines(stateFilePath), out Dictionary<string, string> stateEntries))
                {
                    RefreshRuntimeSnapshot("RestoreStrategyState.Empty");
                    return;
                }

                RestoreDurableStateScaffold(stateEntries);
                RestoreTradeScopedStateScaffold(stateEntries);

                EnsureActiveTradeIdFromCurrentTradeId("RestoreStrategyState");
                stateRestored = true;

                if (Position.Quantity != 0)
                {
                    tradeOpen = true;
                    controllerStopPlaced = hasWorkingStop;
                    SetReconnectObservationState(false, Math.Abs(Position.Quantity), Position.AveragePrice);
                    PreserveRestoredRecoveryHoldState();
                    SetStateRestorationInProgress(true, "RestoreStrategyState.LivePosition");
                }
                else
                {
                    SetReconnectObservationState(true, 0, 0.0);
                    SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                    ClearRestoreFlatPresentationState("RestoreStrategyState.Flat");
                    SetRecoveryResolution("restore-flat", "RestoreStrategyState.Flat");
                    SetStateRestorationInProgress(false, "RestoreStrategyState.Flat");
                }
            }
            catch (Exception ex)
            {
                WriteDebugEvent("STATE_RESTORE_ERROR", $"msg={ex.Message}");
            }

            RefreshRuntimeSnapshot("RestoreStrategyState");
        }

        private void AnchorEntryExecutionState(Execution execution, double fillPrice, string context)
        {
            EnsureActiveTradeIdFromCurrentTradeId(context ?? "AnchorEntryExecutionState");
            entryFillPrice = fillPrice;
            entryPrice = fillPrice;
            avgEntryPrice = fillPrice;
            if (entryFillTime == DateTime.MinValue)
                entryFillTime = execution?.Time ?? DateTime.MinValue;
            RefreshRuntimeSnapshot(context ?? "AnchorEntryExecutionState");
        }

        private void CountTradeOnFirstEntryFill(Execution execution)
        {
            EnsureActiveTradeIdFromCurrentTradeId("CountTradeOnFirstEntryFill");
            if (string.IsNullOrEmpty(_countedTradeSessionId) && !string.IsNullOrEmpty(currentTradeID))
                _countedTradeSessionId = currentTradeID;

            if (_tradesThisSession < int.MaxValue)
                _tradesThisSession++;

            RefreshRuntimeSnapshot("CountTradeOnFirstEntryFill");
        }

        private void AdvanceRestoreObservation(string context)
        {
            if (!stateRestorationInProgress)
                return;

            int liveQty = Math.Abs(Position.Quantity);
            if (liveQty == 0)
            {
                SetReconnectObservationState(true, 0, 0.0);
                SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                ClearRestoreFlatPresentationState($"{context}.RestoreFlat");
                SetRecoveryResolution("restore-flat", $"{context}.RestoreFlat");
                SetStateRestorationInProgress(false, $"{context}.RestoreFlat");
                RefreshRuntimeSnapshot($"{context}.RestoreFlat");
                return;
            }

            RebuildWorkingOrderTruthFromBroker($"{context}.RestoreObservation");
            RestoreProtectiveReplaceLineageFromBroker($"{context}.RestoreObservation");
            SetReconnectObservationState(true, liveQty, Position.AveragePrice);

            bool hasOwnedCoverage = HasWorkingProtectiveCoverage() && SumWorkingProtectiveCoverageQty() >= liveQty;
            if (hasOwnedCoverage)
            {
                _explicitCoverageLossPending = false;
                _protectiveCoverageAmbiguous = false;
                SetProtectiveCoverageDisposition("covered-owned", $"{context}.RestoreCovered");
                SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                SetRecoveryResolution("covered-owned", $"{context}.RestoreCovered");
                if (string.Equals(_recoveryResolution, "covered-owned", StringComparison.Ordinal)
                    && !_protectiveCoverageAmbiguous
                    && !_orphanAdoptionPending
                    && !_adoptDeferPending
                    && !HasPendingRecoveryResolution())
                {
                    SetStateRestorationInProgress(false, $"{context}.RestoreCovered");
                    RefreshRuntimeSnapshot($"{context}.RestoreCovered");
                    return;
                }

                RefreshRuntimeSnapshot($"{context}.RestoreCoveredHeld");
                return;
            }

            if (ReconcileCompatibleBrokerCoverageForRecovery($"{context}.RestoreObservation", liveQty))
            {
                SetRecoveryHoldState(true, false, false, false, true, "compatible-broker-coverage");
                SetRecoveryResolution("compatible-broker-coverage", $"{context}.RestoreCompatible");
                RefreshRuntimeSnapshot($"{context}.RestoreCompatible");
                return;
            }

            double candidateStop = currentControllerStopPrice > 0.0
                ? currentControllerStopPrice
                : (workingStopPrice > 0.0 ? workingStopPrice : initialStopPrice);
            if (candidateStop <= 0.0)
            {
                SetProtectiveCoverageDisposition("restore-missing-stop", $"{context}.RestoreMissingStop");
                SetRecoveryHoldState(false, false, true, false, true, "restore-missing-stop");
                SetRecoveryResolution("restore-missing-stop", $"{context}.RestoreMissingStop");
                RefreshRuntimeSnapshot($"{context}.RestoreMissingStop");
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (!IsProtectiveCoveragePending(nowUtc))
            {
                _coverageGraceUntil = nowUtc.AddMilliseconds(500);
                _lastStopStateChangeAt = nowUtc;
                EnqueueEnsureProtectiveExit(candidateStop, liveQty, "StopLoss_PrimaryEntry", $"RestoreObservation|{context}");
            }

            SetProtectiveCoverageDisposition("pending-owned", $"{context}.RestorePending");
            SetRecoveryHoldState(false, true, false, false, false, string.Empty);
            SetRecoveryResolution("pending-owned", $"{context}.RestorePending");
            WriteRiskEvent(
                "ADOPT",
                $"ctx={context}",
                "outcome=pending-owned",
                $"qty={liveQty}");
            RefreshRuntimeSnapshot($"{context}.RestorePending");
        }

        private void ValidateStopQuantity(string context)
        {
            int liveQty = Math.Abs(Position.Quantity);
            if (liveQty == 0)
            {
                _coverageGraceUntil = DateTime.MinValue;
                return;
            }

            LogCoverageState(context);
            int protectiveQty = SumWorkingProtectiveCoverageQty();
            if (protectiveQty > liveQty)
            {
                WriteRiskEvent(
                    "DOUBLE STOP DETECTED",
                    $"ctx={context}",
                    $"liveQty={liveQty}",
                    $"protectiveQty={protectiveQty}");
            }
            else if (protectiveQty != liveQty)
            {
                WriteRiskEvent(
                    "STOP_QTY_MISMATCH",
                    $"ctx={context}",
                    $"liveQty={liveQty}",
                    $"protectiveQty={protectiveQty}");
            }

            if (protectiveQty >= liveQty)
            {
                _coverageGraceUntil = DateTime.MinValue;
                return;
            }

            if (ReconcileCompatibleBrokerCoverageForRecovery(context, liveQty))
            {
                _coverageGraceUntil = DateTime.MinValue;
                return;
            }

            double candidateStop = currentControllerStopPrice > 0.0
                ? currentControllerStopPrice
                : (workingStopPrice > 0.0 ? workingStopPrice : initialStopPrice);
            if (candidateStop <= 0.0)
                return;

            DateTime nowUtc = DateTime.UtcNow;
            if (IsProtectiveCoveragePending(nowUtc))
            {
                if (_coverageGraceUntil == DateTime.MinValue || _coverageGraceUntil < nowUtc)
                    _coverageGraceUntil = nowUtc.AddMilliseconds(500);

                RefreshRuntimeSnapshot($"ValidateStopQuantity.{context}.Pending");
                return;
            }

            if (_coverageGraceUntil == DateTime.MinValue)
            {
                _coverageGraceUntil = nowUtc.AddMilliseconds(500);
                _lastStopStateChangeAt = nowUtc;
                EnqueueEnsureProtectiveExit(candidateStop, liveQty, "StopLoss_PrimaryEntry", $"CoverageRepair|{context}");
                RefreshRuntimeSnapshot($"ValidateStopQuantity.{context}.RepairQueued");
                return;
            }

            if (nowUtc < _coverageGraceUntil)
                return;

            if (!_flattenInFlight && !isFinalizingTrade)
            {
                MaybeLogFirstStopSlaMiss(DateTime.Now, context);
                TriggerFlatten("ProtectiveCoverageMissing");
            }
        }

        private bool MaybeTriggerSessionControlFlattenOnEntryFill(DateTime time, string context)
        {
            int hhmm = ToTime(time) / 100;
            bool flattenWindowActive = FlattenBeforeClose && hhmm >= FlattenTimeHhmm;
            bool sessionAllowsExposure = !UseSessionFilter || !TradeRthOnly
                || (hhmm >= StartTradingTimeHhmm && hhmm <= EndMorningTimeHhmm)
                || (hhmm >= StartAfternoonTimeHhmm && hhmm <= LastEntryTimeHhmm);

            if (!flattenWindowActive && sessionAllowsExposure)
                return false;

            TriggerFlatten(flattenWindowActive
                ? $"FlattenWindow|{context}"
                : $"SessionControlBlocked|{context}");
            return true;
        }

        internal bool IsPrimaryEntryOrder(Order order)
        {
            if (order == null)
                return false;

            if (order.IsFlattenLike() || order.IsProtectiveStop())
                return false;

            if (!IsPrimaryEntrySideAction(order.OrderAction))
                return false;

            string plannedSignalName = PlannedEntrySignalName();
            string orderName = order.Name ?? string.Empty;
            return string.Equals(orderName, plannedSignalName, StringComparison.Ordinal)
                || string.Equals(orderName, _activeEntrySignal, StringComparison.Ordinal)
                || string.Equals(orderName, _currentEntryTag, StringComparison.Ordinal)
                || string.Equals(orderName, currentTradeID, StringComparison.Ordinal)
                || MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);
        }

        internal void WriteDebugLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            WriteToLogFile(_debugLogPath, "DEBUG", message);
        }

        private SubmissionAuthorityScaffold SubmissionAuthority =>
            _submissionAuthority ??= new SubmissionAuthorityScaffold(new SubmissionAuthorityHostContract
            {
                WriteDebugLog = WriteDebugLog,
                WriteRiskLog = WriteSubmissionAuthorityRiskLog,
                CancelAllWorkingChildrenAndWait = CancelAllWorkingChildrenAndWait,
                PrintOrderHealthSummary = PrintSubmissionAuthorityOrderHealthSummary,
                CancelAllWorkingOrders = CancelAllWorkingOrdersFromSubmissionAuthority,
                EmitOncePer = EmitSubmissionAuthorityOncePer,
                StopSubmitCooldownMsCurrent = GetSubmissionAuthorityStopSubmitCooldownMs,
                NowEt = GetSubmissionAuthorityNowEt,
                Stamp = StampSubmissionAuthorityTime,
            });

        private OrderMaintenanceScaffold OrderMaintenance =>
            _orderMaintenance ??= new OrderMaintenanceScaffold(new OrderMaintenanceHostContract
            {
                WriteDebugLog = WriteDebugLog,
                WriteRiskLog = WriteSubmissionAuthorityRiskLog,
                TriggerFlatten = TriggerFlatten,
                // Scaffold baseline: CancelOrderRequest = null,
                // Playback v1 activates real NT8 cancel wiring here.
                CancelOrderRequest = CancelOrder,
                NullOrderReference = RemoveWorkingOrderReference,
                CanCancelOrder = CanCancelOrderForOrderMaintenance,
                IsPrimaryEntryOrder = IsPrimaryEntryOrder,
                IsProtectiveStopOrder = IsWorkingProtectiveStopOrder,
                PositionQuantity = () => Position.Quantity,
                HasCoverageNow = HasEffectiveProtectiveCoverage,
            });

        private SubmissionAuthorityState SnapshotSubmissionAuthorityState()
        {
            _submissionAuthorityState.IsFinalizingTrade = isFinalizingTrade;
            _submissionAuthorityState.SuppressAllOrderSubmissions = suppressAllOrderSubmissions;
            _submissionAuthorityState.TradeOpen = tradeOpen;
            _submissionAuthorityState.ControllerStopPlaced = controllerStopPlaced;
            _submissionAuthorityState.AutoDisabled = autoDisabled;
            _submissionAuthorityState.GlobalKillSwitch = _globalKillSwitch;
            _submissionAuthorityState.StopSubmitInFlight = _stopSubmitInFlight;
            _submissionAuthorityState.StopSubmissionPending = _stopSubmissionPending;
            _submissionAuthorityState.LastStopSubmitAtUtc = _lastStopSubmitAtUtc;
            _submissionAuthorityState.LastStopSubmissionAtUtc = _lastStopSubmissionAtUtc;
            _submissionAuthorityState.PositionQuantity = Position.Quantity;

            SyncSubmissionAuthorityRetryCorrelation(
                _submissionAuthorityState.PendingRetryCorrelationBySignal,
                _submissionRetryCorrelationBySignal);

            return _submissionAuthorityState;
        }

        private OrderMaintenanceState SnapshotOrderMaintenanceState()
        {
            SyncWorkingOrderState(_orderMaintenanceState.WorkingOrders, _workingOrders);
            SyncQueueState(_orderMaintenanceState.CancelQueue, _cancelQueue);
            SyncDictionaryState(_orderMaintenanceState.CancelAttempts, _cancelAttempts);
            SyncDictionaryState(_orderMaintenanceState.CancelNextAtUtc, _cancelNextAt);
            return _orderMaintenanceState;
        }

        private void ApplySubmissionAuthorityState(SubmissionAuthorityState state)
        {
            if (state == null)
                return;

            isFinalizingTrade = state.IsFinalizingTrade;
            suppressAllOrderSubmissions = state.SuppressAllOrderSubmissions;
            tradeOpen = state.TradeOpen;
            controllerStopPlaced = state.ControllerStopPlaced;
            autoDisabled = state.AutoDisabled;
            _globalKillSwitch = state.GlobalKillSwitch;
            _stopSubmitInFlight = state.StopSubmitInFlight;
            _stopSubmissionPending = state.StopSubmissionPending;
            _lastStopSubmitAtUtc = state.LastStopSubmitAtUtc;
            _lastStopSubmissionAtUtc = state.LastStopSubmissionAtUtc;

            SyncSubmissionAuthorityRetryCorrelation(
                _submissionRetryCorrelationBySignal,
                state.PendingRetryCorrelationBySignal);
        }

        private void ApplyOrderMaintenanceState(OrderMaintenanceState state)
        {
            if (state == null)
                return;

            SyncWorkingOrderState(_workingOrders, state.WorkingOrders);
            SyncQueueState(_cancelQueue, state.CancelQueue);
            SyncDictionaryState(_cancelAttempts, state.CancelAttempts);
            SyncDictionaryState(_cancelNextAt, state.CancelNextAtUtc);
        }

        private static void SyncSubmissionAuthorityRetryCorrelation(
            IDictionary<string, string> target,
            IDictionary<string, string> source)
        {
            if (target == null || source == null)
                return;

            target.Clear();
            foreach (KeyValuePair<string, string> pair in source)
                target[pair.Key] = pair.Value;
        }

        private static void SyncWorkingOrderState(IList<Order> target, IList<Order> source)
        {
            if (target == null || source == null)
                return;

            target.Clear();
            foreach (Order order in source)
                target.Add(order);
        }

        private static void SyncQueueState<T>(Queue<T> target, Queue<T> source)
        {
            if (target == null || source == null)
                return;

            target.Clear();
            foreach (T item in source)
                target.Enqueue(item);
        }

        private static void SyncDictionaryState<TValue>(
            IDictionary<string, TValue> target,
            IDictionary<string, TValue> source)
        {
            if (target == null || source == null)
                return;

            target.Clear();
            foreach (KeyValuePair<string, TValue> pair in source)
                target[pair.Key] = pair.Value;
        }

        private void CancelAllWorkingOrdersFromSubmissionAuthority(
            string reason,
            SubmissionAuthorityCancellationScope scope)
        {
            CancelAllWorkingOrders(reason);
        }

        private void CancelAllWorkingChildrenAndWait(string context)
        {
            int queued = 0;
            foreach (Order order in _workingOrders.ToArray())
            {
                if (order == null
                    || !OrderStateExtensions.IsWorkingLike(order.OrderState)
                    || IsPrimaryEntryOrder(order)
                    || IsStrategyFlattenOrder(order)
                    || !CanCancelOrderForOrderMaintenance(order))
                {
                    continue;
                }

                if (SafeCancelOrder(order, context ?? "FinalizeChildren"))
                    queued++;
            }

            WriteDebugLog($"[FINALIZE_CHILDREN] ctx={context} queued={queued}");
            ProcessCancellationQueue();
        }

        private void PrintSubmissionAuthorityOrderHealthSummary(string context)
        {
            int workingOrders = 0;
            int workingEntries = 0;
            int workingProtective = 0;
            int workingFlatten = 0;

            foreach (Order order in _workingOrders)
            {
                if (order == null || !OrderStateExtensions.IsWorkingLike(order.OrderState))
                    continue;

                workingOrders++;
                if (IsPrimaryEntryOrder(order))
                    workingEntries++;
                else if (IsStrategyFlattenOrder(order))
                    workingFlatten++;
                else if (IsStrategyProtectiveStopOrder(order))
                    workingProtective++;
            }

            string summary =
                $"[OM_HEALTH] {context} | child={workingOrders} entries={workingEntries} protective={workingProtective} flatten={workingFlatten} " +
                $"submits={_protectiveSubmitRequestCount} cancels={_cancelRequestCount} entryCancels={_entryCancelRequestCount} protectiveCancels={_protectiveCancelRequestCount} " +
                $"flattenRequests={_flattenRequestCount} coverage={_protectiveCoverageDisposition} recovery={_recoveryResolution} " +
                $"stopPending={_stopSubmissionPending} stopInFlight={_stopSubmitInFlight}";

            Print(summary);
            WriteRiskLog(summary);
        }

        private void WriteSubmissionAuthorityRiskLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            WriteRiskEvent("SUBMISSION_AUTHORITY", $"detail={message.Trim()}");
        }

        private bool EmitSubmissionAuthorityOncePer(string key, TimeSpan interval, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(key))
                return true;

            if (_submissionAuthorityEmitOnce.TryGetValue(key, out DateTime lastAtUtc)
                && nowUtc - lastAtUtc < interval)
            {
                return false;
            }

            _submissionAuthorityEmitOnce[key] = nowUtc;
            return true;
        }

        private int GetSubmissionAuthorityStopSubmitCooldownMs()
        {
            return SubmissionAuthorityStopSubmitCooldownMs;
        }

        private DateTime GetSubmissionAuthorityNowEt()
        {
            try
            {
                return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return DateTime.UtcNow;
            }
            catch (InvalidTimeZoneException)
            {
                return DateTime.UtcNow;
            }
        }

        private string StampSubmissionAuthorityTime(DateTime time)
        {
            return time.ToString("O");
        }

        private bool CanCancelOrderForOrderMaintenance(Order order)
        {
            return order != null
                && (OrderStateExtensions.IsWorkingLike(order.OrderState)
                || order.OrderState == OrderState.Accepted);
        }

        private void RemoveWorkingOrderReference(Order order)
        {
            if (order == null)
                return;

            string orderId = order.OrderId ?? string.Empty;
            if (!string.IsNullOrEmpty(orderId))
            {
                _workingOrders.RemoveAll(candidate =>
                    candidate != null && string.Equals(candidate.OrderId, orderId, StringComparison.Ordinal));
                return;
            }

            _workingOrders.Remove(order);
        }

        internal bool DebugMode => false;

        internal RuntimeSnapshotScaffold RuntimeSnapshot =>
            _lastRuntimeSnapshot ?? (_lastRuntimeSnapshot = BuildRuntimeSnapshotScaffold("lazy"));

        private void RefreshRuntimeSnapshot(string reason)
        {
            _lastRuntimeSnapshot = BuildRuntimeSnapshotScaffold(reason ?? string.Empty);
        }
    }
}
