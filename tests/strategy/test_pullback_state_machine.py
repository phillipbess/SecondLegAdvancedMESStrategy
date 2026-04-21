from __future__ import annotations

import json
import unittest
from pathlib import Path

from tests.strategy.strategy_logic_helpers import evaluate_pullback_state_machine

FAMILY_ID = "pullback_state_machine"
MANIFEST_PATH = Path(__file__).with_name("strategy_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "pullback_sequences.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class PullbackStateMachineTests(unittest.TestCase):
    def test_manifest_and_fixture_are_promoted(self) -> None:
        family = _family_definition()
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        self.assertEqual(family["status"], "implemented")
        self.assertEqual(fixture["status"], "implemented")
        self.assertEqual(fixture["family_id"], FAMILY_ID)
        self.assertIn(Path(__file__).name, family["planned_artifacts"])

    def test_pullback_sequences(self) -> None:
        fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

        for sequence in fixture["sequences"]:
            with self.subTest(sequence_id=sequence["sequence_id"]):
                result = evaluate_pullback_state_machine(sequence)
                self.assertEqual(result["final_state"], sequence["expected_final_state"])
                self.assertEqual(result["block_reason"], sequence["expected_block_reason"])
                self.assertEqual(result["signal_valid"], sequence["expected_signal_valid"])
                if "expected_state_path" in sequence:
                    self.assertEqual(result["state_path"], sequence["expected_state_path"])


if __name__ == "__main__":
    unittest.main()
