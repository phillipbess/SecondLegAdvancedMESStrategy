# Sweep Reclaim Sequenced Research - 2026-04-27

Goal: test the council diagnosis that the edge was not the named pattern, but the translation of auction failure into executable entries.

## Plain-English Thesis

`SweepReclaimSequenced` is an auction-failure retest strategy.

The market idea:

1. Price attacks an obvious level where stops or breakout traders are likely clustered.
2. Price pushes through the level just enough to sweep liquidity.
3. The move fails and price closes back on the other side of the level.
4. Instead of chasing the reclaim, the strategy waits for price to retest the swept level.
5. If the retest fills, the strategy trades away from the level with the stop beyond the sweep extreme.

The important discovery was not just "liquidity sweep." The important discovery was **how to enter it**. Continuation stop-entry after reclaim was weak. Retest-limit entry after reclaim was strong.

## Level Sources

The strategy uses structure levels built by `BuildStructureLevels()` in the QuantConnect harness:

| Level | Meaning | Directional Role |
|---|---|---|
| `PDH` | Prior RTH session high | Resistance / upside liquidity |
| `PDL` | Prior RTH session low | Support / downside liquidity |
| `ORH` | Current session opening-range high | Resistance / breakout boundary |
| `ORL` | Current session opening-range low | Support / breakdown boundary |
| `SWING_H` | Highest high of the prior 20 analysis bars, excluding current bar | Local resistance / recent high liquidity |
| `SWING_L` | Lowest low of the prior 20 analysis bars, excluding current bar | Local support / recent low liquidity |

Current default level set:

```text
PDH,PDL,ORH,ORL,SWING_H,SWING_L
```

Opening range default for these runs: 15 minutes.

Important caveat: these levels currently come from QuantConnect continuous MES futures using `OpenInterest` mapping and `BackwardsRatio` normalization. Because sweep strategies depend on exact prices, raw-data and mapping-mode validation is mandatory before production interpretation.

## Core Mechanics

### Short Setup

Shorts are triggered from resistance-style levels: `PDH`, `ORH`, and `SWING_H`.

Exact sequence:

1. Price trades above the level by at least `sweepSeqMinTicks`.
2. The sweep depth must not exceed `sweepSeqMaxTicks`.
3. Within `sweepSeqReclaimBars`, price closes back below the level.
4. The reclaim bar must pass shape checks:
   - close location must be bearish enough;
   - body size must be large enough if enabled;
   - bar range must meet the displacement threshold if enabled.
5. The strategy arms a planned short entry.
6. For the winning variant, entry is a limit retest at the swept level.
7. Stop is above the sweep extreme plus `sweepSeqStopBufferTicks`.
8. Target is `profitTargetR` times initial risk.

In short form:

```text
resistance level -> sweep above -> close back below -> retest level -> short
```

### Long Setup

Longs are triggered from support-style levels: `PDL`, `ORL`, and `SWING_L`.

Exact sequence:

1. Price trades below the level by at least `sweepSeqMinTicks`.
2. The sweep depth must not exceed `sweepSeqMaxTicks`.
3. Within `sweepSeqReclaimBars`, price closes back above the level.
4. The reclaim bar must pass shape checks.
5. The strategy arms a planned long entry.
6. For the retest-limit variant, entry is a limit retest at the swept level.
7. Stop is below the sweep extreme minus `sweepSeqStopBufferTicks`.
8. Target is `profitTargetR` times initial risk.

In short form:

```text
support level -> sweep below -> close back above -> retest level -> long
```

## Parameters Used In The Rolling Run

| Parameter | Value | Meaning |
|---|---:|---|
| `entryMode` | `SweepReclaimSequenced` | Enables this research mode |
| `barMinutes` | 1 | Signal bars are 1-minute consolidated bars |
| `openingRangeMinutes` | 15 | ORH/ORL use the first 15 minutes |
| `sweepSeqMinTicks` | 1 | Minimum level penetration |
| `sweepSeqMaxTicks` | 16 | Maximum allowed sweep depth |
| `sweepSeqReclaimBars` | 3 | Reclaim must happen within 3 signal bars |
| `sweepSeqMinSignalMinutes` | 15 | No signals before opening range completes |
| `sweepSeqMaxSignalMinutes` | 300 | Signals allowed through 300 minutes after RTH open |
| `sweepSeqStopBufferTicks` | 2 | Stop buffer beyond sweep extreme |
| `sweepSeqTriggerExpiryBars` | 4 | Pending entry expires after 4 signal bars |
| `sweepSeqMinReclaimClosePct` | 0.55 | Reclaim bar must close directionally |
| `sweepSeqMinReclaimBodyPct` | 0.35 | Reclaim bar must have body participation |
| `sweepSeqMinDisplacementAtr` | 0.25 | Reclaim bar range must show minimum displacement |
| `sweepSeqOneTradePerLevel` | true | One trade per level per session |
| `sweepSeqEntryType` | `limit` | Winning variant waits for retest of level |
| `profitTargetR` | 1.5 | Target is 1.5R |
| `maxOutcomeBars` | 45 | Trade timeout after 45 signal-bar minutes |
| `maxStopAtr` | 4.0 | Blocks extremely wide stops |

## Fill And Outcome Sequencing

The test uses two timeframes:

- One-minute consolidated bars detect sweep, reclaim, and setup state.
- Second-resolution bars handle entry fills, stop/target hits, and timeout.

The harness explicitly blocks same-bar lookahead:

```text
if (input.EndTime <= signalTime)
    do not fill
```

That means a setup must first be confirmed by a completed one-minute signal bar. Only later second bars may fill the pending entry.

For limit entries:

```text
Long:  fill if later second-bar low <= entry
Short: fill if later second-bar high >= entry
```

For stop entries:

```text
Long:  fill if later second-bar high >= entry
Short: fill if later second-bar low <= entry
```

After fill, the fill second is not allowed to immediately stop or target the trade. Stop/target evaluation starts on later second bars. Same-second stop/target ambiguity is counted via `SameBarBoth` and was rare in the rolling run.

## Difference From Prior Sweep Research

This mode is intentionally different from the older `LiquiditySweep` research:

- Old model: sweep level -> reclaim close -> enter next bar open.
- New model: sweep level -> reclaim close -> create planned entry -> fill from second bars only after the signal bar timestamp.
- Entry types tested:
  - `stop`: continuation through the reclaim bar.
  - `limit`: retest of the swept/reclaimed level.

This matters because the old next-open version blended good failures with bad location. The new retest-limit version asks the market to come back to the level before taking risk.

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

## Backtest Method

Platform: QuantConnect LEAN cloud.

Instrument: Micro E-mini S&P 500 continuous futures (`MES`).

Data:

- Signal construction: 1-minute consolidated bars.
- Execution sequencing: second-resolution bars.
- Session: RTH only, 09:30 to 15:55 New York time.
- Futures mapping: `OpenInterest`.
- Data normalization: `BackwardsRatio`.

Accounting:

- Results are virtual R-multiple outcomes.
- No real LEAN orders are submitted.
- Fees and slippage are not included.
- Position size is checked from `riskPerTrade` and point value, but PnL is reported in R, not dollars.

Backtest structure:

- First smoke: 2026-02-23 through 2026-04-23.
- Rolling validation: six two-month windows from 2025-04-23 through 2026-04-23.
- Variants compared:
  - retest-limit both sides;
  - retest-limit short only;
  - stop-entry both sides smoke;
  - stop-entry short only smoke.

## Rolling Outcomes

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
