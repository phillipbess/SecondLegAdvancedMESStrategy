# Consensus Panel Plan

## Verdict

Yes, the idea is worth building.

All reviewers agreed on the same core framing:

- this should be a new repo and a new strategy
- the current strategy should be treated as a hardened runtime engine donor
- the safest seam is to rebuild the entry brain while preserving the order/fill/exit spine
- the first job is to prove the entry edge, not to ship the final feature set all at once

## Agreed Strategy Thesis

The strongest version of the idea is:

- trend continuation
- after a true two-legged countertrend pullback
- with volatility-normalized trend and risk filters
- only when there is enough room before opposing structure to get paid

That is a better edge hypothesis than a generic pullback breakout or broad candle-pattern approach.

## Agreed Build Principle

New strategy, shared hardened engine, new entry brain.

## Agreed MVP

Build `v1` as:

- long-first
- RTH-aware session filter
- EMA200 context
- EMA50 slope normalized by ATR
- ATR regime filter
- explicit two-legged pullback state machine
- stop-entry above signal bar
- stop below pullback leg 2 low plus small buffer
- room-to-structure filter in `R`
- fixed dollar risk sizing
- simple trade management

## What To Defer

These are worthwhile, but were agreed as later-phase items:

- shorts
- RVOL by time bucket
- higher-timeframe alignment
- opening-bias filter
- VWAP-based structure
- breakeven and complex trailing
- heavy parameter optimization

## Agreed Reuse Boundary

Reuse first:

- exit authority
- submission guards
- control lane
- trade manager
- coverage/safety
- order maintenance
- recovery concepts
- persistence patterns
- logging discipline

Rebuild first:

- trade setup detection
- trend context
- impulse scoring
- two-leg pullback counting
- structure-room logic
- strategy properties
- entry state machine
- entry submit path

## Highest-Risk Adaptation Seams

These need the most care during porting:

- execution events
- persistence symmetry
- logging identity and file separation

## Agreed Delivery Sequence

1. Freeze the reused runtime core.
2. Port safety/contract tests first.
3. Implement the minimal second-leg entry model.
4. Measure raw entry expectancy before advanced exit tuning.
5. Add quality filters only after the base edge survives.
6. Require walk-forward plus shadow/sim evidence before live-readiness discussions.

## Promotion Standard

Do not trust a single best parameter combination.

Accept only:

- robust parameter regions
- realistic execution assumptions
- stable out-of-sample behavior
- clear evidence that the entry edge exists before management layers are added
