# ADR-0002: Runtime-Core Authority

- Status: Accepted
- Date: 2026-04-20

## Context

The strategy needs a clear authority model for fills, exits, durable state, and safety
checks before implementation spreads across multiple classes.

## Decision

For M1, the runtime core is the donor import lane for order authority, while the host
shell remains the NT8-facing strategy surface.

Concrete M1 boundaries:

- the host shell keeps `OnStateChange`, `OnBarUpdate`, session resets, setup-state
  transitions, signal planning, and property ownership
- the first donor classes to port are `SubmissionAuthority`, `ControlLane`,
  `OrderMaintenance`, and `TradeManager`, in that order
- `OnExecutionUpdate` remains the event-driven boundary where fill truth enters the
  runtime lane
- `ExitController`, `Coverage`, `StopRisk`, and `Safety` are prepared by contract in
  M1 but imported in M2
- persistence and protected-log identity stay deferred until the M1 authority split is
  proven stable
- log formats become contracts once this repo defines them

## Consequences

- the strategy thesis stays isolated from donor runtime code, so failed-breakdown entry
  logic does not leak into this repo
- the first code port stays mechanical and event-driven instead of mixing runtime import
  with signal experimentation
- `SecondLegAdvancedMESStrategy.RuntimeHost.cs` can stay small and explicit because it
  defines seams first, not permanent duplicate logic
- later M2 work has a prepared exit-lane landing zone instead of an ad hoc port

## First Port Order

1. Keep the host shell and lifecycle files authoritative.
2. Reuse the imported support helpers.
3. Port `SubmissionAuthority`.
4. Port `ControlLane`.
5. Port `OrderMaintenance`.
6. Port `TradeManager`.

## Intentionally Deferred

- `ExitController`, `Coverage`, `StopRisk`, `Safety`
- adapted `ExecutionEvents`
- donor persistence wiring
- protected-log identity and reporting lanes
- overlay/RL hooks
- short-side logic and non-core filters

## Open Questions

- what contract tests should lock the M1 boundaries before M2 starts
- whether `TradeManager` needs thin repo-local adapters at the execution boundary
