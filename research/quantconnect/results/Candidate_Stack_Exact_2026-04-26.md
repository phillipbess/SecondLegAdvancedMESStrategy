# Candidate Stack Exact Backtest - 2026-04-26

Goal: replace the rough stack estimate with an exact QuantConnect research mode that runs both candidates in one algorithm:

- `Opening Auction Room Filter`: 15m accepted auction, opening range 0.75-2.5 ATR, room <= 1R, both sides, first 90 minutes.
- `Afternoon Momentum Long 5m`: 60-minute morning measurement, long continuation late day, 0.5 ATR minimum morning move.
- Shared model: 1.5R target, 24-bar max hold, virtual R accounting.

This is now the benchmark price-only stack. It supersedes the rough estimate in `Opening_Auction_Room_Yearly_And_Stack_2026-04-26.md`.

## Results

| Period | Net R | R/Month | Avg R | Trades | Trades/Month | Wins | Stops | Timeouts | Touch 1R |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2021-2022 | 27.43 | 2.29 | 0.20 | 138 | 11.5 | 50 | 55 | 33 | 69 |
| 2022-2023 | 18.16 | 1.51 | 0.12 | 148 | 12.3 | 49 | 65 | 34 | 75 |
| 2023-2024 | 33.79 | 2.82 | 0.20 | 170 | 14.2 | 60 | 67 | 43 | 88 |
| 2024-2025 | 44.32 | 3.69 | 0.27 | 165 | 13.8 | 66 | 64 | 35 | 88 |
| 2025-2026 | 42.04 | 3.50 | 0.27 | 154 | 12.8 | 62 | 60 | 32 | 81 |
| Total | 165.75 | 2.76 | 0.21 | 775 | 12.9 | 287 | 311 | 177 | 401 |

Backtest IDs:

| Period | QuantConnect Backtest ID |
|---|---|
| 2021-2026 | `2cbc0093140ce26d6dfc2aaa1cf35c13` |
| 2021-2022 | `5d28b78e0751f117bb64363d4936674b` |
| 2022-2023 | `203da62986493b99fb9e7db5c5e7634d` |
| 2023-2024 | `354015797810c2857d4d44f924ab271a` |
| 2024-2025 | `403880d0b80002a9678da3863f2c4052` |
| 2025-2026 | `05a2cdd4e266b4168c8d27b192eb1a0e` |

## Read

This is the best price-only research result so far.

The important positives:

- Every yearly slice is positive.
- Trade frequency is much closer to the target than the earlier second-entry idea.
- The stack produces `165.75R` over five years, or `2.76R/month`.
- The best recent slices are stronger: `3.69R/month` in 2024-2025 and `3.50R/month` in 2025-2026.

The important limits:

- It is still below the business target of `6R-8R/month`.
- Average trade quality is only `0.21R`, so execution costs, slippage, and data differences matter.
- The stack averages `12.9 trades/month`, not the desired `20 trades/month`.

## Verdict

Keep this as the current benchmark, not the finish line.

The prior second-leg strategy idea did not produce enough edge. This broader Resonance Capital research direction is more promising because we now have a stable, positive, multi-year stack. But we still need a second independent engine, or a materially stronger confirmation variable, to reach the actual monthly R goal.

## Next Best Research Step

Do not keep doing small parameter nudges on this stack.

The next high-value test is a structurally different ingredient:

- Order-flow or tick-confirmed failed auction around `PDH`, `PDL`, `ONH`, `ONL`, `ORH`, and `ORL`.
- Or a separate afternoon pullback/continuation engine that does not overlap with the current late-day momentum definition.

The bar for adding a new engine should be clear: it should add at least `2R-3R/month` out-of-sample by yearly slice, or it is not worth combining.
