# Runtime Harness Projection Contract

## Purpose

Define the flat runtime-snapshot projection that `SecondLegAdvancedMESStrategy` can expose to the external harness without taking a compile-time dependency on the harness repo.

This is intentionally a read-only bridge surface. It should not:

- submit or cancel orders
- replace the host-owned runtime snapshot
- introduce a second source of runtime truth

## Design Rule

The projection must flatten the existing local runtime snapshot scaffold into the same field shape used by `Mancini.RuntimeTests.Host.RuntimeSnapshot`.

That means the local strategy keeps:

- `BuildRuntimeSnapshotScaffold(...)`
- `RuntimeSnapshot`

and the projection adds:

- `BuildRuntimeHarnessSnapshotProjection(...)`
- `HarnessProjectedSnapshot`

## Required Projection Surface

The current projection scaffold should expose:

- `UtcNow`
- `StrategyQuantity`
- `AccountQuantity`
- `DurableLiveTradeContext`
- `DurableLiveTradeContextSources`
- `ProtectiveCoverageAmbiguous`
- `OrphanAdoptionPending`
- `ProtectiveReplacePending`
- `ProtectiveReplaceFailurePending`
- `ProtectiveReplaceRejected`
- `AdoptDeferPending`
- `AdoptDeferReason`
- `SessionControlActive`
- `SessionControlReason`
- `SessionNeutralizationPending`
- `FlattenRejectPending`
- `StaleChildCleanupPending`
- `FlatResetDeferred`
- `StateRestorationInProgress`
- `OrphanedOrdersScanComplete`
- `EntryAllowed`
- `WorkingOrderCount`
- `WorkingPrimaryEntryOrderCount`
- `WorkingProtectiveOrderCount`
- `WorkingProtectiveOrderQuantityTotal`
- `OwnedProtectiveOrderCount`
- `CompatibleProtectiveOrderCount`
- `ProtectiveCoverageDisposition`
- `FinalCancelSweepDisposition`
- `CancelRequestCount`
- `EntryCancelRequestCount`
- `ProtectiveCancelRequestCount`
- `ProtectiveSubmitRequestCount`
- `FlattenRequestCount`
- `PreservedProtectiveOrderCount`
- `CurrentTradeId`
- `ActiveTradeId`
- `PersistedTradeId`
- `CountedTradeSessionId`
- `TradeCount`
- `TrackedFillQuantity`
- `Tokens`

## Mapping Rule

`BuildRuntimeHarnessSnapshotProjection(...)` should only flatten existing local truth:

- scaffold root fields map directly
- `Coverage.*` fields flatten into the harness-compatible coverage counters
- `TradeIdentity.*` fields flatten into trade-identity fields
- `Finalization.*` fields flatten into finalization and intent-counter fields

The projection should copy list values into new `List<string>` instances so the bridge remains observational and side-effect free.

## Field Mapping Table

| Projection field group | Local source |
| --- | --- |
| `UtcNow`, `StrategyQuantity`, `AccountQuantity`, `DurableLiveTradeContext`, `DurableLiveTradeContextSources`, `ProtectiveCoverageAmbiguous`, `OrphanAdoptionPending`, `ProtectiveReplacePending`, `ProtectiveReplaceFailurePending`, `ProtectiveReplaceRejected`, `AdoptDeferPending`, `AdoptDeferReason`, `SessionControlActive`, `SessionControlReason`, `SessionNeutralizationPending`, `FlatResetDeferred`, `StateRestorationInProgress`, `OrphanedOrdersScanComplete`, `EntryAllowed`, `Tokens` | `RuntimeSnapshotScaffold` root fields |
| `WorkingOrderCount`, `WorkingPrimaryEntryOrderCount`, `WorkingProtectiveOrderCount`, `WorkingProtectiveOrderQuantityTotal`, `OwnedProtectiveOrderCount`, `CompatibleProtectiveOrderCount`, `ProtectiveCoverageDisposition` | `RuntimeSnapshotScaffold.Coverage` |
| `CurrentTradeId`, `ActiveTradeId`, `PersistedTradeId`, `CountedTradeSessionId`, `TradeCount`, `TrackedFillQuantity` | `RuntimeSnapshotScaffold.TradeIdentity` |
| `FlattenRejectPending`, `StaleChildCleanupPending`, `FinalCancelSweepDisposition`, `CancelRequestCount`, `EntryCancelRequestCount`, `ProtectiveCancelRequestCount`, `ProtectiveSubmitRequestCount`, `FlattenRequestCount`, `PreservedProtectiveOrderCount` | `RuntimeSnapshotScaffold.Finalization` |

## Extension Rule

New external scenario fields must land in the nested local snapshot first, and only then be added to the flat projection.

That preserves the layering:

1. host-owned runtime truth
2. local nested diagnostic scaffold
3. flat external-harness projection

## Pack-1 Use

This projection exists to unlock the first external harness pack:

1. `Restart_WithAccountLong_StrategyQtyZero_DoesNotDeclareFlat`
2. `Restart_WithCompatibleBrokerCoverage_DelayedScan_AfterTradeRestore`
3. `Terminate_WithLiveContext_DoesNotCancelOnlyProtectiveStop`

Those scenarios need a flat, harness-shaped view of:

- false-flat suppression
- reconnect coverage truth
- live-context terminate preservation

before the full `SecondLegAdvanced` entry thesis is wired into the external harness.

The external handoff artifacts that consume this projection live in the harness repo:

- [SECONDLEGADVANCED_ADOPTION.md](</C:/dev/mancini-worktrees/mancini-runtime-tests/docs/SECONDLEGADVANCED_ADOPTION.md>)
- [secondlegadvanced-pack1-bridge.json](</C:/dev/mancini-worktrees/mancini-runtime-tests/manifests/secondlegadvanced-pack1-bridge.json>)
- [RuntimeExternalBridgeManifestTests.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/RuntimeExternalBridgeManifestTests.cs>)
- [RuntimeExternalBridgeProjectionTests.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/RuntimeExternalBridgeProjectionTests.cs>)
