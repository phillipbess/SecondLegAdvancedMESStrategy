"""Doc-level checks for signoff evidence wiring."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

TESTS_ROOT = Path(__file__).resolve().parents[1]
if str(TESTS_ROOT) not in sys.path:
    sys.path.insert(0, str(TESTS_ROOT))

from nt8_contract_helpers import read_doc_file

FAMILY_ID = "signoff_evidence_contract"
MANIFEST_PATH = Path(__file__).with_name("contract_test_manifest.json")


def _family_definition() -> dict:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next(family for family in manifest["families"] if family["id"] == FAMILY_ID)


class SignoffEvidenceContractTests(unittest.TestCase):
    def test_manifest_tracks_signoff_evidence_family(self) -> None:
        family = _family_definition()
        self.assertEqual(family["status"], "implemented")
        self.assertIn(Path(__file__).name, family["planned_artifacts"])
        self.assertIn("evidence", family["goal"])

    def test_checklist_points_to_explicit_evidence_outputs(self) -> None:
        checklist = read_doc_file("Parity_Signoff_Checklist.md")

        for marker in (
            "docs/artifacts/YYYY-MM-DD/test-summary.md",
            "docs/artifacts/YYYY-MM-DD/compile.md",
            "docs/artifacts/YYYY-MM-DD/playback-smoke.md",
            "docs/artifacts/YYYY-MM-DD/runtime-harness.md",
            "docs/artifacts/YYYY-MM-DD/signoff-summary.md",
            "NT8 compile",
            "NT8 Playback evidence",
            "runtime-harness evidence",
            "current unmanaged transport shell",
            "protective stop can change through the active trail path",
            "flatten submit is coherent through the unmanaged transport path",
            "defined",
            "evidence-in-progress",
            "complete",
            "Green repo tests alone are never enough to move a gate to `complete`.",
            "If `docs/artifacts/` contains templates only and no dated evidence folder",
            "external harness bridge/adapter tests may be green",
            "Green external harness bridge/adapter tests do not satisfy this requirement by themselves.",
        ):
            self.assertIn(marker, checklist)

    def test_runbooks_and_artifact_templates_stay_aligned(self) -> None:
        runbooks_index = read_doc_file("runbooks", "README.md")
        playback = read_doc_file("runbooks", "playback.md")
        signoff_runbook = read_doc_file("runbooks", "signoff-evidence.md")
        artifacts_readme = read_doc_file("artifacts", "README.md")

        self.assertIn("signoff-evidence.md", runbooks_index)

        for marker in (
            "docs/artifacts/YYYY-MM-DD/playback-smoke.md",
            "docs/artifacts/TEMPLATE_playback-smoke.md",
        ):
            self.assertIn(marker, playback)

        for marker in (
            "docs/artifacts/YYYY-MM-DD/",
            "test-summary.md",
            "compile.md",
            "playback-smoke.md",
            "runtime-harness.md",
            "signoff-summary.md",
        ):
            self.assertIn(marker, signoff_runbook)
            self.assertIn(marker, artifacts_readme)

        for marker in (
            "strongest claim currently allowed",
            "blocked claim",
            "missing manual evidence files",
            "templates only and no dated folder",
            "Before `entry contract complete` can be claimed",
            "Before `Mancini runtime parity complete` can be claimed",
            "primary entry submit",
            "protective stop submit/change",
            "flatten submit",
            "bridge/adapter tests",
        ):
            self.assertIn(marker, signoff_runbook)

        for marker in (
            "green local tests do not by themselves close entry or runtime signoff",
            "green external harness bridge/adapter tests do not by themselves close runtime parity signoff",
            "templates only and no dated folder",
            "entry submit, protective stop submit/change, and flatten submit were explicitly checked",
        ):
            self.assertIn(marker, artifacts_readme)

        for template_name in (
            "TEMPLATE_test-summary.md",
            "TEMPLATE_compile.md",
            "TEMPLATE_playback-smoke.md",
            "TEMPLATE_runtime-harness.md",
            "TEMPLATE_signoff-summary.md",
        ):
            self.assertIn(template_name, artifacts_readme)
            self.assertTrue(
                Path(TESTS_ROOT).resolve().parents[0].joinpath("docs", "artifacts", template_name).exists()
            )

        template_dir = Path(TESTS_ROOT).resolve().parents[0] / "docs" / "artifacts"
        template_expectations = {
            "TEMPLATE_test-summary.md": (
                "strongest claim supported by this file:",
                "green tests alone do not complete entry or runtime signoff",
                "missing manual evidence after this file is written:",
            ),
            "TEMPLATE_compile.md": (
                "primary entry submit path compiled:",
                "protective stop submit/change path compiled:",
                "flatten submit path compiled:",
                "strongest claim supported by this file:",
                "remaining evidence still required:",
                "blocked claims that remain blocked:",
            ),
            "TEMPLATE_playback-smoke.md": (
                "strongest claim supported by this file:",
                "remaining evidence still required:",
                "blocked claims that remain blocked:",
                "protective stop change observed:",
                "flatten submit coherent through unmanaged transport:",
            ),
            "TEMPLATE_runtime-harness.md": (
                "strongest claim supported by this file:",
                "remaining evidence still required:",
                "blocked claims that remain blocked:",
                "touched entry submit semantics:",
                "touched protective coverage semantics:",
                "touched finalization / flatten semantics:",
            ),
            "TEMPLATE_signoff-summary.md": (
                "strongest allowed claim:",
                "blocked claim:",
                "dated evidence folder present:",
                "templates-only repo state:",
                "local test lane green:",
                "external harness bridge/adapter tests green:",
                "full runtime-harness evidence recorded:",
                "compile note covers entry/protective/flatten transport:",
                "playback note covers entry/protective/flatten transport:",
                "harness note covers touched coverage/finalization lanes:",
                "missing files:",
                "test-summary.md:",
                "compile.md:",
                "playback-smoke.md:",
                "runtime-harness.md:",
                "signoff-summary.md:",
            ),
        }

        for template_name, markers in template_expectations.items():
            template_text = (template_dir / template_name).read_text(encoding="utf-8")
            for marker in markers:
                self.assertIn(marker, template_text)


if __name__ == "__main__":
    unittest.main()
