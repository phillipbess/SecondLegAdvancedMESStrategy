# Video Idea To Strategy Mapping

## Purpose

This note explains how the original discretionary setup idea maps to the current
`SecondLegAdvancedMESStrategy` implementation.

This is not a transcript of the YouTube video.

It is a repo-facing interpretation based on:

- the strategy concept the owner identified as the source idea
- the locked `v1` entry contract
- the implementation and playback work already done in this repo

If this note ever conflicts with the formal strategy rules, trust:

1. [Entry_Brain_V1_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Entry_Brain_V1_Contract.md)
2. [Host_Shell_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Host_Shell_Contract.md)
3. dated playback evidence in `docs/artifacts/`

## The Big Idea

The underlying market idea is simple:

- the market is already moving in one direction
- a real directional push proves initiative
- the countertrend side tries to pull price back
- that countertrend effort takes two legs rather than one clean reversal
- the second countertrend attempt fails
- price resumes in the original direction

In plain English:

trend -> impulse -> two-legged correction -> failed second countertrend push -> continuation

That is the conceptual heart of this strategy.

## What We Preserved From The Original Idea

The repo keeps the core discretionary thesis intact:

- do not trade reversals
- do not trade random breakouts
- require a real trend context
- require a meaningful impulse, not just drift
- require a corrective pullback, not immediate continuation
- require a second countertrend attempt
- require a trend-direction signal after that second attempt
- enter on a stop trigger, not on hope

That means the implementation is still recognizably the same setup idea, just expressed
as code.

## How The Idea Maps To The Entry Brain

### 1. "The market is trending"

Mapped to:

- `Close > EMA200` for long or `Close < EMA200` for short
- `EMA50` slope must point the same way

Why:

This keeps the setup anchored to continuation, not random chop.

### 2. "There was a real move"

Mapped to:

- a hard-gated impulse over the last `3` bars
- impulse range must be at least `1.25 ATR`
- at least `2` of the `3` bars must be strong directional bars
- the final impulse bar must still close in the trend direction and on the correct side of `EMA50`

Why:

This tries to capture actual initiative instead of any three bars that happen to point in
one direction.

### 3. "The market pulled back"

Mapped to:

- `PullbackLeg1`
- separation
- `PullbackLeg2`

Why:

The strategy is not supposed to buy the first shallow pause after an impulse. It wants a
real correction attempt.

### 4. "There were two tries against the trend"

Mapped to:

- a first countertrend leg
- a genuine separation / bounce between legs
- a second countertrend leg that remains corrective

Why:

This is the coded form of the "second entry" / "second leg" idea.

### 5. "The second try failed"

Mapped to:

- leg 2 must stay within retracement bounds
- leg 2 momentum must stay below the configured fraction of impulse momentum
- a valid signal bar must form after the leg-2 candidate
- the stop-entry trigger must break in the trend direction

Why:

The failure is not assumed just because a second leg exists. The code waits for a
trend-direction signal and then a trigger.

## Where We Intentionally Made It Stricter

The coded strategy is deliberately stricter than a discretionary chart-reading version in
several places.

This is true for `StrictV1`, the canonical mode. The repo also now has
`VideoSecondEntryLite`, which is a research mode designed to test whether the original
video idea needs fewer quality gates to appear often enough.

### Trend Context

Discretionary traders may read trend more loosely.

The code requires:

- EMA200 side
- ATR-normalized EMA50 slope

Reason:

This keeps continuation intent explicit and testable.

### Impulse Qualification

Discretionary traders may visually accept an obvious strong move.

The code requires:

- exact bar count
- exact ATR multiple
- exact strong-bar count
- exact final-bar direction and EMA50 side

Reason:

This prevents hindsight from redefining what counts as a "real move."

### Corrective Leg Boundaries

The code requires:

- retracement bounds
- a leg-2 momentum cap
- a real separation bar

Reason:

This tries to distinguish a correction from an actual reversal attempt that is taking over.

### Structure Room

The code requires:

- room before obvious opposing structure

Reason:

This is one of the main places where a believable setup can still be rejected for quality.

### Risk And Order Planning

The code requires:

- a defined stop
- a stop-width cap
- fixed-dollar sizing from that stop

Reason:

A discretionary setup can still be a bad executable trade if the stop is too wide or the
room is too poor.

## Strict V1 vs VideoSecondEntryLite

The strategy now has two ways to express the same core market idea.

### StrictV1

`StrictV1` is the audit mode.

It asks:

- Can the second-entry idea survive a precise, quality-filtered implementation?
- Can we explain every accepted and rejected setup from deterministic rules?
- Can we keep the entry brain narrow enough to compare against QuantConnect and Playback?

Strict mode keeps:

- ATR regime band
- ATR-normalized EMA50 slope threshold
- strict impulse ATR requirement
- strong impulse bar count
- final impulse bar on the correct side of EMA50
- minimum and maximum pullback retracement
- leg-2 momentum cap
- structure-room veto

### VideoSecondEntryLite

`VideoSecondEntryLite` is the research mode.

It asks:

- Are we over-filtering the video idea?
- Does a simpler second-entry detector produce enough observations to test?
- Which strict gates are actually helping, and which are only starving the strategy?

Lite mode keeps:

- EMA200 continuation side
- EMA50 slope sign
- impulse, pullback, separation, leg 2, signal bar, and stop-entry sequence
- risk sizing
- max stop-width protection
- hard risk rails
- flatten behavior
- existing order-management shell

Lite mode relaxes:

- ATR regime band
- slope magnitude threshold
- strict impulse ATR multiple
- strong impulse bar requirement
- final impulse EMA50-side requirement
- minimum pullback retracement
- leg-2 momentum hard block
- structure-room hard block by default

In lite mode, leg-2 momentum and structure room are logged as diagnostics so Playback can
show whether those filters are actually useful.

The key discipline is that lite mode did not replace strict mode. It exists beside it.

## Where We Intentionally Did Not Add More Complexity

We deliberately kept many common filters out of `v1`:

- higher-timeframe alignment
- RVOL
- opening bias
- VWAP
- round numbers
- profile
- fixed targets
- breakeven
- partial exits

Reason:

The goal of `v1` is to test the core market dynamic first, not bury it under extra
filters.

## How To Think About The Current Strategy

The cleanest mental model is:

- the original video idea provides the market narrative
- the repo turns that narrative into a deterministic contract
- playback and logs tell us whether our implementation is too strict, too loose, or
  correctly balanced

So this strategy should be understood as:

- not a generic price-action bot
- not a reversal strategy
- not a trend-following breakout strategy

It is a formalized continuation strategy built around a failed second countertrend attempt.

## What Playback Has Already Taught Us

Playback work in this repo has already shown something important:

- the idea can progress all the way through arm, submit, fill, protect, trail, and close
- some no-trade days are not runtime failures
- many no-trade cases are entry-conversion choices, not shell failures
- QuantConnect frequency probes suggest strict 5-minute filters are too sparse for the
  desired trade frequency
- loose 5-minute probes produced more trades but not a proven edge

That means the current tuning work is mostly about:

- deciding which guards are essential to the edge
- which are just quality filters
- and which may be over-constraining the original idea

## Practical Rule Of Thumb

When deciding whether a future code change still matches the original setup idea, ask:

1. Does it still require a real trend?
2. Does it still require a real impulse?
3. Does it still require a real two-leg correction?
4. Does it still wait for failure of that second countertrend attempt?
5. Does it still enter only on confirmation, not anticipation?

If the answer to those stays yes, the strategy is probably still faithful to the original
concept.

If one of those turns into no, we are no longer refining the same idea. We are changing
the strategy.
