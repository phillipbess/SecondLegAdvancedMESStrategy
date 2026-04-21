from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_entry_qualification

FAMILY_ID = "entry_qualification"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "entry_qualification_cases.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class EntryQualificationTests(unittest.TestCase):
    def test_manifest_and_fixture_are_promoted(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_entry_qualification_cases(self) -> None:
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        for case in fixture["cases"]:
            with self.subTest(case_id=case["case_id"]):
                result = evaluate_entry_qualification(case)
                self.assertEqual(result["accepted"], case["expected_accepted"])
                self.assertEqual(result["block_reason"], case["expected_block_reason"])
                if case["expected_accepted"]:
                    self.assertAlmostEqual(result["entry_price"], case["expected_entry_price"], places=8)
                    self.assertAlmostEqual(result["stop_price"], case["expected_stop_price"], places=8)
                    self.assertEqual(result["quantity"], case["expected_quantity"])


if __name__ == "__main__":
    unittest.main()
