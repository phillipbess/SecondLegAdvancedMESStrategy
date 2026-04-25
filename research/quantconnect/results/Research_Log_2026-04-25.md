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

## Next Research Steps

1. Treat **Afternoon Momentum Long 5m** as the current benchmark, not the finish line.
2. Do not spend more cycles on raw ORB, raw sweep/reclaim, or raw compression unless a new structural variable is added.
3. Build one tick/quote prototype for **OFI-confirmed failed auction** around PDH/PDL/ONH/ONL/ORH/ORL.
4. Continue only if OFI materially beats the price-only sweep baseline and survives yearly slices.
5. If OFI cannot improve failed auctions, stop chasing ES price-action variants and broaden beyond minute-bar ES setups.
