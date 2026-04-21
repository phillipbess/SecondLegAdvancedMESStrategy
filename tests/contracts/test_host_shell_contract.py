"""Source-level checks for the M1 host shell contract."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file, read_strategy_file

FAMILY_ID = "host_shell_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "host_shell_minimum.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


def _method_block(text: str, signature: str) -> str:
    start = text.find(signature)
    if start < 0:
        raise AssertionError(f"Missing signature: {signature}")

    brace_start = text.find("{", start)
    if brace_start < 0:
        raise AssertionError(f"Missing method body for: {signature}")

    depth = 0
    for index in range(brace_start, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[start : index + 1]

    raise AssertionError(f"Unbalanced braces for: {signature}")


class HostShellContractTests(unittest.TestCase):
    def test_manifest_and_fixture_exist(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertEqual(fixture["family_id"], FAMILY_ID)
        self.assertEqual(fixture["status"], "scaffold_assertions")

    def test_strategy_shell_stays_nt8_authority_for_lifecycle_and_bar_flow(self) -> None:
        strategy = read_strategy_file("SecondLegAdvancedMESStrategy.cs")
        lifecycle = read_strategy_file("SecondLegAdvancedMESStrategy.StateLifecycle.cs")
        bar_flow = read_strategy_file("SecondLegAdvancedMESStrategy.BarFlow.cs")

        self.assertIn("public partial class SecondLegAdvancedMESStrategy : Strategy", strategy)
        self.assertIn("private bool _hostShellReady;", strategy)
        self.assertIn("protected override void OnStateChange()", lifecycle)
        self.assertIn("protected override void OnBarUpdate()", lifecycle)
        self.assertIn("Calculate = Calculate.OnEachTick;", lifecycle)
        self.assertIn("IsUnmanaged = true;", lifecycle)
        self.assertIn("HandleFlatRealtimeRestartScaffold();", lifecycle)
        self.assertIn("_hostShellReady = true;", lifecycle)
        self.assertIn("AdvanceClosedBarSessionFlags();", lifecycle)
        self.assertIn("bool hasStrategyLookback = ClosedBarIndex() >=", lifecycle)
        self.assertIn("AdvanceRestoreObservation(context);", lifecycle)
        self.assertIn("private bool IsClosedPrimaryBarPass()", lifecycle)
        self.assertIn("return IsFirstTickOfBar && CurrentBar > 0;", lifecycle)
        self.assertIn("HandleTradeLogic();", lifecycle)
        self.assertIn("private void HandleTradeLogic()", bar_flow)

    def test_runtime_host_declares_required_runtime_fields_and_seams(self) -> None:
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        host_contract = read_doc_file("Host_Shell_Contract.md")

        for marker in (
            "private readonly List<Order> _workingOrders = new List<Order>();",
            "private readonly Queue<Action> _exitOpQueue = new Queue<Action>();",
            "private readonly Queue<(Order Order, string Tag, DateTime EnqueuedAt)> _cancelQueue =",
            "private ExitFlowState _exitState = ExitFlowState.Flat;",
            "private int _exitEpoch;",
            "private DateTime? _flattenAwaitCancelsUntil;",
            "private DateTime? _flattenPostSweepUntil;",
            "private bool _flattenMarketSubmitted;",
            "private bool isFinalizingTrade;",
            "private bool suppressAllOrderSubmissions;",
            "private bool tradeOpen;",
            "private bool controllerStopPlaced;",
            "private bool autoDisabled;",
            "private bool _globalKillSwitch;",
            "private bool stateRestorationInProgress;",
            "private string currentTradeID = string.Empty;",
            "private string _activeTradeId = string.Empty;",
            "private string _currentExitOco = string.Empty;",
            "private string _activeEntrySignal = string.Empty;",
            "private string _currentEntryTag = string.Empty;",
            "private string _recoveryResolution = string.Empty;",
            "private double workingStopPrice;",
            "private bool hasWorkingStop;",
            "private double currentControllerStopPrice;",
            "private RuntimeTradeManager tradeManager;",
            "private ExitController exitCtl;",
            "private RuntimeSnapshotScaffold _lastRuntimeSnapshot;",
        ):
            self.assertIn(marker, runtime_host)

        self.assertIn("## Authority Boundaries", host_contract)
        self.assertIn("## Highest-Risk Seams First", host_contract)
        self.assertIn("### 6. Runtime Snapshot/Diagnostic Surface", host_contract)

    def test_runtime_host_exposes_required_control_and_persistence_methods(self) -> None:
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        transport_adapter = read_strategy_file("SecondLegAdvancedMESStrategy.TransportAdapter.cs")

        for signature in (
            "private void InitializeRuntimeCoreIfNeeded()",
            "private void BeginAtomicFinalization(string reason)",
            "private bool MaySubmitOrders(string context = \"\")",
            "private void TriggerFlatten(string reason)",
            "private void ContinueFlattenProtocol(string context = \"\")",
            "private void CompleteFlattenProtocol(string context, string completionReason)",
            "private void EnqueueEnsureProtectiveExit(double price, int quantity, string tag, string reason, int delayMs = 0)",
            "private void EnqueueExitOp(string label, Action op)",
            "private void EnqueueExitOp(string label, Action op, int delayMs)",
            "private bool IsExitMutateSuppressed(string reason)",
            "private bool CanMutateProtectiveStop(Order order, string context)",
            "private Order FindWorkingExitForRole(OrderRole role)",
            "private string NewExitOco()",
            "private int WorkingExitCount()",
            "private int WorkingResidualExitCount()",
            "private bool HasCoverageNow()",
            "private bool HasEffectiveProtectiveCoverage()",
            "private bool IsStrategyFlattenOrder(Order order)",
            "private bool TryGetOwnedFlattenSignal(Order order, out string ownedSignal)",
            "private IEnumerable<Order> EnumerateBrokerWorkingOrders()",
            "private bool IsCompatibleBrokerProtectiveStopOrder(Order order, int liveQty)",
            "private void RebuildWorkingOrderTruthFromBroker(string context)",
            "private bool IsProtectiveCoveragePending(DateTime nowUtc)",
            "private bool ReconcileCompatibleBrokerCoverageForRecovery(string context, int quantity)",
            "private void CancelAllWorkingOrders(string reason)",
            "private void CancelResidualWorkingOrdersForFlattenSweep()",
            "private void EnqueueOrderCancellation(Order order, string tag)",
            "private void ProcessCancellationQueue()",
            "private bool SafeCancelOrder(Order order, string context = \"\")",
            "private void NullOrderReference(Order order)",
            "private void SetStateRestorationInProgress(bool enabled, string context)",
            "private void SetRecoveryResolution(string resolution, string context)",
            "private bool HasPendingRecoveryResolution()",
            "private void ResetTradeState()",
            "private void SaveStrategyState()",
            "private void RestoreStrategyState()",
            "internal RuntimeSnapshotScaffold RuntimeSnapshot =>",
            "private void RefreshRuntimeSnapshot(string reason)",
        ):
            self.assertIn(signature, runtime_host)

        for signature in (
            "private TransportResult SubmitPrimaryEntryBridge(PlannedEntry plannedEntry)",
            "private TransportResult EnsureProtectiveStopBridge(",
            "private TransportResult ChangeProtectiveStopBridge(Order workingStop, int quantity, double stopPrice)",
            "private TransportResult SubmitFlattenBridge(string fromEntrySignal, int quantity)",
        ):
            self.assertIn(signature, transport_adapter)

    def test_runtime_host_methods_route_through_scaffolds_in_expected_places(self) -> None:
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        orders = read_strategy_file("SecondLegAdvancedMESStrategy.Orders.cs")

        begin_atomic = _method_block(runtime_host, "private void BeginAtomicFinalization(string reason)")
        self.assertIn("SubmissionAuthority.BeginAtomicFinalization", begin_atomic)
        self.assertIn("ApplySubmissionAuthorityState(state);", begin_atomic)

        trigger_flatten = _method_block(runtime_host, "private void TriggerFlatten(string reason)")
        self.assertIn("CancelAllWorkingOrders(reason ?? \"TriggerFlatten\");", trigger_flatten)
        self.assertIn("RepriceWorkingProtectiveOrdersForFlatten(\"TriggerFlatten\");", trigger_flatten)
        self.assertIn("_flattenAwaitCancelsUntil = DateTime.UtcNow.AddMilliseconds(FlattenAwaitCancelWindowMs);", trigger_flatten)
        self.assertIn("ContinueFlattenProtocol(reason ?? \"TriggerFlatten\");", trigger_flatten)

        reprice_flatten = _method_block(runtime_host, "private void RepriceWorkingProtectiveOrdersForFlatten(string context)")
        self.assertIn("order.IsProtectiveStop()", reprice_flatten)
        self.assertIn('exitCtl?.ChangeUnmanaged(order, rounded, "FLATTEN_REPRICE");', reprice_flatten)
        self.assertIn("RuntimeCurrentBidSafe()", reprice_flatten)
        self.assertIn("RuntimeBestAsk()", reprice_flatten)

        continue_flatten = _method_block(runtime_host, "private void ContinueFlattenProtocol(string context = \"\")")
        self.assertIn("if (_exitState == ExitFlowState.Flattening)", continue_flatten)
        self.assertIn("if (_flattenAwaitCancelsUntil.HasValue && DateTime.UtcNow >= _flattenAwaitCancelsUntil.Value)", continue_flatten)
        self.assertIn("bool hasWorkingExits = WorkingResidualExitCount() > 0;", continue_flatten)
        self.assertIn("ExitController.FlattenSubmitResult flattenSubmitResult =", continue_flatten)
        self.assertIn("exitCtl != null", continue_flatten)
        self.assertIn("exitCtl.SubmitFlattenMarket(flattenQty, fromEntrySignal, context ?? \"TriggerFlatten\")", continue_flatten)
        self.assertIn("if (!_flattenMarketSubmitted && Position.Quantity == 0)", continue_flatten)
        self.assertIn("CancelResidualWorkingOrdersForFlattenSweep();", continue_flatten)
        self.assertIn("CompleteFlattenProtocol(context, postSweepExpired ? \"PostSweep.Timeout\" : \"PostSweep.Clean\");", continue_flatten)

        bind_flatten = _method_block(runtime_host, "private void BindFlattenTransportHandle(Order order, string context)")
        self.assertIn("_flattenMarketSubmitted = true;", bind_flatten)
        self.assertIn("if (_exitState == ExitFlowState.Flattening)", bind_flatten)

        begin_reconnect = _method_block(runtime_host, "private void BeginReconnectGrace(string context)")
        self.assertIn("SetReconnectObservationState(false, Math.Abs(Position.Quantity), Position.AveragePrice);", begin_reconnect)

        eval_reconnect = _method_block(runtime_host, "private void EvaluateRealtimeReconnectGrace(string context)")
        self.assertIn("RebuildWorkingOrderTruthFromBroker", eval_reconnect)
        self.assertIn("RestoreProtectiveReplaceLineageFromBroker", eval_reconnect)
        self.assertIn('ValidateStopQuantity($"{context}.ReconnectPending");', eval_reconnect)

        complete_flatten = _method_block(runtime_host, "private void CompleteFlattenProtocol(string context, string completionReason)")
        self.assertIn("_flattenAwaitCancelsUntil = null;", complete_flatten)
        self.assertIn("_flattenPostSweepUntil = null;", complete_flatten)
        self.assertIn("_exitState = ExitFlowState.Flat;", complete_flatten)
        self.assertIn("RefreshRuntimeSnapshot($\"{context}.{completionReason}\");", complete_flatten)

        may_submit = _method_block(runtime_host, "private bool MaySubmitOrders(string context = \"\")")
        self.assertIn("SubmissionAuthority.MaySubmitOrders", may_submit)
        self.assertIn("ApplySubmissionAuthorityState(state);", may_submit)

        cancel_all = _method_block(runtime_host, "private void CancelAllWorkingOrders(string reason)")
        self.assertIn("OrderMaintenance.CancelAllWorkingOrders(state, reason);", cancel_all)
        self.assertIn("ApplyOrderMaintenanceState(state);", cancel_all)

        flatten_order_predicate = _method_block(runtime_host, "private bool IsStrategyFlattenOrder(Order order)")
        self.assertIn("if (order == null || !order.IsFlattenLike())", flatten_order_predicate)
        self.assertIn("if (TryGetOwnedFlattenSignal(order, out string ownedSignal))", flatten_order_predicate)
        self.assertIn("return MatchesOwnedPrimaryEntrySignal(ownedSignal);", flatten_order_predicate)
        self.assertIn("return MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);", flatten_order_predicate)

        flatten_signal_parser = _method_block(runtime_host, "private bool TryGetOwnedFlattenSignal(Order order, out string ownedSignal)")
        self.assertIn('string prefix = NameTokens.SafetyFlatten + "|";', flatten_signal_parser)
        self.assertIn("orderName.StartsWith(prefix, StringComparison.Ordinal)", flatten_signal_parser)

        flatten_sweep = _method_block(runtime_host, "private void CancelResidualWorkingOrdersForFlattenSweep()")
        self.assertIn("|| IsStrategyFlattenOrder(order)", flatten_sweep)
        self.assertIn('SafeCancelOrder(order, "FlattenSweep");', flatten_sweep)

        reset_trade_state = _method_block(runtime_host, "private void ResetTradeState()")
        self.assertIn("ResetRuntimeScenarioState();", reset_trade_state)
        self.assertIn('RefreshRuntimeSnapshot("ResetTradeState");', reset_trade_state)

        enqueue_protective = _method_block(
            runtime_host,
            "private void EnqueueEnsureProtectiveExit(double price, int quantity, string tag, string reason, int delayMs = 0)",
        )
        self.assertIn("if (isFinalizingTrade)", enqueue_protective)
        self.assertIn('WriteDebugEvent("EXIT_OP_CARVEOUT", $"reason={reason}", "phase=finalizing", $"qty={Position.Quantity}");', enqueue_protective)
        self.assertIn("TryBuildProtectiveRetry(", enqueue_protective)
        self.assertIn("EnqueueEnsureProtectiveExit(price, quantity, tag, nextReason, nextDelayMs);", enqueue_protective)

        delayed_exit_op = _method_block(
            runtime_host,
            "private void EnqueueExitOp(string label, Action op, int delayMs)",
        )
        self.assertIn("DateTime executeAt = DateTime.UtcNow.AddMilliseconds(delayMs);", delayed_exit_op)
        self.assertIn("if (DateTime.UtcNow < executeAt)", delayed_exit_op)
        self.assertIn("_exitOpPendingUntil = executeAt.AddMilliseconds(25);", delayed_exit_op)
        self.assertIn("PumpExitOps(label ?? string.Empty);", delayed_exit_op)

        protective_retry = _method_block(
            runtime_host,
            "private bool TryBuildProtectiveRetry(",
        )
        self.assertIn('currentReason.IndexOf("EntryFill", StringComparison.OrdinalIgnoreCase)', protective_retry)
        self.assertIn('currentReason.IndexOf("EntryPartFill", StringComparison.OrdinalIgnoreCase)', protective_retry)
        self.assertIn('Regex.Match(currentReason, "\\\\|RETRY(\\\\d+)/(\\\\d+)")', protective_retry)
        self.assertIn("nextDelayMs = Math.Min(30 * (1 << (attempt - 1)), 300);", protective_retry)

        save_strategy_state = _method_block(runtime_host, "private void SaveStrategyState()")
        self.assertIn('CapturePersistedTradeIdentity("SaveStrategyState");', save_strategy_state)
        self.assertIn('RefreshRuntimeSnapshot("SaveStrategyState");', save_strategy_state)

        validate_stop_qty = _method_block(runtime_host, "private void ValidateStopQuantity(string context)")
        self.assertIn("if (IsProtectiveCoveragePending(nowUtc))", validate_stop_qty)
        self.assertIn('RefreshRuntimeSnapshot($"ValidateStopQuantity.{context}.Pending");', validate_stop_qty)
        self.assertIn("if (ReconcileCompatibleBrokerCoverageForRecovery(context, liveQty))", validate_stop_qty)
        self.assertIn('EnqueueEnsureProtectiveExit(candidateStop, liveQty, "StopLoss_PrimaryEntry", $"CoverageRepair|{context}");', validate_stop_qty)

        pending_coverage = _method_block(runtime_host, "private bool IsProtectiveCoveragePending(DateTime nowUtc)")
        self.assertIn("if (_coverageGraceUntil != DateTime.MinValue && nowUtc < _coverageGraceUntil)", pending_coverage)
        self.assertIn("if (!_stopSubmissionPending && !_stopSubmitInFlight)", pending_coverage)
        self.assertIn("DateTime pendingSinceUtc = _lastStopSubmissionAtUtc > _lastStopSubmitAtUtc", pending_coverage)

        effective_coverage = _method_block(runtime_host, "private bool HasEffectiveProtectiveCoverage()")
        self.assertIn("if (HasWorkingProtectiveCoverage())", effective_coverage)
        self.assertIn("return IsProtectiveCoveragePending(DateTime.UtcNow);", effective_coverage)

        compatible_coverage = _method_block(runtime_host, "private bool ReconcileCompatibleBrokerCoverageForRecovery(string context, int quantity)")
        self.assertIn("RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();", compatible_coverage)
        self.assertIn("coverage.CompatibleProtectiveOrderCount > 0", compatible_coverage)
        self.assertIn("coverage.WorkingProtectiveOrderQuantityTotal >= liveQty", compatible_coverage)
        self.assertIn("_protectiveCoverageAmbiguous = true;", compatible_coverage)
        self.assertIn('SetProtectiveCoverageDisposition("compatible-unattributed", context ?? "CompatibleCoverage");', compatible_coverage)

        rebuild_truth = _method_block(runtime_host, "private void RebuildWorkingOrderTruthFromBroker(string context)")
        self.assertIn("foreach (Order order in EnumerateBrokerWorkingOrders())", rebuild_truth)
        self.assertIn("OrderMaintenanceState state = SnapshotOrderMaintenanceState();", rebuild_truth)
        self.assertIn("SyncWorkingOrderState(state.WorkingOrders, rebuiltOrders);", rebuild_truth)
        self.assertIn("ApplyOrderMaintenanceState(state);", rebuild_truth)
        self.assertIn("_preservedProtectiveOrderCount = preservedProtectiveCount;", rebuild_truth)

        finalize_children = _method_block(runtime_host, "private void CancelAllWorkingChildrenAndWait(string context)")
        self.assertIn("foreach (Order order in _workingOrders.ToArray())", finalize_children)
        self.assertIn("|| IsPrimaryEntryOrder(order)", finalize_children)
        self.assertIn('SafeCancelOrder(order, context ?? "FinalizeChildren")', finalize_children)
        self.assertIn("ProcessCancellationQueue();", finalize_children)

        order_health = _method_block(runtime_host, "private void PrintSubmissionAuthorityOrderHealthSummary(string context)")
        self.assertIn('string summary =', order_health)
        self.assertIn('"[OM_HEALTH]', order_health)
        self.assertIn("Print(summary);", order_health)
        self.assertIn("WriteRiskLog(summary);", order_health)

        stop_cooldown = _method_block(runtime_host, "private int GetSubmissionAuthorityStopSubmitCooldownMs()")
        self.assertIn("return SubmissionAuthorityStopSubmitCooldownMs;", stop_cooldown)

        primary_entry = _method_block(runtime_host, "internal bool IsPrimaryEntryOrder(Order order)")
        self.assertIn("if (order.IsFlattenLike() || order.IsProtectiveStop())", primary_entry)
        self.assertIn("if (!IsPrimaryEntrySideAction(order.OrderAction))", primary_entry)
        self.assertIn("string plannedSignalName = PlannedEntrySignalName();", primary_entry)
        self.assertIn("string.Equals(orderName, currentTradeID, StringComparison.Ordinal)", primary_entry)
        self.assertIn("MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);", primary_entry)

        cancel_pending_entry = _method_block(orders, "private void CancelPendingEntry(string reason)")
        self.assertIn("if (CancelWorkingPrimaryEntries(reason))", cancel_pending_entry)
        self.assertIn("_hasWorkingEntry = HasWorkingPrimaryEntryForActiveSignal();", cancel_pending_entry)
        self.assertIn('ClearEntrySubmitInFlight($"CancelPendingEntry.{reason ?? "Unknown"}");', cancel_pending_entry)

    def test_host_shell_doc_traces_expected_runtime_path(self) -> None:
        host_contract = read_doc_file("Host_Shell_Contract.md")
        self.assertIn("`OnExecutionUpdate -> tradeManager -> exitCtl -> coverage -> flatten/cancel -> SaveStrategyState/ResetTradeState`", host_contract)
        self.assertIn("The operating rule is simple", host_contract)
        self.assertIn("The host strategy must supply:", host_contract)


if __name__ == "__main__":
    unittest.main()
