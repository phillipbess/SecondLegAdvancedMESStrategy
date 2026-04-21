# SecondLegAdvancedMESStrategy Implementation Plan

## Current Recommendation

Continue with the strategy.

The project now has a clean `v1` thesis, a narrowed strategy surface, and a meaningful deterministic test ladder. The next question is no longer "what should the strategy be?" It is "does this implementation survive NT8 compile and Playback honestly?"

## Locked V1 Shape

The canonical source of truth is:

- `docs/Entry_Brain_V1_Contract.md`

`v1` is intentionally narrow:

- trend context:
  - `Close` vs `EMA200`
  - `EMA50` ATR-normalized slope
- ATR regime sanity filter
- hard-gated impulse:
  - last `3` bars only
  - at least `2` strong directional bars
  - strong bar body at least `50%` of range
  - final impulse bar on the correct side of `EMA50`
- two-legged pullback state machine
- midpoint signal bar
- stop-entry trigger
- fixed-dollar risk sizing
- stop-width rejection
- minimal structure-room gate:
  - `PDH/PDL`
  - completed `ORH/ORL`
  - recent swing high/low
- donor-style simple trail
- session and hard risk guardrails

Still excluded from `v1`:

- HTF alignment
- RVOL
- opening bias
- VWAP / VWAP bands
- overnight structure
- prior close structure
- round-number filters
- fixed targets
- breakeven
- partials

## Runtime Principle

The strategy remains parity-first with Mancini:

- keep the donor runtime/order-management behavior as faithfully as practical
- replace only the entry brain
- add only the side-aware plumbing needed for shorts
- do not create a second runtime model for shorts

## What Is Now Done

- stripped `v1` entry contract documented
- property/runtime surface trimmed toward the stripped contract
- hard-coded impulse semantics promoted into runtime code
- deterministic test families implemented for:
  - trend context
  - ATR regime and session gates
  - impulse qualification
  - pullback state machine
  - entry qualification
  - golden cases
- canonical long/short and rejection fixtures added

## Immediate Next Milestone

### Milestone: NT8 Playback Entry Validation

Goal:

Prove that the current stripped `v1` compiles in NinjaTrader and behaves coherently in Playback before adding any new filters or runtime complexity.

Required sequence:

1. compile in NT8
2. run the first Playback smoke pass
3. verify one full long lifecycle:
   - setup
   - resting stop entry
   - fill
   - protective stop present
   - simple trail behavior
   - flat reset
4. verify one full short lifecycle
5. verify stale trigger cancellation
6. verify flatten-before-close behavior

If Playback exposes issues, fix the smallest real runtime defects first rather than broadening the strategy.

### Runtime Middle Layer Reminder

External runtime harness scenarios once the runtime host contract stabilizes remain part of the validation ladder. Playback is the immediate operator gate, but the external harness is still the middle validation layer for restart, reconnect, coverage, and finalization behavior.

## Required Test Layers

Before and during Playback work, keep these layers green:

- source-level contract tests
- strategy-logic deterministic tests
- golden sequence fixtures
- external runtime harness scenarios once the runtime host contract stabilizes
- Playback evidence

## Promotion Gates

1. source-level contracts stay green
2. strategy-logic families stay green
3. NT8 compile succeeds
4. Playback smoke pass shows coherent long/short order lifecycle
5. no missing protective stop or duplicate entry behavior
6. session-close behavior is sane

## What Not To Do Now

- do not re-introduce deferred filters into `v1`
- do not add a second bars series as the first realism step
- do not drift back toward score-based impulse logic
- do not rewrite the donor shell broadly without Playback evidence

## Trail Note

The runtime path remains on donor-style simple trail management. The intended management surface is simple trail arm/lock/distance management, not fixed targets or hybrid target-fallback exits.
