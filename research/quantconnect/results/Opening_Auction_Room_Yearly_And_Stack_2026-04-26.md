# Opening Auction Room Yearly Validation And Candidate Stack - 2026-04-26

Goal: validate the opening-auction room filter year by year, then estimate how it stacks with the current benchmark candidate, `Afternoon Momentum Long 5m`.

## Opening Auction Room Filter: Yearly Slices

Broad version:

`15m accepted auction, opening range 0.75-2.5 ATR, room <= 1R, 1.5R target, 24-bar hold, both sides`

| Period | Net R | R/Month | Avg R | Trades | Trades/Month |
|---|---:|---:|---:|---:|---:|
| 2021-2022 | 16.54 | 1.38 | 0.25 | 65 | 5.4 |
| 2022-2023 | 6.09 | 0.51 | 0.10 | 64 | 5.3 |
| 2023-2024 | 9.17 | 0.76 | 0.11 | 83 | 6.9 |
| 2024-2025 | 22.42 | 1.87 | 0.25 | 89 | 7.4 |
| 2025-2026 | 11.32 | 0.94 | 0.17 | 67 | 5.6 |
| Total | 65.54 | 1.09 | 0.18 | 368 | 6.1 |

Short-only version:

`15m accepted auction, opening range 0.75-2.0 ATR, room <= 1R, 1.5R target, 18-bar hold, short only`

| Period | Net R | R/Month | Avg R | Trades | Trades/Month |
|---|---:|---:|---:|---:|---:|
| 2021-2022 | 12.69 | 1.06 | 0.37 | 34 | 2.8 |
| 2022-2023 | 4.19 | 0.35 | 0.17 | 25 | 2.1 |
| 2023-2024 | 8.71 | 0.73 | 0.21 | 41 | 3.4 |
| 2024-2025 | 17.36 | 1.45 | 0.39 | 44 | 3.7 |
| 2025-2026 | 6.31 | 0.53 | 0.21 | 30 | 2.5 |
| Total | 49.26 | 0.82 | 0.28 | 174 | 2.9 |

Read: both versions passed the regime test. Every yearly slice was positive. The broad version has better total R and frequency; short-only has better per-trade quality.

## Candidate Stack Estimate

This is not a perfect portfolio simulation, but the time separation is clean enough to be useful: opening-auction trades occur in the first 90 minutes, while afternoon momentum trades occur late day. Same-day risk can still stack, but direct intraday signal overlap should be minimal.

Stack A:

`Afternoon Momentum Long 5m` + `Opening Auction Room Filter broad`

| Period | AM Net R | OAR Net R | Combined Net R | Combined R/Month | Combined Trades/Month |
|---|---:|---:|---:|---:|---:|
| 2021-2022 | 7.50 | 16.54 | 24.04 | 2.00 | 11.2 |
| 2022-2023 | 13.50 | 6.09 | 19.59 | 1.63 | 11.5 |
| 2023-2024 | 10.97 | 9.17 | 20.14 | 1.68 | 13.3 |
| 2024-2025 | 18.72 | 22.42 | 41.14 | 3.43 | 13.2 |
| 2025-2026 | 34.24 | 11.32 | 45.56 | 3.80 | 12.0 |
| Total | 84.93 | 65.54 | 150.47 | 2.51 | 12.3 |

Stack B:

`Afternoon Momentum Long 5m` + `Opening Auction Room Filter short-only`

| Period | AM Net R | OAR Short Net R | Combined Net R | Combined R/Month | Combined Trades/Month |
|---|---:|---:|---:|---:|---:|
| 2021-2022 | 7.50 | 12.69 | 20.19 | 1.68 | 8.7 |
| 2022-2023 | 13.50 | 4.19 | 17.69 | 1.47 | 8.3 |
| 2023-2024 | 10.97 | 8.71 | 19.68 | 1.64 | 9.8 |
| 2024-2025 | 18.72 | 17.36 | 36.08 | 3.01 | 9.5 |
| 2025-2026 | 34.24 | 6.31 | 40.55 | 3.38 | 8.9 |
| Total | 84.93 | 49.26 | 134.19 | 2.24 | 9.0 |

## Verdict

The opening-auction room filter is validated enough to remain on the board. It is not a curve-fit-looking one-year wonder.

The combined price-only stack is still below the business goal:

- Current best stack: about `2.5R/month`, `12 trades/month`.
- Goal: `6R-8R/month`, ideally around `20 trades/month`.
- Gap: we still need another independent engine, not another small parameter tweak.

## Next Best Step

The next best research step is an exact candidate-stack backtest or a new structural ingredient.

Priority order:

1. Build a combined-candidate research mode if we want exact daily/portfolio interaction between AM and OAR.
2. If we want the fastest route toward the goal, start the next engine: order-flow / tick-confirmed failed auction or another structurally different ES setup.
3. Do not spend more time on raw opening auction, raw sweep/reclaim, or generic second-entry variants unless a new variable is added.
