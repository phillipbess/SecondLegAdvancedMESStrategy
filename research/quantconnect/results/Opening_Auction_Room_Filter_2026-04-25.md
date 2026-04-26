# Opening Auction Room Filter - 2026-04-25

Goal: revisit the opening-auction acceptance/failure idea because it has enough frequency for a small trader, then isolate the only positive bucket seen in the raw logs: opening-auction trades with `RoomToStructureR < 1.0`.

## Code Change

Added research-only parameters to `OpeningAuctionResearch.cs`:

| Parameter | Purpose |
|---|---|
| `openingAuctionMinRoomR` | Optional minimum allowed opening-range-to-risk ratio. |
| `openingAuctionMaxRoomR` | Optional maximum allowed opening-range-to-risk ratio. |
| `openingAuctionMinSignalMinutes` | Optional lower time bound after the open. |

These do not affect default behavior unless explicitly supplied by a QC backtest parameter.

## Broad Filter Result

| Variant | Net R | R/Month | Avg R | Trades | Trades/Month |
|---|---:|---:|---:|---:|---:|
| accepted_15m_roommax1_range0p75_2p5_t1p5_hold24 | 65.52 | 1.09 | 0.18 | 368 | 6.1 |
| accepted_15m_roommax1_range0p75_2p0_t1p5_hold18 | 63.62 | 1.06 | 0.19 | 331 | 5.5 |
| accepted_15m_both_sig30_90_roommax1_range0p75_2p0_t1p5_hold18 | 62.87 | 1.05 | 0.19 | 324 | 5.4 |
| accepted_15m_short_roommax1_range0p75_2p0_t1p5_hold18 | 49.26 | 0.82 | 0.28 | 174 | 2.9 |
| accepted_15m_short_sig30_90_roommax1_range0p75_2p0_t1p5_hold18 | 44.39 | 0.74 | 0.26 | 169 | 2.8 |
| accepted_30m_roommax1_range0p5_2p0_t1p5_hold24 | 26.48 | 0.44 | 0.25 | 108 | 1.8 |
| accepted_15m_roommax0p75_range0p75_2p5_t1p5_hold24 | 16.57 | 0.28 | 0.08 | 197 | 3.3 |
| accepted_15m_long_roommax1_range0p75_2p0_t1p5_hold18 | 15.73 | 0.26 | 0.10 | 157 | 2.6 |

Raw outputs and CSV summary live in `results/opening_auction_room_filtered/`.

## Comparison To Unfiltered Opening Auction

The unfiltered `15m accepted` run was basically flat: `23.31R`, `0.39R/month`, `0.02R/trade`, `18.3 trades/month`.

The room-filtered version improved quality materially: best broad variant `65.52R`, `1.09R/month`, `0.18R/trade`, `6.1 trades/month`.

## Read

This is a real improvement, not enough to meet the full target. The strongest version is still around `1R/month`, so it needs either another independent edge stack or deeper refinement before it can support the `6R-8R/month` goal.

The short side is cleaner (`0.28R/trade`) but lower frequency. The both-side version has better total R because the long side contributes extra volume even at weaker quality.

Verdict: opening auction acceptance with `RoomToStructureR <= 1.0` is now a live candidate. It should be kept for walk-forward/year-by-year testing and combined-candidate portfolio analysis, but not declared sufficient.
