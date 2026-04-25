#!/usr/bin/env python3
"""
Build a visual Brooks-style label pack from a public ES 5-minute proxy.

This is a research/labeling tool, not a backtester. It exists because the
current QuantConnect account can run cloud tests but cannot export ObjectStore
CSV files. The script fetches recent ES=F 5-minute bars, extracts first-hour
auction contexts, and writes an HTML review sheet plus a CSV manifest so we can
manually label what the rules are missing.
"""

from __future__ import annotations

import argparse
import csv
import html
import json
import math
import statistics
import sys
import time
from dataclasses import dataclass
from datetime import datetime, time as dtime, timezone
from pathlib import Path
from typing import Iterable
from zoneinfo import ZoneInfo

import requests


NY = ZoneInfo("America/New_York")
RTH_OPEN = dtime(9, 30)
RTH_CLOSE = dtime(16, 0)
TICK_SIZE = 0.25


@dataclass
class Bar:
    t: datetime
    o: float
    h: float
    l: float
    c: float
    v: float
    atr: float = 0.0


@dataclass
class Setup:
    setup_id: str
    setup_type: str
    side: str
    setup_index: int
    setup_time: datetime
    session_date: str
    session_open: float
    first_hour_high: float
    first_hour_low: float
    first_hour_move_pts: float
    first_hour_move_atr: float
    strong_bull_bars: int
    strong_bear_bars: int
    bars: list[Bar]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--symbol", default="ES=F", help="Yahoo chart symbol to fetch")
    parser.add_argument("--days", type=int, default=59, help="Lookback days. Yahoo 5m is usually limited to about 60 days.")
    parser.add_argument("--max-setups", type=int, default=100)
    parser.add_argument("--measure-minutes", type=int, default=60)
    parser.add_argument("--before-bars", type=int, default=36)
    parser.add_argument("--after-bars", type=int, default=48)
    parser.add_argument("--out-dir", default="research/quantconnect/labeling/brooks_label_review")
    return parser.parse_args()


def fetch_yahoo_bars(symbol: str, days: int) -> list[Bar]:
    period2 = int(time.time())
    period1 = period2 - max(1, min(days, 59)) * 86400
    url = "https://query1.finance.yahoo.com/v8/finance/chart/" + requests.utils.quote(symbol, safe="")
    params = {
        "period1": period1,
        "period2": period2,
        "interval": "5m",
        "includePrePost": "false",
        "events": "history",
    }
    response = requests.get(url, params=params, timeout=30, headers={"User-Agent": "Mozilla/5.0"})
    response.raise_for_status()
    payload = response.json()
    result = payload.get("chart", {}).get("result") or []
    if not result:
        raise RuntimeError("Yahoo returned no chart data: " + json.dumps(payload)[:500])

    data = result[0]
    timestamps = data.get("timestamp") or []
    quote = (data.get("indicators", {}).get("quote") or [{}])[0]
    opens = quote.get("open") or []
    highs = quote.get("high") or []
    lows = quote.get("low") or []
    closes = quote.get("close") or []
    volumes = quote.get("volume") or []

    bars: list[Bar] = []
    for i, ts in enumerate(timestamps):
        values = [opens, highs, lows, closes]
        if any(i >= len(series) or series[i] is None for series in values):
            continue
        local_time = datetime.fromtimestamp(ts, timezone.utc).astimezone(NY)
        if local_time.weekday() >= 5 or not (RTH_OPEN <= local_time.time() <= RTH_CLOSE):
            continue
        bars.append(Bar(
            t=local_time,
            o=float(opens[i]),
            h=float(highs[i]),
            l=float(lows[i]),
            c=float(closes[i]),
            v=float(volumes[i] or 0),
        ))

    assign_atr(bars)
    return bars


def assign_atr(bars: list[Bar], period: int = 14) -> None:
    true_ranges: list[float] = []
    previous_close: float | None = None
    for bar in bars:
        if previous_close is None:
            tr = bar.h - bar.l
        else:
            tr = max(bar.h - bar.l, abs(bar.h - previous_close), abs(bar.l - previous_close))
        true_ranges.append(tr)
        start = max(0, len(true_ranges) - period)
        bar.atr = statistics.fmean(true_ranges[start:])
        previous_close = bar.c


def close_location(bar: Bar) -> float:
    rng = bar.h - bar.l
    if rng <= 0:
        return 0.5
    return max(0.0, min(1.0, (bar.c - bar.l) / rng))


def body_pct(bar: Bar) -> float:
    rng = bar.h - bar.l
    if rng <= 0:
        return 0.0
    return abs(bar.c - bar.o) / rng


def is_strong_bull(bar: Bar) -> bool:
    return bar.c > bar.o and body_pct(bar) >= 0.45 and close_location(bar) >= 0.65


def is_strong_bear(bar: Bar) -> bool:
    return bar.c < bar.o and body_pct(bar) >= 0.45 and close_location(bar) <= 0.35


def session_key(bar: Bar) -> str:
    return bar.t.strftime("%Y-%m-%d")


def minutes_from_open(bar: Bar) -> int:
    open_dt = bar.t.replace(hour=9, minute=30, second=0, microsecond=0)
    return int((bar.t - open_dt).total_seconds() // 60)


def group_sessions(bars: Iterable[Bar]) -> dict[str, list[Bar]]:
    sessions: dict[str, list[Bar]] = {}
    for bar in bars:
        sessions.setdefault(session_key(bar), []).append(bar)
    return sessions


def build_setups(
    bars: list[Bar],
    max_setups: int,
    measure_minutes: int,
    before_bars: int,
    after_bars: int,
) -> list[Setup]:
    setups: list[Setup] = []
    index_by_time = {bar.t: i for i, bar in enumerate(bars)}
    sessions = group_sessions(bars)

    for date_key in sorted(sessions):
        session = sessions[date_key]
        if len(setups) >= max_setups or len(session) < 20:
            break
        measure = next((bar for bar in session if minutes_from_open(bar) >= measure_minutes), None)
        if measure is None or measure.atr <= 0:
            continue

        first_hour = [bar for bar in session if minutes_from_open(bar) <= measure_minutes]
        if not first_hour:
            continue
        start = session[0]
        move_pts = measure.c - start.o
        move_atr = move_pts / max(measure.atr, TICK_SIZE)
        side = "Long" if move_pts >= 0 else "Short"
        setup_index = index_by_time[measure.t]
        window_start = max(0, setup_index - before_bars)
        window_end = min(len(bars), setup_index + after_bars + 1)
        setup_id = f"{date_key.replace('-', '')}_{measure.t.strftime('%H%M')}_{side}_{len(setups) + 1:03d}"
        setups.append(Setup(
            setup_id=setup_id,
            setup_type="FIRST_HOUR_CONTEXT",
            side=side,
            setup_index=setup_index,
            setup_time=measure.t,
            session_date=date_key,
            session_open=start.o,
            first_hour_high=max(bar.h for bar in first_hour),
            first_hour_low=min(bar.l for bar in first_hour),
            first_hour_move_pts=move_pts,
            first_hour_move_atr=move_atr,
            strong_bull_bars=sum(1 for bar in first_hour if is_strong_bull(bar)),
            strong_bear_bars=sum(1 for bar in first_hour if is_strong_bear(bar)),
            bars=bars[window_start:window_end],
        ))

    return setups


def scale(value: float, lo: float, hi: float, height: int, pad: int) -> float:
    if hi <= lo:
        return height / 2
    return pad + (hi - value) / (hi - lo) * (height - 2 * pad)


def render_svg(setup: Setup, width: int = 900, height: int = 360) -> str:
    bars = setup.bars
    if not bars:
        return ""
    lo = min(bar.l for bar in bars)
    hi = max(bar.h for bar in bars)
    pad = 22
    left_pad = 46
    right_pad = 16
    chart_w = width - left_pad - right_pad
    step = chart_w / max(1, len(bars))
    candle_w = max(2, min(7, step * 0.62))
    setup_x = None

    parts = [
        f'<svg class="chart" viewBox="0 0 {width} {height}" role="img" aria-label="{html.escape(setup.setup_id)}">',
        f'<rect x="0" y="0" width="{width}" height="{height}" fill="#fffaf1"/>',
    ]

    for level, css, label in [
        (setup.session_open, "#7c6f64", "open"),
        (setup.first_hour_high, "#2f855a", "1h high"),
        (setup.first_hour_low, "#c2410c", "1h low"),
    ]:
        y = scale(level, lo, hi, height, pad)
        parts.append(f'<line x1="{left_pad}" y1="{y:.1f}" x2="{width - right_pad}" y2="{y:.1f}" stroke="{css}" stroke-width="1" stroke-dasharray="4 4"/>')
        parts.append(f'<text x="4" y="{y + 4:.1f}" class="axis">{html.escape(label)}</text>')

    for i, bar in enumerate(bars):
        x = left_pad + i * step + step / 2
        if bar.t == setup.setup_time:
            setup_x = x
        y_h = scale(bar.h, lo, hi, height, pad)
        y_l = scale(bar.l, lo, hi, height, pad)
        y_o = scale(bar.o, lo, hi, height, pad)
        y_c = scale(bar.c, lo, hi, height, pad)
        color = "#177245" if bar.c >= bar.o else "#b13a21"
        body_y = min(y_o, y_c)
        body_h = max(1.0, abs(y_c - y_o))
        parts.append(f'<line x1="{x:.1f}" y1="{y_h:.1f}" x2="{x:.1f}" y2="{y_l:.1f}" stroke="{color}" stroke-width="1.2"/>')
        parts.append(f'<rect x="{x - candle_w / 2:.1f}" y="{body_y:.1f}" width="{candle_w:.1f}" height="{body_h:.1f}" fill="{color}" opacity="0.86"/>')

    if setup_x is not None:
        parts.append(f'<line x1="{setup_x:.1f}" y1="12" x2="{setup_x:.1f}" y2="{height - 12}" stroke="#1d4ed8" stroke-width="2"/>')
        parts.append(f'<text x="{setup_x + 5:.1f}" y="24" class="setup">measure</text>')

    parts.append(f'<text x="{left_pad}" y="{height - 6}" class="axis">{bars[0].t.strftime("%m-%d %H:%M")}</text>')
    parts.append(f'<text x="{width - 145}" y="{height - 6}" class="axis">{bars[-1].t.strftime("%m-%d %H:%M")}</text>')
    parts.append("</svg>")
    return "\n".join(parts)


def write_manifest(setups: list[Setup], out_dir: Path) -> Path:
    path = out_dir / "brooks_label_sheet.csv"
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "setupId",
            "setupType",
            "side",
            "setupTime",
            "sessionDate",
            "sessionOpen",
            "firstHourHigh",
            "firstHourLow",
            "firstHourMovePts",
            "firstHourMoveAtr",
            "strongBullBars",
            "strongBearBars",
            "brooksQuality",
            "trendContext",
            "pullbackQuality",
            "signalBarQuality",
            "label",
            "notes",
        ])
        for setup in setups:
            writer.writerow([
                setup.setup_id,
                setup.setup_type,
                setup.side,
                setup.setup_time.strftime("%Y-%m-%d %H:%M"),
                setup.session_date,
                f"{setup.session_open:.2f}",
                f"{setup.first_hour_high:.2f}",
                f"{setup.first_hour_low:.2f}",
                f"{setup.first_hour_move_pts:.2f}",
                f"{setup.first_hour_move_atr:.3f}",
                setup.strong_bull_bars,
                setup.strong_bear_bars,
                "",
                "",
                "",
                "",
                "",
                "",
            ])
    return path


def write_html(setups: list[Setup], out_dir: Path, symbol: str, days: int) -> Path:
    path = out_dir / "brooks_label_review.html"
    cards = []
    for setup in setups:
        cards.append(f"""
        <section class="card">
          <div class="meta">
            <h2>{html.escape(setup.setup_id)} <span>{html.escape(setup.side)}</span></h2>
            <p>{setup.setup_time.strftime('%Y-%m-%d %H:%M %Z')} | move {setup.first_hour_move_pts:.2f} pts / {setup.first_hour_move_atr:.2f} ATR | strong bull {setup.strong_bull_bars} | strong bear {setup.strong_bear_bars}</p>
          </div>
          {render_svg(setup)}
          <div class="labels">
            <span>Brooks quality: ___</span>
            <span>Trend context: ___</span>
            <span>Pullback quality: ___</span>
            <span>Trade/no trade: ___</span>
          </div>
        </section>
        """)

    document = f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Brooks Label Review - {html.escape(symbol)}</title>
  <style>
    :root {{
      --ink: #20160f;
      --muted: #766759;
      --paper: #fff7e8;
      --card: #fffdf8;
      --line: #e7d8c3;
      --accent: #1d4ed8;
    }}
    body {{
      margin: 0;
      font-family: Georgia, 'Times New Roman', serif;
      background: radial-gradient(circle at top left, #ffe8bc, var(--paper) 38%, #f7ead7);
      color: var(--ink);
    }}
    header {{
      padding: 28px 34px 12px;
      border-bottom: 1px solid var(--line);
    }}
    h1 {{
      margin: 0 0 8px;
      font-size: 32px;
    }}
    .intro {{
      max-width: 980px;
      color: var(--muted);
      line-height: 1.45;
    }}
    main {{
      display: grid;
      gap: 22px;
      padding: 24px 34px 42px;
    }}
    .card {{
      background: color-mix(in srgb, var(--card), white 22%);
      border: 1px solid var(--line);
      border-radius: 18px;
      box-shadow: 0 14px 36px rgba(85, 61, 38, 0.12);
      padding: 18px;
      overflow-x: auto;
    }}
    .meta h2 {{
      margin: 0;
      font-size: 20px;
    }}
    .meta span {{
      color: var(--accent);
      font-size: 15px;
    }}
    .meta p, .labels {{
      color: var(--muted);
      font-size: 14px;
    }}
    .chart {{
      width: 100%;
      min-width: 760px;
      margin-top: 8px;
      border-radius: 12px;
      border: 1px solid var(--line);
    }}
    .axis {{
      fill: #6f5d4d;
      font-size: 11px;
      font-family: ui-monospace, Consolas, monospace;
    }}
    .setup {{
      fill: #1d4ed8;
      font-size: 12px;
      font-family: ui-monospace, Consolas, monospace;
      font-weight: 700;
    }}
    .labels {{
      display: flex;
      flex-wrap: wrap;
      gap: 14px;
      margin-top: 10px;
      font-family: ui-monospace, Consolas, monospace;
    }}
  </style>
</head>
<body>
  <header>
    <h1>Brooks Label Review - {html.escape(symbol)}</h1>
    <p class="intro">Generated from Yahoo Finance {html.escape(symbol)} 5-minute bars over the last {days} days. This is a qualitative research pack: mark the best examples, the traps, and the charts you would never trade. Then we can convert the repeated visual traits into deterministic tests.</p>
  </header>
  <main>
    {''.join(cards)}
  </main>
</body>
</html>
"""
    path.write_text(document, encoding="utf-8")
    return path


def write_summary(setups: list[Setup], out_dir: Path, symbol: str, days: int) -> Path:
    path = out_dir / "README.md"
    if setups:
        avg_abs_move_atr = statistics.fmean(abs(s.first_hour_move_atr) for s in setups)
        long_count = sum(1 for s in setups if s.side == "Long")
    else:
        avg_abs_move_atr = 0.0
        long_count = 0
    text = f"""# Brooks Label Review Pack

Generated from `{symbol}` Yahoo 5-minute bars over the last `{days}` days.

This pack is for manual idea extraction, not final performance validation. The goal is to identify which first-hour auction contexts actually look like Brooks-quality trend-from-open or reversal/trap opportunities before we keep coding rules.

## Contents

- `brooks_label_review.html`: visual chart review sheet.
- `brooks_label_sheet.csv`: manifest with blank scoring columns.

## Sample

- Setups: `{len(setups)}`
- Long contexts: `{long_count}`
- Short contexts: `{len(setups) - long_count}`
- Average absolute first-hour move: `{avg_abs_move_atr:.2f} ATR`

## Labeling Rubric

- `brooksQuality`: 0 means no trade, 1 means maybe, 2 means clean, 3 means textbook.
- `trendContext`: describe strong trend, trading range, failed breakout, reversal, or unclear.
- `pullbackQuality`: shallow, deep, two-legged, overlapping, climactic, or none.
- `signalBarQuality`: strong, weak, trap-like, bad location, or no signal.
- `label`: trade, skip, fade, or study.
"""
    path.write_text(text, encoding="utf-8")
    return path


def main() -> int:
    args = parse_args()
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    bars = fetch_yahoo_bars(args.symbol, args.days)
    if not bars:
        raise RuntimeError("No RTH bars were fetched")

    setups = build_setups(
        bars,
        max_setups=max(1, args.max_setups),
        measure_minutes=max(5, args.measure_minutes),
        before_bars=max(5, args.before_bars),
        after_bars=max(5, args.after_bars),
    )
    manifest = write_manifest(setups, out_dir)
    html_path = write_html(setups, out_dir, args.symbol, args.days)
    summary = write_summary(setups, out_dir, args.symbol, args.days)

    print(f"Fetched bars: {len(bars)}")
    print(f"Setups: {len(setups)}")
    print(f"HTML: {html_path}")
    print(f"Manifest: {manifest}")
    print(f"Summary: {summary}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
