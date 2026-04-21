"""Source-level checks for the thin local runtime harness adapter scaffold."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file, read_strategy_file

FAMILY_ID = "runtime_harness_adapter_contract"
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


class RuntimeHarnessAdapterContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertIn("thin local harness-adapter surface", family["goal"])

    def test_adapter_doc_defines_scope_and_required_surface(self) -> None:
        doc = read_doc_file("Runtime_Harness_Adapter_Contract.md")
        for marker in (
            "# Runtime Harness Adapter Contract",
            "restart false-flat suppression",
            "reconnect with compatible broker coverage",
            "`HarnessSnapshot`",
            "`HarnessPack1Snapshot`",
            "`HarnessEnterHistorical(...)`",
            "`HarnessEnterRealtime(...)`",
            "`HarnessSetAccountPosition(...)`",
            "`HarnessSetStrategyPosition(...)`",
            "`HarnessSetOrphanedOrdersScanComplete(...)`",
            "`HarnessSetProtectiveCoverageAmbiguous(...)`",
            "`HarnessSetFlatResetDeferred(...)`",
            "`HarnessSetCoverageSnapshot(...)`",
            "`HarnessClearCoverageSnapshotOverride(...)`",
            "`HarnessEvaluateReconnectCoverage(...)`",
            "`HarnessStartTrade(...)`",
            "`HarnessRecordPrimaryEntryFill(...)`",
            "`HarnessPersistTradeState(...)`",
            "`HarnessRestorePersistedTradeState(...)`",
            "`HarnessSetStateRestorationInProgress(...)`",
            "`HarnessResetIntentCounters(...)`",
            "`HarnessSetIntentCounters(...)`",
            "`HarnessSetFinalCancelSweep(...)`",
            "`HarnessTerminate(...)`",
            "`HarnessRestoreProjectedSnapshot(...)`",
        ):
            self.assertIn(marker, doc)

    def test_adapter_scaffold_declares_first_pack_surface(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHarnessAdapterScaffold.cs")

        for marker in (
            "private int? _harnessStrategyQuantity;",
            "private double? _harnessStrategyAveragePrice;",
            "private bool _harnessCoverageOverrideEnabled;",
            "private int _harnessOwnedProtectiveOrderCount;",
            "private int _harnessCompatibleProtectiveOrderCount;",
            "private int _harnessWorkingProtectiveOrderCount;",
            "private int _harnessWorkingProtectiveOrderQuantityTotal;",
            "private bool _harnessHistorical;",
            "internal RuntimeSnapshotScaffold HarnessSnapshot => RuntimeSnapshot;",
            "internal RuntimeHarnessSnapshotProjection HarnessPack1Snapshot => HarnessProjectedSnapshot;",
            'internal void HarnessEnterHistorical(string context = "")',
            'internal void HarnessEnterRealtime(string context = "")',
            'internal void HarnessSetAccountPosition(int quantity, double averagePrice, string context = "")',
            'internal void HarnessSetStrategyPosition(int quantity, double averagePrice, string context = "")',
            'internal void HarnessSetOrphanedOrdersScanComplete(bool complete, string context = "")',
            'internal void HarnessSetProtectiveCoverageAmbiguous(bool ambiguous, string context = "")',
            'internal void HarnessSetFlatResetDeferred(bool deferred, string context = "")',
            "internal void HarnessSetCoverageSnapshot(",
            'internal void HarnessClearCoverageSnapshotOverride(string context = "")',
            'internal void HarnessEvaluateReconnectCoverage(string context = "")',
            'internal void HarnessStartTrade(string tradeId, string context = "")',
            "internal void HarnessRecordPrimaryEntryFill(",
            'internal void HarnessPersistTradeState(string context = "")',
            'internal void HarnessRestorePersistedTradeState(string context = "")',
            'internal void HarnessSetStateRestorationInProgress(bool enabled, string context = "")',
            'internal void HarnessResetIntentCounters(string context = "")',
            "internal void HarnessSetIntentCounters(",
            "internal void HarnessSetFinalCancelSweep(",
            'internal void HarnessTerminate(string context = "")',
            "internal void HarnessRestoreProjectedSnapshot(",
            "private int GetRuntimeStrategyQuantity()",
            "private double GetRuntimeStrategyAveragePrice()",
            "private bool HasHarnessCoverageSnapshotOverride()",
            "private RuntimeCoverageSnapshot BuildHarnessCoverageSnapshotOverride()",
            "private static string NormalizeProjectionString(string value)",
        ):
            self.assertIn(marker, text)

    def test_adapter_methods_route_through_existing_host_owned_seams(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHarnessAdapterScaffold.cs")

        enter_historical = _method_block(text, 'internal void HarnessEnterHistorical(string context = "")')
        self.assertIn("_harnessHistorical = true;", enter_historical)
        self.assertIn("SetStateRestorationInProgress(true,", enter_historical)
        self.assertIn("RefreshRuntimeSnapshot(", enter_historical)

        enter_realtime = _method_block(text, 'internal void HarnessEnterRealtime(string context = "")')
        self.assertIn("_harnessHistorical = false;", enter_realtime)
        self.assertIn("HandleFlatRealtimeRestartScaffold();", enter_realtime)
        self.assertIn("RefreshRuntimeSnapshot(", enter_realtime)
        execution_identity = read_strategy_file("SecondLegAdvancedMESStrategy.ExecutionIdentityScaffold.cs")
        self.assertIn("HandleFlatRealtimeRestartScaffold()", execution_identity)
        self.assertIn("_flatResetDeferred = false;", execution_identity)

        set_account = _method_block(
            text,
            'internal void HarnessSetAccountPosition(int quantity, double averagePrice, string context = "")',
        )
        self.assertIn("SetReconnectObservationState(_orphanedOrdersScanComplete, quantity, averagePrice);", set_account)
        self.assertIn("RefreshRuntimeSnapshot(", set_account)

        set_orphan_scan = _method_block(
            text,
            'internal void HarnessSetOrphanedOrdersScanComplete(bool complete, string context = "")',
        )
        self.assertIn("SetReconnectObservationState(complete, _accountPositionQuantity, _accountAveragePrice);", set_orphan_scan)

        set_flat_reset = _method_block(
            text,
            'internal void HarnessSetFlatResetDeferred(bool deferred, string context = "")',
        )
        self.assertIn("_flatResetDeferred = deferred;", set_flat_reset)
        self.assertIn("RefreshRuntimeSnapshot(", set_flat_reset)

        set_coverage = _method_block(text, "internal void HarnessSetCoverageSnapshot(")
        for marker in (
            "_harnessCoverageOverrideEnabled = true;",
            "_harnessOwnedProtectiveOrderCount = Math.Max(0, ownedProtectiveOrderCount);",
            "_harnessCompatibleProtectiveOrderCount = Math.Max(0, compatibleProtectiveOrderCount);",
            "_harnessWorkingProtectiveOrderCount = Math.Max(0, workingProtectiveOrderCount);",
            "_harnessWorkingProtectiveOrderQuantityTotal = Math.Max(0, workingProtectiveOrderQuantityTotal);",
            "SetProtectiveCoverageDisposition(",
            "RefreshRuntimeSnapshot(",
        ):
            self.assertIn(marker, set_coverage)

        clear_coverage = _method_block(
            text,
            'internal void HarnessClearCoverageSnapshotOverride(string context = "")',
        )
        for marker in (
            "_harnessCoverageOverrideEnabled = false;",
            "_harnessOwnedProtectiveOrderCount = 0;",
            "_harnessCompatibleProtectiveOrderCount = 0;",
            "_harnessWorkingProtectiveOrderCount = 0;",
            "_harnessWorkingProtectiveOrderQuantityTotal = 0;",
            "SetProtectiveCoverageDisposition(",
            "RefreshRuntimeSnapshot(",
        ):
            self.assertIn(marker, clear_coverage)

        reconnect_eval = _method_block(text, 'internal void HarnessEvaluateReconnectCoverage(string context = "")')
        for marker in (
            "RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();",
            "bool hasCoverage =",
            "bool orphanAdoptionPending = HasDurableLiveTradeContext()",
            "SetProtectiveCoverageDisposition(",
            "BuildProtectiveCoverageDisposition(coverage)",
            "SetRecoveryHoldState(",
            "RefreshRuntimeSnapshot(",
        ):
            self.assertIn(marker, reconnect_eval)

        record_fill = _method_block(text, "internal void HarnessRecordPrimaryEntryFill(")
        for marker in (
            "EnsureActiveTradeIdFromCurrentTradeId(",
            "_tradesThisSession++;",
            "_countedTradeSessionId = currentTradeID;",
            "TrackCumulativeFillQuantity(",
            "_harnessStrategyQuantity = cumulativeFilledQuantity;",
            "_harnessStrategyAveragePrice = averagePrice;",
            "RefreshRuntimeSnapshot(",
        ):
            self.assertIn(marker, record_fill)

        persist = _method_block(text, 'internal void HarnessPersistTradeState(string context = "")')
        self.assertIn("CapturePersistedTradeIdentity(", persist)
        self.assertIn("SetStateRestorationInProgress(true,", persist)

        restore = _method_block(text, 'internal void HarnessRestorePersistedTradeState(string context = "")')
        self.assertIn("if (!string.IsNullOrEmpty(_persistedTradeId))", restore)
        self.assertIn("currentTradeID = _persistedTradeId;", restore)
        self.assertIn("_activeTradeId = _persistedTradeId;", restore)

        reset_intents = _method_block(text, 'internal void HarnessResetIntentCounters(string context = "")')
        self.assertIn("SetRuntimeIntentCounters(0, 0, 0, 0, 0);", reset_intents)
        self.assertIn("SetFinalCancelSweepDisposition(string.Empty, 0,", reset_intents)

        set_intents = _method_block(text, "internal void HarnessSetIntentCounters(")
        self.assertIn("SetRuntimeIntentCounters(", set_intents)
        self.assertIn("RefreshRuntimeSnapshot(", set_intents)

        set_final_cancel = _method_block(text, "internal void HarnessSetFinalCancelSweep(")
        self.assertIn("SetRuntimeIntentCounters(", set_final_cancel)
        self.assertIn("SetFinalCancelSweepDisposition(", set_final_cancel)
        self.assertIn("RefreshRuntimeSnapshot(", set_final_cancel)

        terminate = _method_block(text, 'internal void HarnessTerminate(string context = "")')
        for marker in (
            "RuntimeCoverageSnapshot coverage = BuildRuntimeCoverageSnapshot();",
            "bool durableLiveTradeContext = HasDurableLiveTradeContext();",
            "int protectiveOrderCount = Math.Max(0, coverage.WorkingProtectiveOrderCount);",
            'SetFinalCancelSweepDisposition(',
            '"skip"',
            '"cancel"',
            "SetRuntimeIntentCounters(",
            "RefreshRuntimeSnapshot(",
        ):
            self.assertIn(marker, terminate)

        restore_projection = _method_block(text, "internal void HarnessRestoreProjectedSnapshot(")
        for marker in (
            "_harnessStrategyQuantity = projection.StrategyQuantity;",
            "SetReconnectObservationState(",
            "SetRecoveryHoldState(",
            "SetSessionControlState(",
            "SetFlattenRecoveryState(",
            "SetRuntimeIntentCounters(",
            "SetFinalCancelSweepDisposition(",
            "_harnessCoverageOverrideEnabled = true;",
            "SetProtectiveCoverageDisposition(",
            "currentTradeID = NormalizeProjectionString(projection.CurrentTradeId);",
            "_activeTradeId = NormalizeProjectionString(projection.ActiveTradeId);",
            "_persistedTradeId = NormalizeProjectionString(projection.PersistedTradeId);",
            "_countedTradeSessionId = NormalizeProjectionString(projection.CountedTradeSessionId);",
            "TrackCumulativeFillQuantity(",
            "SetStateRestorationInProgress(",
            "RefreshRuntimeSnapshot(",
        ):
            self.assertIn(marker, restore_projection)

    def test_snapshot_builder_uses_adapter_strategy_overrides(self) -> None:
        snapshot = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs")
        adapter = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHarnessAdapterScaffold.cs")

        self.assertIn("StrategyQuantity = GetRuntimeStrategyQuantity(),", snapshot)
        self.assertIn("if (GetRuntimeStrategyQuantity() != 0)", snapshot)
        self.assertIn("if (GetRuntimeStrategyAveragePrice() > 0.0)", snapshot)
        self.assertIn("private int GetRuntimeStrategyQuantity()", adapter)
        self.assertIn("return _harnessStrategyQuantity ?? Position.Quantity;", adapter)
        self.assertIn("private double GetRuntimeStrategyAveragePrice()", adapter)
        self.assertIn("return _harnessStrategyAveragePrice ?? avgEntryPrice;", adapter)
        self.assertIn("private bool HasHarnessCoverageSnapshotOverride()", adapter)
        self.assertIn("private RuntimeCoverageSnapshot BuildHarnessCoverageSnapshotOverride()", adapter)


if __name__ == "__main__":
    unittest.main()
