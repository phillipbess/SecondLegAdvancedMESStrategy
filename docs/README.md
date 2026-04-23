# Documentation Guide

This folder is the documentation front door for `SecondLegAdvancedMESStrategy`.

If you are new to the repo, do not start by reading every planning document. Use the
ordered path below.

## Start Here

1. [START_HERE.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/START_HERE.md)
   Quick onboarding for what the strategy is, what is implemented, and how the repo is laid out.
2. [CURRENT_STATE.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/CURRENT_STATE.md)
   Honest handoff note for what is done, what is not yet proven, and what the next operator should do.
3. [Entry_Brain_V1_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Entry_Brain_V1_Contract.md)
   Canonical source of truth for the setup logic.
4. [Video_Idea_To_Strategy_Mapping.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Video_Idea_To_Strategy_Mapping.md)
   Plain-English bridge from the original discretionary setup idea to the coded entry brain.
5. [Host_Shell_Contract.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Host_Shell_Contract.md)
   Canonical source of truth for the donor-shaped runtime shell and authority boundaries.
6. [Parity_Signoff_Checklist.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Parity_Signoff_Checklist.md)
   The only document that defines what counts as real completion.

## What Each Section Is For

- `adr/`
  Architecture decisions that explain why the repo is shaped the way it is.
- `artifacts/`
  Dated compile, playback, harness, and signoff evidence packs.
- `research/`
  Research notes that informed the strategy thesis.
- `runbooks/`
  Operator procedures for compile, playback, walk-forward, and signoff evidence capture.
  For the first serious NT8 pass, start with:
  - `runbooks/playback-preflight.md`
  - `runbooks/playback-scenario-matrix.md`
  - `runbooks/playback.md`
  - `runbooks/log-review-guide.md`

## Planning Documents

Several planning documents remain intentionally preserved because they show the design
history and parity decisions:

- `Implementation_Plan.md`
- `Parity_First_V1_Rebuild_Plan.md`
- `Consensus_Panel_Plan.md`
- `Clean_Start_Council_Blueprint.md`
- `Architecture_Reuse_Map.md`

These are useful context, but they are not the current contract. The current contracts
are:

- `Entry_Brain_V1_Contract.md`
- `Host_Shell_Contract.md`
- `Parity_Signoff_Checklist.md`

## Rule Of Thumb

If two docs disagree, trust them in this order:

1. `Parity_Signoff_Checklist.md`
2. `Entry_Brain_V1_Contract.md`
3. `Host_Shell_Contract.md`
4. dated evidence in `artifacts/YYYY-MM-DD/`
5. planning docs
