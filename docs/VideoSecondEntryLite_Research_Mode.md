# VideoSecondEntryLite Research Mode

## Purpose

`VideoSecondEntryLite` is a selectable research entry mode for testing a faster,
closer-to-the-video version of the second-entry idea without weakening the hardened
runtime shell.

It exists because QuantConnect research showed two separate things:

- the strict 5-minute `StrictV1` contract is clean but too sparse
- simply loosening strict filters produced more trades but still negative expectancy

So this mode is not a promoted production default. It is a controlled research branch
inside the same strategy.

## Mode Selection

The entry brain now has:

- `EntryMode = StrictV1`
- `EntryMode = VideoSecondEntryLite`
- `TradeDirection = Both | ShortOnly | LongOnly`

`StrictV1` remains the canonical auditable strategy contract.

`VideoSecondEntryLite` is for playback and research when we want to test whether the
original video dynamic appears more often with fewer quality filters.

## What Stays Hard

Lite mode still requires:

- EMA200 side for continuation context
- EMA50 slope sign in the continuation direction
- a directional impulse bar
- a pullback leg 1
- separation between leg 1 and leg 2
- a renewed countertrend leg 2
- a separate signal bar after leg 2
- stop-entry confirmation
- fixed-dollar sizing from the planned stop
- max stop-width protection
- quantity greater than zero
- hard risk rails
- flatten-before-close behavior
- existing order-management, protection, trail, recovery, and logging shell

## What Is Relaxed

Lite mode relaxes these strict V1 gates:

| Area | StrictV1 | VideoSecondEntryLite |
| --- | --- | --- |
| ATR regime | ATR ratio must be within configured band | ATR only needs to be positive |
| Trend strength | EMA50 slope must exceed `SlopeMinAtrPctPerBar` | EMA50 slope only needs the correct sign |
| Impulse size | `MinImpulseAtrMultiple` | `LiteMinImpulseAtrMultiple`, default `0.75` |
| Strong impulse bars | requires `V1MinStrongBars` | diagnostic only |
| Final impulse EMA50 side | required | not required |
| Pullback min retracement | required | not required |
| Pullback max retracement | `MaxPullbackRetracement`, default `0.618` | `LiteMaxPullbackRetracement`, default `0.95` |
| Leg-2 momentum cap | hard block | diagnostic only |
| Structure room | hard block | diagnostic only unless `LiteStructureVetoEnabled = true` |

## Separation Tracking Fix

The strategy now keeps updating `separationHigh` and `separationLow` while it is waiting
for leg 2 to begin.

That matters because leg-2 momentum should be measured from the full separation phase,
not only from the first bounce bar. This change applies to the strict state machine too
because it is a parity fix, not only a lite-mode feature.

## Logging Tokens

Lite mode adds tokens to existing entry observations rather than changing log filenames.

Important tokens:

- `entryMode=StrictV1`
- `entryMode=VideoSecondEntryLite`
- `impulseAtr=...`
- `requiredAtr=...`
- `ENTRY_DIAGNOSTIC ... leg2Momentum=... strictLimit=... retracement=...`
- `ENTRY_DIAGNOSTIC ... diagnosticRoomR=... level=... structurePrice=...`

These tokens are intended to answer:

- Was this a strict or lite setup?
- Did lite mode pass because a strict gate was relaxed?
- Was structure room poor but diagnostic-only?
- Was leg 2 too aggressive under strict rules?

## Safe-Edit Trace

Entry point:

- closed-bar strategy pass refreshes context
- entry brain chooses `StrictV1` or `VideoSecondEntryLite`
- entry brain produces the same `PlannedEntry` shape
- order submission still goes through existing entry/order-management code

Touched areas:

- entry qualification logic
- property surface
- support enums
- deterministic test helpers and fixtures
- docs/research notes

Untouched areas:

- `OnExecutionUpdate`
- runtime host order-management authority
- transport adapter
- exit controller / trail behavior
- protective order flow
- timing constants
- log filenames
- CSV schemas

## Validation Status

Current status:

- source-level tests cover the mode-aware entry behavior
- QuantConnect frequency research explains why this mode exists
- NT8 compile and Playback evidence are still required before any promotion

Do not use `VideoSecondEntryLite` as a live default until it has its own playback matrix
and trade narrative review.
