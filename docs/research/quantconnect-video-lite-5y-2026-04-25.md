# QuantConnect Video-Lite 5-Year Sweep - 2026-04-25

## Purpose

Test whether the simplified, video-faithful `VideoSecondEntryLite` entry mode can reach
the owner's desired frequency target while retaining non-random expectancy.

This is a QuantConnect/LEAN research harness only. It places no orders. It evaluates
virtual outcomes with a static stop, 2R target, and stop-first same-bar ambiguity.

## Setup

- Repo: `SecondLegAdvancedMESStrategy`
- Local harness: `research/quantconnect/SecondLegQCSpike/Algorithm.cs`
- QuantConnect project id used for current cloud runs: `30609547`
- Cloud display name observed: `SecondLegQCSpike 1`
- Window: `2021-04-24` through `2026-04-23`
- Instrument: MES continuous futures
- Bar size: 5-minute
- Summary CSV: `research/quantconnect/results/video_lite/video_lite_summary.csv`
- Runner: `research/quantconnect/scripts/run_secondleg_video_lite_matrix.ps1`
- Summarizer: `research/quantconnect/scripts/summarize_secondleg_video_lite_results.ps1`

## Results

| Variant | Triggered | Approx/Month | 2R Wins | Stops | Timeouts | Net R | Avg R | Backtest ID |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Lite both, no structure veto | 1564 | 26.1 | 377 | 920 | 267 | -33.02 | -0.02 | `a5a116c5aecab95a1e60614406edc323` |
| Lite long, impulse 0.50 ATR | 927 | 15.4 | 225 | 531 | 171 | +16.09 | +0.02 | `cb73c020ca6673f245b2ec3b206c38ae` |
| Lite long, impulse 0.75 ATR | 926 | 15.4 | 225 | 530 | 171 | +17.09 | +0.02 | `849f2570b009c550868de7d2a15c53d9` |
| Lite long, impulse 1.00 ATR | 921 | 15.4 | 225 | 527 | 169 | +19.16 | +0.02 | `d9c60ba08e7eee0be036ccd3ddd34691` |
| Lite long, impulse 1.25 ATR | 910 | 15.2 | 223 | 522 | 165 | +14.95 | +0.02 | `cc2036b3f24e6502525ec22ae6ff912d` |
| Lite short, no structure veto | 639 | 10.6 | 154 | 389 | 96 | -45.71 | -0.07 | `52ad32be81bb2e30d3fb668062566db5` |
| Lite both, structure veto on | 633 | 10.6 | 127 | 374 | 132 | -59.29 | -0.09 | `544e1dd1cc1d64863bc8556082e008bd` |

## Read

The simplified video-style detector solves the frequency problem. `both` mode reached
about `26` trades/month over five years, which is above the requested minimum.

The edge is not clean yet. Taking every lite signal on both sides is slightly negative.
The short side is the obvious drag. The long side is positive across nearby impulse
thresholds, but the average edge is thin at about `+0.02R/trade`.

Structure-room veto did not help in this harness. It reduced frequency sharply and made
expectancy worse. For this research lane, structure should remain observational unless
a better definition is tested.

## Decision

Do not promote `VideoSecondEntryLite` as a finished edge.

Use it as the active research lane because it finally creates enough sample size to learn
from. The next best tests should focus on improving long-only selection and outcome
modeling instead of trying to rescue the short side immediately.

## Next Tests

- Add year-by-year and month-by-month summary stats so the long-side `+0.02R` is not hiding
  one lucky regime.
- Test long-only context filters that are still close to the video idea, such as time-of-day
  buckets, distance from EMA, and signal-bar quality.
- Test realistic trade management variants after entry edge is clearer: 1R partial, runner,
  breakeven-after-1R, and timeout rules.
- Keep shorts disabled in the research baseline until a separate short thesis earns its way
  back in.
