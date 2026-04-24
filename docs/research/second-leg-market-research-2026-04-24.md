# Second-Leg Continuation Market Research - 2026-04-24

## Strategy Thesis Being Researched

The current strategy thesis is:

trend -> impulse -> two-legged controlled correction -> failed second countertrend attempt -> continuation trigger

This is not a reversal thesis and not a generic breakout thesis. It depends on three market behaviors being real enough to exploit:

- directional persistence after real initiative
- countertrend pullbacks often failing before they become reversals
- continuation triggers offering enough favorable excursion relative to the stop

## External Research Read

### What Supports The Broad Idea

Academic and practitioner research does support the broad family this strategy belongs to:

- Moskowitz, Ooi, and Pedersen document time-series momentum across futures, currencies, commodities, bonds, and equity indexes. This supports the idea that return persistence/trend behavior exists in liquid futures, although their horizon is much longer than this strategy's 5-minute execution horizon.
  Source: https://www.sciencedirect.com/science/article/pii/S0304405X11002613

- Lo, Mamaysky, and Wang show that formally defined technical patterns can contain incremental information. This supports the project direction of turning discretionary chart structure into deterministic rules instead of vague visual judgment.
  Source: https://www.nber.org/papers/w7613

- Zarattini, Aziz, and Barbon report profitable intraday momentum behavior in SPY using demand/supply imbalance and dynamic trailing stops. This is not our exact setup, but it supports the idea that intraday trend-following can exist in S&P-linked markets.
  Source: https://papers.ssrn.com/sol3/papers.cfm?abstract_id=4824172

- Heston, Korajczyk, and Sadka find intraday return continuation patterns in half-hour intervals, with microstructure reversal at very short horizons. This is relevant because our strategy tries to avoid raw first-push chasing and waits for a pullback/confirmation.
  Source: https://conference.nber.org/confer/2008/mms08/korajczyk.pdf

- Recent E-mini order-flow research studies one-second S&P 500 E-mini futures return/order-flow imbalance dynamics. This supports the idea that intraday supply/demand pressure is a real object, but it also warns that macro news can reshape price-flow dynamics sharply.
  Source: https://arxiv.org/abs/2508.06788

### What Pushes Back

The research does not prove this exact edge.

- Long-horizon trend research does not automatically transfer to 5-minute pullback entries.
- Intraday momentum research is often regime-, time-of-day-, and cost-sensitive.
- Some S&P 500 futures research documents short-term return reversal at daily frequency, especially in illiquid conditions.
  Source: https://papers.ssrn.com/sol3/papers.cfm?abstract_id=5284206

The practical read is:

- the market dynamic is plausible and real enough to test seriously
- the exact entry shape must earn its keep in playback/backtest data
- no single video, chart pattern, or two-trade playback sample is enough

## Local Playback Evidence

Current available SecondLeg logs contain 2 completed trades:

- trades: 2
- wins: 1
- losses: 1
- net PnL: -$46.25
- average R: -0.16
- first stop SLA: healthy, 0 ms p95
- order-management readiness score from `metrics.ps1`: 100/100 on the available logs

Interpretation:

- runtime/order management looked healthy in this tiny sample
- entry edge is not statistically proven
- local data is currently implementation evidence, not market-edge evidence

## Public ES Proxy Test

I ran a quick proxy test on public Yahoo Finance `ES=F` 5-minute regular-session bars.

Scope:

- symbol: `ES=F`
- interval: 5 minutes
- regular session only
- sample: 43 recent trading days
- rows: 3,228
- dates: 2026-02-24 through 2026-04-24 partial session

Important limitations:

- this is not NinjaTrader market replay
- this is ES, not MES, though MES should track ES closely
- it approximates the entry contract, not exact NT8 code
- it does not include structure-room filtering
- it uses a simple stop / 1R / 2R / EOD outcome model, not the full Mancini-style trail
- same-bar stop/target ambiguity was treated conservatively

Proxy results:

- signals found: 8
- triggered entries: 6
- expired entries: 2
- long/short split: 2 long, 4 short
- 1R touch rate: 33.3%
- 2R touch rate: 16.7%
- stopout rate: 83.3%
- average MFE: 2.00R
- median MFE: 0.83R
- average MAE: 1.27R
- naive stop/EOD average result: +0.47R

Interpretation:

- the setup was sparse
- most triggered trades were stopped under a simple static-stop model
- one large short winner dominated the positive average
- the favorable-excursion profile is interesting but fragile
- the current exact rule stack is not yet proven as a robust edge

## What The Data Says Right Now

The broad market dynamic is real enough to keep testing:

- trend persistence exists in futures research
- intraday momentum exists in S&P-linked research
- order-flow / imbalance dynamics are real in E-mini markets
- formal technical-pattern testing has academic precedent

But our exact current strategy is not yet proven:

- local playback sample is only 2 trades
- public proxy sample produced only 6 triggered trades
- results are dominated by one large winner
- structure-room and exact NT8 behavior still need platform evidence

## QuantConnect One-Year MES Research Baseline

After authenticating LEAN CLI, I created a QuantConnect Cloud project and ported the entry detector into C# as an orderless research harness.

Backtest:

- project: `SecondLegQCSpike`
- date range: `2025-04-24` through `2026-04-23`
- instrument: MES continuous futures
- resolution: minute data consolidated to 5-minute bars
- RTH 5-minute bars: `19,602`
- orders placed: `0`

Detector funnel:

- trend bars: `8,828`
- impulses: `502`
- leg 1 pullbacks: `279`
- separations: `145`
- leg 2 candidates: `44`
- armed signals: `9`
- triggered signals: `7`
- expired signals: `2`

Virtual outcome result:

- virtual trades: `7`
- touched 1R: `3`
- reached 2R: `2`
- stopped: `4`
- timed out: `1`
- net R: `+1.35R`
- average R per triggered setup: `+0.19R`

Interpretation:

- the zero-trade concern was at least partly a modeling issue, because the first QC pass used ES point value while the strategy is sized for MES
- after correcting MES point value, the rule stack produced triggered opportunities
- the first conservative virtual outcome pass is slightly positive
- the sample is still too small to claim an edge
- the highest-value research work is longer history plus controlled parameter sweeps, not another discretionary rewrite

## Best Next Research Step

The highest-value next step is not adding filters.

It is building a proper evidence set:

1. Export or replay at least 60-120 RTH sessions on MES/ES 5-minute data.
2. Capture every `ENTRY_ARMED`, `ENTRY_SUBMIT`, `ENTRY_FILL`, `TRADE_CLOSE`, `TradesCsv_`, and `StopEvents_` row.
3. Classify each signal by:
   - trend slope
   - impulse ATR multiple
   - pullback retracement
   - leg-2 momentum ratio
   - stop distance / ATR
   - time of day
   - room-to-structure
4. Measure:
   - trigger rate
   - 1R touch before stop
   - 2R touch before stop
   - actual trail capture
   - average R
   - median R
   - drawdown
   - long/short split
   - time-of-day split
5. Only then decide whether to loosen, tighten, or split regimes.

## Current Verdict

Keep the thesis. Do not declare the edge proven.

The idea is market-real enough to deserve serious testing, but the data says our current exact implementation is still in the evidence-building phase. The best work now is broader playback/backtest coverage and signal attribution, not another round of subjective strategy tweaking.
