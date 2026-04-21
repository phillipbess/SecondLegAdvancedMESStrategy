# First Runtime Harness Scenario Pack

## Purpose

This is the first external runtime-harness pack for `SecondLegAdvancedMESStrategy`.

It proves reused Mancini runtime invariants under the new strategy host shell before any entry-edge scenarios are introduced.

This is a runtime-pack, not a strategy-edge-pack.

## Scope

The pack covers host-shell/runtime behavior only:

- restart false-flat suppression
- reconnect with compatible broker coverage
- terminate/live-protection preservation

It does not cover:

- second-leg entry thesis
- impulse/pullback detection
- structure-room math
- trade-management tuning
- flatten-reject/stale-child cleanup
- replace-failure restart holds
- session-control late fills

## Harness-Aligned Observation Surface

The pack depends on the harness-aligned observation surface exposed through:

- `BuildRuntimeSnapshotScaffold(...)`
- `RuntimeSnapshot`
- `BuildRuntimeHarnessSnapshotProjection(...)`
- `HarnessProjectedSnapshot`

The local control/observation entrypoint for pack 1 is the harness adapter scaffold documented in:

- [Runtime_Harness_Adapter_Contract.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Runtime_Harness_Adapter_Contract.md>)
- [Runtime_Harness_Projection_Contract.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Runtime_Harness_Projection_Contract.md>)

Pack 1 may source coverage truth from an adapter-only coverage snapshot override. That override replaces working-order inference only for harness-driven pack-1 scenarios.

### Projection Consumption Rule

External harness scenarios should consume the flat projection seam:

- `HarnessPack1Snapshot`
- `HarnessProjectedSnapshot`
- `BuildRuntimeHarnessSnapshotProjection(...)`

They should not assert directly against nested local snapshot internals when a flat harness-shaped field already exists.

The minimum pack-1 observation truths are:

- `EntryAllowed`
- `DurableLiveTradeContext`
- `DurableLiveTradeContextSources`
- `FlatResetDeferred`
- `StateRestorationInProgress`
- `OrphanedOrdersScanComplete`
- `OrphanAdoptionPending`
- `ProtectiveCoverageAmbiguous`
- `ProtectiveCoverageDisposition`
- `OwnedProtectiveOrderCount`
- `CompatibleProtectiveOrderCount`
- `WorkingProtectiveOrderCount`
- `WorkingProtectiveOrderQuantityTotal`
- `CurrentTradeId`
- `ActiveTradeId`
- `PersistedTradeId`
- `CountedTradeSessionId`
- `TradeCount`
- `TrackedFillQuantity`
- `ProtectiveSubmitRequestCount`
- `CancelRequestCount`
- `ProtectiveCancelRequestCount`
- `PreservedProtectiveOrderCount`
- `FinalCancelSweepDisposition`

## Scenario Pack

### 1. Restart_WithAccountLong_StrategyQtyZero_DoesNotDeclareFlat

Donor source:

- [RestartScenarios.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RestartScenarios.cs>)
- [RuntimeScenarioCases.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs>)

Required snapshot fields:

- `EntryAllowed`
- `DurableLiveTradeContext`
- `FlatResetDeferred`
- `StateRestorationInProgress`
- `DurableLiveTradeContextSources`
- `OrphanAdoptionPending`
- `ProtectiveSubmitRequestCount`
- `StrategyQuantity`
- `AccountQuantity`

Expected tokens / intents / decision lanes:

- `defer-flat-reset`
- `release-flat-reset`
- `hold-for-orphan-adoption`
- `account-position`
- `covered-owned`
- `covered-compatible`
- `orphan-adoption-pending`
- `submit-protective-order`

Recommended local adapter sequence:

- `HarnessSetAccountPosition(...)`
- `HarnessSetStrategyPosition(...)`
- `HarnessEnterRealtime(...)`
- `HarnessSetFlatResetDeferred(...)`
- `HarnessEvaluateReconnectCoverage(...)`

### 2. Restart_WithCompatibleBrokerCoverage_DelayedScan_AfterTradeRestore

Donor source:

- [RestartScenarios.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RestartScenarios.cs>)
- [RuntimeScenarioCases.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs>)

Required snapshot fields:

- `EntryAllowed`
- `StateRestorationInProgress`
- `OrphanAdoptionPending`
- `ProtectiveCoverageAmbiguous`
- `ProtectiveCoverageDisposition`
- `OwnedProtectiveOrderCount`
- `CompatibleProtectiveOrderCount`
- `WorkingProtectiveOrderCount`
- `WorkingProtectiveOrderQuantityTotal`
- `ProtectiveSubmitRequestCount`
- `CurrentTradeId`
- `ActiveTradeId`
- `PersistedTradeId`
- `CountedTradeSessionId`
- `TradeCount`
- `TrackedFillQuantity`

Expected tokens / intents / decision lanes:

- `hold-for-coverage-ambiguity`
- `compatible-unattributed`
- `covered-compatible`
- `covered-owned`
- `orphanedOrdersScanComplete=False`
- `orphanedOrdersScanComplete=True`
- `evaluate reconnect coverage`

Negative expectation:

- no `submit-protective-order` while compatible or ambiguous coverage exists

Recommended local adapter sequence:

- `HarnessStartTrade(...)`
- `HarnessRecordPrimaryEntryFill(...)`
- `HarnessPersistTradeState(...)`
- `HarnessRestoreProjectedSnapshot(...)`
- `HarnessSetCoverageSnapshot(...)`
- `HarnessSetProtectiveCoverageAmbiguous(...)`
- `HarnessSetOrphanedOrdersScanComplete(...)`
- `HarnessEvaluateReconnectCoverage(...)`

### 3. Terminate_WithLiveContext_DoesNotCancelOnlyProtectiveStop

Donor source:

- [RestartScenarios.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RestartScenarios.cs>)
- [RuntimeScenarioCases.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs>)

Required snapshot fields:

- `DurableLiveTradeContext`
- `WorkingProtectiveOrderCount`
- `FinalCancelSweepDisposition`
- `CancelRequestCount`
- `ProtectiveCancelRequestCount`
- `PreservedProtectiveOrderCount`

Expected tokens / intents / decision lanes:

- `final-cancel-sweep`
- `cancel-order`
- `skip`
- `cancel`
- `terminate preserved protective coverage`

Optional negative baseline:

- `Terminate_WithoutLiveContext_CancelsProtectiveStop`

Recommended local adapter sequence:

- `HarnessSetAccountPosition(...)`
- `HarnessSetCoverageSnapshot(...)`
- `HarnessResetIntentCounters(...)`
- `HarnessTerminate(...)`

## Semantic Anchors

### A_FALSE_FLAT_AND_RECONNECT_GATING

Intent:

- block false-flat restart and keep entry closed until live context and reconnect truth converge

Host fields:

- `EntryAllowed`
- `DurableLiveTradeContext`
- `DurableLiveTradeContextSources`
- `FlatResetDeferred`
- `StateRestorationInProgress`
- `OrphanedOrdersScanComplete`
- `OrphanAdoptionPending`
- `StrategyQuantity`
- `AccountQuantity`
- `ProtectiveSubmitRequestCount`

Local seams:

- [SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs>)
  - `BuildRuntimeSnapshotScaffold(...)`
  - `BuildDurableLiveTradeContextSources()`
  - `ComputeRuntimeEntryAllowed(...)`
- [SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs>)
  - `SetReconnectObservationState(...)`
  - `SetRecoveryHoldState(...)`
- [SecondLegAdvancedMESStrategy.ExecutionIdentityScaffold.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.ExecutionIdentityScaffold.cs>)
  - `HandleFlatRealtimeRestartScaffold()`

### A_COVERAGE_AND_ADOPTION_TRUTH

Intent:

- distinguish owned vs compatible vs ambiguous protection and suppress duplicate protective submission while reconnect truth settles

Host fields:

- `ProtectiveCoverageAmbiguous`
- `ProtectiveCoverageDisposition`
- `OwnedProtectiveOrderCount`
- `CompatibleProtectiveOrderCount`
- `WorkingProtectiveOrderCount`
- `WorkingProtectiveOrderQuantityTotal`
- `EntryAllowed`
- `OrphanAdoptionPending`
- `CurrentTradeId`
- `ActiveTradeId`
- `PersistedTradeId`
- `CountedTradeSessionId`
- `TradeCount`
- `TrackedFillQuantity`
- `ProtectiveSubmitRequestCount`

Local seams:

- [SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs>)
  - `BuildRuntimeCoverageSnapshot()`
  - `BuildRuntimeTradeIdentitySnapshot()`
  - `BuildRuntimeSnapshotTokens(...)`
- [SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs>)
  - `SetProtectiveCoverageDisposition(...)`
  - `SetReconnectObservationState(...)`
- [SecondLegAdvancedMESStrategy.RuntimeHost.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeHost.cs>)
  - `ReconcileCompatibleBrokerCoverageForRecovery(...)`
  - `AnchorEntryExecutionState(...)`
  - `CountTradeOnFirstEntryFill(...)`
  - `SaveStrategyState()`
  - `RestoreStrategyState()`

### A_TERMINATE_AND_FLATTEN_AUTHORITY

Intent:

- preserve the only protective stop under durable live context, and cancel stale protection only when truly flat

Host fields:

- `DurableLiveTradeContext`
- `WorkingProtectiveOrderCount`
- `FinalCancelSweepDisposition`
- `CancelRequestCount`
- `ProtectiveCancelRequestCount`
- `PreservedProtectiveOrderCount`

Local seams:

- [SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeSnapshotScaffold.cs>)
  - `BuildRuntimeFinalizationSnapshot()`
  - `BuildRuntimeSnapshotTokens(...)`
- [SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeScenarioState.cs>)
  - `SetFinalCancelSweepDisposition(...)`
  - `SetRuntimeIntentCounters(...)`
- [SecondLegAdvancedMESStrategy.RuntimeHost.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeHost.cs>)
  - `CancelAllWorkingOrders(...)`
  - `EnqueueOrderCancellation(...)`
  - `SafeCancelOrder(...)`
  - `TriggerFlatten(...)`

## Pack-1 Tokens To Freeze

- `durable-live-context`
- `flat-reset-deferred`
- `reconnect-grace`
- `entry-allowed`
- `orphan-adoption-pending`
- `compatible-unattributed`
- `covered-owned`
- `covered-compatible`
- `final-cancel-sweep:skip`
- `final-cancel-sweep:cancel`

## Next Pack

Pack 2 should add:

- flatten-reject / stale-child cleanup
- protective replace failure and restart holds
- session-control late-fill convergence
