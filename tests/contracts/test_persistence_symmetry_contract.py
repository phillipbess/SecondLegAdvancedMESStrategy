"""Source-level persistence symmetry checks for the scaffolded runtime host."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

FAMILY_ID = "persistence_symmetry_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "persistence_fields_expected.json"
REPO_ROOT = Path(__file__).resolve().parents[2]
PERSISTENCE_SCAFFOLD_PATH = (
    REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.PersistenceScaffold.cs"
)
RUNTIME_HOST_PATH = REPO_ROOT / "src" / "strategy" / "SecondLegAdvancedMESStrategy.RuntimeHost.cs"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


class PersistenceSymmetryContractTests(unittest.TestCase):
    def test_manifest_and_fixture_exist(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["family_id"], FAMILY_ID)
        self.assertEqual(fixture["status"], "implemented")

    def test_persistence_scaffold_documents_durable_and_trade_scoped_keys(self) -> None:
        text = _read(PERSISTENCE_SCAFFOLD_PATH)

        expected_markers = [
            'private const string StrategyStateFieldsHeaderMarker = "# StateFields:";',
            'private const string StrategyStateFormatVersionMarker = "# StateFormatVersion:";',
            "private const int StrategyStateFormatVersion = 1;",
            "private static readonly string[] DocumentedDurableStateKeys =",
            '"DailyStopHit",',
            '"DailyLossHit",',
            '"UnknownExitEconomicsBlock",',
            '"AutoDisabled",',
            '"GlobalKillSwitch",',
            '"CurrentTradeId",',
            '"ActiveTradeId",',
            '"CurrentExitOco",',
            '"ActiveEntrySignal",',
            '"CurrentEntryTag",',
            '"EntryPositionSide",',
            '"EntryFillTime",',
            '"AvgEntryPrice",',
            '"InitialStopPrice",',
            "private static readonly string[] DocumentedTradeScopedStateKeys =",
            '"TradeOpen",',
            '"ControllerStopPlaced",',
            '"ProtectiveCoverageAmbiguous",',
            '"ProtectiveCoverageDisposition",',
            '"RecoveryResolution",',
            '"OrphanedOrdersScanComplete",',
            '"OrphanAdoptionPending",',
            '"ExplicitCoverageLossPending",',
            '"ProtectiveReplacePending",',
            '"ProtectiveReplaceFailurePending",',
            '"ProtectiveReplaceRejected",',
            '"ProtectiveReplaceDisposition",',
            '"ProtectiveReplaceContext",',
            '"ProtectiveReplaceReason",',
            '"ProtectiveReplaceSourceOrderId",',
            '"ProtectiveReplaceTargetOrderId",',
            '"ProtectiveReplaceOco",',
            '"ProtectiveReplaceStartedAtUtc",',
            '"AdoptDeferPending",',
            '"AdoptDeferReason",',
            '"LastStopStateChangeAtUtc",',
            '"CoverageGraceUntilUtc",',
            '"StopSubmitInFlight",',
            '"StopSubmissionPending",',
            '"LastStopSubmitAtUtc",',
            '"LastStopSubmissionAtUtc",',
            '"ExitState",',
            '"FlattenRejectPending",',
            '"StaleChildCleanupPending",',
            '"FinalCancelSweepDisposition",',
            '"PreservedProtectiveOrderCount",',
            '"StateRestorationInProgress",',
        ]

        for marker in expected_markers:
            self.assertIn(marker, text)

    def test_persistence_scaffold_keeps_save_restore_reset_responsibilities_separate(self) -> None:
        text = _read(PERSISTENCE_SCAFFOLD_PATH)
        runtime_host = _read(RUNTIME_HOST_PATH)

        expected_methods = [
            "private bool ShouldPersistStrategyState()",
            "private void InitializeStatePersistencePath()",
            "private void AppendStrategyStateHeaderScaffold(ICollection<string> lines)",
            "private void AppendDurableStateSnapshotScaffold(ICollection<string> lines)",
            "private void AppendTradeScopedStateSnapshotScaffold(ICollection<string> lines)",
            "private void RestoreDurableStateScaffold(IReadOnlyDictionary<string, string> stateEntries)",
            "private void RestoreTradeScopedStateScaffold(IReadOnlyDictionary<string, string> stateEntries)",
            "private void ResetTradeStateScaffold()",
            "private void ResetDurableStateForDisableScaffold()",
        ]

        for marker in expected_methods:
            self.assertIn(marker, text)

        self.assertIn("private void ResetTradeState()", runtime_host)
        self.assertIn("CapturePersistedTradeIdentity(\"SaveStrategyState\");", runtime_host)
        self.assertIn("AppendStrategyStateHeaderScaffold(stateLines);", runtime_host)
        self.assertIn("AppendDurableStateSnapshotScaffold(stateLines);", runtime_host)
        self.assertIn("AppendTradeScopedStateSnapshotScaffold(stateLines);", runtime_host)
        self.assertIn("RestoreDurableStateScaffold(stateEntries);", runtime_host)
        self.assertIn("RestoreTradeScopedStateScaffold(stateEntries);", runtime_host)
        self.assertIn("ResetTradeStateScaffold();", runtime_host)
        self.assertIn("_submissionRetryCorrelationBySignal.Clear();", runtime_host)
        self.assertIn("_submissionAuthorityEmitOnce.Clear();", runtime_host)
        self.assertIn("_exitState = ExitFlowState.Flat;", runtime_host)
