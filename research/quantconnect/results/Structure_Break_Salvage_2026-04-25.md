# Structure-Break Salvage Matrix - 2026-04-25

Goal: retest the best positive pullback-continuation cluster after the broader second-leg thesis failed to reach the account-growth target. The setup is long-only `VideoSecondEntryLite` with `liteUseStructureBreakEntry=true`, focused on nearby `SWING_H` and `PDH,SWING_H` structure breaks.

## Result Summary

| Variant | Net R | R/Month | Avg R | Trades | Trades/Month |
|---|---:|---:|---:|---:|---:|
| long_pdh_swing_h_room_0p50_target_2p50_hold36_block13_5y | 52.87 | 0.88 | 0.37 | 144 | 2.4 |
| long_swing_h_room_0p25_target_2p50_hold36_block13_5y | 50.26 | 0.84 | 0.52 | 97 | 1.6 |
| long_pdh_swing_h_room_0p25_target_2p00_hold24_block13_5y | 45.14 | 0.75 | 0.38 | 119 | 2.0 |
| long_pdh_swing_h_room_0p25_target_1p50_hold24_block13_5y | 37.05 | 0.62 | 0.31 | 119 | 2.0 |
| long_pdh_swing_h_room_0p50_target_1p50_hold24_block13_5y | 36.76 | 0.61 | 0.25 | 146 | 2.4 |
| long_pdh_swing_h_room_1p00_target_1p50_hold24_block13_5y | 35.37 | 0.59 | 0.23 | 157 | 2.6 |
| long_swing_h_room_0p25_target_1p50_hold24_block13_5y | 34.55 | 0.58 | 0.35 | 100 | 1.7 |
| long_swing_h_room_0p50_target_1p50_hold24_block13_5y | 34.26 | 0.57 | 0.28 | 122 | 2.0 |

Raw outputs and CSV summary live in `results/structure_break_salvage/`.

## Read

This is the cleanest quality cluster found so far: the tight `SWING_H` version reached `0.52R/trade`, and the broader `PDH,SWING_H` version produced the best total R. The failure is frequency. Even the best total variant is only `0.88R/month`, which is useful as a component but nowhere near the `6R-8R/month` target.

Verdict: keep as a positive micro-edge candidate, but do not treat it as the primary strategy engine.
