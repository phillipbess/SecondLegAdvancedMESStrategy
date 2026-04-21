"""Source-level checks for the first runtime-harness scenario pack."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file, read_strategy_file

FAMILY_ID = "runtime_scenario_pack_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class RuntimeScenarioPackContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertIn("first external runtime-harness scenario pack", family["goal"])

    def test_scenario_pack_doc_defines_first_pack_scope_and_scenarios(self) -> None:
        doc = read_doc_file("First_Runtime_Harness_Scenario_Pack.md")

        for marker in (
            "# First Runtime Harness Scenario Pack",
            "This is a runtime-pack, not a strategy-edge-pack.",
            "Restart_WithAccountLong_StrategyQtyZero_DoesNotDeclareFlat",
            "Restart_WithCompatibleBrokerCoverage_DelayedScan_AfterTradeRestore",
            "Terminate_WithLiveContext_DoesNotCancelOnlyProtectiveStop",
            "Terminate_WithoutLiveContext_CancelsProtectiveStop",
            "flatten-reject/stale-child cleanup",
            "Pack 2 should add:",
        ):
            self.assertIn(marker, doc)

    def test_scenario_pack_doc_locks_required_snapshot_fields(self) -> None:
        doc = read_doc_file("First_Runtime_Harness_Scenario_Pack.md")

        for marker in (
            "`EntryAllowed`",
            "`DurableLiveTradeContext`",
            "`DurableLiveTradeContextSources`",
            "`FlatResetDeferred`",
            "`StateRestorationInProgress`",
            "`OrphanedOrdersScanComplete`",
            "`ProtectiveCoverageAmbiguous`",
            "`ProtectiveCoverageDisposition`",
            "`OwnedProtectiveOrderCount`",
            "`CompatibleProtectiveOrderCount`",
            "`WorkingProtectiveOrderCount`",
            "`WorkingProtectiveOrderQuantityTotal`",
            "`CurrentTradeId`",
            "`ActiveTradeId`",
            "`PersistedTradeId`",
            "`CountedTradeSessionId`",
            "`TradeCount`",
            "`TrackedFillQuantity`",
            "`ProtectiveSubmitRequestCount`",
            "`CancelRequestCount`",
            "`ProtectiveCancelRequestCount`",
            "`PreservedProtectiveOrderCount`",
            "`FinalCancelSweepDisposition`",
        ):
            self.assertIn(marker, doc)

    def test_scenario_pack_doc_locks_expected_tokens_and_anchor_ids(self) -> None:
        doc = read_doc_file("First_Runtime_Harness_Scenario_Pack.md")

        for marker in (
            "A_FALSE_FLAT_AND_RECONNECT_GATING",
            "A_COVERAGE_AND_ADOPTION_TRUTH",
            "A_TERMINATE_AND_FLATTEN_AUTHORITY",
            "`defer-flat-reset`",
            "`release-flat-reset`",
            "`hold-for-orphan-adoption`",
            "`hold-for-coverage-ambiguity`",
            "`submit-protective-order`",
            "`final-cancel-sweep`",
            "`cancel-order`",
            "`durable-live-context`",
            "`flat-reset-deferred`",
            "`reconnect-grace`",
            "`entry-allowed`",
            "`orphan-adoption-pending`",
            "`covered-owned`",
            "`covered-compatible`",
            "`final-cancel-sweep:skip`",
            "`final-cancel-sweep:cancel`",
        ):
            self.assertIn(marker, doc)

    def test_scenario_pack_doc_points_back_to_local_runtime_seams(self) -> None:
        doc = read_doc_file("First_Runtime_Harness_Scenario_Pack.md")
        snapshot = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs")
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        runtime_state = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs")
        execution_identity = read_strategy_file("SecondLegAdvancedMESStrategy.ExecutionIdentityScaffold.cs")

        for marker in (
            "BuildRuntimeSnapshotScaffold(...)",
            "BuildRuntimeCoverageSnapshot()",
            "BuildRuntimeTradeIdentitySnapshot()",
            "BuildRuntimeFinalizationSnapshot()",
            "BuildDurableLiveTradeContextSources()",
            "ComputeRuntimeEntryAllowed(...)",
            "BuildRuntimeSnapshotTokens(...)",
            "SetReconnectObservationState(...)",
            "SetProtectiveCoverageDisposition(...)",
            "SetRecoveryHoldState(...)",
            "SetRuntimeIntentCounters(...)",
            "SetFinalCancelSweepDisposition(...)",
            "HandleFlatRealtimeRestartScaffold()",
            "ReconcileCompatibleBrokerCoverageForRecovery(...)",
            "AnchorEntryExecutionState(...)",
            "CountTradeOnFirstEntryFill(...)",
            "SaveStrategyState()",
            "RestoreStrategyState()",
            "CancelAllWorkingOrders(...)",
            "EnqueueOrderCancellation(...)",
            "SafeCancelOrder(...)",
            "TriggerFlatten(...)",
        ):
            self.assertIn(marker, doc)

        for source_marker in (
            "internal RuntimeSnapshotScaffold BuildRuntimeSnapshotScaffold(string reason = \"\")",
            "private RuntimeCoverageSnapshot BuildRuntimeCoverageSnapshot()",
            "private RuntimeTradeIdentitySnapshot BuildRuntimeTradeIdentitySnapshot()",
            "private RuntimeFinalizationSnapshot BuildRuntimeFinalizationSnapshot()",
            "private List<string> BuildDurableLiveTradeContextSources()",
            "private bool ComputeRuntimeEntryAllowed(bool durableLiveTradeContext)",
            "private List<string> BuildRuntimeSnapshotTokens(",
        ):
            self.assertIn(source_marker, snapshot)

        for source_marker in (
            "private void SetReconnectObservationState(",
            "private void SetProtectiveCoverageDisposition(string disposition, string context)",
            "private void SetRecoveryHoldState(",
            "private void SetRuntimeIntentCounters(",
            "private void SetFinalCancelSweepDisposition(",
        ):
            self.assertIn(source_marker, runtime_state)

        self.assertIn("private void HandleFlatRealtimeRestartScaffold()", execution_identity)

        for source_marker in (
            "private bool ReconcileCompatibleBrokerCoverageForRecovery(string context, int quantity)",
            "private void AnchorEntryExecutionState(Execution execution, double fillPrice, string context)",
            "private void CountTradeOnFirstEntryFill(Execution execution)",
            "private void SaveStrategyState()",
            "private void RestoreStrategyState()",
            "private void CancelAllWorkingOrders(string reason)",
            "private void EnqueueOrderCancellation(Order order, string tag)",
            "private bool SafeCancelOrder(Order order, string context = \"\")",
            "private void TriggerFlatten(string reason)",
        ):
            self.assertIn(source_marker, runtime_host)


if __name__ == "__main__":
    unittest.main()
