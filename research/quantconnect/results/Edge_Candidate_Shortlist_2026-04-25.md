# ES/MES One-Setup Edge Shortlist - 2026-04-25

Goal: find one ES/MES day-trading setup that has a believable path toward `+6R` to `+8R/month` for a small trader.

## Research Baseline

External evidence supports intraday momentum as a real market behavior:

- Gao, Han, Li, and Zhou document that the first half-hour market return predicts the last half-hour return: https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2552752
- Baltussen, Da, Lammers, and Martens study market intraday momentum across global futures and other asset classes: https://www.sciencedirect.com/science/article/pii/S0304405X21001598
- QuantConnect has a public recreation of the intraday ETF momentum idea: https://www.quantconnect.com/research/15348/intraday-etf-momentum/p1

QuantConnect documentation also confirms futures tick/quote data can expose bid/ask quote ticks, which matters for order-flow imbalance research:

- Futures handling data: https://www.quantconnect.com/docs/v2/writing-algorithms/securities/asset-classes/futures/handling-data
- Futures historical tick/quote data: https://www.quantconnect.com/docs/v2/writing-algorithms/historical-data/asset-classes/futures

## What We Have Already Killed

| Idea | Reason |
|---|---|
| Second-leg pullback | Too few/weak trades after simplification and playback/QC work |
| Raw opening auction acceptance/failure | Broad best result was only about `0.39R` to `0.62R/month` |
| Raw liquidity sweep/reclaim | Best prior-day low reclaim was only about `0.11R/month` |
| Raw opening-drive pullback | Essentially flat |
| Raw afternoon compression breakout | Positive pockets, but too rare or too weak |

## Current Benchmark

Best clean price-only benchmark:

| Setup | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 5m Afternoon Momentum Long, first-hour move >= 0.5 ATR, 1.5R target | 84.93 | 1.42 | 6.1 | 0.23 |

Verdict: this is real enough to keep as a benchmark, but not strong enough to be the business by itself.

## Candidate 1: OFI-Confirmed Failed Auction

This is the highest-value next experiment.

Thesis:

- Price sweeps an obvious liquidity level: `PDH`, `PDL`, `ONH`, `ONL`, `ORH`, or `ORL`.
- The sweep does not receive confirming bid/ask pressure.
- Price reclaims the level.
- We fade the failed auction back into the prior range.

Why this is different from the killed sweep test:

- The killed version only knew price touched and reclaimed a level.
- This version asks whether the auction actually found aggressive continuation flow.
- That makes it a microstructure test, not another support/resistance rule.

First contract:

| Rule | Long Failed Breakdown | Short Failed Breakout |
|---|---|---|
| Level event | Trade below level by at least `1-4 ticks` | Trade above level by at least `1-4 ticks` |
| Confirmation window | Next `15-60 seconds` | Next `15-60 seconds` |
| OFI condition | OFI not bearish or flips positive | OFI not bullish or flips negative |
| Entry | Reclaim back above swept level | Reclaim back below swept level |
| Stop | Sweep extreme plus `1-2 ticks` | Sweep extreme plus `1-2 ticks` |
| Target | `1R`, `1.5R`, `2R` sweep | `1R`, `1.5R`, `2R` sweep |
| Timeout | `10-30 minutes` | `10-30 minutes` |

Go criteria:

- Clearly beats the price-only sweep baseline.
- At least `300` trades over five years, or enough level-family frequency to approach `15-20 trades/month`.
- Avg R `>= +0.25R` after conservative slippage assumptions.
- Positive in at least `4/5` yearly slices.

Kill criteria:

- OFI filter does not materially improve price-only sweep results.
- Edge only exists in one level family or one year.
- Results vanish with one tick slippage each side.
- Requires unrealistic tick-perfect fills.

## Candidate 2: Afternoon Momentum Long With Structural Filter

Thesis:

- Intraday momentum is the only tested price-only idea with real evidence.
- We should not add arbitrary filters, but a structural filter might isolate when late-day continuation has room to run.

Possible filters to test:

| Filter | Why It Might Help |
|---|---|
| Above VWAP and VWAP rising | Confirms accepted value higher |
| Above opening range high before 2:30 PM | Confirms day is already accepting higher prices |
| Not within `0.5R-1R` of PDH/major resistance | Avoids buying into obvious liquidity ceiling |
| First-hour move plus midday higher-low structure | Distinguishes trend days from early impulse fades |

Go criteria:

- Improves benchmark from `1.42R/month` toward at least `2.5R/month`.
- Keeps yearly stability.
- Does not cut frequency below about `4 trades/month`.

Kill criteria:

- Any filter improves only one year.
- Net R increases by deleting too many trades.
- The rule becomes a hidden curve fit rather than a market mechanism.

## Candidate 3: VWAP Reclaim After Failed Low

Thesis:

- ES often probes below a morning/overnight level, then reclaims value.
- VWAP reclaim may separate real failed auctions from random level taps.

First contract:

| Rule | Description |
|---|---|
| Bias | Long only first |
| Event | Sweep `ORL`, `ONL`, or `PDL` |
| Reclaim | Close back above level, then close above VWAP |
| Entry | Next bar or first pullback that holds VWAP |
| Stop | Sweep low plus buffer |
| Target | `1R-2R` |
| Timeout | Same session, preferably before final 30 minutes |

Go criteria:

- Beats raw sweep/reclaim by a large margin.
- Has at least `10 trades/month`.
- Works on more than one level family.

Kill criteria:

- Requires exact VWAP timing or exact level choice.
- Stops are too large for MES execution.

## Lead Researcher Recommendation

Build **Candidate 1: OFI-Confirmed Failed Auction** next.

Reason: price-only ES patterns are not getting us near `6R-8R/month`. The next real unlock is not another candle filter; it is adding a structural variable that minute bars do not contain. Order-flow imbalance is the most plausible next variable because it directly tests whether a sweep found continuation participation or only consumed liquidity.

If OFI cannot improve failed auctions, we should stop trying to brute-force ES minute-bar price action and either broaden markets/timeframes or accept Afternoon Momentum Long as a small module rather than the core strategy.
