# ICT Silver Bullet / Judas / 2022 Model Research - 2026-04-26

Goal: test whether deterministic ICT-style ES setups can supply the missing second engine for the Resonance Capital research stack.

Public ICT descriptions generally frame the setup around:

- A liquidity sweep.
- A displacement / market-structure-shift move.
- A fair-value-gap retrace entry.
- Time windows such as the AM Silver Bullet window around 10:00-11:00 New York.

Reference descriptions used for the research translation:

- FairValueHub: `https://fairvaluehub.de/en/blog/ict-silver-bullet-en`
- FXOpen: `https://fxopen.com/blog/en/what-is-the-ict-silver-bullet-strategy-and-how-does-it-work/`
- EBC: `https://www.ebc.com/forex/what-is-the-ict-silver-bullet-meaning-rules-and-examples.html`

## Deterministic Translation

The QuantConnect research engine implements the idea as:

1. Detect sweep of selected liquidity:
   - `swing`: recent swing high/low from the prior `ictLiquidityLookbackBars`.
   - `or`: opening range high/low.
   - `pd`: prior RTH high/low.
2. Assign opposite bias after sweep:
   - Sweep high => short bias.
   - Sweep low => long bias.
3. Require displacement and market-structure shift:
   - Candle body >= `ictMinDisplacementAtr * ATR`.
   - Close breaks local high/low over `ictMssLookbackBars`.
4. Require a same-bar FVG:
   - Bullish FVG: current low > two-bars-back high.
   - Bearish FVG: current high < two-bars-back low.
5. Enter on retrace into the FVG at `ictEntryFvgPct`.
6. Stop beyond the swept extreme plus `ictStopBufferTicks`.

Important correction made during testing:

- `ictLiquiditySet=pd` originally still allowed swing sweeps because swing liquidity was always checked.
- This was fixed so `swing`, `or`, and `pd` are honored independently.
- The corrected results below supersede any earlier mislabeled `pd` / `or,pd` test output.

## Broad Model Results

Five-year period: 2021-04-24 through 2026-04-23.

| Variant | Net R | R/Month | Avg R | Trades | Trades/Month | Read |
|---|---:|---:|---:|---:|---:|---|
| Silver Bullet AM, both sides, 2R | -15.77 | -0.26 | -0.03 | 522 | 8.7 | Mildly negative |
| Silver Bullet AM, both sides, 1.5R | -22.67 | -0.38 | -0.04 | 522 | 8.7 | Negative |
| Judas open, both sides, 1.5R | -38.20 | -0.64 | -0.07 | 522 | 8.7 | Negative |
| Judas open, both sides, 2R | -48.83 | -0.81 | -0.09 | 522 | 8.7 | Negative |
| Silver Bullet PM, both sides, 2R | -43.52 | -0.73 | -0.08 | 558 | 9.3 | Negative |
| Silver Bullet PM, both sides, 1.5R | -55.25 | -0.92 | -0.10 | 558 | 9.3 | Negative |
| ICT 2022 broad RTH, both sides, 1.5R | -168.76 | -2.81 | -0.14 | 1225 | 20.4 | Too many bad trades |
| ICT 2022 broad RTH, both sides, 2R | -187.05 | -3.12 | -0.15 | 1225 | 20.4 | Failed |

Broad verdict: raw ICT-style rules are not enough. The 2022 model fires at the desired frequency, but the expectancy is strongly negative.

## Corrected Salvage Results

The only positive pocket after fixing liquidity-set handling was:

- AM Silver Bullet window: 10:00-11:00 New York.
- Short only.
- Prior-day-high sweep only.
- FVG retrace entry.

| Variant | Net R | R/Month | Avg R | Trades | Trades/Month | Read |
|---|---:|---:|---:|---:|---:|---|
| `silverbullet_am_short_pd_2r` | 24.88 | 0.41 | 0.14 | 181 | 3.0 | Best corrected result |
| `silverbullet_am_short_pd_15r_disp1` | 23.92 | 0.40 | 0.30 | 80 | 1.3 | Cleaner, too low frequency |
| `silverbullet_am_short_pd_15r_disp075` | 21.90 | 0.36 | 0.18 | 120 | 2.0 | Decent quality, too low frequency |
| `silverbullet_am_short_pd_1r` | 21.78 | 0.36 | 0.12 | 181 | 3.0 | Positive but shallow |
| `silverbullet_am_short_pd_125r` | 17.41 | 0.29 | 0.10 | 181 | 3.0 | Positive but weak |
| `silverbullet_am_short_pd_15r` | 15.29 | 0.25 | 0.08 | 181 | 3.0 | Positive but weak |
| `silverbullet_am_short_orpd_125r` | 3.10 | 0.05 | 0.01 | 281 | 4.7 | ORH entries diluted the PDH edge |
| `silverbullet_am_short_orpd_15r` | 2.94 | 0.05 | 0.01 | 281 | 4.7 | Not useful |

## Yearly Validation: Best Corrected Variant

`silverbullet_am_short_pd_2r`: 1-minute bars, AM Silver Bullet window, short only, prior-day-high sweep, 2R target, 45-minute max outcome hold.

| Period | Net R | R/Month | Avg R | Trades | Trades/Month | 2R Wins | Stops | Timeouts |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 2021-2022 | 6.96 | 0.58 | 0.18 | 38 | 3.2 | 14 | 22 | 2 |
| 2022-2023 | 3.54 | 0.30 | 0.09 | 38 | 3.2 | 13 | 22 | 3 |
| 2023-2024 | -0.46 | -0.04 | -0.01 | 32 | 2.7 | 9 | 20 | 3 |
| 2024-2025 | 1.96 | 0.16 | 0.05 | 42 | 3.5 | 11 | 23 | 8 |
| 2025-2026 | 12.88 | 1.07 | 0.42 | 31 | 2.6 | 14 | 16 | 1 |
| Total | 24.88 | 0.41 | 0.14 | 181 | 3.0 | 61 | 103 | 17 |

Backtest IDs:

| Variant / Period | QuantConnect Backtest ID |
|---|---|
| Broad 2021-2026 | `68dba491623bcf24e84ffb0944dfd7bd` |
| 2021-2022 | `2f991ef89d998a997fd3cd32f349801c` |
| 2022-2023 | `1716483829eaf59e0b0b76323f6e2ba0` |
| 2023-2024 | `8097cbb5a0ee85ce1a652058554f9624` |
| 2024-2025 | `e04c0c3922d6acb6c3bc263d6e35c4d0` |
| 2025-2026 | `40d8486f7ed579ef42408684c917a4b5` |

## Read

This is the cleanest ICT result so far, but it is not the answer to the business goal.

What is encouraging:

- The best corrected pocket is positive over five years.
- Four of five yearly slices are positive.
- The trade idea is intuitive: if price raids prior-day high during the AM window and then displaces lower into an FVG, continuation lower has some signal.
- The 2025-2026 slice is strong enough that the idea should not be discarded completely.

What blocks promotion:

- It only averages about `0.41R/month`.
- It only trades about `3 times/month`.
- Average trade quality is only `0.14R` in the best 2R version.
- The broad ICT 2022 model is badly negative.
- Adding ORH to PDH diluted the edge, so the signal is narrow rather than general.

## Verdict

Do not promote ICT as the new main engine.

Keep `silverbullet_am_short_pd_2r` as a possible micro-edge or stack component, but only if it remains additive to the current `CandidateStack` benchmark. It cannot solve the `6R-8R/month` goal by itself.

The research lesson is useful: named ICT concepts are not automatically profitable. The only tested pulse is a narrow small-account setup: AM prior-day-high raid, bearish displacement, bearish FVG retrace, short continuation.

## Intrabar Sequencing Sensitivity

Follow-up audit found that ICT/FVG limit-entry tests are extremely sensitive to one-minute OHLC sequencing:

- Default policy is conservative: if stop and target are both touched in the same bar, count stop first.
- `skipEntryBarOutcome=true` avoids judging stop/target on the same bar that touches the FVG entry.
- `sameBarPolicy=targetfirst` is an optimistic upper bound, not a realistic fill model.

Sensitivity results:

| Test | Policy | Net R | R/Month | Read |
|---|---|---:|---:|---|
| AM Silver Bullet PDH short, 2R | Default stop-first | 24.88 | 0.41 | Conservative baseline |
| AM Silver Bullet PDH short, 2R | Skip entry bar | 0.88 | 0.01 | Positive pocket largely disappears |
| AM Silver Bullet PDH short, 2R | Target-first | 63.88 | 1.06 | Optimistic upper bound |
| ICT 2022 broad RTH, 1.5R | Default stop-first | -168.76 | -2.81 | Failed under conservative sequencing |
| ICT 2022 broad RTH, 1.5R | Skip entry bar | -246.26 | -4.10 | Worse |
| ICT 2022 broad RTH, 1.5R | Target-first | 181.24 | 3.02 | Shows massive sequencing dependence |

This changes the interpretation: the broad ICT result is not safe to call from one-minute OHLC alone. The huge spread between stop-first and target-first means the model needs tick-level or second-level sequencing before any final verdict.

## Next Best Step

Do not do a broad ICT parameter sweep on one-minute OHLC data.

If we continue this branch, the high-value follow-up is to rebuild the ICT/FVG test with finer sequencing:

- Use tick or second-level data if available.
- Model entry first, then stop/target after entry only.
- Re-test the broad ICT 2022 and AM PDH short variants.
- Only then decide whether the idea is dead.

After sequencing is fixed, test whether any surviving ICT edge is additive to the current best stack:

- Run it inside `CandidateStack`.
- Ensure no same-day overlap with opening-auction trades.
- Compare yearly combined results.
- Keep it only if it adds at least `0.5R/month` without damaging 2023-2024 or 2024-2025.
