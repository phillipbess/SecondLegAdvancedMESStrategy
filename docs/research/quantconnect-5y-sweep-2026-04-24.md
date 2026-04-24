# QuantConnect 5-Year Parameter Sweep - 2026-04-24

## Purpose

Move past the one-year feasibility pass and test whether the SecondLeg entry thesis has a stable research shape over a broader MES sample.

This is still an orderless research harness:

- no live orders
- no real fills
- one virtual position at a time
- stop-first when a 5-minute bar contains both stop and target
- static stop / 2R target / timeout after 24 bars or end of RTH

## Baseline Window

- instrument: MES continuous futures
- date range: `2021-04-24` through `2026-04-23`
- total 5-minute bars: `114,201`
- RTH 5-minute bars: `97,951`
- QuantConnect project id: `30595690`

## Baseline Result

Backtest:

- name: `SecondLeg MES 5y baseline`
- id: `b68aa58944a6d95c0ff93b6a36ef5339`
- URL: `https://www.quantconnect.com/project/30595690/b68aa58944a6d95c0ff93b6a36ef5339`

Stats:

- trend bars: `46,963`
- impulses: `2,757`
- leg 1 pullbacks: `1,532`
- separations: `759`
- leg 2 candidates: `275`
- armed signals: `43`
- triggered signals: `37`
- 2R wins: `10`
- stops: `23`
- timeouts: `4`
- net R: `-2.32R`
- average R: `-0.06R`

Read:

- The exact current baseline is active but not yet attractive.
- The one-year `+0.19R` result did not hold across the broader 5-year window.
- This is not a reason to abandon the idea, but it is a reason to stop treating the current defaults as sacred.

## Focused Sweep Results

All rows use the same 5-year window and the same conservative virtual outcome model.

| Variant | Triggered | 2R Wins | Stops | Timeouts | Net R | Avg R | Read |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Baseline: leg2 `0.80`, room `1.00` | 37 | 10 | 23 | 4 | -2.32 | -0.06 | Active, not attractive |
| Room `0.50` | 60 | 14 | 38 | 8 | -8.76 | -0.15 | More trades, worse quality |
| Room `0.00` | 93 | 21 | 55 | 17 | -12.85 | -0.14 | Removing structure-room hurts |
| Impulse `1.00` | 36 | 10 | 22 | 4 | -1.32 | -0.04 | Slightly less bad, not meaningful |
| Impulse `1.50` | 39 | 10 | 23 | 6 | -1.84 | -0.05 | Stricter impulse alone does not solve it |
| Leg2 momentum `0.55` | 23 | 9 | 13 | 1 | +4.89 | +0.21 | Positive, fewer/cleaner trades |
| Leg2 momentum `0.65` | 27 | 10 | 15 | 2 | +6.25 | +0.23 | Best single-parameter result |
| Leg2 momentum `0.75` | 34 | 9 | 21 | 4 | -2.32 | -0.07 | Edge fades before baseline |
| Leg2 momentum `1.00` | 38 | 11 | 23 | 4 | -0.32 | -0.01 | Near flat, not better |
| Leg2 `0.65` + impulse `1.00` | 27 | 10 | 15 | 2 | +6.25 | +0.23 | Same as leg2 `0.65`; impulse loosen did not matter |
| Leg2 `0.65` + room `0.50` | 46 | 14 | 26 | 6 | +3.82 | +0.08 | More trades but diluted |
| Min retracement `0.15` | 47 | 13 | 29 | 5 | -1.03 | -0.02 | Looser shallow pullbacks do not help |
| Max retracement `0.75` | 43 | 10 | 25 | 8 | -1.96 | -0.05 | Deeper pullbacks do not help |

Raw CLI outputs are saved under:

- `research/quantconnect/results/`

## Research Read

The useful clue is very specific:

- The strategy does not improve by simply allowing more trades.
- Loosening structure-room increases quantity but worsens quality.
- Loosening pullback depth does not help.
- The strongest improvement came from requiring leg 2 to be more controlled.

That aligns with the original market idea:

trend -> impulse -> controlled two-legged correction -> failed second countertrend attempt -> continuation

When leg 2 is too forceful, it may no longer be a controlled correction. It may be early reversal pressure.

## Current Best Candidate

The best current research candidate is:

- keep impulse baseline near `1.25 ATR`
- keep structure-room baseline at `1.00R`
- keep pullback retracement baseline at `0.236` to `0.618`
- tighten `SecondLegMaxMomentumRatio` from `0.80` toward `0.55` to `0.65`

Do not update production defaults solely from this pass. The next step is to confirm this shape with:

- year-by-year breakdown
- long/short split
- time-of-day split
- trade list export
- comparison against NT8 playback logs

## Next Step

Add structured virtual trade export to the QC harness so every triggered setup can be reviewed, grouped, and compared to NinjaTrader playback:

- entry time
- side
- entry/stop/1R/2R
- impulse ATR multiple
- pullback retracement
- leg-2 momentum ratio
- room-to-structure
- outcome
- realized R

This will let us see whether `leg2=0.65` is genuinely cleaner or just lucky over 27 trades.
