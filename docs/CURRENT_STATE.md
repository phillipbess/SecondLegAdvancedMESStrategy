# Current State

This is the honest project handoff note.

Read this if you want to know where the repo stands today, what is complete in code,
what is still only a claim, and what the next person should actually do.

## What The Repo Is

`SecondLegAdvancedMESStrategy` is a sibling strategy repo that reuses Mancini-style
runtime and observability patterns while replacing the trading idea with a stripped,
rule-based second-leg continuation entry brain.

The repo is intentionally not a copy of Mancini entry logic.

## What Is Strong Today

### Entry Brain

The entry implementation is narrow, explicit, and documented.

The repo now has two entry modes.

`StrictV1` remains the canonical contract. It includes:

- trend context
- ATR regime sanity
- hard-gated impulse
- two-legged pullback state machine
- signal-bar validation
- stop-entry planning
- stop-based sizing
- structure-room gate
- long/short symmetry

`VideoSecondEntryLite` is a research mode. It keeps the same runtime shell and hard
risk/order-management rails, but relaxes selected entry-quality gates so we can test a
closer-to-the-video version without mutating the strict contract.

The current entry brain still excludes most of the tempting extras:

- HTF filters
- RVOL
- opening bias
- VWAP
- round numbers
- fixed targets
- breakeven
- partials

That is intentional. The repo is trying to prove a clean base thesis before adding
ornament.

### Runtime Shell

The shell is donor-shaped after `ManciniMESStrategy`:

- `OnEachTick` host shell
- closed-bar entry adapter
- unmanaged transport adapter
- donor-style simple trail
- runtime control lane for protection and flattening
- persistence/recovery scaffolds
- strong logging contracts

### Logging

The repo now has real file-backed logging and a frozen logging contract.

The logging is designed to answer two different questions:

- Why did the setup advance, block, or expire?
- What did the runtime do with orders, protection, flattening, and recovery?

### Playback And Analysis

Playback is no longer hypothetical in this repo.

We now have dated artifact packs, emitted `Patterns_` / `Trades_` / `Risk_` / `Debug_`
text logs, Mancini-compatible `TradesCsv_` / `StopEvents_` CSV outputs, and two
operator-facing analysis scripts:

- [trade_narrative.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/scripts/trade_narrative.py)
- [metrics.ps1](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/scripts/metrics.ps1)

Those tools make the current posture much more honest:

- trade narratives can now be reconstructed from real log output
- completed trades now have one-row CSV summaries for spreadsheet/pipeline review
- stop and protective events now have Mancini-shaped CSV rows for order-management review
- order-management readiness can now be evaluated from real log output
- playback findings are now being captured as dated evidence instead of ad hoc notes

### QuantConnect Research

QuantConnect research now gives us a clearer read:

- strict 5-minute baseline over five years was sparse and slightly negative
- short-only strict refinement around `SecondLegMaxMomentumRatio = 0.60` was the best
  quality pocket so far, but very sparse
- aggressive frequency probes produced more trades but stayed negative
- the desired `20` trades/month target is not reachable by simply loosening the strict
  5-minute contract

That is why `VideoSecondEntryLite` was added as a separate research mode.

### Tests

The repo has a meaningful deterministic ladder:

- strategy logic tests
- runtime/contract tests
- logging contract tests
- signoff evidence contract tests

This means the source shape is much harder to accidentally erode than it was earlier
in the project.

## What Is Not Yet Proven

The repo is still honest about one thing:

source-level correctness is not the same as platform proof.

The project still needs:

- NT8 compile evidence captured cleanly in-platform
- broader NT8 Playback coverage across the scenario matrix
- dedicated Playback evidence for `VideoSecondEntryLite`
- runtime-harness evidence for the touched parity lanes

Those requirements are not optional, and they are the reason the signoff checklist
still distinguishes `defined`, `evidence-in-progress`, and `complete`.

Canonical signoff doc:

- [Parity_Signoff_Checklist.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Parity_Signoff_Checklist.md)

## Strongest Honest Claim Right Now

The strongest honest claim today is:

- entry contract is well-defined and implemented in source
- runtime shell parity is strong in source shape
- logging and diagnostics are strong in source shape
- playback is active and producing useful evidence
- QuantConnect evidence says strict 5-minute is too sparse for the desired frequency
- lite mode now exists for controlled video-faithful research
- the repo still needs broader compile/playback/harness proof

What should not be claimed yet:

- completed entry signoff
- completed runtime parity signoff
- completed live-readiness

## Recommended Next Moves

If you are picking this up now, do the work in this order.

1. Run local tests and preserve the current green baseline.
2. Compile in NinjaTrader 8 and record the result in a dated evidence folder.
3. Run `StrictV1` Playback against the scenario matrix, especially:
   - clean short symmetry
   - stale trigger expiry
   - flatten-before-close
   - protective replace / recovery breadth
4. Run `VideoSecondEntryLite` Playback separately and label every artifact with the mode.
5. Review the emitted `Patterns_`, `Trades_`, `Risk_`, `Debug_`, `TradesCsv_`, and `StopEvents_` logs with:
   - [trade_narrative.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/scripts/trade_narrative.py)
   - [metrics.ps1](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/scripts/metrics.ps1)
6. Run or extend the runtime harness for the touched parity lanes.
7. Only then update signoff language.

## Practical File Path For New Work

If you are changing setup logic, start with:

- [Entry_Brain_V1_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Entry_Brain_V1_Contract.md)
- [VideoSecondEntryLite_Research_Mode.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/VideoSecondEntryLite_Research_Mode.md)
- [SecondLegAdvancedMESStrategy.EntryAnalysis.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.EntryAnalysis.cs)

If you are changing runtime ownership or parity behavior, start with:

- [Host_Shell_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Host_Shell_Contract.md)
- [SecondLegAdvancedMESStrategy.RuntimeHost.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeHost.cs)
- [SecondLegAdvancedRuntimeControlLane.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedRuntimeControlLane.cs)
- [SecondLegAdvancedMESStrategy.TransportAdapter.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.TransportAdapter.cs)

If you are changing observability, start with:

- [SecondLegAdvancedMESStrategy.Logging.cs](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.Logging.cs)
- [tests/contracts/test_logging_contract.py](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/tests/contracts/test_logging_contract.py)

## Short Summary

This repo is no longer in the “what should this be?” phase.

It is in the “expand platform proof and scenario coverage, with logs and evidence”
phase.
