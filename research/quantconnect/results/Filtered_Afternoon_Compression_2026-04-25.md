# Filtered Afternoon Compression - 2026-04-25

Goal: test a complementary setup to filtered BrooksTFO.

Hypothesis:

- Strong/clean first-hour auction establishes direction.
- Midday compresses into a box.
- Afternoon breakout in the morning direction can produce continuation.

We added optional first-hour quality filters to `AfternoonCompressionResearch.cs`:

- `afternoonCompressionMinStrongDominance`
- `afternoonCompressionMaxOpeningCounterStrongBars`
- `afternoonCompressionMinMeasureCloseLocation`
- `afternoonCompressionStrongBodyPct`
- `afternoonCompressionStrongClosePct`

Defaults preserve the old behavior.

## Matrix

Script: `research/quantconnect/scripts/run_filtered_afternoon_compression_matrix.ps1`

Window: `2021-04-24` through `2026-04-23`

| Variant | Net R | Monthly R | Trades | Trades/mo | Avg R | Touch 1R | Wins |
|---|---:|---:|---:|---:|---:|---:|---:|
| Both, move 1.5 ATR, dominance 2, loc 0.65, box 5 ATR, stop 1 ATR, target 2R | 33.02 | 0.55 | 88 | 1.47 | 0.38 | 48 | 38 |
| Both, move 1.0 ATR, dominance 2, loc 0.65, box 4 ATR, stop 0.75 ATR, target 1.5R | 5.50 | 0.09 | 67 | 1.12 | 0.08 | 37 | 29 |
| Both, move 1.5 ATR, dominance 3, loc 0.70, box 4 ATR, stop 0.75 ATR, target 1.5R | 5.50 | 0.09 | 42 | 0.70 | 0.13 | 25 | 19 |
| Long, move 1.5 ATR, dominance 3, loc 0.70, box 4 ATR, stop 0.75 ATR, target 1.5R | 4.50 | 0.08 | 28 | 0.47 | 0.16 | 17 | 13 |

## Read

The 2R / wider-box variant is directionally interesting but still too small:

- `0.38R/trade` is the best quality so far among the afternoon-compression tests.
- Frequency is only about `1.5 trades/month`.
- Monthly contribution is only about `0.55R/month`.

This is **not** the complementary engine we need.

It could be a small component in a basket, but it does not solve the goal. Combined with filtered BrooksTFO, the two best components are still roughly:

- Filtered BrooksTFO: about `1.20R/month`
- Filtered afternoon compression: about `0.55R/month`
- Combined rough ceiling before overlap: about `1.75R/month`

That is still far below the `6R-8R/month` target.

## Verdict

Keep as a possible research component, but do not continue optimizing this branch now.

The next search should target a higher-frequency dynamic, not another low-frequency filtered continuation variant.

Best candidates:

- Opening range failure with stronger context and delayed confirmation.
- Prior-day high/low sweep plus reclaim with a fast target.
- First pullback after liquidity sweep, not plain breakout.
- VWAP/mean-reversion failure after trapped opening-drive traders.
