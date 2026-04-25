# Structure Attribution Research

Date: 2026-04-25

This run tested whether the broad second-entry idea becomes more real when the
entry is close to a specific liquidity/structure family.

## Setup

- Data/harness: QuantConnect LEAN cloud, MES continuous futures proxy.
- Window: 2021-04-01 through 2026-04-01.
- Entry mode: `VideoSecondEntryLite`.
- Base parameters: long/short side split, `liteMinImpulseAtr=0.75`,
  `liteMaxPullbackRetracement=0.95`, `liteMaxLeg2Retracement=0.95`,
  `liteMinSignalClosePct=0.55`, `profitTargetR=2.0`, `touchProbeR=1.0`,
  `maxOutcomeBars=24`, `liteBlockedHours=13`.
- Structure filter: new `liteAllowedStructures` QC parameter.

## Main Read

The broad second-entry strategy is weak. The first stable positive pocket is:

```text
Long only
Nearest structure = SWING_H
Room to structure <= 0.25R
5-minute bars
```

Five-year result:

| Variant | Trades | Trades/Month | Net R | Avg R |
| --- | ---: | ---: | ---: | ---: |
| Long SWING_H room <= 0.25R | 180 | 3.0 | +58.68 | +0.33 |
| Long SWING_H room <= 0.50R | 287 | 4.8 | +76.11 | +0.27 |
| Long SWING_H room <= 1.00R | 397 | 6.6 | +72.27 | +0.18 |
| Long SWING_H unbounded room | 462 | 7.7 | +55.70 | +0.12 |

The edge degrades as room widens. That supports the idea that the edge is not
"second entries broadly"; it is specifically a long continuation/retest close
to recent swing-high liquidity.

## Structure Split

| Side | Structure | Room Max | Trades | Trades/Month | Net R | Avg R |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Long | SWING_H | 0.25R | 180 | 3.0 | +58.68 | +0.33 |
| Long | PDH | 0.50R | 41 | 0.7 | +11.06 | +0.27 |
| Long | PDH + SWING_H | 0.50R | 323 | 5.4 | +87.84 | +0.27 |
| Long | SWING_H | 0.50R | 287 | 4.8 | +76.11 | +0.27 |
| Short | ORL | 0.50R | 55 | 0.9 | +10.14 | +0.18 |
| Short | PDL | 0.50R | 30 | 0.5 | +3.81 | +0.13 |
| Long | ORH | 0.50R | 89 | 1.5 | -3.96 | -0.04 |
| Short | SWING_L | 0.50R | 155 | 2.6 | -11.57 | -0.07 |

ORH being negative is important. It says this is not simply "any overhead
structure is good." The useful behavior is concentrated around recent swing
highs, with PDH possibly helpful but too sparse to stand alone.

## Yearly Stability

Top candidate: long `SWING_H`, room <= 0.25R.

| Period | Trades | Trades/Month | Net R | Avg R |
| --- | ---: | ---: | ---: | ---: |
| 2021-2022 | 39 | 3.3 | +7.80 | +0.20 |
| 2022-2023 | 33 | 2.8 | +9.81 | +0.30 |
| 2023-2024 | 36 | 3.0 | +15.50 | +0.43 |
| 2024-2025 | 31 | 2.6 | +15.60 | +0.50 |
| 2025-2026 | 38 | 3.2 | +12.97 | +0.34 |

This is the strongest evidence in the research so far. Every yearly slice is
positive, with similar trade counts. The issue is frequency, not stability.

## Faster-Bar Frequency Test

The council recommendation was to test faster bars instead of loosening the
structure room. That would preserve the market thesis while attempting to raise
trade count.

| Bar Size | Trigger Window | Hold Window | Trades | Trades/Month | Net R | Avg R |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3m | 15m | 120m | 259 | 4.3 | -13.38 | -0.05 |
| 2m | 16m | 120m | 427 | 7.1 | +13.69 | +0.03 |

Faster bars did not rescue frequency. They created more trades, but expectancy
collapsed. The 5-minute bar appears to be part of the useful signal.

## Liquidity-Takeout Variant

Final council idea: if the edge is actually the swing high acting as a magnet,
do not merely enter near the swing high. Require the stop-entry to take the
level by moving the virtual entry to `SWING_H + 1 tick` when that is above the
normal signal-bar entry.

This was added as an optional QC research switch:

```text
liteUseStructureBreakEntry=true
```

Five-year result:

| Variant | Trades | Trades/Month | Net R | Avg R |
| --- | ---: | ---: | ---: | ---: |
| Long SWING_H room <= 0.25R, normal entry | 180 | 3.0 | +58.68 | +0.33 |
| Long SWING_H room <= 0.25R, liquidity-break entry | 100 | 1.7 | +43.03 | +0.43 |

Yearly liquidity-break result:

| Period | Trades | Trades/Month | Net R | Avg R |
| --- | ---: | ---: | ---: | ---: |
| 2021-2022 | 17 | 1.4 | +10.93 | +0.64 |
| 2022-2023 | 25 | 2.1 | +4.66 | +0.19 |
| 2023-2024 | 19 | 1.6 | +7.95 | +0.42 |
| 2024-2025 | 20 | 1.7 | +12.52 | +0.63 |
| 2025-2026 | 17 | 1.4 | +8.97 | +0.53 |

This supports the market read. The cleaner the test becomes around taking
nearby buy-side liquidity, the better the average R gets. The cost is frequency.

## Verdict

Do not keep optimizing the broad second-entry system. It has not shown a real
edge.

Keep only the narrow branch if we want a selective setup:

```text
Long, 5-minute, second-entry continuation, very near recent swing-high liquidity.
Best quality form: require the entry to take SWING_H by 1 tick.
```

This is not a 20-trades/month strategy in its current form. It is closer to an
A+ discretionary-style setup at roughly 1.7-3.0 trades/month. The next honest
decision is whether that is useful enough to keep, or whether to kill the branch
because it cannot meet the frequency goal without diluting the edge.

## Artifacts

- `structure_attribution_summary.csv`
- `yearly_swing_h_0p25/yearly_swing_h_0p25_summary.csv`
- `faster_bars/faster_bars_summary.csv`
- `liquidity_takeout/liquidity_takeout_summary.csv`
- `liquidity_takeout/yearly/liquidity_takeout_yearly_summary.csv`
