from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_session_and_regime

FAMILY_ID = "regime_and_session_filters"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "regime_session_cases.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class RegimeAndSessionFilterTests(unittest.TestCase):
    def test_manifest_and_fixture_are_promoted(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_regime_and_session_cases(self) -> None:
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        for case in fixture["cases"]:
            with self.subTest(case_id=case["case_id"]):
                result = evaluate_session_and_regime(case)
                self.assertEqual(result["session_valid"], case["expected_session_valid"])
                self.assertEqual(result["regime_valid"], case["expected_regime_valid"])
                self.assertEqual(result["participate"], case["expected_participate"])
                if "expected_flatten_window_active" in case:
                    self.assertEqual(result["flatten_window_active"], case["expected_flatten_window_active"])
                if "expected_hard_risk_valid" in case:
                    self.assertEqual(result["hard_risk_valid"], case["expected_hard_risk_valid"])
                if "expected_hard_risk_reason" in case:
                    self.assertEqual(result["hard_risk_reason"], case["expected_hard_risk_reason"])


if __name__ == "__main__":
    unittest.main()
