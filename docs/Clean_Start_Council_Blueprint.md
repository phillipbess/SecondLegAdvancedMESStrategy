# Clean-Start Council Blueprint

## Compliance Gate

`elegant, nt8 best practice aligned, event driven, no downstream side effects (verified with code traced end to end)`

- Elegant/minimal scope: build a genuinely new `SecondLegAdvanced` strategy architecture and treat `ManciniMESStrategy` as a runtime donor, not as a patch target.
- NT8 best-practice alignment: keep live order truth in `OnOrderUpdate`, `OnExecutionUpdate`, and `OnPositionUpdate`; keep signal logic pure and side-effect free.
- Event-driven design: use the Mancini execution model as the blueprint:
  - `Calculate.OnEachTick`
  - `OnMarketData(...)` for tick responsiveness
  - primary-series `OnBarUpdate()` for closed-bar-gated signal evaluation via the closed-bar adapter
  - execution and position callbacks as the sole source of live order/fill truth
- No downstream side effects:
  - do not change Mancini logs, timing constants, `ExitController`, or pipeline contracts
  - keep `SecondLegAdvancedMESStrategy` as a sibling strategy

## Forbidden-Area Check

- Do not modify:
  - Mancini log formats or filenames
  - Mancini `ExitController`
  - Mancini `OnExecutionUpdate` behavior
  - Mancini timing constants or emergency windows
- Do not create a second ad hoc order-management system.
- Do not make the second-leg signal layer responsible for live order lifecycle.
- Do not start with `AddDataSeries(...)` as the realism path.

## Donor Blueprint

The clean-start runtime model should mirror these Mancini seams:

- `Calculate = Calculate.OnEachTick` in [ManciniMESStrategy.StateLifecycle.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.StateLifecycle.cs:26>)
- `IsUnmanaged = true` in [ManciniMESStrategy.StateLifecycle.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.StateLifecycle.cs:32>)
- No secondary data series by default in [ManciniMESStrategy.StateLifecycle.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.StateLifecycle.cs:121>)
- Tick-responsive `OnMarketData(...)` in [ManciniMESStrategy.MarketFlow.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.MarketFlow.cs:36>)
- Primary-series `OnBarUpdate()` in [ManciniMESStrategy.MarketFlow.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.MarketFlow.cs:265>)
- `OnExecutionUpdate(...)` as authoritative fill truth in [ManciniMESStrategy.ExecutionEvents.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.ExecutionEvents.cs:20>)
- `OnPositionUpdate(...)` for flat/position lifecycle truth in [ManciniMESStrategy.ExecutionEvents.cs](</C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/ManciniMESStrategy/src/ManciniMESStrategy.ExecutionEvents.cs:994>)

## Corrected Scope

The user clarified the intended surface:

- do **not** rewrite the runtime shell from zero
- reuse as much of the Mancini runtime/order-management blueprint as possible
- replace the Adam-specific entry system
- adapt only the plumbing needed for a new second-leg strategy that can operate long and short

That means the real delta is:

- new entry brain
- new entry-state machine
- new structure / regime / participation qualification
- directional plumbing for short-capable behavior
- only the minimum runtime adaptation needed to support those changes

## Architecture Boundary

### Reuse From Mancini

Reuse or port with minimal behavioral change:

- submission authority
- control lane
- order maintenance
- trade manager
- protective-stop orchestration
- flatten authority and escalation rules
- cancel/retry behavior
- restart/recovery concepts
- runtime snapshot/evidence concepts

The strong default should be:

- keep the Mancini runtime shell
- do not replace order authority
- do not replace flatten/protective ownership
- do not replace event-driven fill truth

### Rebuild Fresh

Author new strategy-specific code for:

- trend context
- impulse scoring
- explicit two-leg pullback state machine
- signal-bar qualification
- room-to-structure qualification
- session/bias/participation filters
- second-leg trade plan generation
- mirrored short entry qualification and stop anchoring
- any direction-sensitive runtime plumbing that currently assumes long-only behavior

## End-to-End Code Trace

Target flow for the corrected scope:

1. `OnStateChange`
   - configure strategy mode, reuse the donor runtime shell, and bind second-leg properties/state
2. `OnMarketData`
   - maintain tick-responsive safety/trailing/session checks
3. `OnBarUpdate`
   - update market context
   - run the second-leg brain only on the closed primary-bar pass in place of the Adam entry pattern logic
   - emit a neutral `TradePlan`
4. donor runtime host consumes `TradePlan`
   - submit or decline based on runtime authority and guardrails
5. `OnOrderUpdate` / `OnExecutionUpdate`
   - own all live order/fill truth
   - ensure protective coverage
   - escalate to flatten when needed
6. `OnPositionUpdate`
   - finalize flat state, reset, and restart-safe lifecycle truth
7. persistence / diagnostics
   - serialize runtime truth plus minimal second-leg state

This keeps the signal layer pure while the runtime shell remains event-driven and authoritative.

## Build Sequence

1. Freeze the donor runtime contract and keep it authoritative.
2. Define the replacement entry surface:
   - trend context
   - impulse scoring
   - two-leg pullback state machine
   - signal-bar / stop-entry planning
3. Adapt only the plumbing needed for short-capable behavior.
4. Bridge the new brain to the existing runtime shell with one narrow `TradePlan` handoff.
5. Keep one management mode only:
   - initial stop
   - simple trail
6. Finish persistence/reset symmetry for any new strategy state.
7. Build the proof ladder:
   - contract tests
   - pure logic tests
   - playback evidence

## Validation Plan

### Source / Contract

- runtime authority tests
- protection / flatten tests
- persistence shape tests
- logging / evidence contract tests where needed

### Pure Logic

- trend context
- impulse scoring
- two-leg pullback state machine
- entry qualification
- regime/session guardrails
- golden bar-sequence cases

### Playback

- compile cleanly in NT8
- trend day
- choppy day
- high-volatility day
- verify:
  - no duplicate entries
  - protective stop always exists after fill
  - simple trail only tightens
  - session-close cancels/flattens correctly
  - guardrails actually block entries

## Go / No-Go Verdict

Go.

The clean-start council recommendation is:

Use the Mancini runtime blueprint as the base shell, replace the entry brain, and change only the plumbing required for second-leg semantics and long/short symmetry.

That is the safest way to do the original ask correctly.
