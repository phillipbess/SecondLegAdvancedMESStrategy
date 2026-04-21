from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = REPO_ROOT / "src"
STRATEGY_ROOT = SRC_ROOT / "strategy"
RUNTIME_ROOT = SRC_ROOT / "runtime-core"
DOCS_ROOT = REPO_ROOT / "docs"


def read_strategy_file(name: str) -> str:
    return (STRATEGY_ROOT / name).read_text(encoding="utf-8")


def read_runtime_file(name: str) -> str:
    return (RUNTIME_ROOT / name).read_text(encoding="utf-8")


def read_doc_file(*parts: str) -> str:
    return DOCS_ROOT.joinpath(*parts).read_text(encoding="utf-8")


def iter_source_files() -> list[Path]:
    return sorted(SRC_ROOT.rglob("*.cs"))
