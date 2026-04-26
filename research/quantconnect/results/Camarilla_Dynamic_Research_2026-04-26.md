# Camarilla Dynamic Research - 2026-04-26

Goal: test whether Camarilla pivots plus session-anchored VWAP slope can become a new ES day-trading engine for the Resonance Capital research stack.

Formula used:

- `H3 = priorClose + priorRange * 1.1 / 4`
- `H4 = priorClose + priorRange * 1.1 / 2`
- `L3 = priorClose - priorRange * 1.1 / 4`
- `L4 = priorClose - priorRange * 1.1 / 2`

This is the standard H3/H4 convention used by common Camarilla references. The pasted `/2` and `/1.1` H3/H4 version would push levels much farther away and was not used for the first research pass.

## Model Tested

Dynamic mode:

- Flat anchored-VWAP slope: allow H3/L3 fade logic.
- Strong anchored-VWAP slope: allow H4/L4 breakout logic.
- Slope is normalized by ATR: `(sessionVWAP[0] - sessionVWAP[N]) / N / max(ATR, tickSize)`.
- Base thresholds: flat <= `0.025`, breakout >= `0.035`.

Initial classic Camarilla stops used H4/L4 as the stop reference. That was too wide for the small-account constraint, so a tradable stop mode was added:

- `level`: classic Camarilla H4/L4 stop.
- `atr`: fixed ATR stop from the signal close.
- `tighter`: min/max of classic level stop and ATR stop, using the tighter stop.

## Five-Year Matrix

| Variant | Net R | R/Month | Avg R | Trades | Trades/Month | Read |
|---|---:|---:|---:|---:|---:|---|
| `tight_dynamic_1p25` | 106.03 | 1.77 | 0.10 | 1046 | 17.4 | Best small-account-compatible version |
| `atr_dynamic_1p0` | 97.81 | 1.63 | 0.09 | 1046 | 17.4 | Similar, more stop-outs |
| `raw_dynamic` | 95.54 | 1.59 | 0.09 | 1041 | 17.4 | Shows raw pattern has pulse when wide-stop sizing block is removed |
| `atr_dynamic_1p25` | 90.54 | 1.51 | 0.09 | 1045 | 17.4 | Positive, but not best |
| `tight_dynamic_1p25_long` | 80.22 | 1.34 | 0.12 | 674 | 11.2 | Cleaner than both-side in recent data, lower total R |
| `raw_fade_short_15r` | 70.25 | 1.17 | 0.13 | 542 | 9.0 | Short H3 fade has some historical edge, but recent weakness matters |
| `dynamic_base` | 2.92 | 0.05 | 0.02 | 118 | 2.0 | Classic level stops with small-account sizing are not usable |

## Yearly Validation: Best Both-Side Variant

`tight_dynamic_1p25`: both sides, dynamic mode, tighter stop, 1.25 ATR cap, 1.5R target, 24-bar hold.

| Period | Net R | R/Month | Avg R | Trades | Trades/Month |
|---|---:|---:|---:|---:|---:|
| 2021-2022 | 60.13 | 5.01 | 0.28 | 218 | 18.2 |
| 2022-2023 | 19.25 | 1.60 | 0.10 | 202 | 16.8 |
| 2023-2024 | 13.58 | 1.13 | 0.06 | 214 | 17.8 |
| 2024-2025 | 13.94 | 1.16 | 0.07 | 203 | 16.9 |
| 2025-2026 | -1.88 | -0.16 | -0.01 | 205 | 17.1 |
| Total | 105.02 | 1.75 | 0.10 | 1042 | 17.4 |

## Yearly Validation: Long-Only Variant

`tight_dynamic_1p25_long`: long only, dynamic mode, tighter stop, 1.25 ATR cap, 1.5R target, 24-bar hold.

| Period | Net R | R/Month | Avg R | Trades | Trades/Month |
|---|---:|---:|---:|---:|---:|
| 2021-2022 | 37.51 | 3.13 | 0.27 | 141 | 11.8 |
| 2022-2023 | 14.79 | 1.23 | 0.12 | 124 | 10.3 |
| 2023-2024 | 21.48 | 1.79 | 0.15 | 140 | 11.7 |
| 2024-2025 | 5.87 | 0.49 | 0.05 | 126 | 10.5 |
| 2025-2026 | -1.42 | -0.12 | -0.01 | 140 | 11.7 |
| Total | 78.23 | 1.30 | 0.12 | 671 | 11.2 |

## Read

This idea is not dead, but it is not strong enough to crown as the next core engine.

What improved:

- The standard Camarilla idea was almost useless with classic H4/L4 stops under small-account sizing.
- Adding a tighter ATR-capped stop made it tradable and lifted the best version to about `1.75R/month`.
- Trade frequency is useful: about `17 trades/month` for both-side dynamic.

What blocks promotion:

- The best both-side variant is slightly negative in 2025-2026.
- Long-only reduces short-side damage but is still slightly negative in 2025-2026.
- The edge appears front-loaded in 2021-2022 and fades after that.
- Average trade quality is only `0.10R`, which leaves less room for slippage and implementation error.

## Verdict

Keep Camarilla/AVWAP as a research artifact and possible stack component, but do not promote it as the new main strategy yet.

It is useful because it gives us frequency and a different structural idea. It is not enough because recent performance is too weak. The next test should not be another broad parameter sweep. The useful refinement would be to isolate why `CAM_L3_FADE` and `CAM_H4_BREAK` were the stronger long-side components, then add a context filter that specifically removes the 2024-2026 weak trades.

## Next Best Step

If we continue this path, test a long-side-only split:

- `CAM_L3_FADE` only.
- `CAM_H4_BREAK` only.
- Add market regime filters: prior day trend, gap direction, open relative to prior close, and first-30-minute direction.

If those do not stabilize 2024-2026, Camarilla should remain a low-priority add-on and we should move to a different edge family.
