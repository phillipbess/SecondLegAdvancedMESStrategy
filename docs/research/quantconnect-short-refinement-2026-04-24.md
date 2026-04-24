# QuantConnect Short-Only Refinement - 2026-04-24

## Purpose

Follow the side-split evidence and test whether the short-side edge is stable around the best leg-2 momentum zone.

## Method

- Instrument: MES continuous futures
- Window: `2021-04-24` through `2026-04-23`
- Side filter: short only
- Outcome model: static stop / 2R target / 24-bar or RTH timeout
- Same-bar ambiguity: stop-first
- Orders: none

## Short-Only Refinement Results

| Variant | Triggered | 2R Wins | Stops | Timeouts | Net R | Avg R | Read |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `leg2=0.60`, room `1.00` | 19 | 8 | 9 | 2 | +8.25 | +0.43 | Best result so far |
| `leg2=0.65`, room `1.00` | 20 | 8 | 10 | 2 | +7.25 | +0.36 | Confirms prior result |
| `leg2=0.70`, room `1.00` | 20 | 8 | 10 | 2 | +7.25 | +0.36 | Same as `0.65` in this pass |
| `leg2=0.65`, room `0.75` | 25 | 9 | 13 | 3 | +5.84 | +0.23 | More trades, diluted quality |
| `leg2=0.65`, room `1.25` | 9 | 2 | 6 | 1 | -0.65 | -0.07 | Too restrictive / adverse selection |

Raw outputs:

- `research/quantconnect/results/short_refine/`

Summary CSV:

- `research/quantconnect/results/short_refine/short_refine_summary.csv`

## Year-By-Year Stability For `leg2=0.60`

| Window | Triggered | 2R Wins | Stops | Timeouts | Net R | Avg R |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `2021-04-24` to `2022-04-23` | 3 | 1 | 1 | 1 | +0.89 | +0.30 |
| `2022-04-24` to `2023-04-23` | 4 | 2 | 2 | 0 | +2.00 | +0.50 |
| `2023-04-24` to `2024-04-23` | 5 | 2 | 3 | 0 | +1.00 | +0.20 |
| `2024-04-24` to `2025-04-23` | 3 | 1 | 2 | 0 | 0.00 | 0.00 |
| `2025-04-24` to `2026-04-23` | 4 | 2 | 1 | 1 | +4.35 | +1.09 |

Raw outputs:

- `research/quantconnect/results/short_yearly/`

Summary CSV:

- `research/quantconnect/results/short_yearly/short_0_60_yearly_summary.csv`

## Read

This is the strongest research evidence so far.

The short-side strategy is still sparse, but the five-year result is not carried by a single yearly window. The `leg2=0.60` setting was non-negative in all five yearly windows and positive in four of five.

The structure-room read is also useful:

- loosening room from `1.00R` to `0.75R` increased trades but reduced average R
- tightening room to `1.25R` made the result negative
- current evidence favors keeping room near `1.00R`

## Current Research Candidate

The current best QC proxy candidate is:

- side: short only
- `SecondLegMaxMomentumRatio = 0.60`
- `MinRoomToStructureR = 1.00`
- baseline retracement and impulse settings unchanged

Do not call this production-ready yet. The samples are still small, and the QC model is a static virtual outcome model rather than the full NT8 order-management/trailing-stop runtime.

## Next Step

The next high-value step is to decide whether to mirror this research candidate into NT8 as a test profile, not as a final production default:

- add a short-only test mode or profile
- set `SecondLegMaxMomentumRatio` near `0.60`
- keep room-to-structure at `1.00R`
- run playback and compare NT8 trade rows against QC candidate dates
