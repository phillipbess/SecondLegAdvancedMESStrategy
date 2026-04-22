# Golden Log Sequences

These are not exact full log dumps. They are the healthy narrative shapes we expect to
see during Playback.

Use them as comparison guides.

## 1. Healthy Long Setup To Managed Trade

Expected story:

1. `Patterns_`
   trend valid
2. `Patterns_`
   ATR regime valid
3. `Patterns_`
   impulse valid
4. `Patterns_`
   leg 1 tracked
5. `Patterns_`
   separation valid
6. `Patterns_`
   leg 2 candidate valid
7. `Patterns_`
   signal valid
8. `Patterns_`
   planned entry armed
9. `Trades_`
   entry submitted
10. `Trades_`
    entry filled
11. `Risk_`
    `STOP_SUBMIT`
12. `Risk_`
    `STOP_ACK` / `STOP_CONFIRMED`
13. `Risk_`
    trail/stop change sequence if price advances
14. `Trades_`
    flatten/close
15. `Patterns_` or `Debug_`
    setup reset

## 2. Healthy Short Setup To Managed Trade

Expected story is the mirrored version of the long path:

1. bearish trend and impulse
2. two-leg upward pullback
3. short signal bar
4. sell-stop armed and filled
5. stop protection appears
6. trail tightens coherently
7. trade closes and state resets

## 3. Healthy Trigger Expiry

Expected story:

1. `Patterns_`
   valid setup reaches armed state
2. no fill occurs within allowed trigger bars
3. `Patterns_`
   expiry reason recorded
4. `Risk_` / `Debug_`
   cancel or cleanup is visible
5. no residual working entry remains
6. setup returns to a searchable/reset state

## 4. Healthy Protective Replace

Expected story:

1. `Risk_`
   an existing stop is no longer acceptable because of side/qty/state
2. `Risk_`
   replace begin / submit path appears
3. `Risk_`
   old stop is retired only when replacement is genuinely present or recovery takes over
4. `Risk_`
   coverage returns to healthy
5. no double-stop residue remains

## 5. Healthy Flatten Before Close

Expected story:

1. `Risk_`
   flatten request / enqueue
2. `Risk_`
   child handling / cancel / flatten submit
3. `Trades_`
   flatten lifecycle is visible
4. `Debug_`
   finalization/reset breadcrumbs
5. no armed setup or live protection residue remains after flat

## 6. Healthy Reconnect / Recovery

Expected story:

1. `Debug_` / `Risk_`
   reconnect grace starts
2. `Risk_`
   orphan/adopt/coverage checks appear
3. `Risk_`
   `ADOPT`, `ORPHAN_CHECK`, `ORPHAN_SWEEP`, or `COVERAGE_STATE` tell the recovery story
4. protection truth is rebuilt before restore hold clears
5. runtime returns to healthy state without duplicate exits

## What A Bad Sequence Looks Like

Playback should be treated as suspicious immediately if you see:

- `Trades_` fill without nearby `STOP_SUBMIT`
- repeated flatten submits for one event
- `Patterns_` signal/arm without enough setup story beforehand
- trail updates before protection is clearly live
- reset while orders or position are still active
