#!/usr/bin/env python3
"""
Trade narrative engine for SecondLegAdvancedMESStrategy.

Reconstructs trade stories from the strategy's text logs:
- Patterns_*.txt
- Trades_*.txt
- Risk_*.txt
- Debug_*.txt

Usage:
    python trade_narrative.py
    python trade_narrative.py --date 20260423
    python trade_narrative.py --trade-id PE2L_1037
    python trade_narrative.py --run-segment LastActive
"""

from __future__ import annotations

import argparse
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple


LOG_DIR = Path(r"C:\Users\bessp\Documents\NinjaTrader 8\logs\SecondLegAdvancedMES")


@dataclass
class SetupContext:
    signal: str
    armed_time: str = ""
    bias: str = ""
    state: str = ""
    signal_bar: str = ""
    entry: float = 0.0
    stop: float = 0.0
    qty: int = 0
    expiry_bar: str = ""
    atr: float = 0.0
    atr_ratio: float = 0.0
    impulse_range: float = 0.0
    retracement: float = 0.0
    leg2_momentum: float = 0.0
    impulse_momentum: float = 0.0
    structure: str = ""
    room: Optional[float] = None
    required: Optional[float] = None
    blocks: List[str] = field(default_factory=list)


@dataclass
class TrailChange:
    time: str
    stop: float
    reason: str = ""


@dataclass
class TradeNarrative:
    trade_id: str
    side: str = ""
    qty: int = 0
    submit_time: str = ""
    entry_time: str = ""
    exit_time: str = ""
    entry_price: float = 0.0
    stop_price: float = 0.0
    exit_price: float = 0.0
    pnl_currency: float = 0.0
    pnl_r: float = 0.0
    exit_role: str = ""
    setup: Optional[SetupContext] = None
    first_stop_sla_ms: Optional[int] = None
    stop_changes: List[TrailChange] = field(default_factory=list)
    risk_notes: List[str] = field(default_factory=list)
    debug_notes: List[str] = field(default_factory=list)

    @property
    def result(self) -> str:
        if self.pnl_currency > 0:
            return "WIN"
        if self.pnl_currency < 0:
            return "LOSS"
        return "FLAT"


LINE_PATTERN = re.compile(r"^(?P<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \| \[(?P<event>[A-Z0-9_ ]+)\] ?(?P<body>.*)$")
SESSION_PATTERN = re.compile(r"_(\d{8})(?:_|\.|$)")
KEY_VALUE_PATTERN = re.compile(r"([A-Za-z][A-Za-z0-9_]*)=([^|]+?)(?=(?:\s+[A-Za-z][A-Za-z0-9_]*=)|(?:\s*\|\s*)|$)")
ACTIVE_MARKERS = (
    "[ENTRY_SUBMIT]",
    "[ENTRY_FILL]",
    "[STOP_ACK]",
    "[STOP_CHANGE]",
    "[TRADE_CLOSE]",
    "[EXIT_FILL]",
)


def extract_session_token(path: Path) -> Optional[str]:
    match = SESSION_PATTERN.search(path.name)
    return match.group(1) if match else None


def find_log_files(date_str: Optional[str] = None) -> Tuple[Dict[str, Path], Optional[str]]:
    log_types = ("Patterns", "Trades", "Risk", "Debug")
    anchor_types = {"Trades", "Risk", "Debug"}
    files: Dict[str, Path] = {}

    if date_str:
        for log_type in log_types:
            matches = list(LOG_DIR.glob(f"{log_type}_*{date_str}*.txt"))
            if matches:
                files[log_type] = max(matches, key=lambda p: p.stat().st_mtime)
        return files, date_str

    grouped: Dict[str, Dict[str, Path]] = {}
    newest_anchor: Optional[Path] = None
    for log_type in log_types:
        for match in LOG_DIR.glob(f"{log_type}_*.txt"):
            token = extract_session_token(match)
            if not token:
                continue
            slot = grouped.setdefault(token, {})
            current = slot.get(log_type)
            if current is None or match.stat().st_mtime > current.stat().st_mtime:
                slot[log_type] = match
            if log_type in anchor_types and (newest_anchor is None or match.stat().st_mtime > newest_anchor.stat().st_mtime):
                newest_anchor = match

    if newest_anchor is None:
        return {}, None

    selected = extract_session_token(newest_anchor)
    if not selected:
        return {}, None
    return grouped.get(selected, {}), selected


def read_lines(path: Path) -> List[str]:
    return path.read_text(encoding="utf-8").splitlines()


def segment_is_active(segment: Iterable[str]) -> bool:
    return any(marker in line for line in segment for marker in ACTIVE_MARKERS)


def slice_run_segment(lines: List[str], run_segment: str) -> List[str]:
    if not lines or run_segment == "AllDay":
        return lines

    run_starts = [i for i, line in enumerate(lines) if "=== NEW RUN ===" in line]
    if not run_starts:
        return lines

    chosen_start = run_starts[-1]
    if run_segment == "LastActive":
        for idx in range(len(run_starts) - 1, -1, -1):
            start = run_starts[idx]
            end = run_starts[idx + 1] if idx < len(run_starts) - 1 else len(lines)
            if segment_is_active(lines[start:end]):
                chosen_start = start
                break

    chosen_end = len(lines)
    for idx in range(len(run_starts) - 1):
        if run_starts[idx] == chosen_start:
            chosen_end = run_starts[idx + 1]
            break
    return lines[chosen_start:chosen_end]


def parse_float(value: Optional[str]) -> float:
    if not value:
        return 0.0
    try:
        return float(value)
    except ValueError:
        return 0.0


def parse_int(value: Optional[str]) -> int:
    if not value:
        return 0
    try:
        return int(float(value))
    except ValueError:
        return 0


def parse_log_line(line: str) -> Optional[Tuple[str, str, Dict[str, str], str]]:
    match = LINE_PATTERN.match(line)
    if not match:
        return None

    body = match.group("body").strip()
    fields = {key: value.strip() for key, value in KEY_VALUE_PATTERN.findall(body)}
    return match.group("timestamp"), match.group("event").strip(), fields, body


def collect_setup_context(pattern_lines: List[str]) -> Dict[str, SetupContext]:
    contexts: Dict[str, SetupContext] = {}
    latest_waiting_blocks: List[str] = []

    for line in pattern_lines:
        parsed = parse_log_line(line)
        if parsed is None:
            continue
        timestamp, event, fields, body = parsed

        if event == "ENTRY_BLOCK":
            latest_waiting_blocks.append(body)
            if len(latest_waiting_blocks) > 4:
                latest_waiting_blocks.pop(0)
            continue

        if event != "ENTRY_ARMED":
            continue

        signal = fields.get("signal", "")
        if not signal:
            continue

        context = SetupContext(
            signal=signal,
            armed_time=timestamp,
            bias=fields.get("bias", ""),
            state=fields.get("state", ""),
            signal_bar=fields.get("signalBar", ""),
            entry=parse_float(fields.get("entry")),
            stop=parse_float(fields.get("stop")),
            qty=parse_int(fields.get("qty")),
            expiry_bar=fields.get("expiry", ""),
            atr=parse_float(fields.get("atr")),
            atr_ratio=parse_float(fields.get("atrRatio")),
            impulse_range=parse_float(fields.get("impulseRange")),
            retracement=parse_float(fields.get("retracement")),
            leg2_momentum=parse_float(fields.get("leg2Momentum")),
            impulse_momentum=parse_float(fields.get("impulseMomentum")),
            structure=fields.get("structure", ""),
            room=parse_float(fields.get("room")) if "room" in fields else None,
            required=parse_float(fields.get("required")) if "required" in fields else None,
            blocks=list(latest_waiting_blocks),
        )
        contexts[signal] = context

    return contexts


def infer_trade_by_timestamp(trades: Dict[str, TradeNarrative], timestamp: str) -> Optional[str]:
    active: List[str] = []
    for trade_id, trade in trades.items():
        start = trade.submit_time or trade.entry_time
        end = trade.exit_time
        if start and start <= timestamp and (not end or timestamp <= end):
            active.append(trade_id)
    if len(active) == 1:
        return active[0]
    return None


def build_trade_narratives(files: Dict[str, Path], run_segment: str = "AllDay") -> List[TradeNarrative]:
    pattern_lines = slice_run_segment(read_lines(files["Patterns"]), run_segment) if "Patterns" in files else []
    trade_lines = slice_run_segment(read_lines(files["Trades"]), run_segment) if "Trades" in files else []
    risk_lines = slice_run_segment(read_lines(files["Risk"]), run_segment) if "Risk" in files else []
    debug_lines = slice_run_segment(read_lines(files["Debug"]), run_segment) if "Debug" in files else []

    setups = collect_setup_context(pattern_lines)
    trades: Dict[str, TradeNarrative] = {}

    for line in trade_lines:
        parsed = parse_log_line(line)
        if parsed is None:
            continue
        timestamp, event, fields, body = parsed
        trade_id = (fields.get("trade") or fields.get("signal") or "").strip()
        if not trade_id or trade_id == "=" or trade_id == "trade=":
            continue

        trade = trades.setdefault(trade_id, TradeNarrative(trade_id=trade_id))
        if trade.setup is None and trade_id in setups:
            trade.setup = setups[trade_id]

        if event == "ENTRY_SUBMIT":
            trade.submit_time = timestamp
            trade.side = fields.get("bias", trade.side)
            trade.qty = parse_int(fields.get("qty")) or trade.qty
            trade.entry_price = parse_float(fields.get("entry")) or trade.entry_price
            trade.stop_price = parse_float(fields.get("stop")) or trade.stop_price
        elif event == "ENTRY_FILL":
            trade.entry_time = timestamp
            trade.qty = parse_int(fields.get("qty")) or trade.qty
            trade.entry_price = parse_float(fields.get("fillPrice")) or trade.entry_price
        elif event == "EXIT_FILL":
            trade.exit_time = timestamp
            trade.exit_price = parse_float(fields.get("fillPrice")) or trade.exit_price
            trade.exit_role = fields.get("role", trade.exit_role)
        elif event == "TRADE_CLOSE":
            trade.exit_time = timestamp
            trade.exit_price = parse_float(fields.get("exitPrice")) or trade.exit_price
            trade.pnl_currency = parse_float(fields.get("pnlCurrency"))
            trade.pnl_r = parse_float(fields.get("pnlR"))
            trade.exit_role = fields.get("role", trade.exit_role)
        elif event in {"FLATTEN_REQUEST", "FLATTEN_SUBMIT", "FLATTEN_COMPLETE"}:
            trade.debug_notes.append(f"{timestamp} {event} {body}")

    for line in risk_lines:
        parsed = parse_log_line(line)
        if parsed is None:
            continue
        timestamp, event, fields, body = parsed
        trade_id = fields.get("trade") or infer_trade_by_timestamp(trades, timestamp)

        if event == "FIRST_STOP_SLA" and trade_id and trade_id in trades:
            match = re.search(r"(\d+)ms", body)
            if match:
                trades[trade_id].first_stop_sla_ms = int(match.group(1))
        elif event == "STOP_CHANGE":
            target_id = trade_id
            if not target_id:
                for existing_id in trades:
                    if existing_id in body:
                        target_id = existing_id
                        break
            if target_id and target_id in trades:
                trades[target_id].stop_changes.append(
                    TrailChange(time=timestamp, stop=parse_float(fields.get("stop")), reason=fields.get("reason", ""))
                )
        elif trade_id and trade_id in trades:
            if event in {"STOP_SUBMIT", "STOP_FILLED_ACK", "OM_HEALTH", "PROTECTIVE_COVERAGE"}:
                trades[trade_id].risk_notes.append(f"{timestamp} {event} {body}")

    for line in debug_lines:
        parsed = parse_log_line(line)
        if parsed is None:
            continue
        timestamp, event, fields, body = parsed
        for trade_id, trade in trades.items():
            if trade_id in body:
                if event in {"ENTRY_BLOCK", "EXIT_OP_BEGIN", "EXIT_OP_END", "RECONNECT_GRACE", "RECONNECT_OBSERVATION"}:
                    trade.debug_notes.append(f"{timestamp} [{event}] {body}")
                break

    return sorted(trades.values(), key=lambda trade: (trade.entry_time or trade.submit_time or trade.trade_id))


def render_trade(trade: TradeNarrative) -> str:
    lines = [
        f"Trade {trade.trade_id} ({trade.side or 'Unknown'})",
        f"  Submit: {trade.submit_time or 'n/a'}",
        f"  Entry:  {trade.entry_time or 'n/a'} @ {trade.entry_price:.2f}" if trade.entry_price else f"  Entry:  {trade.entry_time or 'n/a'}",
        f"  Stop:   {trade.stop_price:.2f}" if trade.stop_price else "  Stop:   n/a",
        f"  Exit:   {trade.exit_time or 'n/a'} @ {trade.exit_price:.2f}" if trade.exit_price else f"  Exit:   {trade.exit_time or 'n/a'}",
        f"  Result: {trade.result} | pnl=${trade.pnl_currency:.2f} | R={trade.pnl_r:.2f}",
    ]

    if trade.setup is not None:
        setup = trade.setup
        lines.extend(
            [
                "  Setup:",
                f"    armed={setup.armed_time} signalBar={setup.signal_bar} expiryBar={setup.expiry_bar}",
                f"    atr={setup.atr:.2f} atrRatio={setup.atr_ratio:.2f} impulseRange={setup.impulse_range:.2f} retracement={setup.retracement:.3f}",
                f"    leg2Momentum={setup.leg2_momentum:.3f} impulseMomentum={setup.impulse_momentum:.3f}",
                f"    structure={setup.structure or 'clear'}",
            ]
        )
        if setup.blocks:
            lines.append("    pre-arm blockers:")
            lines.extend(f"      - {block}" for block in setup.blocks[-2:])

    if trade.first_stop_sla_ms is not None or trade.stop_changes:
        lines.append("  Risk / stop management:")
        if trade.first_stop_sla_ms is not None:
            lines.append(f"    firstStopSla={trade.first_stop_sla_ms}ms")
        if trade.stop_changes:
            for change in trade.stop_changes:
                reason = f" reason={change.reason}" if change.reason else ""
                lines.append(f"    trail {change.time} -> {change.stop:.2f}{reason}")

    if trade.risk_notes:
        lines.append("  Risk notes:")
        lines.extend(f"    - {note}" for note in trade.risk_notes[-4:])

    if trade.debug_notes:
        lines.append("  Debug notes:")
        lines.extend(f"    - {note}" for note in trade.debug_notes[-4:])

    return "\n".join(lines)


def render_session_summary(trades: List[TradeNarrative], session_token: Optional[str]) -> str:
    total = len(trades)
    wins = sum(1 for trade in trades if trade.pnl_currency > 0)
    losses = sum(1 for trade in trades if trade.pnl_currency < 0)
    net_pnl = sum(trade.pnl_currency for trade in trades)
    avg_r = sum(trade.pnl_r for trade in trades) / total if total else 0.0
    return "\n".join(
        [
            f"Session: {session_token or 'unknown'}",
            f"Trades:  {total} | wins={wins} losses={losses}",
            f"Net PnL: ${net_pnl:.2f}",
            f"Avg R:   {avg_r:.2f}",
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Build SecondLegAdvanced trade narratives from strategy logs.")
    parser.add_argument("--date", help="Session token (YYYYMMDD) to inspect.")
    parser.add_argument("--trade-id", help="Specific trade id / signal id to print.")
    parser.add_argument("--run-segment", choices=("AllDay", "Last", "LastActive"), default="AllDay")
    args = parser.parse_args()

    files, selected = find_log_files(args.date)
    if not files:
        print("No matching log files found.")
        return 1

    trades = build_trade_narratives(files, run_segment=args.run_segment)
    if args.trade_id:
        trades = [trade for trade in trades if trade.trade_id == args.trade_id]

    print(render_session_summary(trades, selected))
    if not trades:
        print("\nNo trades found in the selected logs.")
        return 0

    print()
    for trade in trades:
        print(render_trade(trade))
        print()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
