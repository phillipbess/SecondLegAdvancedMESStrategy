# QuantConnect Feasibility Spike - 2026-04-24

## Purpose

Test whether QuantConnect Cloud can provide a seamless, non-NT8 path for large-scale ES/MES-style strategy research.

## Setup Completed

- Installed LEAN CLI `1.0.225`
- Authenticated to QuantConnect Cloud
- Initialized local workspace at `C:\Users\bessp\Documents\QuantConnectLean`
- Created cloud project `SecondLegQCSpike`
- Pushed C# project through LEAN CLI
- Ran cloud backtests successfully

No credentials are stored in this repo.

## QuantConnect Account Resources Observed

The active organization has:

- Researcher seat
- `B4-12` backtest node
- free `R-MICRO` research node

The B4-12 node was used by the cloud backtest.

## Spike Algorithm

Saved copy:

- `research/quantconnect/SecondLegQCSpike/Algorithm.cs`

Behavior:

- subscribes to `Futures.Indices.MicroSP500EMini`
- uses minute data
- maps continuous futures by open interest
- consolidates to 5-minute bars
- filters regular session bars from `09:30` through `15:55` New York time
- implements an orderless version of the SecondLeg entry detector
- models MES risk using `$5` per point and `RiskPerTrade = $150`
- observes virtual outcomes after a trigger without placing orders
- reports detector funnel counts, block reasons, and simple R-multiple outcome stats
- accepts LEAN CLI parameters for date range and key entry thresholds

## Cloud Data Plumbing Result

Backtest:

- name: `ES data access spike runtime stats`
- project id: `30595690`
- backtest id: `2f2ebbd68b277910665af56e1a6cd33e`
- URL: `https://www.quantconnect.com/project/30595690/2f2ebbd68b277910665af56e1a6cd33e`

Runtime statistics:

- total 5-minute bars: `22,839`
- RTH 5-minute bars: `19,602`
- calendar months touched: `13`
- orders: `0`

Date range:

- `2025-04-24` through `2026-04-23`

## Read

QuantConnect Cloud is viable for the next research phase.

The important proof points are:

- C# cloud project compiled successfully.
- ES continuous futures minute data loaded for about one trailing year.
- 5-minute RTH consolidation worked.
- Runtime statistics were visible in CLI output.

## Entry Detector Result

Backtest:

- name: `SecondLeg MES virtual outcome v1`
- project id: `30595690`
- backtest id: `8e5c39d85f9a284b7c7076eb2d50b1e4`
- URL: `https://www.quantconnect.com/project/30595690/8e5c39d85f9a284b7c7076eb2d50b1e4`

Scope:

- date range: `2025-04-24` through `2026-04-23`
- total 5-minute bars: `22,839`
- RTH 5-minute bars: `19,602`
- orders placed: `0`

Detector funnel:

- trend bars: `8,828`
- impulses: `502`
- leg 1 pullbacks: `279`
- separations: `145`
- leg 2 candidates: `44`
- armed signals: `9`
- triggered signals: `7`
- expired signals: `2`
- long armed: `3`
- short armed: `6`

Top blockers:

- `SecondLegTooShallow`: `383`
- `PullbackTooDeep`: `172`
- `SecondLegTooDeep`: `151`
- `PullbackTooLong`: `121`
- `SecondLegTooStrong`: `57`

Final gate blockers:

- `SignalInvalid`: `7`
- `StopTooWide`: `17`
- `StructureRoom`: `15`
- `RiskTooSmall`: `5`

Virtual outcome model:

- one simulated position at a time
- no real orders
- stop-first when a 5-minute bar contains both stop and target
- 2R target / static stop / timeout after 24 bars or end of RTH session

Virtual outcome result:

- virtual trades: `7`
- touched 1R: `3`
- reached 2R: `2`
- stopped: `4`
- timed out: `1`
- net R: `+1.35R`
- average R per triggered setup: `+0.19R`

## Research Read

The QuantConnect spike is now useful, but not decisive.

What it supports:

- The strategy can be ported into LEAN C# cleanly enough for broad research.
- QC Cloud can run MES continuous futures minute data over a one-year window.
- The exact current rule stack is sparse but not dead after correcting MES point value.
- The first virtual outcome pass is slightly positive under conservative same-bar handling.

What it does not prove:

- Seven triggered trades is far too small to claim an edge.
- The virtual outcome model is a research proxy, not live order-management parity.
- The next pass needs longer history, parameter sweeps, and eventually trade/order simulation.

Follow-up 5-year sweep:

- `docs/research/quantconnect-5y-sweep-2026-04-24.md`

## Next Step

Move from feasibility to research:

1. Run the detector over the maximum available free/paid QC MES history.
2. Add CSV-style debug exports for every armed/triggered virtual trade.
3. Sweep only the few core parameters that control sparsity:
   - `MinImpulseAtrMultiple`
   - `MinPullbackRetracement`
   - `MaxPullbackRetracement`
   - `SecondLegMaxMomentumRatio`
   - `MinRoomToStructureR`
4. Compare static 1R/2R outcomes against the NT8 trail logic later.
5. Keep the QC version as a research harness, not the production runtime.

Do not declare the edge proven from this run. Treat it as the first clean research baseline.
