# QuantConnect Frequency Probe - 2026-04-24

## Purpose

Test whether the current 5-minute second-leg architecture can reach the owner's desired
trade frequency of roughly `20` trades per month by loosening the strict filters.

This was a research probe, not a production backtest.

## Method

- Instrument: MES continuous futures
- Window: `2021-04-24` through `2026-04-23`
- Bar size: 5-minute
- Orders: none
- Outcome model: virtual static stop, 2R target, stop-first same-bar ambiguity
- Goal: see whether frequency can be raised without destroying expectancy

## Results

| Variant | Triggered | Approx/Month | 2R Wins | Stops | Timeouts | Net R | Avg R | Backtest ID |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Strict baseline | 37 | 0.6 | 10 | 23 | 4 | -2.32 | -0.06 | `b68aa58944a6d95c0ff93b6a36ef5339` |
| `both_loose_1` | 265 | 4.4 | 60 | 153 | 52 | -15.49 | -0.06 | `af2c9bcea393f71ff5f4b9c89a5a7e7d` |
| `short_loose_1` | 147 | 2.5 | 33 | 90 | 24 | -17.78 | -0.12 | `303d1eb001f3d73bfb12d30accc6f135` |
| `both_loose_2` | 353 | 5.9 | 75 | 206 | 72 | -25.88 | -0.07 | `d3d9226c0157cba6cbc8e5e8c1de6807` |
| `both_loose_stop_unbounded` | 459 | 7.7 | 85 | 253 | 121 | -25.12 | -0.05 | `1714e915d8f41dec6dd29e51ddb4736d` |

Raw CLI outputs:

- `research/quantconnect/results/frequency_probe/`

## Read

The important read is not that the strategy is impossible.

The important read is that the current strict 5-minute architecture does not get close
to `20` trades per month, even when loosened aggressively. The loosest probe reached only
about `7.7` trades per month and still had negative expectancy under the simple static
outcome model.

So the answer is:

- do not keep randomly loosening `StrictV1`
- preserve the clean strict contract for auditability
- test a separate video-aligned/lite branch
- consider faster timeframes or a different execution venue for frequency research

## Decision

This probe is the reason `VideoSecondEntryLite` exists as a separate mode instead of
silently mutating `StrictV1`.

The next research question is not "Can strict 5-minute be loosened enough?"

The next research question is:

Can a simpler video-faithful second-entry detector produce enough trades and still show
non-random expectancy when tested separately?
