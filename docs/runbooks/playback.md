# Playback Runbook

## Purpose

Run the first disciplined NT8 Playback pass against the current stripped `v1` strategy and collect enough evidence to decide whether the implementation is coherent before any further feature growth.

This runbook assumes:

- single primary chart
- no second series
- donor-style simple trail
- stripped `v1` entry contract from `docs/Entry_Brain_V1_Contract.md`

## Preconditions

Before starting Playback:

1. current repo tests are green
2. strategy compiles in NinjaTrader
3. the chart/session template matches the intended `v1` session rules
4. user-configured parameters match the locked `v1` defaults unless intentionally testing a specific threshold
5. a dated artifact folder exists under `docs/artifacts/YYYY-MM-DD/`

Use these operator companions before and during the run:

- [playback-preflight.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/runbooks/playback-preflight.md)
- [playback-scenario-matrix.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/runbooks/playback-scenario-matrix.md)
- [log-review-guide.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/runbooks/log-review-guide.md)
- [golden-log-sequences.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/runbooks/golden-log-sequences.md)

## Suggested First Playback Scope

Use a small but honest first pass:

- instrument:
  - `MES`
- session style:
  - `RTH`
- focus windows:
  - one morning session with clear trend behavior
  - one afternoon session
  - at least one day with churn or failed setups

The goal of the first pass is not broad statistics. It is to verify lifecycle truth.

## What To Verify First

### Long lifecycle

Confirm one complete long sequence:

1. trend context becomes valid
2. impulse is recognized
3. two-leg pullback is recognized
4. signal bar appears
5. stop-entry rests above the signal bar
6. entry fills once
7. protective stop exists
8. simple trail can arm and tighten
9. trade exits cleanly
10. setup resets cleanly

### Short lifecycle

Confirm the same sequence for a short setup.

### Cancellation paths

Confirm at least one example of:

- stale trigger expiry
- setup invalidation before fill
- flatten-before-close cancellation/flatten behavior

## Evidence To Capture

For each first-pass Playback session, capture:

- date
- instrument
- timeframe
- session template
- parameter snapshot if changed
- whether long lifecycle was observed
- whether short lifecycle was observed
- whether stale trigger cancellation was observed
- whether flatten-before-close was observed
- any missing-stop, duplicate-entry, or reset anomalies

Record the result in:

- `docs/artifacts/YYYY-MM-DD/playback-smoke.md`

Use `docs/artifacts/TEMPLATE_playback-smoke.md` as the starting point.

## First-Pass Pass Criteria

Playback passes the first smoke gate only if all of the following are true:

- the strategy compiles
- at least one long lifecycle is coherent end to end
- at least one short lifecycle is coherent end to end
- no duplicate entry appears for one armed setup
- a protective stop is present after fill
- simple trail behavior is monotonic
- stale entries cancel
- flatten-before-close behavior is coherent

## First-Pass Failure Conditions

Stop and fix code before expanding Playback if you see:

- missing protective stop after fill
- duplicate entry for the same setup
- entry remains working past expiry
- short path behaves differently than the mirrored long contract
- setup state fails to reset after flat
- flatten-before-close leaves the strategy in a broken armed state

## What Not To Do In This Pass

- do not optimize parameters
- do not add deferred filters during Playback
- do not switch to a second series as the first response to a logic issue
- do not judge edge quality from one or two anecdotal sessions

## Output

Create dated playback notes that answer:

1. Did long and short both behave coherently?
2. Did the strategy arm, fill, protect, trail, and reset correctly?
3. What is the first real runtime defect, if any?
4. What is the first entry-quality defect, if any?

Also link the playback note from:

- `docs/artifacts/YYYY-MM-DD/signoff-summary.md`
