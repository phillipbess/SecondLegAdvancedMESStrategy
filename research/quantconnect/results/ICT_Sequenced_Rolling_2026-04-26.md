# ICT Sequenced Rolling Research - 2026-04-26

Goal: test the ICT/FVG ideas with realistic intrabar sequencing, not one-minute OHLC assumptions.

The earlier one-minute tests were too sensitive to same-bar policy. The broad ICT 2022 model could look very poor under stop-first sequencing and very good under target-first sequencing. That means the test was dominated by unknown intrabar order, so the next valid step was second-resolution execution sequencing.

## Harness

Mode: `entryMode=ICTSequenced`.

Behavior:

- Build one-minute signal bars with LEAN consolidation.
- Detect sweep -> displacement/MSS -> FVG retrace setups from those one-minute bars.
- Fill pending FVG entries from later second bars.
- Do not allow the fill second to immediately stop or target the trade.
- Evaluate stop, target, and timeout from subsequent second bars in timestamp order.

This is still a research harness, not production order routing, but it removes the biggest one-minute OHLC ambiguity.

## Rolling Windows

Tested six rolling two-month windows from 2025-04-23 through 2026-04-23.

| Variant | Net R | Trades | Avg R | R/month | Trades/month | Read |
|---|---:|---:|---:|---:|---:|---|
| PDH short 2R | 4.20 | 31 | 0.14 | 0.35 | 2.6 | Positive, but far too sparse |
| ICT 2022 RTH 1.5R | -56.59 | 251 | -0.23 | -4.72 | 20.9 | Enough frequency, decisively negative |

## Window Detail

| Variant | Window | Net R | Avg R | Trades | Wins | Stops | Timeouts | Touch 1R |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| ICT 2022 RTH 1.5R | 2025-04-23 to 2025-06-23 | 0.83 | 0.02 | 42 | 17 | 24 | 1 | 22 |
| ICT 2022 RTH 1.5R | 2025-06-23 to 2025-08-23 | -4.50 | -0.11 | 42 | 15 | 27 | 0 | 17 |
| ICT 2022 RTH 1.5R | 2025-08-23 to 2025-10-23 | -10.50 | -0.24 | 43 | 13 | 30 | 0 | 16 |
| ICT 2022 RTH 1.5R | 2025-10-23 to 2025-12-23 | -11.92 | -0.29 | 41 | 11 | 29 | 1 | 16 |
| ICT 2022 RTH 1.5R | 2025-12-23 to 2026-02-23 | -21.00 | -0.51 | 41 | 8 | 33 | 0 | 11 |
| ICT 2022 RTH 1.5R | 2026-02-23 to 2026-04-23 | -9.50 | -0.23 | 42 | 13 | 29 | 0 | 17 |
| PDH short 2R | 2025-04-23 to 2025-06-23 | 3.00 | 0.50 | 6 | 3 | 3 | 0 | 3 |
| PDH short 2R | 2025-06-23 to 2025-08-23 | 1.00 | 0.13 | 8 | 3 | 5 | 0 | 3 |
| PDH short 2R | 2025-08-23 to 2025-10-23 | 0.00 | 0.00 | 0 | 0 | 0 | 0 | 0 |
| PDH short 2R | 2025-10-23 to 2025-12-23 | 1.00 | 0.13 | 8 | 3 | 5 | 0 | 4 |
| PDH short 2R | 2025-12-23 to 2026-02-23 | -1.80 | -0.45 | 4 | 0 | 3 | 1 | 2 |
| PDH short 2R | 2026-02-23 to 2026-04-23 | 1.00 | 0.20 | 5 | 2 | 3 | 0 | 2 |

## Read

The broad ICT 2022 model is not worth promoting as coded. It has the needed trade frequency, but the second-sequenced result is strongly negative across the recent rolling year.

The PDH short Silver Bullet pocket remains directionally interesting, but it is not a main engine. It only produced 31 trades over roughly twelve months, about 2.6 trades per month, and only 0.35R/month. That is useful as a possible micro-edge or context feature, not as the path to the 6R-8R/month goal.

Most important: the large target-first one-minute optimism did not survive proper sequencing. The issue was not that the platform was missing a hidden huge ICT edge; the earlier one-minute version was just too ambiguous to trust.

## Decision

- Kill the broad ICT 2022 RTH model as a standalone strategy.
- Keep PDH short Silver Bullet as a watchlist micro-edge only.
- Do not spend more time optimizing this exact ICT family unless we add a materially new variable such as order-flow imbalance, relative volume, news/session classification, or a stronger higher-timeframe context label.
- Keep the current price-only benchmark as `CandidateStack`: about 2.76R/month over 2021-2026.

## Artifacts

- Rolling runner: `research/quantconnect/scripts/run_ict_sequenced_rolling.ps1`
- Summary CSV: `research/quantconnect/results/ict_sequenced/ictseq_rolling_summary.csv`
- Raw result files: `research/quantconnect/results/ict_sequenced/ictseq_*.txt`
