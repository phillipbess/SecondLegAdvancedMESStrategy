# Source Notes

This folder holds the strategy implementation.

Use this as a code map, not as the canonical strategy contract. The canonical contracts
live under `docs/`.

## Top-Level Layout

- `strategy/`
  - strategy host shell
  - entry brain
  - closed-bar adapter
  - unmanaged transport adapter
  - logging, persistence, harness, and runtime host surfaces
- `runtime-core/`
  - donor-shaped runtime/control/support code lifted and adapted from the donor strategy

## Read Order

If you want to understand the runtime quickly:

1. `strategy/SecondLegAdvancedMESStrategy.cs`
2. `strategy/SecondLegAdvancedMESStrategy.StateLifecycle.cs`
3. `strategy/SecondLegAdvancedMESStrategy.RuntimeHost.cs`
4. `strategy/SecondLegAdvancedMESStrategy.TransportAdapter.cs`
5. `runtime-core/SecondLegAdvancedRuntimeControlLane.cs`

If you want to understand the entry thesis quickly:

1. `strategy/SecondLegAdvancedMESStrategy.EntryAnalysis.cs`
2. `strategy/SecondLegAdvancedMESStrategy.AdvancedContext.cs`
3. `strategy/SecondLegAdvancedMESStrategy.BarFlow.cs`
4. `strategy/SecondLegAdvancedMESStrategy.ClosedBarAdapter.cs`
5. `strategy/SecondLegAdvancedMESStrategy.PlannedEntry.cs`

## Important Reminder

The entry brain and runtime shell are intentionally separated.

The entry brain decides whether a setup exists.

The runtime shell owns order/fill/protection/flatten behavior.

Porting rule:

- rebuild the entry brain here
- freeze the reused runtime core as early as possible
- adapt execution events, persistence, and logging identity carefully
