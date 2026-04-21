"""Source-level checks for the flat runtime harness projection scaffold."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file, read_strategy_file

FAMILY_ID = "runtime_harness_projection_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
CONTRACTS_README = Path(__file__).with_name("README.md")


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


class RuntimeHarnessProjectionContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertIn("flat harness-shaped snapshot projection", family["goal"])

    def test_projection_doc_defines_purpose_and_surface(self) -> None:
        doc = read_doc_file("Runtime_Harness_Projection_Contract.md")
        for marker in (
            "# Runtime Harness Projection Contract",
            "flat runtime-snapshot projection",
            "`BuildRuntimeHarnessSnapshotProjection(...)`",
            "`HarnessProjectedSnapshot`",
            "`Mancini.RuntimeTests.Host.RuntimeSnapshot`",
            "false-flat suppression",
            "reconnect coverage truth",
            "live-context terminate preservation",
        ):
            self.assertIn(marker, doc)

    def test_projection_scaffold_declares_harness_shaped_type(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHarnessProjectionScaffold.cs")

        for marker in (
            "public sealed class RuntimeHarnessSnapshotProjection",
            "public System.DateTime UtcNow { get; set; }",
            "public int StrategyQuantity { get; set; }",
            "public int AccountQuantity { get; set; }",
            "public bool DurableLiveTradeContext { get; set; }",
            "public IList<string> DurableLiveTradeContextSources { get; set; }",
            "public string RecoveryResolution { get; set; }",
            "public bool ProtectiveCoverageAmbiguous { get; set; }",
            "public bool OrphanAdoptionPending { get; set; }",
            "public bool ExplicitCoverageLossPending { get; set; }",
            "public bool ProtectiveReplacePending { get; set; }",
            "public bool ProtectiveReplaceFailurePending { get; set; }",
            "public bool ProtectiveReplaceRejected { get; set; }",
            "public string ProtectiveReplaceDisposition { get; set; }",
            "public string ProtectiveReplaceContext { get; set; }",
            "public string ProtectiveReplaceReason { get; set; }",
            "public string ProtectiveReplaceSourceOrderId { get; set; }",
            "public string ProtectiveReplaceTargetOrderId { get; set; }",
            "public string ProtectiveReplaceOco { get; set; }",
            "public System.DateTime ProtectiveReplaceStartedAtUtc { get; set; }",
            "public bool AdoptDeferPending { get; set; }",
            "public string AdoptDeferReason { get; set; }",
            "public bool SessionControlActive { get; set; }",
            "public string SessionControlReason { get; set; }",
            "public bool SessionNeutralizationPending { get; set; }",
            "public bool FlattenRejectPending { get; set; }",
            "public bool StaleChildCleanupPending { get; set; }",
            "public bool FlatResetDeferred { get; set; }",
            "public bool StateRestorationInProgress { get; set; }",
            "public bool OrphanedOrdersScanComplete { get; set; }",
            "public bool EntryAllowed { get; set; }",
            "public string ExitState { get; set; }",
            "public bool TradeOpen { get; set; }",
            "public bool ControllerStopPlaced { get; set; }",
            "public bool StopSubmissionPending { get; set; }",
            "public bool StopSubmitInFlight { get; set; }",
            "public int WorkingOrderCount { get; set; }",
            "public int WorkingPrimaryEntryOrderCount { get; set; }",
            "public int WorkingProtectiveOrderCount { get; set; }",
            "public int WorkingProtectiveOrderQuantityTotal { get; set; }",
            "public int OwnedProtectiveOrderCount { get; set; }",
            "public int CompatibleProtectiveOrderCount { get; set; }",
            "public string ProtectiveCoverageDisposition { get; set; }",
            "public string FinalCancelSweepDisposition { get; set; }",
            "public int CancelRequestCount { get; set; }",
            "public int EntryCancelRequestCount { get; set; }",
            "public int ProtectiveCancelRequestCount { get; set; }",
            "public int ProtectiveSubmitRequestCount { get; set; }",
            "public int FlattenRequestCount { get; set; }",
            "public int PreservedProtectiveOrderCount { get; set; }",
            "public string CurrentTradeId { get; set; }",
            "public string ActiveTradeId { get; set; }",
            "public string PersistedTradeId { get; set; }",
            "public string CountedTradeSessionId { get; set; }",
            "public int TradeCount { get; set; }",
            "public int TrackedFillQuantity { get; set; }",
            "public IList<string> Tokens { get; set; }",
            "internal RuntimeHarnessSnapshotProjection HarnessProjectedSnapshot =>",
            'BuildRuntimeHarnessSnapshotProjection("HarnessProjectedSnapshot");',
            'internal RuntimeHarnessSnapshotProjection BuildRuntimeHarnessSnapshotProjection(string reason = "")',
        ):
            self.assertIn(marker, text)

    def test_projection_builder_flattens_root_coverage_identity_and_finalization_fields(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHarnessProjectionScaffold.cs")
        build_projection = _method_block(
            text,
            'internal RuntimeHarnessSnapshotProjection BuildRuntimeHarnessSnapshotProjection(string reason = "")',
        )

        for marker in (
            'RuntimeSnapshotScaffold snapshot = BuildRuntimeSnapshotScaffold(reason ?? string.Empty);',
            "UtcNow = snapshot.CapturedAtUtc,",
            "StrategyQuantity = snapshot.StrategyQuantity,",
            "AccountQuantity = snapshot.AccountQuantity,",
            "DurableLiveTradeContext = snapshot.DurableLiveTradeContext,",
            "DurableLiveTradeContextSources = new List<string>(snapshot.DurableLiveTradeContextSources),",
            "RecoveryResolution = snapshot.RecoveryResolution ?? string.Empty,",
            "ProtectiveCoverageAmbiguous = snapshot.ProtectiveCoverageAmbiguous,",
            "OrphanAdoptionPending = snapshot.OrphanAdoptionPending,",
            "ExplicitCoverageLossPending = snapshot.ExplicitCoverageLossPending,",
            "ProtectiveReplacePending = snapshot.ProtectiveReplacePending,",
            "ProtectiveReplaceFailurePending = snapshot.ProtectiveReplaceFailurePending,",
            "ProtectiveReplaceRejected = snapshot.ProtectiveReplaceRejected,",
            "ProtectiveReplaceDisposition = snapshot.ProtectiveReplaceDisposition ?? string.Empty,",
            "ProtectiveReplaceContext = snapshot.ProtectiveReplaceContext ?? string.Empty,",
            "ProtectiveReplaceReason = snapshot.ProtectiveReplaceReason ?? string.Empty,",
            "ProtectiveReplaceSourceOrderId = snapshot.ProtectiveReplaceSourceOrderId ?? string.Empty,",
            "ProtectiveReplaceTargetOrderId = snapshot.ProtectiveReplaceTargetOrderId ?? string.Empty,",
            "ProtectiveReplaceOco = snapshot.ProtectiveReplaceOco ?? string.Empty,",
            "ProtectiveReplaceStartedAtUtc = snapshot.ProtectiveReplaceStartedAtUtc,",
            "AdoptDeferPending = snapshot.AdoptDeferPending,",
            "AdoptDeferReason = snapshot.AdoptDeferReason ?? string.Empty,",
            "SessionControlActive = snapshot.SessionControlActive,",
            "SessionControlReason = snapshot.SessionControlReason ?? string.Empty,",
            "SessionNeutralizationPending = snapshot.SessionNeutralizationPending,",
            "FlattenRejectPending = snapshot.Finalization.FlattenRejectPending,",
            "StaleChildCleanupPending = snapshot.Finalization.StaleChildCleanupPending,",
            "FlatResetDeferred = snapshot.FlatResetDeferred,",
            "StateRestorationInProgress = snapshot.StateRestorationInProgress,",
            "OrphanedOrdersScanComplete = snapshot.OrphanedOrdersScanComplete,",
            "EntryAllowed = snapshot.EntryAllowed,",
            "ExitState = snapshot.Finalization.ExitState ?? string.Empty,",
            "TradeOpen = snapshot.Finalization.TradeOpen,",
            "ControllerStopPlaced = snapshot.Finalization.ControllerStopPlaced,",
            "StopSubmissionPending = snapshot.Finalization.StopSubmissionPending,",
            "StopSubmitInFlight = snapshot.Finalization.StopSubmitInFlight,",
            "WorkingOrderCount = snapshot.Coverage.WorkingOrderCount,",
            "WorkingPrimaryEntryOrderCount = snapshot.Coverage.WorkingPrimaryEntryOrderCount,",
            "WorkingProtectiveOrderCount = snapshot.Coverage.WorkingProtectiveOrderCount,",
            "WorkingProtectiveOrderQuantityTotal = snapshot.Coverage.WorkingProtectiveOrderQuantityTotal,",
            "OwnedProtectiveOrderCount = snapshot.Coverage.OwnedProtectiveOrderCount,",
            "CompatibleProtectiveOrderCount = snapshot.Coverage.CompatibleProtectiveOrderCount,",
            "ProtectiveCoverageDisposition = snapshot.Coverage.ProtectiveCoverageDisposition ?? string.Empty,",
            "FinalCancelSweepDisposition = snapshot.Finalization.FinalCancelSweepDisposition ?? string.Empty,",
            "CancelRequestCount = snapshot.Finalization.CancelRequestCount,",
            "EntryCancelRequestCount = snapshot.Finalization.EntryCancelRequestCount,",
            "ProtectiveCancelRequestCount = snapshot.Finalization.ProtectiveCancelRequestCount,",
            "ProtectiveSubmitRequestCount = snapshot.Finalization.ProtectiveSubmitRequestCount,",
            "FlattenRequestCount = snapshot.Finalization.FlattenRequestCount,",
            "PreservedProtectiveOrderCount = snapshot.Finalization.PreservedProtectiveOrderCount,",
            "CurrentTradeId = snapshot.TradeIdentity.CurrentTradeId ?? string.Empty,",
            "ActiveTradeId = snapshot.TradeIdentity.ActiveTradeId ?? string.Empty,",
            "PersistedTradeId = snapshot.TradeIdentity.PersistedTradeId ?? string.Empty,",
            "CountedTradeSessionId = snapshot.TradeIdentity.CountedTradeSessionId ?? string.Empty,",
            "TradeCount = snapshot.TradeIdentity.TradeCount,",
            "TrackedFillQuantity = snapshot.TradeIdentity.TrackedFillQuantity,",
            "Tokens = new List<string>(snapshot.Tokens),",
        ):
            self.assertIn(marker, build_projection)

    def test_docs_treat_projection_as_the_next_external_harness_bridge_seam(self) -> None:
        host_contract = read_doc_file("Host_Shell_Contract.md")
        adoption_plan = read_doc_file("External_Runtime_Harness_Adoption_Plan.md")
        adapter_contract = read_doc_file("Runtime_Harness_Adapter_Contract.md")
        first_pack = read_doc_file("First_Runtime_Harness_Scenario_Pack.md")
        contracts_readme = CONTRACTS_README.read_text(encoding="utf-8")

        self.assertIn("### 8. Runtime Harness Projection", host_contract)
        self.assertIn("BuildRuntimeHarnessSnapshotProjection(...)", host_contract)
        self.assertIn("HarnessProjectedSnapshot", host_contract)

        self.assertIn("BuildRuntimeHarnessSnapshotProjection(...)", adoption_plan)
        self.assertIn("HarnessProjectedSnapshot", adoption_plan)
        self.assertIn("Runtime_Harness_Projection_Contract.md", adoption_plan)

        self.assertIn("BuildRuntimeHarnessSnapshotProjection(...)", adapter_contract)
        self.assertIn("harness projection refreshes", adapter_contract)

        self.assertIn("BuildRuntimeHarnessSnapshotProjection(...)", first_pack)
        self.assertIn("HarnessPack1Snapshot", first_pack)
        self.assertIn("HarnessProjectedSnapshot", first_pack)
        self.assertIn("Runtime_Harness_Projection_Contract.md", first_pack)

        self.assertIn("runtime_harness_projection_contract", contracts_readme)


if __name__ == "__main__":
    unittest.main()
