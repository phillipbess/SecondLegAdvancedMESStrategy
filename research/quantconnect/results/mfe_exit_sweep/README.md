# MFE / Exit Sweep Snapshot

Generated from QuantConnect cloud project `30609547` / `SecondLegQCSpike 1`.

## Short Read

Shrinking the profit target did **not** rescue the broad high-frequency version. The 2R target remained the best broad fixed-target variant:

```text
0.50R target: 1353 trades, 22.6/month, +6.68R,  Avg R 0.00
0.75R target: 1315 trades, 21.9/month, +15.40R, Avg R 0.01
1.00R target: 1287 trades, 21.4/month, +18.15R, Avg R 0.01
1.25R target: 1267 trades, 21.1/month, -3.94R,  Avg R 0.00
1.50R target: 1253 trades, 20.9/month, -8.63R,  Avg R -0.01
2.00R target: 1218 trades, 20.3/month, +34.37R, Avg R 0.03
```

So the issue is **not** that we were using too large a target and failing to harvest small MFE. The stronger read is that the broad entry set contains too much chop.

## Better Signal: Near Structure

The runtime buckets suggested that trades with nearby structure were carrying the positive expectancy. Direct room-filter tests confirmed it:

```text
room <= 0.25R, both sides: 403 trades, 6.7/month, +79.71R, Avg R 0.20
room <= 0.50R, both sides: 623 trades, 10.4/month, +85.46R, Avg R 0.14
room <= 1.00R, both sides: 893 trades, 14.9/month, +68.65R, Avg R 0.08
```

Long-only near-structure was cleaner:

```text
room <= 0.25R, long only: 260 trades, 4.3/month, +75.69R, Avg R 0.29
room <= 0.50R, long only: 401 trades, 6.7/month, +84.29R, Avg R 0.21
```

## Interpretation

The edge is probably not "any second entry in trend." It looks more like:

```text
Second-entry continuation signal near a nearby structure level,
where that level may be acting as a breakout / liquidity magnet rather than a veto.
```

This is important because it reverses one earlier intuition: structure-room as a veto may be harmful for this idea. Near structure may be where the MFE is.

## Files

```text
mfe_exit_sweep_summary.csv
target_*_hold24_block13_short11_5y.txt
room_max_*_target_2p00_hold24_block13_short11_5y.txt
long_room_max_*_target_2p00_hold24_block13_5y.txt
```

Rerun:

```powershell
.\scripts\run_secondleg_mfe_exit_sweep.ps1 -PushLocal -Force
```
