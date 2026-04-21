# ADR-0003: Second-Leg State Machine

- Status: Proposed
- Date: 2026-04-20

## Context

The core edge hypothesis depends on a structured two-legged pullback continuation model.
That logic needs an explicit state machine rather than a pile of ad hoc flags.

## Decision Placeholder

Pending review. The likely baseline is:

- represent the setup as named states with explicit entry and invalidation rules
- separate context qualification from pullback-leg tracking and trigger readiness
- keep long and short behavior symmetric where possible, even if long-first ships first
- make stale-setup cancellation rules visible and testable

## Consequences to Confirm

- cleaner replay analysis because each setup phase is explainable
- reduced overfitting risk compared with many hidden condition toggles
- stronger unit-test surface for edge validation before trade-management tuning

## Open Questions

- what minimum state set is enough for the MVP
- how are reset and invalidation rules encoded across sessions and impulses
- which transitions should emit operator-facing diagnostics once logging is defined
