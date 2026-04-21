# Entry Brain V1 Contract

## Purpose

This document is the canonical `v1` contract for the `SecondLegAdvancedMESStrategy`
entry engine.

It defines the setup logic only. It does not define the full runtime shell.

The runtime shell remains donor-owned and is responsible for:

- order lifecycle
- fill truth
- protective stop authority
- Mancini-style simple trail
- flatten/session-close behavior
- risk/session accounting
- persistence/recovery

The entry brain is only responsible for deciding:

- whether a setup exists
- which side it is on
- where the entry should be
- where the initial stop should be
- how much size is allowed
- when the setup expires or resets

## Core Thesis

Trade only when price is already trending, forms a real impulse, pulls back in two
controlled countertrend legs, fails to reverse, and then breaks back in the trend
direction with enough room before obvious nearby structure.

This strategy is not trying to predict reversals.

It is trying to enter continuation after a failed countertrend attempt.

## V1 Scope

Included in strict `v1`:

- trend context using `EMA200` and `EMA50` slope
- ATR regime sanity filter
- hard-gated impulse qualification
- two-legged pullback state machine
- stop-entry trigger from a valid signal bar
- fixed-dollar sizing from initial stop distance
- stop-width rejection
- minimal structure-room gate
- session and hard risk guardrails
- long and short support

Excluded from strict `v1`:

- higher-timeframe alignment
- relative volume
- opening bias
- VWAP or VWAP bands
- overnight structure
- prior close structure
- round-number filters
- volume profile
- partial exits
- breakeven logic
- fixed profit targets
- alternate exit modes

## Data And Timing Assumptions

- Primary chart only
- No second data series in `v1`
- Runtime shell runs `OnEachTick`
- Entry setup logic remains closed-bar driven through the closed-bar adapter
- Protection, cancellation, flatten progression, and simple-trail maintenance may stay tick-responsive
- Opening-range structure is only active after the opening range is complete

## Runtime Boundary

The entry brain produces one `PlannedEntry` and hands it to the runtime shell.

The entry brain must not:

- place exits directly
- own trail movement
- flatten trades
- count realized `R`
- own recovery state
- bypass donor order/fill authority

## Filter Ordering

The filter order is fixed:

1. Session and hard risk gates
2. Trend context
3. ATR regime
4. Impulse qualification
5. Pullback leg 1
6. Separation / bounce
7. Pullback leg 2
8. Corrective-leg validation
9. Signal bar validation
10. Entry / stop / size planning
11. Structure-room gate
12. Armed-entry expiry / cancellation

Implementation must not reorder these gates.

## State Machine

The entry engine uses these states:

1. `Blocked`
2. `SeekingBias`
3. `SeekingImpulse`
4. `TrackingPullbackLeg1`
5. `TrackingSeparation`
6. `TrackingPullbackLeg2`
7. `WaitingForSignalBar`
8. `WaitingForTrigger`
9. `ManagingTrade`
10. `Reset`

Meaning:

- `Blocked`
  Session, flatten window, cooldown, or risk rails block new setups.
- `SeekingBias`
  Trend and ATR regime are checked and side is chosen.
- `SeekingImpulse`
  Wait for a valid impulse in the active direction.
- `TrackingPullbackLeg1`
  First countertrend leg is forming.
- `TrackingSeparation`
  A real bounce/interruption between leg 1 and leg 2 is required.
- `TrackingPullbackLeg2`
  Second countertrend leg is forming.
- `WaitingForSignalBar`
  Leg 2 is a valid candidate and the reversal bar is awaited.
- `WaitingForTrigger`
  Signal bar is frozen and a stop-entry waits to trigger.
- `ManagingTrade`
  Runtime shell owns the live trade.
- `Reset`
  Clear setup-local state and return to `SeekingBias`.

Important:

Leg 2 is only a candidate until the stop-entry trigger breaks.

The code should not mark leg 2 as "failed" before the trigger actually confirms.

## Trend Context Rules

Long trend context:

- `Close[0] > EMA200`
- `EMA50 slope per bar >= SlopeMinAtrPctPerBar * ATR`

Short trend context:

- `Close[0] < EMA200`
- `EMA50 slope per bar <= -SlopeMinAtrPctPerBar * ATR`

ATR-normalized slope:

- `emaSlopePerBar = (EMA50[0] - EMA50[SlopeLookbackBars]) / SlopeLookbackBars`
- `atrNormalizedSlope = emaSlopePerBar / ATR`

## ATR Regime Rules

Use:

- `atrRegime = ATR(AtrPeriod)[0] / SMA(ATR(AtrPeriod), AtrRegimeLookback)[0]`

Pass only if:

- `MinAtrRegimeRatio <= atrRegime <= MaxAtrRegimeRatio`

This is a sanity filter, not an edge filter.

## Impulse Definition

Impulse uses the last `ImpulseBars` bars only.

For long:

- `impulseLow = lowest low of last ImpulseBars bars`
- `impulseHigh = highest high of last ImpulseBars bars`
- `impulseMove = impulseHigh - impulseLow`
- `impulseMove >= MinImpulseAtrMultiple * ATR`
- at least `MinStrongBars` of the last `ImpulseBars` bars are bullish
- strong bullish bar means:
  - `Close > Open`
  - `abs(Close - Open) / max(High - Low, TickSize) >= StrongBodyPct`
- final impulse bar must:
  - `Close[0] > Open[0]`
  - `Close[0] > EMA50`

For short:

- `impulseHigh = highest high of last ImpulseBars bars`
- `impulseLow = lowest low of last ImpulseBars bars`
- `impulseMove = impulseHigh - impulseLow`
- `impulseMove >= MinImpulseAtrMultiple * ATR`
- at least `MinStrongBars` of the last `ImpulseBars` bars are bearish
- strong bearish bar means:
  - `Close < Open`
  - `abs(Close - Open) / max(High - Low, TickSize) >= StrongBodyPct`
- final impulse bar must:
  - `Close[0] < Open[0]`
  - `Close[0] < EMA50`

Persist only:

- `impulseStartBar`
- `impulseEndBar`
- `impulseHigh`
- `impulseLow`
- `impulseMove`
- `impulseDirection`

No score-only partial credit in `v1`.

## Pullback Leg 1 Rules

After a valid impulse, price must begin a countertrend pullback.

For long:

- leg 1 is a meaningful move down from the impulse high
- track `pullbackLeg1Low`

For short:

- leg 1 is a meaningful move up from the impulse low
- track `pullbackLeg1High`

Leg 1 remains valid only if:

- total pullback duration does not exceed `MaxPullbackBars`
- retracement has not exceeded `MaxPullbackRetracement`

## Separation Rules

Separation must be a real interruption between leg 1 and leg 2.

For long, after leg 1 down, require at least one bar that:

- `Low[0] >= currentPullbackLow`
- `Close[0] > Close[1]`
- `High[0] > High[1]`

For short, after leg 1 up, require at least one bar that:

- `High[0] <= currentPullbackHigh`
- `Close[0] < Close[1]`
- `Low[0] < Low[1]`

If price simply continues the same pullback without a valid separation bar, it is
still one continuous leg, not two legs.

## Pullback Leg 2 Rules

Leg 2 begins only after a valid separation.

For long:

- a renewed countertrend push begins when:
  - `Low[0] < Low[1]` or `Close[0] < Close[1]`
- track:
  - `leg2Low = lowest low after separation`

For short:

- a renewed countertrend push begins when:
  - `High[0] > High[1]` or `Close[0] > Close[1]`
- track:
  - `leg2High = highest high after separation`

## Retracement Rules

For long:

- `retracement = (impulseHigh - pullbackLow) / max(impulseMove, TickSize)`

For short:

- `retracement = (pullbackHigh - impulseLow) / max(impulseMove, TickSize)`

Pass only if:

- `MinPullbackRetracement <= retracement <= MaxPullbackRetracement`

## Corrective-Leg Validation

Leg 2 must remain corrective, not become a reversal impulse.

Momentum is defined mechanically:

- `impulseMomentum = impulseMove / ImpulseBars`

For long:

- `leg2CountertrendMove = separationHigh - leg2Low`
- `leg2Bars = number of bars in leg 2`
- `leg2Momentum = leg2CountertrendMove / max(leg2Bars, 1)`

For short:

- `leg2CountertrendMove = leg2High - separationLow`
- `leg2Bars = number of bars in leg 2`
- `leg2Momentum = leg2CountertrendMove / max(leg2Bars, 1)`

Pass only if:

- `leg2Momentum <= SecondLegMaxMomentumRatio * impulseMomentum`
- the corrective bar still stays on the correct side of `EMA200`:
  - long: `Close[0] > EMA200`
  - short: `Close[0] < EMA200`
- do not reintroduce `EMA50` slope gating here

## Signal Bar Qualification

For long:

- signal bar forms after a valid leg-2 candidate
- signal bar must be later than the bar that completed the leg-2 candidate
- `Close[0] >= midpoint of bar`
- bar must not make a new deeper leg-2 low

For short:

- signal bar forms after a valid leg-2 candidate
- signal bar must be later than the bar that completed the leg-2 candidate
- `Close[0] <= midpoint of bar`
- bar must not make a new deeper leg-2 high

Midpoint rule:

- `midpoint = Low[0] + (High[0] - Low[0]) * 0.5`

## Planned Entry Contract

Once a valid signal bar exists, compute:

- `Bias`
- `SignalName`
- `EntryPrice`
- `InitialStopPrice`
- `Quantity`
- `ExpiryBar`
- `Reason`

For long:

- `EntryPrice = signalBarHigh + EntryOffsetTicks * TickSize`
- `InitialStopPrice = leg2Low - StopBufferTicks * TickSize`

For short:

- `EntryPrice = signalBarLow - EntryOffsetTicks * TickSize`
- `InitialStopPrice = leg2High + StopBufferTicks * TickSize`

## Risk And Sizing Rules

Compute:

- `stopDistance = abs(EntryPrice - InitialStopPrice)`
- reject if `stopDistance / ATR > MaxStopAtrMultiple`
- `riskPerContract = stopDistance * PointValue`
- `Quantity = floor(RiskPerTrade / riskPerContract)`

Reject if:

- `riskPerContract <= 0`
- `Quantity <= 0`

## Structure-Room Gate

Use only these structure families in `v1`:

- prior day high / low
- opening range high / low after opening range completion
- recent swing high / low

Exclude:

- overnight high / low
- prior close
- VWAP
- round numbers
- weekly levels
- volume profile

For long:

- nearest resistance above entry from:
  - `PDH`
  - `ORH` if complete
  - recent swing high
- `room = nearestResistance - EntryPrice`

For short:

- nearest support below entry from:
  - `PDL`
  - `ORL` if complete
  - recent swing low
- `room = EntryPrice - nearestSupport`

Pass only if:

- `room >= MinRoomToStructureR * stopDistance`

If no valid candidate exists, structure is treated as clear.

## Entry Expiry And Cancellation

Armed entries expire if not triggered within `MaxTriggerBars`.

Cancel and reset if any of these occur before fill:

- expiry reached
- trend context fails
- ATR regime fails
- setup becomes too deep
- setup exceeds `MaxPullbackBars`
- opposite-side setup invalidates the current setup
- session/flatten rules block fresh entries

## Session And Hard Guardrails

The entry brain must honor these gates before arming a setup:

- session window valid
- not past `LastEntryTime`
- not in flatten-only window
- max trades per session not exceeded
- max consecutive losses not exceeded
- daily loss limit not exceeded
- cooldown-after-loss not active

## Long / Short Symmetry

Every major rule in `v1` must mirror cleanly by side:

- trend qualification
- impulse qualification
- separation
- retracement
- momentum ratio
- signal bar
- entry
- initial stop
- structure-room

No separate short runtime is allowed.

## Hard-Coded Semantics

These semantics are fixed in `v1` and should not be optimized:

- impulse uses the last `3` bars
- strong bar means body at least `50%` of range
- impulse requires at least `2` strong directional bars
- separation requires a real bounce bar, not a wick-only condition
- leg 2 is only a candidate until trigger confirms
- signal bar must close on the correct half of its range
- structure set is minimal:
  - `PDH/PDL`
  - `ORH/ORL`
  - recent swing high/low

## Configurable Thresholds

These remain configurable in `v1`:

- `SlopeMinAtrPctPerBar`
- `MinImpulseAtrMultiple`
- `MinPullbackRetracement`
- `MaxPullbackRetracement`
- `SecondLegMaxMomentumRatio`
- `MaxStopAtrMultiple`
- `MinRoomToStructureR`
- `SwingLookbackBars`
- session times
- hard risk guardrails

## Required Deterministic Tests

The following strategy-logic families must exist and stay green:

- trend context
- ATR regime and session guards
- impulse qualification
- pullback state machine
- entry qualification
- armed-entry lifecycle
- long golden case
- short golden case
- invalid continuous pullback case
- stale / expired trigger case

## Canonical Block Reasons

At minimum, diagnostics should distinguish:

- `SessionBlocked`
- `FlattenWindow`
- `TrendInvalid`
- `AtrRegimeInvalid`
- `ImpulseInvalid`
- `PullbackTooDeep`
- `PullbackTooLong`
- `SecondLegTooStrong`
- `SignalInvalid`
- `StopTooWide`
- `RiskTooSmall`
- `StructureRoom`
- `EntryExpired`
- `OppositeSignal`

## Final V1 Rule Stack

1. Session valid
2. Trend valid
3. ATR regime valid
4. Impulse valid
5. Pullback leg 1 valid
6. Separation valid
7. Pullback leg 2 valid
8. Leg 2 remains corrective
9. Signal bar appears
10. Stop entry triggers
11. Room to structure is sufficient
12. Risk is acceptable

Everything else is deferred until this base version proves itself.
