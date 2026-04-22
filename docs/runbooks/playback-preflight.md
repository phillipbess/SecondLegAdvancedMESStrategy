# Playback Preflight

Use this before the first serious NT8 Playback pass.

The goal is simple: remove preventable setup mistakes so any defect we see is more
likely to be a real strategy issue instead of operator noise.

## Preflight Checklist

### Repo And Build

- latest intended commit is checked out
- local source tests are green
- strategy names do not collide with sibling NT8 strategies
- `SecondLegAdvancedMESStrategy` compiles in NinjaTrader
- no unexpected compile warnings appeared during import/compile

### Strategy Scope

- strategy under test is `SecondLegAdvancedMESStrategy`
- expected model is stripped `v1`
- no HTF / RVOL / VWAP / opening-bias filters are expected
- donor-style simple trail is expected
- no fixed target / breakeven / partials are expected

### Chart Setup

- instrument is explicitly recorded
- timeframe is explicitly recorded
- session template is explicitly recorded
- data range is explicitly recorded
- Playback speed is explicitly recorded
- chart template and indicators are stable before the run begins

### Parameter Snapshot

Record whether defaults are used for:

- trend filters
- ATR regime
- impulse thresholds
- pullback thresholds
- trigger expiry
- room-to-structure gate
- session window
- hard risk rails

If anything differs from the locked defaults, write it down before the run.

### Logs And Evidence

- `Patterns_` log location confirmed
- `Trades_` log location confirmed
- `Risk_` log location confirmed
- `Debug_` log location confirmed
- dated artifact folder created under `docs/artifacts/YYYY-MM-DD/`
- playback note file created from:
  - [TEMPLATE_playback-smoke.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/artifacts/TEMPLATE_playback-smoke.md)

### What Must Be Observed

Before calling the session useful, expect to capture at least:

- one coherent long setup lifecycle
- one coherent short setup lifecycle
- one rejected or expired setup
- one example of protective stop presence after fill
- one example of simple trail behavior
- one example of clean reset after flat

## Hard Stop Conditions

Stop the session and fix code/setup before continuing if you see:

- duplicate entry for one armed setup
- no protective stop after fill
- stale working entry that does not cancel
- broken reset after flat
- flatten-before-close leaves active/armed residue
- logs are missing or unreadable

## Output

When preflight is complete, the operator should be able to answer:

1. What exact build is being tested?
2. What exact NT8 chart/session setup is being used?
3. Where will the evidence and logs be stored?
4. What behaviors must be observed before the session counts as useful?
