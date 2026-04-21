from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_golden_case

FAMILY_ID = "golden_cases"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_DIR = Path(__file__).parent / "fixtures" / "golden_cases"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class GoldenCasesTests(unittest.TestCase):
    def test_manifest_and_fixture_directory_are_promoted(self) -> None:
        family = _family_definition()
        fixtures = sorted(FIXTURE_DIR.glob("*.json"))

        self.assertEqual(family["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertTrue(fixtures)
        for fixture_path in fixtures:
            fixture = json.loads(fixture_path.read_text(encoding="utf-8"))
            self.assertEqual(fixture["family_id"], FAMILY_ID)
            self.assertEqual(fixture["status"], "implemented")

    def test_golden_case_sequences(self) -> None:
        fixtures = sorted(FIXTURE_DIR.glob("*.json"))

        for fixture_path in fixtures:
            fixture = json.loads(fixture_path.read_text(encoding="utf-8"))
            with self.subTest(case_id=fixture["case_id"]):
                result = evaluate_golden_case(fixture)
                self.assertEqual(result["accepted"], fixture["expected_accepted"])
                self.assertEqual(result["armed"], fixture["expected_armed"])
                self.assertEqual(result["stage"], fixture["expected_stage"])
                self.assertEqual(result["block_reason"], fixture["expected_block_reason"])


if __name__ == "__main__":
    unittest.main()
