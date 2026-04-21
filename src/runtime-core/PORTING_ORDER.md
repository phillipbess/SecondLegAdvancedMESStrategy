# Runtime-Core Porting Order

## M0

- import low-risk shared support types and extensions
- define the host-shell contract
- freeze the port order in writing

## M1

- `SupportTypes`
- `SubmissionAuthority`
- `ControlLane`
- `OrderMaintenance`
- `TradeManager`

Current M1 note:

- `OrderMaintenance` now has a scaffold-safe donor-aligned queue/cancel/working-order helper surface under `src/runtime-core`.
- Activation remains deferred until the host shell explicitly binds cancel authority, working-order tracking, and flatten escalation hooks.

## M2

- `ExitController`
- `Coverage`
- `StopRisk`
- `Safety`

## M3

- adapt `ExecutionEvents`
- adapt `Persistence`
- adapt `Logging` identity

## M4

- `Recovery`
- `OrderSemantics`
- `OrderIdentity`
- `ProtectiveFlow`

## Rule

Do not port `ExecutionEvents`, `Persistence`, or protected-log wiring before the host-shell contract is explicit.
