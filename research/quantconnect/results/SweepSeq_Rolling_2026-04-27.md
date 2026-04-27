# Sweep Reclaim Sequenced Research - 2026-04-27

Goal: test the council diagnosis that the edge was not the named pattern, but the translation of auction failure into executable entries.

This mode is intentionally different from the older `LiquiditySweep` research:

- Old model: sweep level -> reclaim close -> enter next bar open.
- New model: sweep level -> reclaim close -> create planned entry -> fill from second bars only after the signal bar timestamp.
- Entry types tested:
  - `stop`: continuation through the reclaim bar.
  - `limit`: retest of the swept/reclaimed level.

The first smoke showed the important split:

| Variant | Net R | Avg R | Trades | Read |
|---|---:|---:|---:|---|
| Stop-entry both | -10.34 | -0.06 | 170 | Weak |
| Stop-entry short | -2.48 | -0.03 | 83 | Weak |
| Retest-limit both | 25.00 | 0.17 | 150 | Strong |
| Retest-limit short | 16.50 | 0.23 | 71 | Strong |

That supports the idea that entry translation mattered. Stop-through confirmation was not the edge; retest-limit after reclaim was.

## Anti-Lookahead Fix

After the first smoke, the harness was patched so pending entries cannot fill on or before the completed signal bar timestamp:

```text
if (input.EndTime <= _sweepSeqPending.SignalTime)
    return;
```

The rolling results below are after that fix.

## Rolling Windows

Tested six rolling two-month windows from 2025-04-23 through 2026-04-23.

| Variant | Net R | Trades | Avg R | R/month | Trades/month | Same-bar both | Read |
|---|---:|---:|---:|---:|---:|---:|---|
| Retest-limit both | 145.00 | 930 | 0.16 | 12.08 | 77.5 | 2 | Very strong, but trade-dense and execution-sensitive |
| Retest-limit short | 78.00 | 487 | 0.16 | 6.50 | 40.6 | 1 | Meets R goal in recent rolling year, still too many trades |

## Window Detail

| Variant | Window | Net R | Avg R | Trades | Wins | Stops | Expired | Same-bar both |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Retest-limit both | 2025-04-23 to 2025-06-23 | 23.50 | 0.15 | 154 | 71 | 83 | 200 | 1 |
| Retest-limit both | 2025-06-23 to 2025-08-23 | 23.00 | 0.14 | 162 | 74 | 88 | 169 | 0 |
| Retest-limit both | 2025-08-23 to 2025-10-23 | 15.00 | 0.09 | 160 | 70 | 90 | 211 | 0 |
| Retest-limit both | 2025-10-23 to 2025-12-23 | 44.50 | 0.29 | 153 | 79 | 74 | 180 | 0 |
| Retest-limit both | 2025-12-23 to 2026-02-23 | 14.00 | 0.09 | 151 | 66 | 85 | 186 | 0 |
| Retest-limit both | 2026-02-23 to 2026-04-23 | 25.00 | 0.17 | 150 | 70 | 80 | 263 | 1 |
| Retest-limit short | 2025-04-23 to 2025-06-23 | 19.50 | 0.23 | 83 | 41 | 42 | 80 | 0 |
| Retest-limit short | 2025-06-23 to 2025-08-23 | 18.50 | 0.21 | 89 | 43 | 46 | 79 | 0 |
| Retest-limit short | 2025-08-23 to 2025-10-23 | -1.00 | -0.01 | 86 | 34 | 52 | 95 | 0 |
| Retest-limit short | 2025-10-23 to 2025-12-23 | 18.50 | 0.23 | 79 | 39 | 40 | 77 | 1 |
| Retest-limit short | 2025-12-23 to 2026-02-23 | 6.00 | 0.08 | 79 | 34 | 45 | 99 | 0 |
| Retest-limit short | 2026-02-23 to 2026-04-23 | 16.50 | 0.23 | 71 | 35 | 36 | 152 | 0 |

## Read

This is the first result that actually reaches the original monthly R target in recent data.

The important positives:

- The result survived the anti-lookahead patch.
- Every two-month window was positive for `Retest-limit both`.
- `Retest-limit short` hit `6.50R/month` across the rolling year.
- Same-bar stop/target ambiguity was rare: 1-2 events across hundreds of trades.
- The stop-entry version failed while the retest-limit version worked, which is a useful behavioral clue.

The important risks:

- These are still virtual fills, not real LEAN orders.
- Limit-touch fills are optimistic unless we model bid/ask, queue, and slippage.
- No commissions or execution friction are included.
- The trade count is high, especially `Retest-limit both` at 77.5 trades/month.
- The test uses continuous MES futures with `BackwardsRatio` normalization and `OpenInterest` mapping, so absolute level tests need raw/mapping validation.
- This is only a rolling recent year. It needs longer yearly slices before promotion.

Cost haircut sensitivity:

| Variant | Raw Net R | 0.05R/trade haircut | 0.10R/trade haircut |
|---|---:|---:|---:|
| Retest-limit both | 145.00 | 98.50 | 52.00 |
| Retest-limit short | 78.00 | 53.65 | 29.30 |

Even after a rough `0.05R/trade` haircut, the short-only version remains near `4.47R/month`. At `0.10R/trade`, it drops to `2.44R/month`. So the edge may be real, but execution modeling is now the whole game.

## Decision

Do not call this production-ready.

Do call it the first serious breakthrough candidate.

Next validation order:

1. Add a fill-friction model for limit entries: require trade-through by 1 tick, or subtract fixed R haircut per trade.
2. Run raw-vs-adjusted and mapping-mode checks for level integrity.
3. Run five yearly slices from 2021-2026.
4. Split attribution by level family: `ORH`, `PDH`, `SWING_H`, and time of day.
5. Reduce frequency toward 15-30 trades/month while preserving average R.

## Artifacts

- Harness: `research/quantconnect/SecondLegQCSpike/SweepReclaimSequencedResearch.cs`
- Smoke runner: `research/quantconnect/scripts/run_sweepseq_smoke.ps1`
- Rolling runner: `research/quantconnect/scripts/run_sweepseq_rolling.ps1`
- Summary CSV: `research/quantconnect/results/sweepseq/sweepseq_rolling_summary.csv`
- Raw results: `research/quantconnect/results/sweepseq/sweepseq_limit_*.txt`
