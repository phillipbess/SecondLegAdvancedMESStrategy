# SecondLegAdvancedMESStrategy

Separate NinjaTrader strategy repo for a second-entry / second-leg pullback continuation model.

This repo is intentionally separate from `ManciniMESStrategy` so we can:

- preserve the current production strategy unchanged
- reuse hardened order-management and safety concepts selectively
- develop a new entry engine around trend + impulse + two-legged pullback structure
- validate the new edge hypothesis without mixing it into Adam failed-breakdown logic

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

The next practical milestone is NT8 compile plus disciplined Playback validation, not more filter expansion.

The canonical setup definition now lives in:

- `docs/Entry_Brain_V1_Contract.md`

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

## Initial Repo Layout

- `AGENTS.md` - local repo editing and reuse contract
- `docs/Entry_Brain_V1_Contract.md` - canonical stripped-down `v1` entry-engine contract
- `docs/Parity_Signoff_Checklist.md` - exact gates for "entry contract complete" and "Mancini runtime parity complete"
- `docs/Consensus_Panel_Plan.md` - agreed plan from a 3-reviewer panel
- `docs/Implementation_Plan.md` - agreed implementation roadmap
- `docs/Parity_First_V1_Rebuild_Plan.md` - locked parity-first rebuild target for lean `v1`
- `docs/Architecture_Reuse_Map.md` - what to reuse vs rebuild
- `src/` - strategy source
- `tests/` - validation and contract tests

## Guiding Principle

New strategy, shared hardened engine, new entry brain.
