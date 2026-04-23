"""Source-level checks for the logging contract family."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_runtime_file, read_strategy_file

FAMILY_ID = "logging_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")
FIXTURE_PATH = Path(__file__).parent / "fixtures" / "log_contracts.json"


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


def _fixture() -> dict:
    return json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))


def _normalize_whitespace(text: str) -> str:
    return " ".join(text.split())


class LoggingContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.fixture = _fixture()
        cls.logging_file = read_strategy_file("SecondLegAdvancedMESStrategy.Logging.cs")
        cls.runtime_host = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeHost.cs")
        cls.runtime_scenario_state = read_strategy_file("SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs")
        cls.runtime_core = read_runtime_file("SecondLegAdvancedRuntimeControlLane.cs")
        cls.entry_analysis = read_strategy_file("SecondLegAdvancedMESStrategy.EntryAnalysis.cs")
        cls.state_lifecycle = read_strategy_file("SecondLegAdvancedMESStrategy.StateLifecycle.cs")
        cls.orders = read_strategy_file("SecondLegAdvancedMESStrategy.Orders.cs")

    def test_manifest_and_fixture_exist(self) -> None:
        family = _family_definition()
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertEqual(family["status"], "implemented")
        self.assertEqual(self.fixture["family_id"], FAMILY_ID)
        self.assertEqual(self.fixture["status"], "implemented")

    def test_logging_partial_freezes_headers_and_line_grammar(self) -> None:
        self.assertIn(self.fixture["base_path_marker"], self.logging_file)

        grammar = self.fixture["line_grammar"]
        self.assertIn(grammar["source_marker"], self.logging_file)
        self.assertIn(grammar["timestamp_format_marker"], self.logging_file)

        for marker in self.fixture["helper_markers"]:
            self.assertIn(marker, self.logging_file)

        for marker in self.fixture["header_schema_markers"]:
            self.assertIn(marker, self.logging_file)

    def test_sink_contracts_freeze_filename_patterns_and_writers(self) -> None:
        writer_surfaces = {
            "logging": self.logging_file,
            "runtime_host": self.runtime_host,
        }

        for sink_name, sink_contract in self.fixture["sinks"].items():
            with self.subTest(sink=sink_name):
                self.assertIn(sink_contract["schema_marker"], self.logging_file)
                self.assertIn(sink_contract["filename_marker"], self.logging_file)
                self.assertIn(sink_contract["log_type_marker"], self.logging_file)
                writer_surface = writer_surfaces[sink_contract.get("writer_source", "logging")]
                self.assertIn(sink_contract["writer_marker"], writer_surface)

    def test_pattern_context_schema_is_pinned(self) -> None:
        self.assertIn('WritePatternLog($"[{eventName}] {context}");', self.logging_file)
        self.assertIn('pdhPdl=unavailable(no_prior_rth)', self.logging_file)
        for marker in self.fixture["entry_context_schema_markers"]:
            with self.subTest(marker=marker):
                self.assertIn(f'"{marker}', self.logging_file)

        context_start = self.logging_file.index('return string.Join(" | ", new[]')
        context_end = self.logging_file.index('}) + detailPart;', context_start)
        context_slice = self.logging_file[context_start:context_end]
        ordered_markers = self.fixture["entry_context_ordered_markers"]
        last_index = -1
        for marker in ordered_markers:
            with self.subTest(ordered_marker=marker):
                marker_index = context_slice.find(marker)
                self.assertGreater(marker_index, last_index)
                last_index = marker_index

    def test_pattern_event_vocabulary_and_reasons_are_frozen(self) -> None:
        pattern_contract = self.fixture["sinks"]["pattern"]
        pattern_surface = "\n".join(
            (
                self.logging_file,
                self.entry_analysis,
                self.state_lifecycle,
                self.orders,
            )
        )

        for event_marker in pattern_contract["event_markers"]:
            with self.subTest(event_marker=event_marker):
                self.assertIn(event_marker, pattern_surface)

        for reason in pattern_contract["state_reason_markers"]:
            with self.subTest(state_reason=reason):
                self.assertTrue(
                    reason in self.entry_analysis
                    or reason in self.state_lifecycle
                    or reason in self.orders,
                    msg=f"Missing state reason marker: {reason}",
                )

        for reason in pattern_contract["block_reason_markers"]:
            with self.subTest(block_reason=reason):
                self.assertTrue(
                    f'"{reason}"' in self.entry_analysis or f'"{reason}"' in self.orders,
                    msg=f"Missing block reason marker: {reason}",
                )

    def test_trade_log_contract_freezes_lifecycle_schema_and_vocabulary(self) -> None:
        trade_contract = self.fixture["sinks"]["trade"]
        trade_surface = "\n".join((self.logging_file, self.runtime_host, self.orders))
        normalized_trade_surface = _normalize_whitespace(trade_surface)

        for event_marker in trade_contract["event_markers"]:
            with self.subTest(event_marker=event_marker):
                self.assertIn(event_marker, trade_surface)

        for field_marker in trade_contract["required_field_markers"]:
            with self.subTest(field_marker=field_marker):
                self.assertIn(field_marker, self.orders)

        for callsite_marker in trade_contract["callsite_markers"]:
            with self.subTest(callsite_marker=callsite_marker):
                self.assertIn(_normalize_whitespace(callsite_marker), normalized_trade_surface)

        for exact_field_marker in trade_contract["exact_field_order_markers"]:
            with self.subTest(exact_field_marker=exact_field_marker):
                self.assertIn(_normalize_whitespace(exact_field_marker), normalized_trade_surface)

    def test_runtime_host_debug_and_risk_sinks_are_real_and_not_placeholders(self) -> None:
        self.assertIn('WriteToLogFile(_debugLogPath, "DEBUG", message);', self.runtime_host)
        self.assertIn('WriteRiskEvent("SUBMISSION_AUTHORITY", $"detail={message.Trim()}");', self.runtime_host)
        self.assertNotIn("// Placeholder logger for M1 host-shell wiring.", self.runtime_host)

    def test_risk_log_vocabulary_is_pinned(self) -> None:
        risk_contract = self.fixture["sinks"]["risk"]
        risk_surface = "\n".join(
            (
                self.logging_file,
                self.runtime_host,
                self.runtime_scenario_state,
                self.runtime_core,
                self.orders,
            )
        )
        normalized_risk_surface = _normalize_whitespace(risk_surface)

        for event_marker in risk_contract["event_markers"]:
            with self.subTest(event_marker=event_marker):
                self.assertIn(event_marker, self.logging_file)

        for route_marker in risk_contract["vocabulary_route_markers"]:
            with self.subTest(route_marker=route_marker):
                self.assertIn(route_marker, self.logging_file)

        for callsite_marker in risk_contract["callsite_markers"]:
            with self.subTest(callsite_marker=callsite_marker):
                self.assertIn(_normalize_whitespace(callsite_marker), normalized_risk_surface)

    def test_debug_log_vocabulary_is_pinned_to_current_runtime_surface(self) -> None:
        debug_contract = self.fixture["sinks"]["debug"]
        runtime_surface = "\n".join((self.logging_file, self.runtime_host, self.orders))
        normalized_runtime_surface = _normalize_whitespace(runtime_surface)

        for event_marker in debug_contract["event_markers"]:
            with self.subTest(event_marker=event_marker):
                self.assertIn(event_marker, runtime_surface)

        for callsite_marker in debug_contract["callsite_markers"]:
            with self.subTest(callsite_marker=callsite_marker):
                self.assertIn(_normalize_whitespace(callsite_marker), normalized_runtime_surface)


if __name__ == "__main__":
    unittest.main()
