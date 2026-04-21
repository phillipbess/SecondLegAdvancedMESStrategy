"""Source-level checks for the harness-aligned runtime snapshot scaffold."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file, read_strategy_file

FAMILY_ID = "runtime_snapshot_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")


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


class RuntimeSnapshotContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertIn("snapshot/diagnostic surface", family["goal"])

    def test_runtime_snapshot_scaffold_declares_harness_facing_types(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs")

        for marker in (
            "public sealed class RuntimeCoverageSnapshot",
            "public sealed class RuntimeTradeIdentitySnapshot",
            "public sealed class RuntimeFinalizationSnapshot",
            "public sealed class RuntimeSnapshotScaffold",
            "public int StrategyQuantity { get; set; }",
            "public int AccountQuantity { get; set; }",
            "public bool DurableLiveTradeContext { get; set; }",
            "public IList<string> DurableLiveTradeContextSources { get; set; }",
            "public string RecoveryResolution { get; set; }",
            "public string ProtectiveReplaceDisposition { get; set; }",
            "public string ProtectiveReplaceContext { get; set; }",
            "public string ProtectiveReplaceReason { get; set; }",
            "public string ProtectiveReplaceSourceOrderId { get; set; }",
            "public string ProtectiveReplaceTargetOrderId { get; set; }",
            "public string ProtectiveReplaceOco { get; set; }",
            "public DateTime ProtectiveReplaceStartedAtUtc { get; set; }",
            "public bool EntryAllowed { get; set; }",
            "public RuntimeCoverageSnapshot Coverage { get; set; }",
            "public RuntimeTradeIdentitySnapshot TradeIdentity { get; set; }",
            "public RuntimeFinalizationSnapshot Finalization { get; set; }",
            "public IList<string> Tokens { get; set; }",
            "public int WorkingOrderCount { get; set; }",
            "public int WorkingPrimaryEntryOrderCount { get; set; }",
            "public int WorkingProtectiveOrderCount { get; set; }",
            "public int WorkingProtectiveOrderQuantityTotal { get; set; }",
            "public int OwnedProtectiveOrderCount { get; set; }",
            "public int CompatibleProtectiveOrderCount { get; set; }",
            "public string ProtectiveCoverageDisposition { get; set; }",
            "public string CurrentTradeId { get; set; }",
            "public string ActiveTradeId { get; set; }",
            "public string PersistedTradeId { get; set; }",
            "public string CountedTradeSessionId { get; set; }",
            "public int TradeCount { get; set; }",
            "public int TrackedFillQuantity { get; set; }",
            "public string FinalCancelSweepDisposition { get; set; }",
            "public int CancelRequestCount { get; set; }",
            "public int EntryCancelRequestCount { get; set; }",
            "public int ProtectiveCancelRequestCount { get; set; }",
            "public int ProtectiveSubmitRequestCount { get; set; }",
            "public int FlattenRequestCount { get; set; }",
            "public int PreservedProtectiveOrderCount { get; set; }",
            "public bool StopSubmissionPending { get; set; }",
            "public bool StopSubmitInFlight { get; set; }",
        ):
            self.assertIn(marker, text)

    def test_snapshot_builder_maps_trade_identity_and_runtime_flags(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs")
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        runtime_state = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs")

        build_snapshot = _method_block(
            text,
            'internal RuntimeSnapshotScaffold BuildRuntimeSnapshotScaffold(string reason = "")',
        )
        for marker in (
            "StrategyQuantity = GetRuntimeStrategyQuantity(),",
            "AccountQuantity = _accountPositionQuantity,",
            "FlatResetDeferred = _flatResetDeferred,",
            "ProtectiveCoverageAmbiguous = _protectiveCoverageAmbiguous,",
            "RecoveryResolution = _recoveryResolution ?? string.Empty,",
            "OrphanAdoptionPending = _orphanAdoptionPending,",
            "ExplicitCoverageLossPending = _explicitCoverageLossPending,",
            "ProtectiveReplacePending = _protectiveReplacePending,",
            "ProtectiveReplaceFailurePending = _protectiveReplaceFailurePending,",
            "ProtectiveReplaceRejected = _protectiveReplaceRejected,",
            "ProtectiveReplaceDisposition = _protectiveReplaceDisposition ?? string.Empty,",
            "ProtectiveReplaceContext = _protectiveReplaceContext ?? string.Empty,",
            "ProtectiveReplaceReason = _protectiveReplaceReason ?? string.Empty,",
            "ProtectiveReplaceSourceOrderId = _protectiveReplaceSourceOrderId ?? string.Empty,",
            "ProtectiveReplaceTargetOrderId = _protectiveReplaceTargetOrderId ?? string.Empty,",
            "ProtectiveReplaceOco = _protectiveReplaceOco ?? string.Empty,",
            "ProtectiveReplaceStartedAtUtc = _protectiveReplaceStartedAtUtc,",
            "AdoptDeferPending = _adoptDeferPending,",
            "AdoptDeferReason = _adoptDeferReason ?? string.Empty,",
            "SessionControlActive = _sessionControlActive,",
            "SessionControlReason = _sessionControlReason ?? string.Empty,",
            "SessionNeutralizationPending = _sessionNeutralizationPending,",
            "StateRestorationInProgress = stateRestorationInProgress,",
            "OrphanedOrdersScanComplete = _orphanedOrdersScanComplete,",
            "EntryAllowed = entryAllowed,",
        ):
            self.assertIn(marker, build_snapshot)

        trade_identity = _method_block(
            text,
            "private RuntimeTradeIdentitySnapshot BuildRuntimeTradeIdentitySnapshot()",
        )
        for marker in (
            "CurrentTradeId = currentTradeID ?? string.Empty,",
            "ActiveTradeId = _activeTradeId ?? string.Empty,",
            "PersistedTradeId = _persistedTradeId ?? string.Empty,",
            "CountedTradeSessionId = _countedTradeSessionId ?? string.Empty,",
            "TradeCount = Math.Max(0, _tradesThisSession),",
            "TrackedFillQuantity = Math.Max(0, _trackedFillQuantity),",
        ):
            self.assertIn(marker, trade_identity)

        finalization = _method_block(
            text,
            "private RuntimeFinalizationSnapshot BuildRuntimeFinalizationSnapshot()",
        )
        for marker in (
            "TradeOpen = tradeOpen,",
            "ControllerStopPlaced = controllerStopPlaced,",
            "StopSubmissionPending = _stopSubmissionPending,",
            "StopSubmitInFlight = _stopSubmitInFlight,",
            "StateRestorationInProgress = stateRestorationInProgress,",
        ):
            self.assertIn(marker, finalization)

        self.assertIn("private RuntimeSnapshotScaffold _lastRuntimeSnapshot;", runtime_host)
        self.assertIn("internal RuntimeSnapshotScaffold RuntimeSnapshot =>", runtime_host)
        self.assertIn("private void RefreshRuntimeSnapshot(string reason)", runtime_host)
        self.assertIn('RefreshRuntimeSnapshot("ResetTradeState");', runtime_host)
        self.assertIn('RefreshRuntimeSnapshot("SaveStrategyState");', runtime_host)
        self.assertIn('RefreshRuntimeSnapshot("RestoreStrategyState");', runtime_host)
        self.assertIn("RefreshRuntimeSnapshot(context ?? \"AnchorEntryExecutionState\");", runtime_host)
        self.assertIn('RefreshRuntimeSnapshot("CountTradeOnFirstEntryFill");', runtime_host)

        for marker in (
            "private bool _orphanedOrdersScanComplete = true;",
            "private int _accountPositionQuantity;",
            "private double _accountAveragePrice;",
            "private void SetReconnectObservationState(",
        ):
            self.assertIn(marker, runtime_state)

    def test_snapshot_builder_reports_working_order_and_coverage_diagnostics(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs")
        coverage_builder = _method_block(
            text,
            "private RuntimeCoverageSnapshot BuildRuntimeCoverageSnapshot()",
        )
        for marker in (
            "if (HasHarnessCoverageSnapshotOverride())",
            "return BuildHarnessCoverageSnapshotOverride();",
            "OrderMaintenanceState state = SnapshotOrderMaintenanceState();",
            "WorkingOrderCount = state.WorkingOrders.Count,",
            "if (order == null || !OrderStateExtensions.IsWorkingLike(order.OrderState))",
            "bool isProtective = order.IsProtectiveStop();",
            "snapshot.WorkingProtectiveOrderCount++;",
            "snapshot.WorkingProtectiveOrderQuantityTotal += Math.Max(0, order.Quantity);",
            "if (IsOurStrategyExitOrder(order))",
            "snapshot.OwnedProtectiveOrderCount++;",
            "snapshot.CompatibleProtectiveOrderCount++;",
            "if (IsPrimaryEntryOrder(order))",
            "snapshot.WorkingPrimaryEntryOrderCount++;",
            "MergeBrokerWorkingCoverageSnapshot(snapshot);",
            "snapshot.ProtectiveCoverageDisposition = BuildProtectiveCoverageDisposition(snapshot);",
        ):
            self.assertIn(marker, coverage_builder)

        broker_merge = _method_block(
            text,
            "private void MergeBrokerWorkingCoverageSnapshot(RuntimeCoverageSnapshot snapshot)",
        )
        for marker in (
            "if (snapshot == null || Account?.Orders == null)",
            "foreach (Order workingOrder in _workingOrders)",
            "int liveQty = Math.Abs(Position.Quantity);",
            "foreach (Order order in EnumerateBrokerWorkingOrders())",
            "if (!IsCompatibleBrokerProtectiveStopOrder(order, liveQty))",
            "if (IsOurStrategyExitOrder(order))",
            "snapshot.OwnedProtectiveOrderCount++;",
            "snapshot.CompatibleProtectiveOrderCount++;",
        ):
            self.assertIn(marker, broker_merge)

        strategy_exit_predicate = _method_block(
            read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs"),
            "private bool IsOurStrategyExitOrder(Order order)",
        )
        execution_identity = read_strategy_file("SecondLegAdvancedMESStrategy.ExecutionIdentityScaffold.cs")
        owned_signal_match = _method_block(
            execution_identity,
            "private bool MatchesOwnedPrimaryEntrySignal(string signal)",
        )
        self.assertIn("string plannedSignalName = PlannedEntrySignalName();", owned_signal_match)
        self.assertIn("string.Equals(signal, _activeEntrySignal, StringComparison.Ordinal)", owned_signal_match)
        self.assertIn("string.Equals(signal, plannedSignalName, StringComparison.Ordinal)", owned_signal_match)
        self.assertIn("string.Equals(signal, currentTradeID, StringComparison.Ordinal)", owned_signal_match)
        self.assertNotIn('signal.StartsWith("PE_", StringComparison.Ordinal)', owned_signal_match)
        self.assertIn("return MatchesOwnedPrimaryEntrySignal(order.FromEntrySignal);", strategy_exit_predicate)
        self.assertNotIn("if (order.IsProtectiveStop())", strategy_exit_predicate)

        harness_override = _method_block(
            read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHarnessAdapterScaffold.cs"),
            "private RuntimeCoverageSnapshot BuildHarnessCoverageSnapshotOverride()",
        )
        for marker in (
            "WorkingOrderCount = _harnessWorkingProtectiveOrderCount,",
            "WorkingPrimaryEntryOrderCount = 0,",
            "WorkingProtectiveOrderCount = _harnessWorkingProtectiveOrderCount,",
            "WorkingProtectiveOrderQuantityTotal = _harnessWorkingProtectiveOrderQuantityTotal,",
            "OwnedProtectiveOrderCount = _harnessOwnedProtectiveOrderCount,",
            "CompatibleProtectiveOrderCount = _harnessCompatibleProtectiveOrderCount,",
            "snapshot.ProtectiveCoverageDisposition = BuildProtectiveCoverageDisposition(snapshot);",
        ):
            self.assertIn(marker, harness_override)

        disposition_builder = _method_block(
            text,
            "private string BuildProtectiveCoverageDisposition(RuntimeCoverageSnapshot snapshot)",
        )
        for marker in (
            "if (!string.IsNullOrEmpty(_protectiveCoverageDisposition))",
            'return _protectiveCoverageDisposition;',
            "if (snapshot.OwnedProtectiveOrderCount > 0)",
            'return "covered-owned";',
            "if (snapshot.CompatibleProtectiveOrderCount > 0)",
            '_protectiveCoverageAmbiguous ? "compatible-unattributed" : "covered-compatible";',
            "if (_stopSubmissionPending || _stopSubmitInFlight)",
            'return "pending-owned";',
        ):
            self.assertIn(marker, disposition_builder)

        durable_context = _method_block(text, "private List<string> BuildDurableLiveTradeContextSources()")
        for marker in (
            'sources.Add("strategy-position");',
            'sources.Add("account-position");',
            'sources.Add("entry-side");',
            'sources.Add("entry-fill-price");',
            'sources.Add("average-entry-price");',
            'sources.Add("account-average-price");',
            'sources.Add("current-trade-id");',
            'sources.Add("active-trade-id");',
        ):
            self.assertIn(marker, durable_context)

    def test_snapshot_builder_emits_harness_tokens_and_entry_gate(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs")

        entry_gate = _method_block(text, "private bool ComputeRuntimeEntryAllowed(bool durableLiveTradeContext)")
        for marker in (
            "bool flattenRecoveryPending = _flattenRejectPending || _staleChildCleanupPending;",
            "bool recoveryResolutionPending = HasPendingRecoveryResolution();",
            "bool runtimeHoldPending =",
            "_protectiveCoverageAmbiguous",
            "_orphanAdoptionPending",
            "_explicitCoverageLossPending",
            "_adoptDeferPending",
            "_protectiveReplacePending",
            "_protectiveReplaceFailurePending",
            "!stateRestorationInProgress",
            "!_sessionControlActive",
            "!durableLiveTradeContext",
            "_orphanedOrdersScanComplete",
            "!recoveryResolutionPending",
        ):
            self.assertIn(marker, entry_gate)

        tokens = _method_block(
            text,
            "private List<string> BuildRuntimeSnapshotTokens(",
        )
        for marker in (
            '"durable-live-context"',
            '"flat-reset-deferred"',
            'recovery-resolution:{_recoveryResolution}',
            '"coverage-ambiguous"',
            '"orphan-adoption-pending"',
            '"explicit-coverage-loss-pending"',
            '"protective-replace-pending"',
            '"protective-replace-failure-pending"',
            '"protective-replace-rejected"',
            'protective-replace:{_protectiveReplaceDisposition}',
            'protective-replace-reason:{_protectiveReplaceReason}',
            '"protective-replace-lineage"',
            '"protective-submit-pending"',
            '"protective-submit-inflight"',
            '"session-control-active"',
            '"session-neutralization-pending"',
            '"reconnect-grace"',
            '"state-restoration"',
            '"entry-allowed"',
            '"covered-owned"',
            '"compatible-unattributed"',
            '"covered-compatible"',
            '"pending-owned"',
            '"flatten-reject-pending"',
            '"stale-child-cleanup-pending"',
            'final-cancel-sweep:',
        ):
            self.assertIn(marker, tokens)

    def test_docs_define_snapshot_as_harness_observation_surface(self) -> None:
        host_contract = read_doc_file("Host_Shell_Contract.md")
        adoption_plan = read_doc_file("External_Runtime_Harness_Adoption_Plan.md")

        self.assertIn("Runtime Snapshot/Diagnostic Surface", host_contract)
        self.assertIn("BuildRuntimeSnapshotScaffold(...)", host_contract)
        self.assertIn("RuntimeSnapshot", host_contract)
        self.assertIn("false-flat restart suppression", host_contract)
        self.assertIn("terminate/live-protection preservation", host_contract)

        self.assertIn("The next adoption seam is a harness-aligned snapshot/diagnostic surface", adoption_plan)
        self.assertIn("BuildRuntimeSnapshotScaffold(...)", adoption_plan)
        self.assertIn("EntryAllowed", adoption_plan)
        self.assertIn("reconnect with compatible broker coverage", adoption_plan)
        self.assertIn("terminate/live-protection preservation", adoption_plan)


if __name__ == "__main__":
    unittest.main()
