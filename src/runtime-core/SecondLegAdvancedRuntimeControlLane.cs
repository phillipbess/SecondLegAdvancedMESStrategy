using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SecondLegAdvancedMESStrategy
    {
        private sealed class RuntimeTradeManager
        {
            private readonly SecondLegAdvancedMESStrategy _strategy;
            private readonly TradeManager _inner;

            internal RuntimeTradeManager(SecondLegAdvancedMESStrategy strategy)
            {
                _strategy = strategy;
                _inner = strategy.TradeStateManager;
            }

            internal double StopPrice
            {
                get { return _inner.StopPrice; }
            }

            internal void SetPlannedStop(double stopPrice, string context)
            {
                _inner.StopPrice = stopPrice;
                _strategy.initialStopPrice = stopPrice;
                _strategy.WriteDebugLog($"[RUNTIME_TRADE_MANAGER] stop planned | context={context} price={stopPrice:F2}");
            }

            internal void Clear(string context)
            {
                _inner.StopPrice = 0.0;
                _strategy.WriteDebugLog($"[RUNTIME_TRADE_MANAGER] reset | context={context}");
            }

            internal void SubmitPrimaryEntry(PlannedEntry plannedEntry)
            {
                if (plannedEntry == null || !plannedEntry.IsReady)
                    return;

                TransportResult result = _strategy.SubmitPrimaryEntryBridge(plannedEntry);
                if (!result.IsSuccess)
                {
                    _strategy._entryPending = false;
                    _strategy._hasWorkingEntry = false;
                    _strategy._lastBlockReason = string.IsNullOrEmpty(result.FailureReason)
                        ? "PrimaryEntrySubmitFailed"
                        : result.FailureReason;
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_TRADE_MANAGER][FAIL] primary entry transport failed | signal={plannedEntry.SignalName} reason={result.FailureReason}");
                    _strategy.RefreshRuntimeSnapshot("RuntimeTradeManager.SubmitPrimaryEntry.Failed");
                    return;
                }

                _strategy.BindPrimaryEntryTransportHandle(result.Order, "RuntimeTradeManager.SubmitPrimaryEntry");
            }
        }

        private sealed class ExitController
        {
            internal enum FlattenSubmitResult
            {
                Submitted = 0,
                Failed = 1,
            }

            private readonly SecondLegAdvancedMESStrategy _strategy;

            internal ExitController(SecondLegAdvancedMESStrategy strategy)
            {
                _strategy = strategy;
            }

            private static bool IsWorkingLike(Order order)
            {
                return order != null && OrderStateExtensions.IsWorkingLike(order.OrderState);
            }

            private Order ExistingWorkingStop()
            {
                return _strategy.ResolveBoundProtectiveStopHandle();
            }

            private int EffectiveProtectiveQuantity(int requestedQty)
            {
                int liveQty = Math.Abs(_strategy.Position.Quantity);
                if (liveQty <= 0)
                    return 0;

                if (requestedQty <= 0)
                    return liveQty;

                return Math.Min(liveQty, requestedQty);
            }

            private string ResolveProtectiveSignalName()
            {
                return !string.IsNullOrEmpty(_strategy._activeEntrySignal)
                    ? _strategy._activeEntrySignal
                    : _strategy.PlannedEntrySignalName();
            }

            private void ArmProtectiveReplaceState(string disposition, string context)
            {
                _strategy._lastStopStateChangeAt = DateTime.UtcNow;
                _strategy._coverageGraceUntil = DateTime.UtcNow.AddMilliseconds(500);
                _strategy.SetRecoveryHoldState(false, true, false, false, false, string.Empty);
                _strategy.SetProtectiveCoverageDisposition(disposition, context);
            }

            private void ClearProtectiveReplaceState(string context)
            {
                _strategy.SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                _strategy.SetProtectiveCoverageDisposition("covered-owned", context);
            }

            private bool SubmitProtectiveReplace(
                Order existing,
                string signalName,
                double roundedStop,
                int effQty,
                string stopTag,
                string reason,
                string replaceContext)
            {
                if (existing == null)
                    return false;

                string freshOco = _strategy.NewExitOco();
                DateTime submitAtUtc = DateTime.UtcNow;
                ArmProtectiveReplaceState("replace-pending", replaceContext);
                _strategy.BeginProtectiveReplaceLineage(existing, freshOco, replaceContext, reason);
                _strategy._lastStopStateChangeAt = submitAtUtc;
                _strategy._stopSubmitInFlight = true;
                _strategy._stopSubmissionPending = true;
                _strategy._lastStopSubmitAtUtc = submitAtUtc;
                _strategy._lastStopSubmissionAtUtc = submitAtUtc;
                _strategy._protectiveSubmitRequestCount++;

                TransportResult replaceResult =
                    _strategy.EnsureProtectiveStopBridge(signalName, roundedStop, effQty, freshOco);
                if (!replaceResult.IsSuccess)
                {
                    _strategy._stopSubmitInFlight = false;
                    _strategy._stopSubmissionPending = false;
                    _strategy._protectiveReplaceRejected = false;
                    _strategy._lastBlockReason = string.IsNullOrEmpty(replaceResult.FailureReason)
                        ? "ProtectiveReplaceSubmitFailed"
                        : replaceResult.FailureReason;
                    _strategy.FailProtectiveReplaceLineage($"{replaceContext}.TransportFailed");
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL][FAIL] protective replace submit failed | tag={stopTag} reason={reason} ctx={replaceContext} detail={replaceResult.FailureReason}");
                    _strategy.RefreshRuntimeSnapshot($"{replaceContext}.Failed");
                    return false;
                }

                _strategy.BindProtectiveTransportHandle(
                    replaceResult.Order,
                    string.IsNullOrEmpty(replaceResult.Oco) ? freshOco : replaceResult.Oco,
                    replaceContext);
                _strategy.WriteRiskEvent(
                    "STOP_SUBMIT",
                    $"ctx={replaceContext}",
                    "mode=replace",
                    $"qty={effQty}",
                    $"stop={roundedStop:F2}",
                    $"oco={string.IsNullOrEmpty(replaceResult.Oco) ? freshOco : replaceResult.Oco}");
                _strategy.RefreshRuntimeSnapshot(replaceContext);
                _strategy.WriteDebugLog(
                    $"[RUNTIME_EXIT_CTL] protective replace submit | tag={stopTag} reason={reason} ctx={replaceContext} qty={effQty} stop={roundedStop:F2}");
                return true;
            }

            private void DeferProtectiveEnsure(double stopPx, int qty, string stopTag, string reason, string deferredReason)
            {
                _strategy.EnqueueExitOp(
                    deferredReason,
                    () => EnsureProtectiveExit(stopPx, qty, stopTag, deferredReason),
                    75);
                _strategy.WriteDebugLog(
                    $"[RUNTIME_EXIT_CTL] defer protective ensure | reason={reason} next={deferredReason}");
            }

            private void MarkFlattenSubmitFailure(string reason, string signalName, int quantity, string detail)
            {
                _strategy.SetFlattenRecoveryState(true, false);
                _strategy.RefreshRuntimeSnapshot("ExitController.SubmitFlattenMarket.Failed");
                _strategy.WriteDebugLog(
                    $"[RUNTIME_EXIT_CTL] flatten submit failed | signal={signalName} reason={reason} qty={quantity} detail={detail}");
            }

            internal void EnsureProtectiveExit(double stopPx, int qty, string stopTag, string reason)
            {
                int effQty = EffectiveProtectiveQuantity(qty);
                if (effQty <= 0)
                    return;

                if (_strategy.Position.Quantity == 0)
                {
                    _strategy.WriteDebugLog($"[RUNTIME_EXIT_CTL][DROP] ensure protective while flat | reason={reason}");
                    return;
                }

                if (!_strategy.MaySubmitOrders("ExitController.EnsureProtectiveExit") && !_strategy.isFinalizingTrade)
                {
                    _strategy.WriteDebugLog($"[RUNTIME_EXIT_CTL][DROP] submit suppressed | reason={reason}");
                    return;
                }

                Order existing = ExistingWorkingStop();
                OrderAction expectedAction = _strategy.ResolveProtectiveStopAction();
                string signalName = ResolveProtectiveSignalName();
                double roundedStop = _strategy.Instrument.MasterInstrument.RoundToTickSize(stopPx);

                if (_strategy._protectiveReplacePending)
                {
                    DeferProtectiveEnsure(stopPx, effQty, stopTag, reason, "DEFERRED_REPLACE");
                    return;
                }

                if (existing != null && existing.OrderAction != expectedAction)
                {
                    if (_strategy.IsExitMutateSuppressed($"STOP_SIDE_MISMATCH {reason}"))
                        return;

                    if (!SubmitProtectiveReplace(
                            existing,
                            signalName,
                            roundedStop,
                            effQty,
                            stopTag,
                            reason,
                            "ExitController.EnsureProtectiveExit.SideMismatch"))
                    {
                        _strategy.SetRecoveryHoldState(false, false, true, false, false, string.Empty);
                    }
                    return;
                }

                if (existing == null)
                {
                    string oco = !string.IsNullOrEmpty(_strategy._currentExitOco)
                        ? _strategy._currentExitOco
                        : _strategy.NewExitOco();
                    DateTime submitAtUtc = DateTime.UtcNow;
                    _strategy._exitState = ExitFlowState.Live;
                    _strategy.workingStopPrice = stopPx;
                    _strategy.currentControllerStopPrice = stopPx;
                    _strategy.initialStopPrice = _strategy.initialStopPrice > 0.0 ? _strategy.initialStopPrice : stopPx;
                    _strategy._lastStopStateChangeAt = submitAtUtc;
                    _strategy._stopSubmitInFlight = true;
                    _strategy._stopSubmissionPending = true;
                    _strategy._lastStopSubmitAtUtc = submitAtUtc;
                    _strategy._lastStopSubmissionAtUtc = submitAtUtc;
                    _strategy._protectiveSubmitRequestCount++;
                    _strategy.SetRecoveryHoldState(false, false, false, false, false, string.Empty);
                    _strategy.SetProtectiveCoverageDisposition("pending-owned", "ExitController.EnsureProtectiveExit");
                    TransportResult submitResult =
                        _strategy.EnsureProtectiveStopBridge(signalName, roundedStop, effQty, oco);
                    if (!submitResult.IsSuccess)
                    {
                        _strategy._stopSubmitInFlight = false;
                        _strategy._stopSubmissionPending = false;
                        _strategy.controllerStopPlaced = false;
                        _strategy.hasWorkingStop = false;
                        _strategy.SetRecoveryHoldState(false, false, true, false, false, string.Empty);
                        _strategy.SetProtectiveCoverageDisposition("submit-failed", "ExitController.EnsureProtectiveExit");
                        _strategy._lastBlockReason = string.IsNullOrEmpty(submitResult.FailureReason)
                            ? "ProtectiveSubmitFailed"
                            : submitResult.FailureReason;
                        _strategy.WriteDebugLog(
                            $"[RUNTIME_EXIT_CTL] protective submit failed | reason={reason} detail={submitResult.FailureReason}");
                        _strategy.RefreshRuntimeSnapshot("ExitController.EnsureProtectiveExit.Failed");
                        return;
                    }

                    _strategy.BindProtectiveTransportHandle(
                        submitResult.Order,
                        string.IsNullOrEmpty(submitResult.Oco) ? oco : submitResult.Oco,
                        "ExitController.EnsureProtectiveExit");
                    _strategy.WriteRiskEvent(
                        "STOP_SUBMIT",
                        "ctx=ExitController.EnsureProtectiveExit",
                        "mode=initial",
                        $"qty={effQty}",
                        $"stop={roundedStop:F2}",
                        $"oco={string.IsNullOrEmpty(submitResult.Oco) ? oco : submitResult.Oco}");
                    _strategy.RefreshRuntimeSnapshot("ExitController.EnsureProtectiveExit");
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL] ensure protective submit | tag={stopTag} reason={reason} qty={effQty} stop={stopPx:F2}");
                    return;
                }

                if (!IsWorkingLike(existing))
                {
                    DeferProtectiveEnsure(stopPx, effQty, stopTag, reason, "DEFERRED_CHANGE");
                    return;
                }

                if (existing.Quantity != effQty)
                {
                    if (_strategy.IsExitMutateSuppressed($"STOP_QTY_MISMATCH {reason}"))
                        return;

                    if (!SubmitProtectiveReplace(
                            existing,
                            signalName,
                            roundedStop,
                            effQty,
                            stopTag,
                            reason,
                            "ExitController.EnsureProtectiveExit.QuantityMismatch"))
                    {
                        _strategy.SetRecoveryHoldState(false, false, true, false, false, string.Empty);
                    }
                    return;
                }

                ChangeUnmanaged(existing, roundedStop, reason);
            }

            internal void ChangeUnmanaged(Order order, double newStopPrice, string reason)
            {
                if (order == null)
                {
                    EnsureProtectiveExit(newStopPrice, Math.Abs(_strategy.Position.Quantity), "StopLoss_PrimaryEntry", reason);
                    return;
                }

                if (!_strategy.CanMutateProtectiveStop(order, reason))
                    return;

                if (_strategy._protectiveReplacePending)
                {
                    DeferProtectiveEnsure(newStopPrice, EffectiveProtectiveQuantity(order.Quantity), order.Name ?? "StopLoss_PrimaryEntry", reason, "DEFERRED_REPLACE");
                    return;
                }

                if (!IsWorkingLike(order))
                {
                    DeferProtectiveEnsure(newStopPrice, order.Quantity, order.Name ?? "StopLoss_PrimaryEntry", reason, "DEFERRED_CHANGE");
                    return;
                }

                _strategy.workingStopPrice = newStopPrice;
                _strategy.currentControllerStopPrice = newStopPrice;
                _strategy._lastStopStateChangeAt = DateTime.UtcNow;
                _strategy._stopSubmitInFlight = true;
                _strategy._stopSubmissionPending = true;
                _strategy._lastStopSubmitAtUtc = DateTime.UtcNow;
                _strategy._lastStopSubmissionAtUtc = DateTime.UtcNow;
                _strategy._protectiveSubmitRequestCount++;

                double roundedStop = _strategy.Instrument.MasterInstrument.RoundToTickSize(newStopPrice);
                string signalName = ResolveProtectiveSignalName();
                int effQty = EffectiveProtectiveQuantity(Math.Abs(_strategy.Position.Quantity));
                TransportResult changeResult =
                    _strategy.ChangeProtectiveStopBridge(order, effQty, roundedStop);
                if (!changeResult.IsSuccess)
                {
                    _strategy._stopSubmitInFlight = false;
                    _strategy._stopSubmissionPending = false;
                    _strategy._lastBlockReason = string.IsNullOrEmpty(changeResult.FailureReason)
                        ? "ProtectiveChangeFailed"
                        : changeResult.FailureReason;
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL] protective change failed | order={order.Name} detail={changeResult.FailureReason}");

                    if (!SubmitProtectiveReplace(
                            order,
                            signalName,
                            roundedStop,
                            effQty,
                            order.Name ?? "StopLoss_PrimaryEntry",
                            $"{reason}|Replace",
                            "ExitController.ChangeUnmanaged.Replace"))
                    {
                        _strategy.SetRecoveryHoldState(false, false, true, false, false, string.Empty);
                        _strategy.SetProtectiveCoverageDisposition("change-failed", "ExitController.ChangeUnmanaged");
                    }

                    return;
                }

                if (changeResult.IsPendingAck)
                {
                    _strategy.WriteRiskEvent(
                        "STOP_CHANGE",
                        "ctx=ExitController.ChangeUnmanaged.PendingAck",
                        $"qty={effQty}",
                        $"stop={roundedStop:F2}",
                        $"reason={reason}");
                    _strategy.SetProtectiveCoverageDisposition("pending-owned", "ExitController.ChangeUnmanaged.PendingAck");
                    _strategy.BindProtectiveTransportHandle(
                        changeResult.Order ?? order,
                        string.IsNullOrEmpty(changeResult.Oco) ? order.Oco ?? string.Empty : changeResult.Oco,
                        "ExitController.ChangeUnmanaged.PendingAck");
                    _strategy.RefreshRuntimeSnapshot("ExitController.ChangeUnmanaged.PendingAck");
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL] change protective requested | order={order.Name} reason={reason} stop={newStopPrice:F2}");
                    return;
                }

                ClearProtectiveReplaceState("ExitController.ChangeUnmanaged");
                _strategy.BindProtectiveTransportHandle(
                    changeResult.Order ?? order,
                    string.IsNullOrEmpty(changeResult.Oco) ? order.Oco ?? string.Empty : changeResult.Oco,
                    "ExitController.ChangeUnmanaged");
                _strategy.RefreshRuntimeSnapshot("ExitController.ChangeUnmanaged");
                _strategy.WriteDebugLog(
                    $"[RUNTIME_EXIT_CTL] change protective | order={order.Name} reason={reason} stop={newStopPrice:F2}");
            }

            internal FlattenSubmitResult SubmitFlattenMarket(int qty, string signalName, string reason)
            {
                int liveQty = Math.Abs(_strategy.Position.Quantity);
                if (qty <= 0 && liveQty == 0)
                {
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL][DROP] flatten submit while flat | signal={signalName} reason={reason}");
                    return FlattenSubmitResult.Failed;
                }

                if (liveQty <= 0)
                {
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL][DROP] flatten submit has no live quantity | signal={signalName} reason={reason}");
                    return FlattenSubmitResult.Failed;
                }

                int effQty = qty > 0 ? Math.Min(qty, liveQty) : liveQty;
                string ownedSignal = !string.IsNullOrEmpty(signalName) ? signalName : ResolveProtectiveSignalName();
                TransportResult flattenResult = _strategy.SubmitFlattenBridge(ownedSignal, effQty);
                if (!flattenResult.IsSuccess)
                {
                    MarkFlattenSubmitFailure(reason, ownedSignal, effQty, flattenResult.FailureReason);
                    return FlattenSubmitResult.Failed;
                }

                _strategy.SetFlattenRecoveryState(false, false);
                _strategy.BindFlattenTransportHandle(flattenResult.Order, "ExitController.SubmitFlattenMarket");
                _strategy.WriteDebugLog(
                    $"[RUNTIME_EXIT_CTL] flatten submitted | signal={ownedSignal} reason={reason} qty={effQty}");
                return FlattenSubmitResult.Submitted;
            }

            internal void OnEntryFilled(string fromEntry, double fillPrice, MarketPosition side, int totalQty)
            {
                _strategy._activeEntrySignal = fromEntry ?? string.Empty;
                _strategy._currentEntryTag = fromEntry ?? string.Empty;
                _strategy.entryPositionSide = side;
                _strategy.entryFillPrice = fillPrice;
                _strategy.entryPrice = fillPrice;
                _strategy.avgEntryPrice = fillPrice;
                _strategy.entryQuantity = Math.Max(1, Math.Abs(totalQty));
                _strategy._exitState = ExitFlowState.Live;

                double stopPrice = _strategy.tradeManager != null && _strategy.tradeManager.StopPrice > 0.0
                    ? _strategy.tradeManager.StopPrice
                    : (_strategy.initialStopPrice > 0.0 ? _strategy.initialStopPrice : fillPrice);

                _strategy.EnqueueEnsureProtectiveExit(stopPrice, _strategy.entryQuantity, "StopLoss_PrimaryEntry", "EntryFill");
                _strategy.WriteDebugLog(
                    $"[RUNTIME_EXIT_CTL] entry fill scaffold | entry={fromEntry} side={side} qty={totalQty} fill={fillPrice:F2}");
            }

            public void Flatten(string reason = "ExitController")
            {
                RequestFlatten(reason);
            }

            public void RequestFlatten(string reason)
            {
                if (_strategy.Position.Quantity == 0 && !_strategy.tradeOpen)
                {
                    _strategy.WriteDebugLog($"[RUNTIME_EXIT_CTL][DROP] flatten while flat | reason={reason}");
                    return;
                }

                _strategy.TriggerFlatten(reason);
            }

            internal void SubmitFlatten(string fromEntrySignal)
            {
                SubmitFlattenMarket(Math.Abs(_strategy.Position.Quantity), fromEntrySignal, "ExitController");
            }

            internal void OnOrderRejected(Order order)
            {
                if (order == null || _strategy.Position.Quantity == 0 || _strategy.isFinalizingTrade)
                    return;

                if (!_strategy.IsStrategyProtectiveStopOrder(order))
                {
                    _strategy.TriggerFlatten("AUTO_FLATTEN_CHILD_REJECT");
                    return;
                }

                DateTime nowUtc = DateTime.UtcNow;
                if (_strategy._lastOcoResubmitAt != DateTime.MinValue
                    && (nowUtc - _strategy._lastOcoResubmitAt).TotalMilliseconds < 1200)
                {
                    _strategy.WriteDebugLog(
                        $"[RUNTIME_EXIT_CTL] suppress duplicate protective reject recovery | order={order.Name}");
                    return;
                }

                _strategy._lastOcoResubmitAt = nowUtc;
                _strategy.WriteRiskEvent(
                    "OCO_RESUBMIT",
                    $"signal={_strategy._activeEntrySignal}",
                    $"order={order.Name}",
                    $"qty={Math.Abs(_strategy.Position.Quantity)}",
                    $"reason={order.OrderState}");
                _strategy.SetRecoveryHoldState(false, true, true, true, false, string.Empty);
                _strategy.SetProtectiveCoverageDisposition("replace-rejected", "ExitController.OnOrderRejected");
                _strategy._coverageGraceUntil = nowUtc.AddMilliseconds(500);
                _strategy._lastStopStateChangeAt = nowUtc;

                double retryStop = order.StopPrice > 0.0
                    ? order.StopPrice
                    : (_strategy.currentControllerStopPrice > 0.0
                        ? _strategy.currentControllerStopPrice
                        : _strategy.initialStopPrice);
                retryStop = _strategy.Instrument.MasterInstrument.RoundToTickSize(retryStop);
                int liveQty = Math.Abs(_strategy.Position.Quantity);
                _strategy.NewExitOco();
                _strategy.EnqueueExitOp(
                    "OCO_RESUBMIT",
                    () => EnsureProtectiveExit(retryStop, liveQty, "StopLoss_PrimaryEntry", "OCO_RESUBMIT"),
                    150);
                _strategy.RefreshRuntimeSnapshot("ExitController.OnOrderRejected");
            }
        }
    }
}
