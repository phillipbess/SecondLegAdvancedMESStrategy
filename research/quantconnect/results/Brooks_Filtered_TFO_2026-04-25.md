# Brooks Filtered TFO - 2026-04-25

Goal: test whether the label-trait scan improves the Brooks trend-from-open pullback idea.

The trait scan suggested the broad first-hour continuation idea was too weak, but that stronger first-hour moves and strong-bar dominance had some lift. We added these optional filters to `BrooksTrendPullbackResearch.cs`:

- `brooksMinStrongDominance`
- `brooksMaxOpeningCounterStrongBars`
- `brooksMinMeasureCloseLocation`

These filters preserve the old behavior by default, but allow focused filtered-TFO tests.

## Matrix

Script: `research/quantconnect/scripts/run_brooks_filtered_tfo_matrix.ps1`

Window: `2021-04-24` through `2026-04-23`

| Variant | Net R | Monthly R | Trades | Trades/mo | Avg R | Touch 1R | Win 1.5R |
|---|---:|---:|---:|---:|---:|---:|---:|
| Both, move 1.5 ATR, dominance 2, close loc 0.65 | 71.93 | 1.20 | 291 | 4.85 | 0.25 | 173 | 142 |
| Both, move 1.5 ATR, dominance 3, close loc 0.70 | 66.11 | 1.10 | 229 | 3.82 | 0.29 | 140 | 116 |
| Both, move 2.0 ATR, dominance 3, close loc 0.70 | 52.61 | 0.88 | 200 | 3.33 | 0.26 | 121 | 99 |
| Long only, move 1.5 ATR, dominance 3, close loc 0.70 | 48.68 | 0.81 | 138 | 2.30 | 0.35 | 89 | 73 |

## Read

This is the first Brooks-derived result that looks directionally improved.

Compared with the earlier best BrooksTFO run (`57.97R`, `0.97R/month`, `0.22R/trade`), the filtered tests improve quality:

- Better best net: `71.93R`
- Better best average trade: `0.35R`
- Cleaner long-only pocket

But it still does **not** meet the target.

The business target is about `6R-8R/month`. The best filtered result is about `1.2R/month`. At `0.35R/trade`, we would need roughly `17-23` high-quality trades per month to reach the target, but this family only produces about `2-5` trades per month.

## Current Verdict

Filtered BrooksTFO is a real improvement, but not a standalone strategy.

It may be useful as one setup inside a broader Renaissance Capital research basket, but it should not be treated as the answer by itself.

## Best Next Action

Do not keep tightening this single setup. Tightening improved average trade but reduced frequency, and frequency is now the main blocker.

Next research should either:

- use filtered BrooksTFO as one positive component in a multi-setup ES day-trading basket, or
- search for a higher-frequency complementary setup with a different market dynamic.

Candidate complementary dynamics:

- afternoon continuation after a filtered trend-from-open morning,
- failed reversal after a strong first-hour trend that traps countertrend traders,
- volatility expansion out of a tight midday range,
- first pullback after a liquidity sweep at prior-day high/low.
