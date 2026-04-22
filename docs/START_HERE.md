# Start Here

This repo is a separate NinjaTrader strategy project for a stripped-down second-entry /
second-leg pullback continuation model.

It exists so we can keep `ManciniMESStrategy` intact while building a different entry
idea on top of a donor-shaped runtime shell.

## In One Sentence

`SecondLegAdvancedMESStrategy` uses a simple continuation thesis:

trend -> impulse -> two-legged pullback -> second-entry trigger -> room to structure ->
runtime-managed trade

## What This Repo Is Trying To Do

- preserve the hardened runtime lessons from `ManciniMESStrategy`
- replace the trading idea with a cleaner second-leg continuation entry brain
- keep the base strategy intentionally narrow so the edge can be tested honestly
- make logging and diagnostics strong enough to explain both entry decisions and order
  management behavior

## What Is In Scope

The current strict `v1` includes:

- trend context
- ATR regime sanity
- hard-gated impulse qualification
- two-legged pullback state machine
- signal-bar validation
- stop-entry trigger
- fixed-dollar sizing from the initial stop
- structure-room gate
- donor-style simple trail
- session and hard risk rails
- long and short symmetry

The current strict `v1` excludes:

- HTF alignment
- RVOL
- opening bias
- VWAP / VWAP bands
- round-number filters
- fixed targets
- breakeven
- partials

## Architecture In Plain English

There are two big halves.

### 1. Entry Brain

The entry brain decides whether a setup exists and produces a `PlannedEntry`.

It owns:

- trend / ATR / impulse checks
- pullback leg counting
- signal-bar validation
- entry / stop / qty / expiry planning
- structure-room and reset logic

It does not own:

- exits
- trail movement
- flattening
- realized PnL accounting
- recovery state

Canonical doc:

- [Entry_Brain_V1_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Entry_Brain_V1_Contract.md)

### 2. Runtime Shell

The runtime shell is donor-shaped after `ManciniMESStrategy`.

It owns:

- NT8 event handling
- order submit / cancel / change
- fill truth
- protective stop authority
- simple trail management
- flatten/session-close behavior
- persistence / recovery
- runtime logging

Canonical doc:

- [Host_Shell_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Host_Shell_Contract.md)

## Code Map

If you want to understand the repo quickly, read these files in order.

### Strategy Host

- [SecondLegAdvancedMESStrategy.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.cs)
  Main strategy type and root surface.
- [SecondLegAdvancedMESStrategy.StateLifecycle.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.StateLifecycle.cs)
  Lifecycle setup and the main event split.
- [SecondLegAdvancedMESStrategy.RuntimeHost.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeHost.cs)
  Host authority, finalization, recovery, and runtime coordination.
- [SecondLegAdvancedMESStrategy.TransportAdapter.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.TransportAdapter.cs)
  NT8 unmanaged transport bridge.

### Entry Brain

- [SecondLegAdvancedMESStrategy.EntryAnalysis.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.EntryAnalysis.cs)
  Setup logic and state transitions.
- [SecondLegAdvancedMESStrategy.AdvancedContext.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.AdvancedContext.cs)
  Trend, ATR, structure, and context helpers.
- [SecondLegAdvancedMESStrategy.BarFlow.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.BarFlow.cs)
  Setup-state progression on the closed-bar pass.
- [SecondLegAdvancedMESStrategy.ClosedBarAdapter.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.ClosedBarAdapter.cs)
  Keeps the entry brain closed-bar driven even though the shell runs `OnEachTick`.
- [SecondLegAdvancedMESStrategy.PlannedEntry.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.PlannedEntry.cs)
  Entry handoff contract.

### Runtime Core

- [SecondLegAdvancedRuntimeControlLane.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedRuntimeControlLane.cs)
  Donor-shaped control lane for protection, flattening, and runtime sequencing.
- [SecondLegAdvancedMESStrategy.SubmissionAuthorityScaffold.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedMESStrategy.SubmissionAuthorityScaffold.cs)
  Order submission/finalization gating.
- [SecondLegAdvancedMESStrategy.OrderMaintenanceScaffold.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedMESStrategy.OrderMaintenanceScaffold.cs)
  Working-order tracking and maintenance.
- [SecondLegAdvancedMESStrategy.Logging.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.Logging.cs)
  Real logging sinks and event vocabulary.

## Test Map

There are two main test layers.

### Strategy Logic

These prove the entry thesis before playback:

- [tests/strategy/test_trend_context.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/strategy/test_trend_context.py)
- [tests/strategy/test_impulse_scoring.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/strategy/test_impulse_scoring.py)
- [tests/strategy/test_pullback_state_machine.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/strategy/test_pullback_state_machine.py)
- [tests/strategy/test_entry_qualification.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/strategy/test_entry_qualification.py)
- [tests/strategy/test_armed_entry_lifecycle.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/strategy/test_armed_entry_lifecycle.py)
- [tests/strategy/test_golden_cases.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/strategy/test_golden_cases.py)

### Contract / Parity

These freeze the runtime shell and evidence expectations:

- [tests/contracts/test_host_shell_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_host_shell_contract.py)
- [tests/contracts/test_execution_event_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_execution_event_contract.py)
- [tests/contracts/test_logging_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_logging_contract.py)
- [tests/contracts/test_runtime_snapshot_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_runtime_snapshot_contract.py)
- [tests/contracts/test_signoff_evidence_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_signoff_evidence_contract.py)

## Logging

The project now aims for Mancini-level observability.

Main log families:

- `Patterns_` for setup and entry decisions
- `Trades_` for trade lifecycle milestones
- `Risk_` for runtime risk / order-management state
- `Debug_` for supporting runtime breadcrumbs

The logging contract is frozen by:

- [tests/contracts/test_logging_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_logging_contract.py)

## Current Truth

The repo is advanced on code shape and documentation, but signoff is not claimed from
source alone.

For the honest current posture, read:

- [CURRENT_STATE.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/CURRENT_STATE.md)
- [Parity_Signoff_Checklist.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Parity_Signoff_Checklist.md)

## If You Only Read Three Things

1. `Entry_Brain_V1_Contract.md`
2. `Host_Shell_Contract.md`
3. `CURRENT_STATE.md`
