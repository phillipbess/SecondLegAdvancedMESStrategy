# Host Shell Contract

## Purpose

Define the minimum host-shell surface that `SecondLegAdvancedMESStrategy` must provide before the hardened donor runtime can be ported safely.

## M1 First Port Order

M1 prepares the host shell first, then ports only the lowest-risk runtime-core pieces in this order:

1. Keep `SecondLegAdvancedMESStrategy.RuntimeHost.cs` and `SecondLegAdvancedMESStrategy.StateLifecycle.cs` as the NT8 host authority for lifecycle hooks, setup resets, and runtime bootstrap.
2. Adapt donor runtime contract tests from `ManciniMESStrategy/tests` into source-level gates in this repo.
3. Bind the already-imported support helpers from `src/runtime-core/SecondLegAdvancedRuntimeSupport.cs`.
4. Port `SubmissionAuthority` so submit/cancel/finalization gating stops living in placeholders.
5. Port `ControlLane` so `_exitState`, `_exitOpQueue`, `_exitEpoch`, and flatten sequencing move under donor runtime rules.
6. Port `OrderMaintenance` so working-order tracking and cancellation retries stop being host-shell stubs.
7. Port `TradeManager` last in M1, after the execution-truth fields and shell-owned hooks are stable.

`ExitController` is intentionally not part of the first code port. M1 only preserves its seam.

## Authority Boundaries

| Area | Host shell owns in M1 | Runtime core owns in M1 |
| --- | --- | --- |
| NT8 event surface | `OnStateChange`, `OnBarUpdate`, `OnMarketData`, strategy bootstrap, session resets | None |
| Strategy thesis | trend context, impulse/pullback detection, signal planning, parameter surface | None |
| Order authority | calling into the runtime lane from NT8 events | `SubmissionAuthority`, `ControlLane`, `OrderMaintenance` once imported |
| Execution truth | hosting the fields that `OnExecutionUpdate` writes into | `TradeManager` becomes the canonical fill/trade accounting lane once imported |
| Exit lane | field/seam preparation only | Deferred to M2 |
| Persistence/log identity | field/seam preparation only | Deferred until after M1 authority is stable |

The operating rule is simple: the strategy decides whether a second-leg setup exists, while the runtime core decides how accepted orders, fills, and cancellations are governed.

In the current parity shape, the host shell may run `OnBarUpdate` under `Calculate.OnEachTick`, but the strategy thesis still consumes only the closed primary-bar pass through the closed-bar adapter. Tick cadence belongs to runtime maintenance, not intrabar entry mutation.

Unmanaged NT8 transport now remains isolated behind a dedicated host transport adapter partial. `RuntimeHost` keeps lifecycle and authority decisions, while the transport adapter is the only place that touches `SubmitOrderUnmanaged(...)` and `ChangeOrder(...)` for entry submit, protective stop mutation, and flatten submit.

## Highest-Risk Seams First

### 1. Exit-Lane Shell

The donor `ExitController` is not portable by itself.

The host strategy must supply:

- `exitCtl`
- `ExitFlowState`
- `_exitEpoch`
- `_exitOpQueue`
- `_exitOpBusy`
- `_exitOpPendingUntil`
- `TriggerFlatten(...)`
- `EnqueueEnsureProtectiveExit(...)`
- `EnqueueExitOp(...)`
- `FindWorkingExitForRole(...)`
- `NewExitOco(...)`
- `MaySubmitOrders(...)`
- `CanMutateProtectiveStop(...)`
- `IsExitMutateSuppressed(...)`

### 2. Execution Source Of Truth

`OnExecutionUpdate` remains the single source of truth for trade lifecycle state.

The host must support:

- `tradeManager`
- `currentTradeID`
- `entryPositionSide`
- `entryFillTime`
- `entryFillPrice`
- `entryQuantity`
- `entryPrice`
- `avgEntryPrice`
- `initialPositionSize`
- `tradeRiskPerContract`
- `initialStopPrice`
- `initialTradeRisk`
- `AnchorEntryExecutionState(...)`
- `CountTradeOnFirstEntryFill(...)`
- `ValidateStopQuantity(...)`
- `MaybeTriggerSessionControlFlattenOnEntryFill(...)`
- `SaveStrategyState()`

The Adam-specific wick re-anchor logic is not part of the preserved contract.

### 3. Coverage Truth Model

Coverage logic depends on:

- `Position.Quantity`
- `_workingOrders`
- `Account.Orders`

Required host helpers and fields:

- `HasCoverageNow()`
- `WorkingExitCount()`
- `IsOurStrategyExitOrder(...)`
- `IsOrderInCurrentSession(...)`
- `ReconcileCompatibleBrokerCoverageForRecovery(...)`
- `LogCoverageState(...)`
- `workingStopPrice`
- `hasWorkingStop`
- `currentControllerStopPrice`
- `_coverageGraceUntil`
- `_stopFillLikelyUntilUtc`
- `_lastStopStateChangeAt`
- `_lastOcoResubmitAt`

### 4. Submission/Finalization Gate

The host must preserve a hard submission/finalization authority:

- `BeginAtomicFinalization(...)`
- `MaySubmitOrders(...)`
- `CancelAllWorkingOrders(...)`
- `EnqueueOrderCancellation(...)`
- `ProcessCancellationQueue()`
- `SafeCancelOrder(...)`
- `NullOrderReference(...)`
- `SetStateRestorationInProgress(...)`

Flags:

- `isFinalizingTrade`
- `suppressAllOrderSubmissions`
- `tradeOpen`
- `controllerStopPlaced`
- `_globalKillSwitch`
- `autoDisabled`
- `stateRestorationInProgress`

### 5. Persistence Symmetry

Before porting deeper runtime logic, the host needs:

- `ResetTradeState()`
- `SaveStrategyState()`
- `RestoreStrategyState()`

Durable/resettable fields must include:

- state file path and restore flags
- active trade and OCO identity
- entry/fill identity
- session risk blocks
- finalization flags
- retry and restart state

### 6. Runtime Snapshot/Diagnostic Surface

Before the external harness can observe `SecondLegAdvancedMESStrategy`, the host must expose a harness-aligned snapshot surface that can be built without activating live order authority.

The current scaffold seam is:

- `BuildRuntimeSnapshotScaffold(...)`
- `RuntimeSnapshot`
- `BuildRuntimeCoverageSnapshot()`
- `BuildRuntimeTradeIdentitySnapshot()`
- `BuildRuntimeFinalizationSnapshot()`

That snapshot/diagnostic surface should stay focused on the first runtime scenario families:

- false-flat restart suppression
- reconnect with compatible broker coverage
- terminate/live-protection preservation

The required observation truths are:

- strategy/account quantity and durable live-context sources
- entry-eligibility gating during restart/reconnect holds
- owned vs compatible protective coverage disposition
- trade identity continuity across fill/save/restore seams
- final-cancel-sweep and protective-intent counters

### 7. Runtime Harness Adapter Scaffold

Before the external harness repo can drive `SecondLegAdvancedMESStrategy`, the local repo needs a thin adapter scaffold that reuses the host-owned runtime truth instead of inventing a parallel model.

The current adapter seam is documented in:

- [Runtime_Harness_Adapter_Contract.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Runtime_Harness_Adapter_Contract.md>)

That adapter should stay limited to pack-1 scenario control:

- restart false-flat suppression
- reconnect with compatible broker coverage

The adapter should drive existing helpers such as:

- `SetReconnectObservationState(...)`
- `SetProtectiveCoverageDisposition(...)`
- `SetRecoveryHoldState(...)`
- `CapturePersistedTradeIdentity(...)`
- `HandleFlatRealtimeRestartScaffold()`
- `RefreshRuntimeSnapshot(...)`

The coverage snapshot override is adapter-only and must not become production order truth. `BuildRuntimeCoverageSnapshot()` may short-circuit to the harness override before normal order-maintenance inference when the first harness pack is driving coverage explicitly.

### 8. Runtime Harness Projection

Before the external harness can consume `SecondLegAdvancedMESStrategy` snapshot truth, the local repo should also expose a flat projection that mirrors the harness `RuntimeSnapshot` shape without referencing the harness assembly.

The current projection seam is documented in:

- [Runtime_Harness_Projection_Contract.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Runtime_Harness_Projection_Contract.md>)

That projection should stay purely observational and be built through:

- `BuildRuntimeHarnessSnapshotProjection(...)`
- `HarnessProjectedSnapshot`

The required projection truths are:

- root runtime gating fields already present on `RuntimeSnapshot`
- flattened coverage counters and disposition
- flattened trade-identity continuity fields
- flattened finalization and intent-counter fields

This keeps the external harness integration path explicit while preserving the local `RuntimeSnapshotScaffold` as the single host-owned source of diagnostic truth.

## Lowest-Risk Initial Shims

These can be stubbed or omitted in the first runtime import:

- RL emission hooks
- overlay publishing hooks

Protected log writers are not safe stub candidates once runtime imports begin.

## Middle Layer

The donor testing suite is the middle layer for this repo:

1. port low-coupling runtime contract tests first
2. use those tests to shape the host shell and scaffolds
3. only then deepen the runtime import

## Porting Rule

If the host satisfies:

`OnExecutionUpdate -> tradeManager -> exitCtl -> coverage -> flatten/cancel -> SaveStrategyState/ResetTradeState`

then the rest of the donor runtime will port much more cleanly.

## Intentionally Deferred Beyond M1

The following items stay out of the first code port on purpose:

- `ExitController`, `Coverage`, `StopRisk`, and `Safety`
- adapted `ExecutionEvents`
- donor `Persistence`
- protected-log identity and reporting wiring
- overlay or RL emission hooks
- short-side symmetry, relative-volume filters, higher-timeframe alignment, and trade-management tuning
