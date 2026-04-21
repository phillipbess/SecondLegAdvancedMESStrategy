from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_trend_context

FAMILY_ID = "trend_context"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "trend_context_cases.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class TrendContextTests(unittest.TestCase):
    def test_manifest_and_fixture_are_promoted(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_trend_context_cases(self) -> None:
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        for case in fixture["cases"]:
            with self.subTest(case_id=case["case_id"]):
                result = evaluate_trend_context(case)
                self.assertEqual(result["active_bias"], case["expected_active_bias"])
                self.assertEqual(result["trend_context_valid"], case["expected_trend_context_valid"])


if __name__ == "__main__":
    unittest.main()
