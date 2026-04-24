# QuantConnect Side-Split Sweep - 2026-04-24

## Purpose

Test whether the promising `SecondLegMaxMomentumRatio = 0.65` result is genuinely a full-strategy improvement or mostly a short-side effect.

## Method

- Instrument: MES continuous futures
- Window: `2021-04-24` through `2026-04-23`
- Resolution: minute data consolidated to 5-minute RTH bars
- Outcome model: static stop / 2R target / timeout after 24 bars or end of RTH
- Same-bar ambiguity: stop-first
- Orders: none
- Harness parameter added: `sideFilter = both | long | short`

The first attempted side-split run was discarded because it did not include `--push` and therefore reused the prior cloud code. The results below are from the valid v2 runs where runtime stats showed `Params side=short` or `Params side=long`.

## Results

| Side | Leg2 Momentum Max | Triggered | 2R Wins | Stops | Timeouts | Net R | Avg R |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Short | `0.55` | 17 | 7 | 9 | 1 | +4.89 | +0.29 |
| Short | `0.65` | 20 | 8 | 10 | 2 | +7.25 | +0.36 |
| Short | `0.75` | 24 | 7 | 13 | 4 | +1.68 | +0.07 |
| Short | `0.80` | 25 | 8 | 13 | 4 | +3.68 | +0.15 |
| Long | `0.55` | 6 | 2 | 4 | 0 | 0.00 | 0.00 |
| Long | `0.65` | 7 | 2 | 5 | 0 | -1.00 | -0.14 |
| Long | `0.75` | 10 | 2 | 8 | 0 | -4.00 | -0.40 |
| Long | `0.80` | 12 | 2 | 10 | 0 | -6.00 | -0.50 |

Raw outputs:

- `research/quantconnect/results/side_split/`

## Read

The edge is not symmetric.

The short side is the only side with positive expectancy across this five-year QC proxy. The best short result is `leg2=0.65`, with `20` triggered trades, `+7.25R`, and `+0.36R` average R.

The long side is not proven. It is break-even only at `leg2=0.55`, then gets worse as the leg-2 momentum allowance loosens.

This does not mean long trades can never work. It means the current long entry contract is not yet carrying its weight in the same way the short side is.

## Implications

Do not blindly optimize a shared long/short parameter from combined results.

Best current research candidates:

- short side: keep investigating `SecondLegMaxMomentumRatio` around `0.55` to `0.65`
- long side: either disable in research or require a separate long-specific improvement before enabling
- production NT8 defaults should not be changed solely from this proxy pass

## Next Step

Run a short-only refinement pass around the best zone:

- `leg2=0.60`
- `leg2=0.65`
- `leg2=0.70`
- optional structure-room sensitivity only on shorts

Then inspect the short trade rows for clustering, year-by-year stability, and whether the profits are dominated by one market regime.

Follow-up completed:

- `docs/research/quantconnect-short-refinement-2026-04-24.md`
