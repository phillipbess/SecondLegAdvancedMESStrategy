#!/usr/bin/env python3
"""
Analyze the Brooks label review sheet for simple measurable traits.

This is intentionally lightweight: it compares contexts that hit 1R before the
opposite first-hour stop against contexts that stopped/failed, then ranks simple
one- and two-feature filters. The goal is not to declare an edge from 41 recent
samples; it is to decide what deserves the next QuantConnect test.
"""

from __future__ import annotations

import argparse
import csv
import math
import statistics
from dataclasses import dataclass
from pathlib import Path
from typing import Callable


@dataclass
class Row:
    setup_id: str
    side: str
    first_hour_move_atr: float
    strong_bull_bars: int
    strong_bear_bars: int
    entry_price: float
    first_hour_high: float
    first_hour_low: float
    risk_pts: float
    continuation_mfe_r: float
    continuation_mae_r: float
    continuation_eod_pts: float
    hit_1r: bool
    grade: str

    @property
    def abs_move_atr(self) -> float:
        return abs(self.first_hour_move_atr)

    @property
    def first_hour_range_pts(self) -> float:
        return max(0.0, self.first_hour_high - self.first_hour_low)

    @property
    def first_hour_close_location(self) -> float:
        rng = max(self.first_hour_range_pts, 0.25)
        if self.side == "Long":
            return (self.entry_price - self.first_hour_low) / rng
        return (self.first_hour_high - self.entry_price) / rng

    @property
    def strong_with_bars(self) -> int:
        return self.strong_bull_bars if self.side == "Long" else self.strong_bear_bars

    @property
    def strong_against_bars(self) -> int:
        return self.strong_bear_bars if self.side == "Long" else self.strong_bull_bars

    @property
    def strong_dominance(self) -> int:
        return self.strong_with_bars - self.strong_against_bars

    @property
    def mixed_auction(self) -> bool:
        return self.strong_with_bars > 0 and self.strong_against_bars > 0

    @property
    def clean_strength(self) -> bool:
        return self.strong_with_bars >= 3 and self.strong_against_bars <= 1


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", default="research/quantconnect/labeling/brooks_label_review/brooks_label_sheet.csv")
    parser.add_argument("--output", default="research/quantconnect/results/Brooks_Label_Trait_Analysis_2026-04-25.md")
    parser.add_argument("--min-trades", type=int, default=8)
    return parser.parse_args()


def as_float(value: str) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def as_int(value: str) -> int:
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return 0


def load_rows(path: Path) -> list[Row]:
    rows: list[Row] = []
    with path.open("r", newline="", encoding="utf-8") as handle:
        for raw in csv.DictReader(handle):
            rows.append(Row(
                setup_id=raw["setupId"],
                side=raw["side"],
                first_hour_move_atr=as_float(raw["firstHourMoveAtr"]),
                strong_bull_bars=as_int(raw["strongBullBars"]),
                strong_bear_bars=as_int(raw["strongBearBars"]),
                entry_price=as_float(raw["entryPrice"]),
                first_hour_high=as_float(raw["firstHourHigh"]),
                first_hour_low=as_float(raw["firstHourLow"]),
                risk_pts=as_float(raw["riskPts"]),
                continuation_mfe_r=as_float(raw["continuationMfeR"]),
                continuation_mae_r=as_float(raw["continuationMaeR"]),
                continuation_eod_pts=as_float(raw["continuationEodPts"]),
                hit_1r=str(raw["hit1RBeforeStop"]).lower() == "true",
                grade=raw["continuationGrade"],
            ))
    return rows


def avg(values: list[float]) -> float:
    return statistics.fmean(values) if values else 0.0


def summarize_group(rows: list[Row]) -> dict[str, float]:
    if not rows:
        return {
            "n": 0,
            "hit_rate": 0.0,
            "avg_mfe_r": 0.0,
            "avg_mae_r": 0.0,
            "avg_eod_pts": 0.0,
            "avg_abs_move_atr": 0.0,
            "avg_close_location": 0.0,
            "avg_strong_dominance": 0.0,
        }
    return {
        "n": len(rows),
        "hit_rate": sum(1 for row in rows if row.hit_1r) / len(rows),
        "avg_mfe_r": avg([row.continuation_mfe_r for row in rows]),
        "avg_mae_r": avg([row.continuation_mae_r for row in rows]),
        "avg_eod_pts": avg([row.continuation_eod_pts for row in rows]),
        "avg_abs_move_atr": avg([row.abs_move_atr for row in rows]),
        "avg_close_location": avg([row.first_hour_close_location for row in rows]),
        "avg_strong_dominance": avg([row.strong_dominance for row in rows]),
    }


def filter_stats(name: str, rows: list[Row], predicate: Callable[[Row], bool], min_trades: int) -> dict[str, float | str] | None:
    selected = [row for row in rows if predicate(row)]
    if len(selected) < min_trades:
        return None
    stats = summarize_group(selected)
    stats["name"] = name
    return stats


def rank_filters(rows: list[Row], min_trades: int) -> list[dict[str, float | str]]:
    candidates: list[tuple[str, Callable[[Row], bool]]] = []
    for threshold in [0.25, 0.5, 0.75, 1.0, 1.5, 2.0]:
        candidates.append((f"abs first-hour move >= {threshold:.2f} ATR", lambda row, t=threshold: row.abs_move_atr >= t))
        candidates.append((f"abs first-hour move <= {threshold:.2f} ATR", lambda row, t=threshold: row.abs_move_atr <= t))
    for threshold in [0.55, 0.60, 0.65, 0.70, 0.75, 0.80]:
        candidates.append((f"close location >= {threshold:.2f}", lambda row, t=threshold: row.first_hour_close_location >= t))
    for threshold in [1, 2, 3, 4, 5]:
        candidates.append((f"strong with-bars >= {threshold}", lambda row, t=threshold: row.strong_with_bars >= t))
        candidates.append((f"strong dominance >= {threshold}", lambda row, t=threshold: row.strong_dominance >= t))
        candidates.append((f"strong against-bars <= {threshold}", lambda row, t=threshold: row.strong_against_bars <= t))
    for threshold in [20, 30, 40, 50, 60, 70]:
        candidates.append((f"risk <= {threshold} pts", lambda row, t=threshold: row.risk_pts <= t))
    candidates.extend([
        ("clean strength: with >= 3 and against <= 1", lambda row: row.clean_strength),
        ("not mixed auction", lambda row: not row.mixed_auction),
        ("mixed auction", lambda row: row.mixed_auction),
        ("long only", lambda row: row.side == "Long"),
        ("short only", lambda row: row.side == "Short"),
    ])

    ranked: list[dict[str, float | str]] = []
    baseline = summarize_group(rows)["hit_rate"]
    for name, predicate in candidates:
        stats = filter_stats(name, rows, predicate, min_trades)
        if stats is None:
            continue
        stats["lift"] = float(stats["hit_rate"]) - baseline
        ranked.append(stats)

    ranked.sort(key=lambda item: (float(item["hit_rate"]), float(item["avg_mfe_r"]), int(item["n"])), reverse=True)
    return ranked


def format_pct(value: float) -> str:
    return f"{value * 100:.1f}%"


def render_table(rows: list[dict[str, float | str]], limit: int = 12) -> str:
    lines = [
        "| Filter | N | Hit 1R | Lift | Avg MFE R | Avg MAE R | Avg EOD Pts |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for item in rows[:limit]:
        lines.append(
            f"| {item['name']} | {int(item['n'])} | {format_pct(float(item['hit_rate']))} | "
            f"{format_pct(float(item['lift']))} | {float(item['avg_mfe_r']):.2f} | "
            f"{float(item['avg_mae_r']):.2f} | {float(item['avg_eod_pts']):.2f} |"
        )
    return "\n".join(lines)


def render_report(rows: list[Row], ranked: list[dict[str, float | str]]) -> str:
    baseline = summarize_group(rows)
    hits = [row for row in rows if row.hit_1r]
    misses = [row for row in rows if not row.hit_1r]
    hit_stats = summarize_group(hits)
    miss_stats = summarize_group(misses)
    best = ranked[0] if ranked else None

    if best:
        recommendation = (
            f"The best simple split is `{best['name']}`, with `{int(best['n'])}` contexts and "
            f"`{format_pct(float(best['hit_rate']))}` hit-1R. Treat this as a hypothesis only; "
            "the sample is small and public Yahoo data is a proxy."
        )
    else:
        recommendation = "No simple filter met the minimum sample threshold."

    return f"""# Brooks Label Trait Analysis - 2026-04-25

Input: `research/quantconnect/labeling/brooks_label_review/brooks_label_sheet.csv`

This compares first-hour contexts that hit `1R` before the opposite first-hour stop against the rest.

## Baseline

- Contexts: `{int(baseline['n'])}`
- Hit 1R: `{format_pct(float(baseline['hit_rate']))}`
- Avg MFE: `{float(baseline['avg_mfe_r']):.2f}R`
- Avg MAE: `{float(baseline['avg_mae_r']):.2f}R`
- Avg EOD continuation: `{float(baseline['avg_eod_pts']):.2f}` ES points

## Winner vs Miss Traits

| Group | N | Avg Abs Move ATR | Avg Close Location | Avg Strong Dominance | Avg MFE R | Avg MAE R | Avg EOD Pts |
|---|---:|---:|---:|---:|---:|---:|---:|
| Hit 1R | {int(hit_stats['n'])} | {float(hit_stats['avg_abs_move_atr']):.2f} | {float(hit_stats['avg_close_location']):.2f} | {float(hit_stats['avg_strong_dominance']):.2f} | {float(hit_stats['avg_mfe_r']):.2f} | {float(hit_stats['avg_mae_r']):.2f} | {float(hit_stats['avg_eod_pts']):.2f} |
| Miss | {int(miss_stats['n'])} | {float(miss_stats['avg_abs_move_atr']):.2f} | {float(miss_stats['avg_close_location']):.2f} | {float(miss_stats['avg_strong_dominance']):.2f} | {float(miss_stats['avg_mfe_r']):.2f} | {float(miss_stats['avg_mae_r']):.2f} | {float(miss_stats['avg_eod_pts']):.2f} |

## Best Simple Filters

{render_table(ranked)}

## Read

{recommendation}

The current evidence still argues against a blind 10:30 continuation entry. The better hypothesis is narrower: first-hour context may matter only when the close is near the favorable extreme and the opposing strong bars are limited. The next coded test should wait for a post-measurement pullback/trigger, not enter immediately at 10:30.
"""


def main() -> int:
    args = parse_args()
    rows = load_rows(Path(args.input))
    ranked = rank_filters(rows, min_trades=max(1, args.min_trades))
    report = render_report(rows, ranked)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(report, encoding="utf-8")
    print(report)
    print(f"Wrote {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
