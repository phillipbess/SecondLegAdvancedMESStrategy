"""Source-level checks for external runtime harness readiness."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file, read_strategy_file

FAMILY_ID = "external_harness_readiness_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class ExternalHarnessReadinessContractTests(unittest.TestCase):
    def test_manifest_mentions_module(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "scaffold_assertions")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_runtime_scenario_state_declares_restart_reconnect_and_session_fields(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs")

        expected_markers = [
            "private bool _flatResetDeferred;",
            "private bool _protectiveCoverageAmbiguous;",
            "private bool _orphanAdoptionPending;",
            "private bool _explicitCoverageLossPending;",
            "private bool _protectiveReplacePending;",
            "private bool _protectiveReplaceFailurePending;",
            "private bool _protectiveReplaceRejected;",
            'private string _protectiveReplaceDisposition = string.Empty;',
            'private string _protectiveReplaceContext = string.Empty;',
            'private string _protectiveReplaceReason = string.Empty;',
            'private string _protectiveReplaceSourceOrderId = string.Empty;',
            'private string _protectiveReplaceTargetOrderId = string.Empty;',
            'private string _protectiveReplaceOco = string.Empty;',
            "private DateTime _protectiveReplaceStartedAtUtc = DateTime.MinValue;",
            "private bool _adoptDeferPending;",
            'private string _adoptDeferReason = string.Empty;',
            "private bool _sessionControlActive;",
            'private string _sessionControlReason = string.Empty;',
            "private bool _sessionNeutralizationPending;",
            "private bool _flattenRejectPending;",
            "private bool _staleChildCleanupPending;",
            'private string _protectiveCoverageDisposition = string.Empty;',
            'private string _countedTradeSessionId = string.Empty;',
            "private int _trackedFillQuantity;",
        ]

        for marker in expected_markers:
            self.assertIn(marker, text)

    def test_runtime_scenario_state_exposes_harness_facing_setters_and_reset(self) -> None:
        text = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs")
        runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")

        expected_methods = [
            "private void ResetRuntimeScenarioState()",
            "private void SetProtectiveCoverageDisposition(string disposition, string context)",
            "private void SetProtectiveReplaceLineageState(",
            "private void SetSessionControlState(bool active, string reason, bool neutralizationPending)",
            "private void SetRecoveryHoldState(",
            "private void SetFlattenRecoveryState(",
        ]

        for marker in expected_methods:
            self.assertIn(marker, text)

        self.assertIn("ResetRuntimeScenarioState();", runtime_host)

    def test_docs_define_external_harness_as_middle_layer(self) -> None:
        adoption_plan = read_doc_file("External_Runtime_Harness_Adoption_Plan.md")
        impl_plan = read_doc_file("Implementation_Plan.md")

        self.assertIn("The harness is the middle validation layer", adoption_plan)
        self.assertIn("Recommended first three scenarios:", adoption_plan)
        self.assertIn("Restart with account-live / strategy-flat mismatch", adoption_plan)
        self.assertIn("Reconnect with compatible broker protective coverage", adoption_plan)
        self.assertIn("Terminate or final sweep with live context preserves the only protective stop", adoption_plan)
        self.assertIn("External runtime harness scenarios once the runtime host contract stabilizes", impl_plan)
