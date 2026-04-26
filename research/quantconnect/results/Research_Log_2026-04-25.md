# ES/MES Intraday Research Log - 2026-04-25

Goal: find day-trade modules that can contribute toward a `+6R` to `+8R` average monthly portfolio target for a small MES trader.

## Current Verdict

No single module tested today reaches the full target. The first branch that looks worth carrying forward is **Afternoon Momentum Long**, but it is a module candidate, not a complete business by itself. The strongest one-setup direction remains intraday momentum, but current price-only variants are still materially below the `+6R` to `+8R/month` goal.

## Ideas Tested

### Opening Auction Acceptance / Failure

Tested first 15m/30m opening auction acceptance and failure, with 2R targets, tighter 1R/1.5R variants, side splits, and opening-range ATR buckets.

Best broad result:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 15m accepted, 2R | 23.31 | 0.39 | 18.3 | 0.02 |
| 15m accepted, OR range >= 1.5 ATR | 37.21 | 0.62 | 14.1 | 0.04 |

Verdict: **kill as a standalone edge**. There are small positive pockets, but nothing near the business target. Failure models were clearly bad.

### Liquidity Sweep Reclaim

Tested PDH/PDL/ORH/ORL sweep-and-reclaim with 2-12 tick sweep depth, reclaim within 3 bars, next-bar entry, stop beyond sweep extreme.

Best result:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| PDL reclaim, 1.5R | 6.85 | 0.11 | 4.9 | 0.02 |

Verdict: **kill raw sweep/reclaim as standalone**. OR sweeps were strongly negative and prior-day level sweeps were too weak.

### Opening Drive Pullback

Tested 15m/30m opening-drive pullback continuation with broad drive-strength thresholds.

Best result:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 30m drive, min 0.6-0.8 ATR, 2R | 3.79 | 0.06 | 12.2 | 0.01 |

Verdict: **kill this simple implementation**. It is basically flat at best.

### Afternoon Momentum

Tested first-hour directional information, then continuation after 2:30 PM. Long side outperformed short side decisively.

Best result:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| Long only, first-hour move >= 0.5 ATR, 1.5R target | 84.93 | 1.42 | 6.1 | 0.23 |

Year slices for the best variant:

| Period | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 2021-04 to 2022-04 | 7.50 | 0.62 | 5.8 | 0.11 |
| 2022-04 to 2023-04 | 13.50 | 1.12 | 6.2 | 0.18 |
| 2023-04 to 2024-04 | 10.97 | 0.91 | 6.4 | 0.14 |
| 2024-04 to 2025-04 | 18.72 | 1.56 | 5.8 | 0.27 |
| 2025-04 to 2026-04 | 34.24 | 2.85 | 6.4 | 0.44 |

Verdict: **keep as a candidate module**. It is positive in all five yearly slices, has a plausible market premise, and is structurally different from the failed second-leg/opening-auction work. It is not enough alone.

Additional 1-minute stress tests were run to see if finer bars and lower morning thresholds could raise frequency enough to approach the monthly R target.

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 1m long, first-hour move >= 0.25 ATR, 1.5R target | 57.72 | 0.96 | 6.9 | 0.14 |
| 1m long, first-hour move >= 0.5 ATR, 1.5R target | 59.22 | 0.99 | 6.7 | 0.15 |
| 1m both sides, first-hour move >= 0.25 ATR, 1.5R target | 31.22 | 0.52 | 12.8 | 0.04 |
| 1m both sides, first-hour move >= 0.5 ATR, 1.5R target | 34.72 | 0.58 | 12.5 | 0.05 |

Verdict: **1-minute bars did not unlock the edge**. They increased sample size in both-side mode, but diluted expectancy. The long-only 5-minute version remains the best clean price-only expression so far.

### Afternoon Momentum Compression Breakout

Built a new `AfternoonCompression` / `CompressionBreakout` research mode:

- Morning directional move sets the bias.
- Midday range forms a compression box.
- Later breakout in the morning-bias direction arms the trade.
- ATR stop controls risk so small-account execution is not forced into huge box stops.

First-pass 5-year results:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| Long, morning move >= 0.5 ATR, box <= 3.0 ATR, 0.75 ATR stop, 1.5R target | 5.50 | 0.09 | 0.2 | 0.46 |
| Long, morning move >= 0.5 ATR, box <= 4.0 ATR, 0.75 ATR stop, 1.5R target | 4.50 | 0.08 | 1.2 | 0.06 |
| Long, morning move >= 0.75 ATR, box <= 4.0 ATR, 0.75 ATR stop, 1.5R target | 6.00 | 0.10 | 1.2 | 0.09 |
| Long, morning move >= 1.0 ATR, box <= 5.0 ATR, 1.0 ATR stop, 2R target | 17.49 | 0.29 | 1.7 | 0.17 |
| Both sides, morning move >= 0.75 ATR, box <= 4.0 ATR, 0.75 ATR stop, 1.5R target | 18.50 | 0.31 | 1.8 | 0.17 |

Verdict: **not strong enough as currently expressed**. The tightest variant has good avg R but almost no trades. Looser variants get more trades but lose the punch. This may still be useful as a filter or context tag, but it is not the one-setup breakthrough yet.

### Council Research Notes

The panel converged on three broader ideas:

| Candidate | Why It Might Exist | Problem / Next Test |
|---|---|---|
| Opening auction acceptance/failure | First auction creates trapped inventory and continuation/failure pressure | Price-only version already tested weak; do not revisit without a new ingredient |
| Liquidity sweep/reclaim | Stops at PDH/PDL/ONH/ONL can create forced-flow reversals | Price-only version already tested weak |
| OFI-confirmed failed auction | A sweep that is not confirmed by bid/ask order-flow imbalance is a more real microstructure failure | Requires quote/tick data and a separate LEAN tick prototype |

The most credible "new ingredient" is **order-flow imbalance confirmation**. QuantConnect documentation says futures tick data includes trade and quote ticks with bid/ask price and size, so this is feasible to test in LEAN, but it should be treated as a new research harness rather than another minute-bar tweak.

### Brooks Bar-by-Bar Price Action

Reviewed `Reading Price Charts Bar by Bar` and converted two of the strongest price-action families into deterministic tests:

- `BrooksTFO`: trend-from-open context followed by weak with-trend pullback continuation.
- `BrooksOR`: failed opening breakout/reversal around prior-day or opening-range levels.

Best BrooksTFO result:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 5m long, 60m measure, 0.75 ATR move, no EMA-side requirement, 1.5R target, 60-bar hold | 57.97 | 0.97 | 4.4 | 0.22 |

BrooksOR results were negative across the tested matrix. The worst broad version was OR-level failed breakouts at `-97.91R` over five years.

Verdict: **Brooks helped conceptually, but deterministic minute-bar Brooks rules did not meet the goal**. The useful lesson is that context matters more than the named pattern. Price-only versions still fall far short of `+6R` to `+8R/month`.

### Structure-Break Salvage

Retested the best positive second-leg cluster as a standalone candidate: long-only `VideoSecondEntryLite`, nearby `SWING_H` / `PDH,SWING_H`, and structure-break entry.

Best results:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| `PDH,SWING_H`, room <= 0.50R, 2.5R target, 36-bar hold | 52.87 | 0.88 | 2.4 | 0.37 |
| `SWING_H`, room <= 0.25R, 2.5R target, 36-bar hold | 50.26 | 0.84 | 1.6 | 0.52 |
| `PDH,SWING_H`, room <= 0.25R, 2.0R target, 24-bar hold | 45.14 | 0.75 | 2.0 | 0.38 |

Verdict: **keep as a micro-edge candidate, not a main engine**. Quality improved, but frequency is far too low for the `6R-8R/month` goal.

### Opening Auction Room Filter

Revisited opening auction acceptance because it has the frequency profile we need. The unfiltered 15m accepted version was weak: `23.31R`, `0.39R/month`, `0.02R/trade`, `18.3 trades/month`.

Added research-only filters:

- `openingAuctionMinRoomR`
- `openingAuctionMaxRoomR`
- `openingAuctionMinSignalMinutes`

Best room-filtered results:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 15m accepted, range 0.75-2.5 ATR, room <= 1R, 1.5R target | 65.52 | 1.09 | 6.1 | 0.18 |
| 15m accepted, range 0.75-2.0 ATR, room <= 1R, 1.5R target | 63.62 | 1.06 | 5.5 | 0.19 |
| Same as above, signal 30-90 minutes after open | 62.87 | 1.05 | 5.4 | 0.19 |
| Short-only, range 0.75-2.0 ATR, room <= 1R, 1.5R target | 49.26 | 0.82 | 2.9 | 0.28 |

Verdict: **this is the best new discovery in the latest push**. The room filter turns a flat high-frequency idea into a modest positive candidate. Still not enough alone, but it is now worth yearly validation and possible portfolio combination with afternoon momentum / structure-break candidates.

Follow-up yearly validation on 2026-04-26:

| Variant | Positive Years | Total Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|---:|
| Opening auction room filter, broad | 5/5 | 65.54 | 1.09 | 6.1 | 0.18 |
| Opening auction room filter, short-only | 5/5 | 49.26 | 0.82 | 2.9 | 0.28 |

Rough stack with `Afternoon Momentum Long 5m`:

| Stack | Net R | Monthly R | Trades/mo | Read |
|---|---:|---:|---:|---|
| AM Long + OAR broad | 150.47 | 2.51 | 12.3 | Best total price-only stack so far |
| AM Long + OAR short-only | 134.19 | 2.24 | 9.0 | Cleaner trade quality, less frequency |

Verdict: **the candidate is stable enough to keep, but the stack is still not enough**. We now have a real benchmark around `2.5R/month`, not the `6R-8R/month` target.

Exact stack validation on 2026-04-26:

Built `CandidateStack` research mode so the opening-auction room filter and afternoon momentum long candidate run together in one QuantConnect algorithm, rather than being added by spreadsheet estimate.

| Period | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| 2021-2022 | 27.43 | 2.29 | 11.5 | 0.20 |
| 2022-2023 | 18.16 | 1.51 | 12.3 | 0.12 |
| 2023-2024 | 33.79 | 2.82 | 14.2 | 0.20 |
| 2024-2025 | 44.32 | 3.69 | 13.8 | 0.27 |
| 2025-2026 | 42.04 | 3.50 | 12.8 | 0.27 |
| Total | 165.75 | 2.76 | 12.9 | 0.21 |

Verdict: **this is the new price-only benchmark**. It is stable and clearly better than the original second-leg idea, but still below the `6R-8R/month` target. Next work should add a genuinely new engine or a structurally different confirmation variable, not just tweak this stack.

## Next Research Steps

1. Treat **Afternoon Momentum Long 5m** as the current benchmark, not the finish line.
2. Treat **Opening Auction Room Filter** as validated enough to keep on the board.
3. Use the exact `CandidateStack` result as the new price-only benchmark: about `2.8R/month`.
4. Do not spend more cycles on raw ORB, raw sweep/reclaim, or raw compression unless a new structural variable is added.
5. Build one tick/quote prototype for **OFI-confirmed failed auction** around PDH/PDL/ONH/ONL/ORH/ORL.
6. Continue only if OFI materially beats the price-only sweep baseline and survives yearly slices.
7. If OFI cannot improve failed auctions, stop chasing ES price-action variants and broaden beyond minute-bar ES setups.

### Camarilla Dynamic / AVWAP Slope Research

Tested Camarilla pivots with session-anchored VWAP slope as a dynamic regime switch:

- Flat VWAP slope: H3/L3 fade logic.
- Strong VWAP slope: H4/L4 breakout logic.
- Standard Camarilla formulas: H3/L3 = prior close +/- prior range * 1.1 / 4; H4/L4 = prior close +/- prior range * 1.1 / 2.

Initial classic H4/L4 stops were too wide for the small-account sizing constraint and produced only `2.92R` over five years. Adding a tighter ATR-capped stop found a real but weak candidate.

| Variant | Net R | Monthly R | Trades/mo | Avg R | Read |
|---|---:|---:|---:|---:|---|
| Both-side dynamic, tighter 1.25 ATR stop | 105.02 | 1.75 | 17.4 | 0.10 | Useful frequency, not stable enough |
| Long-only dynamic, tighter 1.25 ATR stop | 78.23 | 1.30 | 11.2 | 0.12 | Cleaner but still weak recently |

Yearly check for the best both-side variant:

| Period | Net R | Monthly R | Trades/mo |
|---|---:|---:|---:|
| 2021-2022 | 60.13 | 5.01 | 18.2 |
| 2022-2023 | 19.25 | 1.60 | 16.8 |
| 2023-2024 | 13.58 | 1.13 | 17.8 |
| 2024-2025 | 13.94 | 1.16 | 16.9 |
| 2025-2026 | -1.88 | -0.16 | 17.1 |

Verdict: **keep as a research artifact, not a promoted engine**. The frequency is attractive, but the recent degradation means it needs a context filter or setup split before it can be stacked with the current benchmark.

### ICT Silver Bullet / Judas / 2022 Model

Built a deterministic ICT research engine around:

- Liquidity sweep.
- Displacement / market-structure shift.
- Fair-value-gap retrace entry.
- Configurable liquidity set: swing, opening range, prior day.
- AM Silver Bullet, PM Silver Bullet, Judas open, and broad ICT 2022 windows.

Important correction: `ictLiquiditySet=pd` initially still included swing sweeps. That was fixed so `swing`, `or`, and `pd` are honored independently. Corrected results supersede earlier mislabeled output.

Broad model results were weak:

| Variant | Net R | Monthly R | Trades/mo | Avg R |
|---|---:|---:|---:|---:|
| Silver Bullet AM, both sides, 2R | -15.77 | -0.26 | 8.7 | -0.03 |
| Judas open, both sides, 1.5R | -38.20 | -0.64 | 8.7 | -0.07 |
| ICT 2022 broad RTH, both sides, 1.5R | -168.76 | -2.81 | 20.4 | -0.14 |

The only positive corrected pocket:

| Variant | Net R | Monthly R | Trades/mo | Avg R | Read |
|---|---:|---:|---:|---:|---|
| AM Silver Bullet, short only, prior-day-high sweep, 2R | 24.88 | 0.41 | 3.0 | 0.14 | Positive micro-edge, not main engine |

Yearly validation for the best ICT pocket:

| Period | Net R | Monthly R | Trades/mo |
|---|---:|---:|---:|
| 2021-2022 | 6.96 | 0.58 | 3.2 |
| 2022-2023 | 3.54 | 0.30 | 3.2 |
| 2023-2024 | -0.46 | -0.04 | 2.7 |
| 2024-2025 | 1.96 | 0.16 | 3.5 |
| 2025-2026 | 12.88 | 1.07 | 2.6 |

Verdict: **do not promote ICT as the new main strategy**. Keep the AM PDH short Silver Bullet as a possible micro-edge only. It is stable enough to remember, but far too small for the `6R-8R/month` goal.

Follow-up audit: the ICT/FVG family is extremely sensitive to intrabar sequencing on one-minute OHLC data.

| Test | Policy | Net R | Monthly R | Read |
|---|---|---:|---:|---|
| AM Silver Bullet PDH short, 2R | Default stop-first | 24.88 | 0.41 | Conservative baseline |
| AM Silver Bullet PDH short, 2R | Skip entry bar | 0.88 | 0.01 | Positive pocket mostly disappears |
| AM Silver Bullet PDH short, 2R | Target-first | 63.88 | 1.06 | Optimistic upper bound |
| ICT 2022 broad RTH, 1.5R | Default stop-first | -168.76 | -2.81 | Failed under conservative sequencing |
| ICT 2022 broad RTH, 1.5R | Skip entry bar | -246.26 | -4.10 | Worse |
| ICT 2022 broad RTH, 1.5R | Target-first | 181.24 | 3.02 | Shows massive sequencing dependence |

Updated verdict: **one-minute OHLC is not trustworthy enough to kill or crown ICT/FVG limit-entry ideas**. The next valid test needs tick or second-level sequencing so entry happens first and only subsequent prices can stop/target the trade.
