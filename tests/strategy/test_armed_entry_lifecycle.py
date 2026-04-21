from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_armed_entry_lifecycle

FAMILY_ID = "armed_entry_lifecycle"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "armed_entry_lifecycle_cases.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class ArmedEntryLifecycleTests(unittest.TestCase):
    def test_manifest_and_fixture_are_promoted(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["status"], "implemented")
        self.assertEqual(fixture["family_id"], FAMILY_ID)
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_armed_entry_lifecycle_cases(self) -> None:
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
        case_ids = [case["case_id"] for case in fixture["cases"]]

        self.assertEqual(len(case_ids), len(set(case_ids)))

        for case in fixture["cases"]:
            with self.subTest(case_id=case["case_id"]):
                result = evaluate_armed_entry_lifecycle(case)
                self.assertEqual(result["final_state"], case["expected_final_state"])
                self.assertEqual(result["block_reason"], case["expected_block_reason"])
                self.assertEqual(result["trail_armed"], case["expected_trail_armed"])
                self.assertAlmostEqual(result["final_stop"], case["expected_final_stop"], places=8)
                self.assertEqual(result["target_used"], case["expected_target_used"])


if __name__ == "__main__":
    unittest.main()
