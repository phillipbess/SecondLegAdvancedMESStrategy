# Video Lite Change Review - 2026-04-24

## Compliance Gate

Default phrase:

`elegant, nt8 best practice aligned, event driven, no downstream side effects (verified with code traced end to end)`

This change is intentionally entry-only.

## What Changed

- Added `EntryMode` with `StrictV1` and `VideoSecondEntryLite`.
- Added `TradeDirection` with `Both`, `ShortOnly`, and `LongOnly`.
- Added lite-mode controls for impulse ATR, pullback depth, and structure veto behavior.
- Fixed separation tracking so the full separation phase defines `separationHigh` /
  `separationLow` before leg 2 begins.
- Added deterministic tests for strict/lite mode differences.
- Added documentation for the QuantConnect frequency probe and lite-mode intent.

## Forbidden-Area Check

Not touched:

- `OnExecutionUpdate`
- order-management authority
- transport adapter
- exit / trail flow
- protective order flow
- emergency timing windows
- strategy timing constants
- log filenames
- CSV schemas

The change adds detail tokens to existing entry observations but does not change log file
names or parser-facing CSV shape.

## End-To-End Code Trace

Entry path:

1. Closed-bar pass refreshes entry context.
2. `EntryMode` selects strict or lite gate behavior.
3. Entry brain advances through trend, impulse, pullback, separation, leg 2, signal, and trigger.
4. The strategy still builds the same `PlannedEntry` shape.
5. Existing order submission and runtime management handle the plan.
6. Existing protection, flatten, trail, persistence, and recovery code remain authoritative.

No downstream order-management side effects were introduced.

## Validation

Focused tests run:

- `python -m pytest -q tests/contracts/test_edge_filters_contract.py`
- `python -m pytest -q tests/strategy/test_trend_context.py tests/strategy/test_regime_and_session_filters.py tests/strategy/test_impulse_scoring.py tests/strategy/test_pullback_state_machine.py tests/strategy/test_entry_qualification.py`
- `git diff --check -- src tests`

Full-suite tests run:

- `python -m pytest -q`
- `python -m unittest discover -s tests -p "test_*.py"`
- `git diff --check`

Result: source/test validation is clean. `git diff --check` only emitted normal
LF-to-CRLF warnings.

NT8 compile and Playback evidence are still required before promoting lite mode beyond
research status.
