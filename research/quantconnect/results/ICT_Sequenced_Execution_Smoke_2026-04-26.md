# ICT Sequenced Execution Smoke Test - 2026-04-26

Goal: address the intrabar sequencing problem in ICT/FVG limit-entry tests.

The previous one-minute OHLC tests were too sensitive to same-bar assumptions:

- Conservative stop-first could make ICT look terrible.
- Optimistic target-first could make the broad ICT 2022 model look excellent.
- That spread proved the test was dominated by unknown within-bar order, not just strategy logic.

## Harness Change

Added `entryMode=ICTSequenced`.

Behavior:

- Subscribe to MES continuous futures at `Resolution.Second`.
- Still build 1-minute signal bars with LEAN consolidation.
- Detect the same ICT sweep -> displacement/MSS -> FVG retrace setup on 1-minute signal bars.
- Manage execution from second bars:
  - Pending FVG entry fills when a later second bar touches the entry.
  - The fill second does not immediately stop/target the trade.
  - Stop/target/timeout are evaluated from subsequent second bars in timestamp order.

This is still a research harness, not live order routing, but it removes the biggest one-minute OHLC ambiguity.

## Smoke Test Window

Period: 2026-02-23 through 2026-04-23.

This was intentionally short because second/tick data availability and compute cost are different from minute data. The purpose was to validate mechanics before doing larger runs.

## Results

| Variant | Net R | Avg R | Trades | Touch 1R | Wins | Stops | Read |
|---|---:|---:|---:|---:|---:|---:|---|
| AM Silver Bullet, short only, PDH sweep, 2R | 1.00 | 0.20 | 5 | 2 | 2 | 3 | Mechanics work; sample too small |
| ICT 2022 broad RTH, both sides, 1.5R | -9.50 | -0.23 | 42 | 17 | 13 | 29 | Did not reproduce target-first optimism |

Backtest IDs:

| Variant | QuantConnect Backtest ID |
|---|---|
| AM Silver Bullet PDH short, 2R | `7198f1ce2ed355e00f33782c10d6d326` |
| ICT 2022 broad RTH, 1.5R | `74c9220af224ac3aab6acd1db34d0219` |

## Read

This is the right testing direction.

The first sequenced run says:

- The platform can run the second-resolution sequencing harness.
- The PDH short pocket remains slightly positive in this small recent sample.
- The broad ICT 2022 model does not look rescued by realistic second-bar sequencing in this window.

This does not fully kill ICT because the sample is only about two months. It does kill the idea that the `+181R` target-first result should be trusted.

## Next Step

Run rolling second-resolution windows if QuantConnect data access allows it:

- 2025-12-23 to 2026-02-23.
- 2025-10-23 to 2025-12-23.
- Continue backward until data access or compute limits block us.

If second-resolution history is limited, use the user's own tick/second data locally or in LEAN CLI local mode. The sequencing harness is now structurally ready for that.
