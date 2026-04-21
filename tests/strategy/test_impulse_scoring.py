from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_impulse_qualification

FAMILY_ID = "impulse_scoring"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "impulse_cases.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class ImpulseQualificationTests(unittest.TestCase):
    def test_manifest_and_fixture_are_promoted(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["status"], "implemented")
        self.assertEqual(fixture["family_id"], FAMILY_ID)
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_impulse_cases(self) -> None:
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        for case in fixture["cases"]:
            with self.subTest(case_id=case["case_id"]):
                result = evaluate_impulse_qualification(case)
                self.assertEqual(result["qualified"], case["expected_qualified"])
                self.assertEqual(result["block_reason"], case["expected_block_reason"])
                self.assertAlmostEqual(result["impulse_move"], case["expected_impulse_move"], places=8)
                self.assertEqual(result["strong_bars"], case["expected_strong_bars"])


if __name__ == "__main__":
    unittest.main()
