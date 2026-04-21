# Runtime Harness Adapter Contract

## Purpose

Define the thin local adapter surface that makes `SecondLegAdvancedMESStrategy` drivable by the first external runtime-harness scenarios without activating live NT8 order authority.

This adapter is intentionally narrow. It exists only to bridge the first scenario pack:

- restart false-flat suppression
- reconnect with compatible broker coverage

## Design Rule

The adapter should reuse existing host-owned state and snapshot helpers instead of inventing a second runtime model.

That means adapter methods should drive:

- reconnect observation state
- trade identity scaffolds
- state-restoration gating
- snapshot refreshes
- harness projection refreshes indirectly through the host snapshot

The adapter should not:

- submit or cancel live orders
- bypass `ExitController`
- replace real `OnExecutionUpdate`

## Required Local Adapter Surface

The current scaffold surface is:

- `HarnessSnapshot`
- `HarnessPack1Snapshot`
- `HarnessEnterHistorical(...)`
- `HarnessEnterRealtime(...)`
- `HarnessSetAccountPosition(...)`
- `HarnessSetStrategyPosition(...)`
- `HarnessSetOrphanedOrdersScanComplete(...)`
- `HarnessSetProtectiveCoverageAmbiguous(...)`
- `HarnessSetFlatResetDeferred(...)`
- `HarnessSetCoverageSnapshot(...)`
- `HarnessClearCoverageSnapshotOverride(...)`
- `HarnessEvaluateReconnectCoverage(...)`
- `HarnessStartTrade(...)`
- `HarnessRecordPrimaryEntryFill(...)`
- `HarnessPersistTradeState(...)`
- `HarnessRestorePersistedTradeState(...)`
- `HarnessSetStateRestorationInProgress(...)`
- `HarnessResetIntentCounters(...)`
- `HarnessSetIntentCounters(...)`
- `HarnessSetFinalCancelSweep(...)`
- `HarnessTerminate(...)`
- `HarnessRestoreProjectedSnapshot(...)`

## Reused Local Seams

The adapter should route through existing host-owned helpers:

- `SetReconnectObservationState(...)`
- `SetProtectiveCoverageDisposition(...)`
- `SetRecoveryHoldState(...)`
- `SetStateRestorationInProgress(...)`
- `SetRuntimeIntentCounters(...)`
- `SetFinalCancelSweepDisposition(...)`
- `CapturePersistedTradeIdentity(...)`
- `EnsureActiveTradeIdFromCurrentTradeId(...)`
- `TrackCumulativeFillQuantity(...)`
- `HandleFlatRealtimeRestartScaffold()`
- `BuildRuntimeCoverageSnapshot()`
- `BuildProtectiveCoverageDisposition(...)`
- `RefreshRuntimeSnapshot(...)`
- `BuildRuntimeHarnessSnapshotProjection(...)`

For pack 1, coverage truth may come from an adapter-only coverage snapshot override instead of synthetic NT8 `Order` objects. That is intentional and lower-risk than fabricating unmanaged order state before the real external harness integration exists.

Coverage counts and coverage ambiguity should stay separable:

- `HarnessSetCoverageSnapshot(...)` controls owned/compatible/protective count truth
- `HarnessSetProtectiveCoverageAmbiguous(...)` controls ambiguous ownership truth
- `HarnessClearCoverageSnapshotOverride(...)` removes harness-driven coverage truth and returns coverage inference to the normal order-maintenance path

## First-Pack Expectations

The adapter only needs to make these first-pack truths observable and driveable:

- account-live / strategy-flat restart gaps
- reconnect grace / orphan scan completion
- compatible-vs-owned protective coverage disposition
- persisted trade identity restoration
- restart entry-eligibility blocking

## Next Step

Once this adapter surface and the first scenario pack are stable, the next move is to implement the first two actual external harness cases against it:

1. `Restart_WithAccountLong_StrategyQtyZero_DoesNotDeclareFlat`
2. `Restart_WithCompatibleBrokerCoverage_DelayedScan_AfterTradeRestore`
3. `Terminate_WithLiveContext_DoesNotCancelOnlyProtectiveStop`
