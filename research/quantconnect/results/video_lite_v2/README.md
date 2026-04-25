# Video Second Entry Lite V2 Research Snapshot

Generated from QuantConnect cloud project `30609547` / `SecondLegQCSpike 1`.

## Current Read

The strongest 5-year candidate so far is:

```text
entryMode=VideoSecondEntryLite
sideFilter=both
liteMinImpulseAtr=0.75
liteMaxPullbackRetracement=0.95
liteMaxLeg2Retracement=0.95
liteMinSignalClosePct=0.55
liteBlockedHours=13
liteBlockedShortHours=11
```

Result:

```text
2021-04-24 through 2026-04-23
Triggered: 1218
Approx trades/month: 20.3
Net R: +34.37
Avg R: +0.03
Backtest: https://www.quantconnect.com/project/30609547/f7679219b36718a01ab06f45cc9d8521
```

This is not a final edge claim. It is a useful candidate because it meets the requested trade-frequency target while staying positive over the 5-year sample.

## Why This Candidate Exists

The unfiltered both-side frequency run reached the frequency target but lost money:

```text
v2_both_frequency_5y: 1532 trades, 25.5/month, -24.22R
```

The first useful filter was the 13:00 ET hour:

```text
v2_both_frequency_block13_5y: 1313 trades, 21.9/month, +17.11R
```

The next useful filter was short entries during 11:00 ET:

```text
v2_both_frequency_block13_short11_5y: 1218 trades, 20.3/month, +34.37R
```

## Diagnostic Upgrade

Because the current QuantConnect plan blocks Object Store downloads, the algorithm now publishes compact diagnostics directly into runtime statistics:

```text
Side L / Side S
H10 L / H10 S / ...
Room <0.25R / Room <0.50R / Room <1.00R / Room >=1R / Room Clear
```

Each bucket includes trade count, avg R, net R, 2R wins, stops, timeouts, touch-1R, average MFE, and average MAE.

## Files

```text
video_lite_v2_summary.csv
v2_long_quality_5y.txt
v2_long_frequency_5y.txt
v2_both_frequency_5y.txt
v2_both_frequency_block13_5y.txt
v2_both_frequency_block13_short11_5y.txt
```

Use this to rerun the matrix:

```powershell
.\scripts\run_secondleg_video_lite_v2_matrix.ps1 -PushLocal -Force
.\scripts\summarize_secondleg_video_lite_results.ps1 -InputDir .\results\video_lite_v2 -OutputCsv .\results\video_lite_v2\video_lite_v2_summary.csv -Months 60
```
