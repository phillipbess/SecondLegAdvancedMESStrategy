# Parity-First V1 Rebuild Plan

## Compliance Gate

`elegant, nt8 best practice aligned, event driven, no downstream side effects (verified with code traced end to end)`

- Runtime parity first: reuse the Mancini runtime shell as faithfully as possible.
- Strategy delta only where intended:
  - replace the Adam entry model with the second-leg model
  - add the minimum plumbing required for short support
- Trade management:
  - simple trail only
  - no fixed target
  - no alternate exit modes in `v1`

## Locked Scope

The user selected the **lean v1** path.

The canonical entry-engine definition for this plan is:

- `docs/Entry_Brain_V1_Contract.md`

That means `v1` includes only the filters and features that are core to the edge thesis and runtime safety:

- Mancini-style runtime shell
- simple trail only
- long and short support
- trend context
- ATR regime
- hard-gated impulse qualification
- explicit two-legged pullback state machine
- signal-bar stop-entry logic
- fixed-dollar risk sizing
- stop-width rejection
- minimal room-to-structure filter
- session guardrails
- cooldown / max-trades / daily-loss guardrails

These stay out of strict `v1` unless needed later:

- HTF alignment
- RVOL
- opening-bias filter
- VWAP structure
- richer structure families beyond the minimum set
- extra trail modes
- breakeven logic
- target logic

## Forbidden-Area Check

- Do not modify Mancini itself.
- Do not invent a second runtime shell.
- Do not add a second data series in the first parity pass.
- Do not mix entry logic into runtime/order-management partials.
- Do not add target-based exits.

## End-to-End Code Trace

Target `v1` flow:

1. `OnStateChange`
   - Mancini-parity lifecycle shape
   - `Calculate.OnEachTick`
   - no `AddDataSeries(...)` in first pass

2. `OnMarketData`
   - donor-style intrabar lane for:
     - simple trail progression
     - cancel queue pumping
     - flatten/protection progression

3. `OnBarUpdate`
   - closed-bar-gated second-leg entry brain only via `IsClosedPrimaryBarPass()`:
     - context
      - impulse
      - pullback leg 1
      - separation
      - pullback leg 2
      - signal validation
      - planned entry / stop / qty / expiry

4. `PlannedEntry`
   - small handoff object from the entry brain to the donor runtime shell

5. `OnOrderUpdate` / `OnExecutionUpdate` / `OnPositionUpdate`
   - donor shell remains the sole authority for:
     - order truth
     - fills
     - protection
     - flatten
     - reset
     - persistence/recovery

## Donor Reuse Scope

Reuse or transplant as closely as possible:

- execution-event shell
- submission authority
- control lane
- order maintenance
- trade manager
- protective-stop / flatten authority
- persistence/recovery model
- runtime snapshot / harness shape

Rewrite fresh:

- entry state machine
- trend context
- impulse qualification
- pullback leg logic
- signal-bar validation
- structure-room math
- side-aware entry planning for long and short

## Lean V1 Entry Brain

State machine:

1. `Blocked`
2. `SeekingBias`
3. `SeekingImpulse`
4. `TrackingPullback1`
5. `TrackingSeparation`
6. `TrackingPullback2`
7. `ValidatingSignal`
8. `Armed`
9. `ManagingTrade`
10. `Reset`

Filter order:

1. Session and hard risk gates
2. Trend context
3. ATR regime
4. Impulse qualification
5. Pullback leg 1
6. Bounce/separation
7. Pullback leg 2 failure check
8. Signal-bar validation
9. Entry / stop / qty planning
10. Structure room
11. Armed-entry expiry / cancel logic

## Minimum Short Plumbing

Do not build a separate short runtime. Only generalize:

- bias / direction enums
- entry submission side
- stop placement side
- structure-room side
- trail high-water / low-water logic
- risk / favorable-excursion math
- execution anchoring by side

## Validation Plan

### Contract / Runtime

- donor runtime parity contracts
- simple-trail only contracts
- short-capable plumbing contracts
- session-close / flatten contracts
- persistence/recovery shape contracts

### Pure Logic

- trend context
- impulse scoring
- pullback state machine
- entry qualification
- regime/session guardrails
- golden long/short cases

### Playback

- long smoke path:
  - submit
  - fill
  - protective stop
  - simple trail
  - flatten
- short smoke path:
  - submit
  - fill
  - protective stop
  - simple trail
  - flatten
- session-close behavior
- no duplicate entries
- stop always present after fill

## Build Sequence

1. Freeze the parity target:
   - Mancini runtime shell stays authoritative.
2. Rebuild the strategy around that shell, not around the current hybrid.
3. Introduce a narrow `PlannedEntry` seam.
4. Replace the entry brain with the lean second-leg model.
5. Add the minimum side-aware plumbing for shorts.
6. Keep only simple trail after fill.
7. Validate parity in contracts, then Playback.

## Go / No-Go Verdict

Go.

The agreed `v1` is:

**Mancini runtime parity + new second-leg entry brain + short-capable plumbing + simple trail only.**
