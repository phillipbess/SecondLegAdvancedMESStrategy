# SecondLegAdvancedMESStrategy

Separate NinjaTrader strategy repo for a second-entry / second-leg pullback continuation
model.

This repo is intentionally separate from `ManciniMESStrategy` so we can:

- preserve the current production strategy unchanged
- reuse hardened order-management and safety concepts selectively
- develop a new entry engine around trend + impulse + two-legged pullback structure
- validate the new edge hypothesis without mixing it into Adam failed-breakdown logic

## Read This First

If you are new to the repo, use this path:

1. [docs/START_HERE.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/START_HERE.md)
2. [docs/CURRENT_STATE.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/CURRENT_STATE.md)
3. [docs/Entry_Brain_V1_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Entry_Brain_V1_Contract.md)
4. [docs/Host_Shell_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Host_Shell_Contract.md)
5. [docs/Parity_Signoff_Checklist.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Parity_Signoff_Checklist.md)

The documentation index lives at:

- [docs/README.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/README.md)

## Strategy Summary

The strategy thesis is intentionally narrow:

- trend
- ATR regime sanity
- hard-gated impulse
- two-legged pullback
- signal bar
- stop-entry trigger
- room to structure
- runtime-managed trade

The design goal is to prove a clean continuation hypothesis before adding more filters.

## Current Status

The repo is now in a stripped, parity-first `v1` state:

- reuse Mancini runtime/order-management behavior as faithfully as practical
- replace only the entry brain
- add only the side-aware plumbing needed for shorts
- use donor-style simple trail rather than fixed targets
- keep the entry model deliberately stripped down for `v1`
- keep the core entry semantics fixed in code:
  - `EMA50`
  - `3` impulse bars
  - `2` strong directional bars
  - `50%` minimum strong-bar body

The first-pass deterministic ladder is now in place:

- source-level contract tests
- strategy logic tests for trend, impulse, pullback, entry, regime/session
- canonical golden cases for valid and invalid `v1` sequences

The next practical milestone is NT8 compile plus disciplined Playback validation, not
more filter expansion.

The canonical setup definition now lives in:

- [docs/Entry_Brain_V1_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Entry_Brain_V1_Contract.md)

That document is the source of truth for:

- trend context
- ATR regime
- impulse
- leg separation
- second-leg candidate validation
- signal bar rules
- entry/stop/size planning
- structure room
- cancellation and reset semantics

The canonical runtime-shell definition now lives in:

- [docs/Host_Shell_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Host_Shell_Contract.md)

The canonical signoff definition now lives in:

- [docs/Parity_Signoff_Checklist.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Parity_Signoff_Checklist.md)

## Repo Layout

- `AGENTS.md` - local repo editing and reuse contract
- `docs/` - documentation front door, contracts, plans, runbooks, and evidence
- `src/` - strategy source
- `tests/` - validation and contract tests

## Honest Posture

The repo is strong in source shape, but the signoff checklist still requires:

- NT8 compile evidence
- playback smoke evidence
- harness evidence where parity is claimed

So the right current claim is:

- code and docs are advanced
- the next phase is proof, not more strategy expansion

## Guiding Principle

New strategy, shared hardened engine, new entry brain.
