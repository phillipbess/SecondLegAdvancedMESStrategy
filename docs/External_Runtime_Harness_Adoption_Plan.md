# External Runtime Harness Adoption Plan

## Purpose

This document maps the external runtime harness into the `SecondLegAdvancedMESStrategy` build plan.

The harness is the middle validation layer between:

- fast source-level contract tests in this repo
- full NinjaTrader Playback / SIM validation

It is the right place to prove restart, reconnect, coverage, orphan-adoption, flatten, and protective-order recovery behavior once the new strategy runtime shell is stable enough.

## Canonical Harness

Canonical docs in the main repo:

- [External_Runtime_Harness_Guide.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/docs/Technical_Documentation/External_Runtime_Harness_Guide.md:1>)

External harness repo:

- [C:\dev\mancini-worktrees\mancini-runtime-tests](</C:/dev/mancini-worktrees/mancini-runtime-tests>)
- [README.md](</C:/dev/mancini-worktrees/mancini-runtime-tests/README.md:1>)
- [docs/PROJECT_OVERVIEW.md](</C:/dev/mancini-worktrees/mancini-runtime-tests/docs/PROJECT_OVERVIEW.md:1>)
- [tools/run_runtime_tests.ps1](</C:/dev/mancini-worktrees/mancini-runtime-tests/tools/run_runtime_tests.ps1:1>)

## Harness Shape

The current harness already has the exact structure we want to reuse conceptually:

- `Host`
  - [StrategyRuntimeHost.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Host/StrategyRuntimeHost.cs:1>)
  - manual clock, runtime snapshot, lifecycle/control toggles, trace recording
- `Fakes`
  - fake account/order/position objects under [Fakes](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Fakes>)
- `Scenarios`
  - scenario definitions and executable cases under [Scenarios](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios>)
- `Assertions`
  - reusable invariant assertions in [RuntimeAssertions.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Assertions/RuntimeAssertions.cs:1>)
- `Evidence`
  - `*.verified.txt` trace snapshots plus repo-level evidence rollups

This confirms the intended middle-layer contract:

1. prove event-sequence behavior in a deterministic host
2. review traces and assertions together
3. then use Playback as the final truth gate

## Reuse Strategy For SecondLegAdvanced

`SecondLegAdvancedMESStrategy` should not fork the Mancini harness immediately.

Instead, use a staged adoption path:

1. keep source-level contract tests in this repo as the first safety layer
2. stabilize the new runtime shell and host-field contract
3. introduce a sibling harness lane only after the new runtime shell exposes the same core truths the harness needs
4. keep Playback as the final acceptance gate for runtime-sensitive behavior

The main thing to reuse is not the donor scenarios blindly. It is the harness pattern:

- host model with explicit runtime state
- invariant-style assertions
- semantic anchors back to mirrored production code
- scenario catalog tied to concrete bug families
- evidence snapshots that make behavioral diffs reviewable

## Host Contract Needed Before Harness Adoption

Before the new strategy can use a harness lane, its runtime shell should expose stable equivalents for:

- trade identity
  - `currentTradeID`
  - `_activeTradeId`
  - `_currentEntryTag`
- lifecycle/finalization
  - `_exitState`
  - `isFinalizingTrade`
  - `suppressAllOrderSubmissions`
  - `stateRestorationInProgress`
- protection state
  - `workingStopPrice`
  - `hasWorkingStop`
  - `currentControllerStopPrice`
  - `_coverageGraceUntil`
  - `_stopFillLikelyUntilUtc`
  - `_lastStopStateChangeAt`
- order-maintenance state
  - `_workingOrders`
  - cancel queue / retry state
- session-control state
  - session block / neutralization flags
  - late-fill flatten intent

The current repo is approaching this boundary through:

- [SecondLegAdvancedMESStrategy.RuntimeHost.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/strategy/SecondLegAdvancedMESStrategy.RuntimeHost.cs:1>)
- [SecondLegAdvancedMESStrategy.OrderMaintenanceScaffold.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedMESStrategy.OrderMaintenanceScaffold.cs:1>)
- [SecondLegAdvancedMESStrategy.SubmissionAuthorityScaffold.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedMESStrategy.SubmissionAuthorityScaffold.cs:1>)
- [SecondLegAdvancedRuntimeControlLane.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/src/runtime-core/SecondLegAdvancedRuntimeControlLane.cs:1>)

## Runtime Snapshot/Diagnostic Surface

The next adoption seam is a harness-aligned snapshot/diagnostic surface, not direct harness integration.

That surface should currently be built through:

- `BuildRuntimeSnapshotScaffold(...)`
- `RuntimeSnapshot`
- `BuildRuntimeHarnessSnapshotProjection(...)`
- `HarnessProjectedSnapshot`

The first snapshot version should answer the three highest-value runtime questions:

1. false-flat restart suppression
2. reconnect with compatible broker coverage
3. terminate/live-protection preservation

That means the snapshot must expose:

- `EntryAllowed`, `DurableLiveTradeContext`, `FlatResetDeferred`, and `StateRestorationInProgress`
- `DurableLiveTradeContextSources`, including `account-position` when reconnect context is live
- protective coverage disposition and owned/compatible counts
- trade identity continuity fields and tracked fill quantity
- final-cancel-sweep disposition plus protective/cancel intent counters

The local control/observation bridge for that first pack is:

- [Runtime_Harness_Adapter_Contract.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Runtime_Harness_Adapter_Contract.md>)
- [Runtime_Harness_Projection_Contract.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/Runtime_Harness_Projection_Contract.md>)

The current external handoff artifacts now live in the harness repo:

- [SECONDLEGADVANCED_ADOPTION.md](</C:/dev/mancini-worktrees/mancini-runtime-tests/docs/SECONDLEGADVANCED_ADOPTION.md>)
- [secondlegadvanced-pack1-bridge.json](</C:/dev/mancini-worktrees/mancini-runtime-tests/manifests/secondlegadvanced-pack1-bridge.json>)
- [RuntimeExternalBridgeManifestTests.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/RuntimeExternalBridgeManifestTests.cs>)
- [RuntimeExternalBridgeProjectionTests.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/RuntimeExternalBridgeProjectionTests.cs>)

## First Runtime Scenario Set

The first `SecondLegAdvanced` runtime scenarios should avoid entry-thesis complexity and focus only on reused runtime safety lanes.

Recommended first three scenarios:

1. Restart with account-live / strategy-flat mismatch does not false-flat and does not reopen entry eligibility.
2. Reconnect with compatible broker protective coverage does not submit duplicate protection.
3. Terminate or final sweep with live context preserves the only protective stop.

The current local pack definition for those scenarios lives in:

- [First_Runtime_Harness_Scenario_Pack.md](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/First_Runtime_Harness_Scenario_Pack.md>)

Recommended next three after that:

1. Flatten reject / stale-child cleanup does not reopen entry eligibility early.
2. Protective replace pending or rejected lanes suppress orphan-adoption until the hold clears.
3. Session-control late fill converges to flatten and blocks re-entry.

These are the highest-value runtime families because they are largely independent of the second-leg entry thesis.

## Scenario Mapping To Harness Donors

Best donor cases to model first:

- [RunRestartWithAccountLongStrategyQtyZero](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs:7>)
- [RunReconnectWithBrokerStopAmbiguousOwnership](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs:46>)
- [RunTerminateWithLiveContextPreservesProtectiveStop](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs:22>)
- [RunFlattenRejectStaleChildCleanup](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeScenarioCases.cs:244>)

The semantic-anchor pattern should also be copied, not improvised:

- [RuntimeSemanticAnchors.cs](</C:/dev/mancini-worktrees/mancini-runtime-tests/src/Mancini.RuntimeTests/Scenarios/RuntimeSemanticAnchors.cs:1>)

That anchor model is especially important because the new strategy will reuse runtime behavior while changing the entry brain completely.

## Recommended Adoption Order

1. Finish source-level scaffold contracts for host shell, persistence symmetry, exit authority, and execution identity.
2. Tighten `RuntimeHost` until donor-style runtime fields and transitions stop moving around.
3. Add a small runtime-harness design doc or sibling harness repo plan for `SecondLegAdvanced`.
4. Start with only three runtime scenarios and trace snapshots.
5. Add semantic anchors that point back to the new repo's runtime partials.
6. Only after the harness is stable should entry-thesis-specific scenarios be added.

## What Not To Do

- do not try to run the existing Mancini harness directly against `SecondLegAdvancedMESStrategy` yet
- do not port every donor scenario before the new runtime shell is stable
- do not bring entry-theory details into the first runtime-harness scenarios
- do not treat the harness as a Playback replacement

## Current Local Note

The external harness repo cloned successfully and its structure is valid, but the local wrapper run currently fails on a mirror-integrity precondition because one mirrored file is not read-only. That is a harness repo hygiene issue, not a blocker for this strategy repo's planning.
