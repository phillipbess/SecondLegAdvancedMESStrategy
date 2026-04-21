# Architecture Reuse Map

## Thesis

`SecondLegAdvancedMESStrategy` should be a new strategy with a new setup/state-machine lane, while reusing as much hardened runtime infrastructure as possible from `ManciniMESStrategy`.

## Reuse First

- Execution/fill routing patterns
- Exit authority and protective-stop flow
- Submission guards
- Order maintenance and recovery
- Coverage and safety checks
- Logging/reporting discipline
- Persistence symmetry patterns
- Session and validation helpers

## Rebuild First

- Trend-context detection
- Impulse scoring
- Two-legged pullback state machine
- Signal-bar and stop-entry logic
- Structure-room evaluation
- Relative-volume by time bucket
- Strategy parameter surface

## File-Level Reuse Guidance

### Best Lift-Intact Candidates

- `ExitController`
- `SubmissionAuthority`
- `ControlLane`
- `OrderMaintenance`
- `TradeManager`
- `Coverage`
- `StopRisk`
- `Safety`
- `OperationalLogging`
- shared math/utilities/support-type helpers

### Adapt Carefully

- `ExecutionEvents`
- `Persistence`
- `Session`
- `Validation`
- `Logging`
- `Reporting`
- `Overlay`
- `OrderSemantics`
- `OrderIdentity`
- `Recovery`
- `ProtectiveFlow`

### Rebuild

- `BarFlow`
- `EntryAnalysis`
- `EntrySubmit`
- `EntryPricing`
- `EntryState`
- `EntryExecution`
- `EntryRetry`
- `MarketContext`
- `Properties`

## Architecture Warning

Partial-class coupling is the main porting risk. Anything brought over from the current strategy must be mapped against an explicit host-shell field contract instead of copied opportunistically.

## Target Runtime Shape

- Event shell remains event-driven.
- Entry lane becomes strategy-specific.
- Exit/control lane remains strict and centralized.
- Logging and persistence stay deliberate and contract-minded.
- External runtime harness remains a separate middle validation layer outside the NT8 workspace.

## First Safe Boundary

The first implementation should preserve the conceptual role of:

- `OnExecutionUpdate`
- `ExitController`
- protective-stop orchestration
- order-maintenance/recovery

while replacing the setup detector and qualification state machine entirely.

## External Harness Reuse

The harness pattern to reuse comes from:

- [C:\dev\mancini-worktrees\mancini-runtime-tests](</C:/dev/mancini-worktrees/mancini-runtime-tests>)

Reuse conceptually:

- host model
- fake runtime objects
- invariant assertions
- semantic anchors
- scenario catalog and trace-snapshot review model

Do not reuse blindly:

- Mancini entry-thesis assumptions
- donor-specific scenario names where the runtime truth differs
- mirrored source paths that point to the old strategy repo

See:

- [External_Runtime_Harness_Adoption_Plan.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/External_Runtime_Harness_Adoption_Plan.md>)
