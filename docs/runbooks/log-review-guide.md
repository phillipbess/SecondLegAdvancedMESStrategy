# Log Review Guide

The point of Playback is not just to watch bars. It is to compare the observed strategy
story against the strategy story we intended to build.

This guide explains which logs answer which questions.

## Fast Triage

If the question is:

- "Why did this setup arm or fail?"
  - start with `Patterns_`
- "Did the trade submit, fill, protect, and flatten correctly?"
  - start with `Trades_`
- "Did protection, trail, replace, flatten, or recovery behave correctly?"
  - start with `Risk_`
- "What exact internal runtime step happened around the anomaly?"
  - use `Debug_`

## `Patterns_`

Use `Patterns_` to reconstruct entry intent.

Questions it should answer:

- was trend context valid?
- was ATR regime valid?
- did impulse qualify?
- did leg 1 and separation form?
- did leg 2 remain corrective?
- did the signal bar qualify?
- why was the setup blocked, armed, expired, or reset?

Look for fields/reasons around:

- impulse size
- strong-bar count
- retracement
- leg-2 momentum ratio
- room to structure
- risk sanity
- block reason

## `Trades_`

Use `Trades_` as the compact lifecycle spine.

Questions it should answer:

- when was the entry submitted?
- when did it fill?
- when did flatten submit?
- when did the trade close?
- did recovery/adoption change the lifecycle?

If `Trades_` and `Risk_` disagree, trust neither blindly. Reconcile them with `Debug_`.

## `Risk_`

Use `Risk_` for order-management truth.

Questions it should answer:

- was the first protective stop submitted?
- did stop change or replace occur?
- did stop ack/confirm happen?
- was there a quantity mismatch or double-stop warning?
- did flatten progress correctly?
- did orphan/adoption/coverage health checks fire?

High-value tokens to recognize quickly:

- `STOP_SUBMIT`
- `STOP_CHANGE`
- `STOP_ACK`
- `STOP_CONFIRMED`
- `STOP_CANCELLED_ACK`
- `STOP_FILLED_ACK`
- `STOP_QTY_MISMATCH`
- `DOUBLE STOP DETECTED`
- `ORPHAN_CHECK`
- `ORPHAN_SWEEP`
- `OM_HEALTH`
- `FIRST_STOP_SLA`
- `ADOPT`
- `OCO_RESUBMIT`
- `COVERAGE_STATE`
- `EXIT_OP_ENQ`
- `EXIT_OP_BEGIN`
- `EXIT_OP_END`
- `EXIT_OP_TIMEOUT_RELEASE`

## `Debug_`

Use `Debug_` when the higher-level story is not enough.

Questions it should answer:

- what state transition fired first?
- did a duplicate action get suppressed?
- did a retry/defer happen?
- did reconnect grace start?
- did runtime maintenance or reset happen unexpectedly?

`Debug_` is the operator microscope, not the first summary layer.

## Healthy Golden Sequences

Use these as fast comparison patterns during Playback.

Canonical references:

- [golden-log-sequences.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/runbooks/golden-log-sequences.md)

## Review Order For A Suspicious Trade

1. `Trades_`
   Confirm the lifecycle spine.
2. `Patterns_`
   Confirm the setup should have existed.
3. `Risk_`
   Confirm protection/flatten/runtime truth.
4. `Debug_`
   Resolve the remaining ambiguity.

## What To Write Down

For any suspicious sequence, record:

- scenario id
- relevant timestamps
- expected behavior
- observed behavior
- which log family proved the mismatch
- likely layer:
  - entry brain
  - runtime shell
  - transport
  - platform behavior
