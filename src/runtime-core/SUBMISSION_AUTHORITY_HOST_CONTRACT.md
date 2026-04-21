# SubmissionAuthority Host Contract

This scaffold ports the donor strategy's submission/finalization shape into `src/runtime-core` without changing live strategy behavior yet.

Imported scaffold surface:

- `SubmissionAuthorityScaffold.BeginAtomicFinalization(...)`
- `SubmissionAuthorityScaffold.MaySubmitOrders(...)`
- donor-style protective-context tokens
- donor-style stale-latch release logic for protective stop submissions

Required host-shell wiring before activation:

- Strategy state mapping into `SubmissionAuthorityState`
- `isFinalizingTrade`
- `suppressAllOrderSubmissions`
- `tradeOpen`
- `controllerStopPlaced`
- `autoDisabled`
- `_globalKillSwitch`
- stop in-flight / pending flags and timestamps
- atomic live quantity (`Position.Quantity`)
- the strategy's retry-correlation map

- Host actions on `SubmissionAuthorityHostContract`
- `CancelAllWorkingChildrenAndWait("[FINALIZE]")`
- `PrintOrderHealthSummary("TradeComplete")`
- `CancelAllWorkingOrders(reason, AllOrders)`
- `WriteDebugLog(...)`
- `WriteRiskLog(...)`
- `EmitOncePer(...)`
- `StopSubmitCooldownMsCurrent()`
- `NowEt()` and `Stamp(...)`

TODO seams left intentionally:

- No guess was made for the sibling repo's cancel-scope enum; the scaffold carries its own `SubmissionAuthorityCancellationScope`.
- No guess was made for how the sibling host wants to expose the retry-correlation map.
- No attempt was made to wire strategy partial methods or replace existing host-shell stubs outside `src/runtime-core`.

End-to-end trace for side-effect review:

- Entry points in `src/strategy` remain unchanged.
- The new scaffold lives only under `src/runtime-core`.
- No existing log format, order-routing path, timing constant, or `OnExecutionUpdate` path was edited.
- Until the host binds `SubmissionAuthorityScaffold` explicitly, runtime behavior stays unchanged.
