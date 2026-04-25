# Brooks Bar-by-Bar ES Research - 2026-04-25

Source reviewed: `C:\Users\bessp\Downloads\Reading Price Charts Bar by Bar_ The Technical Analysis of -- Al Brooks [Brooks, Al] -- 2009.pdf`

Goal: pull the best Brooks-style ideas into deterministic QuantConnect tests and see if any have a credible path toward `+6R` to `+8R/month` as one ES/MES setup.

## What Looked Most Testable

The book is highly discretionary, but several recurring setup families can be converted into rules:

| Brooks Idea | Deterministic Research Translation |
|---|---|
| Strong trend bars in the first hour often predict same-direction strength later | Trend-from-open context, then first weak with-trend pullback continuation |
| Pullbacks in a strong trend | Enter after a shallow/weak pullback resumes in the trend direction |
| Opening patterns and reversals | Fade failed opening breakouts around prior-day levels or opening-range levels |
| Failed high/low entries and traps | Requires better intrabar/order-flow confirmation than minute bars provide |

The two best price-only candidates to test first were:

1. **Brooks Trend From Open Pullback (`BrooksTFO`)**
2. **Brooks Opening Reversal (`BrooksOR`)**

## BrooksTFO Contract

Rules implemented:

- Measure early session trend strength over `30` or `60` minutes.
- Require net move in ATR units.
- Require multiple strong trend bars closing near the extreme.
- Wait for a weak pullback.
- Reject pullbacks that are too deep, too long, or too strong against the trend.
- Enter when price resumes with trend.
- Stop beyond pullback extreme.
- Test `1R`, `1.5R`, and `2R` style exits.

Implementation files:

- `research/quantconnect/SecondLegQCSpike/BrooksTrendPullbackResearch.cs`
- `research/quantconnect/scripts/run_brooks_tfo_matrix.ps1`

First matrix:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 5m long, 60m measure, 0.75 ATR move, 3 strong bars | 35.61 | 0.59 | 3.3 | 0.18 |
| 5m long, 30m measure, 0.5 ATR move, 2 strong bars | 15.91 | 0.27 | 2.6 | 0.10 |
| 5m both, 60m measure, 0.75 ATR move, 3 strong bars | 31.58 | 0.53 | 6.8 | 0.08 |
| 5m both, 30m measure, 0.5 ATR move, 2 strong bars | 9.17 | 0.15 | 4.6 | 0.03 |
| 1m long, 30m measure, 0.5 ATR move, 6 strong bars | -21.50 | -0.36 | 3.8 | -0.09 |
| 1m both, 30m measure, 0.5 ATR move, 6 strong bars | -28.50 | -0.48 | 7.0 | -0.07 |

Salvage sweep on the best 5m long family:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| Long, 60m, 0.75 ATR, no EMA-side requirement, 1.5R, 60-bar hold | 57.97 | 0.97 | 4.4 | 0.22 |
| Long, 60m, 0.5 ATR, 2 strong bars, 1.5R, 60-bar hold | 45.48 | 0.76 | 4.1 | 0.19 |
| Long, 60m, 0.75 ATR, 1.5R, 60-bar hold | 47.97 | 0.80 | 3.6 | 0.22 |
| Long, 60m, 0.75 ATR, 2R, 60-bar hold | 38.90 | 0.65 | 3.6 | 0.18 |
| Long, 60m, 0.75 ATR, 1R, 24-bar hold | 31.91 | 0.53 | 3.6 | 0.15 |

Verdict: **BrooksTFO is positive but far below target**.

The best deterministic expression reached about `0.97R/month`, which is useful context but not the `6R-8R/month` business target.

## BrooksOR Contract

Rules implemented:

- During the first `90-120` minutes, watch for failed breaks of:
  - Prior RTH high / low
  - Opening range high / low
- If price sweeps the level by at least `2 ticks` and closes back through it, fade the failed breakout.
- Stop beyond the session sweep extreme.
- Test `1R` and `1.5R` exits.

Implementation file:

- `research/quantconnect/SecondLegQCSpike/BrooksOpeningReversalResearch.cs`

Results:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| Prior levels, both sides, 1.5R | -56.27 | -0.94 | 15.3 | -0.06 |
| Prior levels, long only, 1.5R | -41.35 | -0.69 | 9.8 | -0.07 |
| Opening range levels, both sides, 1.5R | -97.91 | -1.63 | 15.4 | -0.11 |
| Prior + OR levels, both sides, 1.5R | -50.51 | -0.84 | 15.6 | -0.05 |
| Prior + OR levels, both sides, 1R | -45.94 | -0.77 | 15.6 | -0.05 |

Verdict: **kill BrooksOR as a price-only minute-bar setup**.

The idea may still exist discretionary, but our deterministic version says a simple failed opening level break is not enough. It likely needs stronger context, order-flow confirmation, or a different execution model.

## Updated Research Read

Brooks helped clarify an important point: the setup is not the edge by itself. The edge is **context + setup + trader judgment**.

For code, that means:

- Second entries alone are not enough.
- Failed breakouts alone are not enough.
- Trend-from-open continuation is real-ish but too small as currently expressed.
- Minute bars may be missing the auction information Brooks is implicitly reading from bar shape, speed, follow-through, and trap behavior.

## Current Leaderboard

| Candidate | Monthly R | Trades/mo | Avg R | Status |
|---|---:|---:|---:|---|
| Afternoon Momentum Long 5m | 1.42 | 6.1 | 0.23 | Best price-only benchmark |
| BrooksTFO best salvage | 0.97 | 4.4 | 0.22 | Positive, not enough |
| Opening auction acceptance best pocket | 0.62 | 14.1 | 0.04 | Too weak |
| BrooksOR | Negative | 9.8-15.6 | Negative | Kill |
| Raw liquidity sweep/reclaim | 0.11 | 4.9 | 0.02 | Kill |

## Lead Researcher Recommendation

Do **not** keep trying to force Brooks price-action rules into minute bars as-is.

The best Brooks lesson is structural:

> Context first, setup second.

Our next credible path is to add a context variable that minute bars do not currently contain:

- order-flow imbalance around failed breakouts,
- volume/VWAP acceptance,
- or tick-level behavior around trap entries.

If we stay price-only, the honest benchmark remains **Afternoon Momentum Long**, but it is not close to the monthly R target by itself.
