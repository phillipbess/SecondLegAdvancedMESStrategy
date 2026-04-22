# Playback Issue Template

Use this whenever a Playback scenario fails, looks suspicious, or is inconclusive.

## Header

- `scenario_id`:
- `date`:
- `instrument`:
- `timeframe`:
- `session template`:
- `build / commit`:

## Expected

Write the exact expected behavior in one short paragraph.

## Observed

Write the exact observed behavior in one short paragraph.

## Evidence

- `Patterns_` excerpt:
- `Trades_` excerpt:
- `Risk_` excerpt:
- `Debug_` excerpt:

## Likely Layer

Choose one primary layer:

- entry brain
- runtime shell
- transport
- platform / NT8 behavior
- operator setup

## Impact

- blocks Playback continuation: `yes/no`
- safety risk: `low/medium/high`
- parity risk: `low/medium/high`

## Next Action

- fix code
- rerun same scenario
- gather more evidence
- mark inconclusive and continue
