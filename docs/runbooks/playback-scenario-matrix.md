# Playback Scenario Matrix

This is the minimum high-signal scenario pack for the first disciplined Playback phase.

Do not expand breadth before these are covered cleanly.

## Core Entry Scenarios

| ID | Scenario | Why It Matters | Primary Evidence |
| --- | --- | --- | --- |
| `PB-01` | Clean long continuation winner | proves long setup, trigger, protection, trail, reset | `Patterns_`, `Trades_`, `Risk_` |
| `PB-02` | Clean short continuation winner | proves short symmetry | `Patterns_`, `Trades_`, `Risk_` |
| `PB-03` | Armed entry expires without fill | proves trigger expiry and cancel flow | `Patterns_`, `Risk_`, `Debug_` |
| `PB-04` | Setup invalidates before trigger | proves rejection/reset path | `Patterns_`, `Debug_` |
| `PB-05` | No-room-to-structure rejection | proves structure-room gate is real | `Patterns_` |
| `PB-06` | Risk-too-large rejection | proves stop/ATR sanity gate is real | `Patterns_` |

## Runtime / Order-Management Scenarios

| ID | Scenario | Why It Matters | Primary Evidence |
| --- | --- | --- | --- |
| `PB-07` | Entry fills and initial stop appears promptly | first hard runtime truth check | `Risk_`, `Debug_`, `Trades_` |
| `PB-08` | Simple trail tightens monotonically | validates donor-style trail behavior | `Risk_`, `Trades_` |
| `PB-09` | Protective stop change goes pending-ack then settles | validates transport/result ownership | `Risk_`, `Debug_` |
| `PB-10` | Protective replace path | validates quantity/side mismatch recovery | `Risk_`, `Debug_` |
| `PB-11` | Flatten-before-close | validates finalization and reset | `Risk_`, `Trades_`, `Debug_` |
| `PB-12` | Child reject / recovery | validates OCO resubmit / flatten safety | `Risk_`, `Debug_` |

## Recovery / Resilience Scenarios

| ID | Scenario | Why It Matters | Primary Evidence |
| --- | --- | --- | --- |
| `PB-13` | Reconnect grace / orphan scan | validates donor-style recovery lane | `Risk_`, `Debug_` |
| `PB-14` | Restore with active live position | validates adoption and coverage rebuild | `Risk_`, `Debug_`, runtime snapshot |
| `PB-15` | Coverage-loss heartbeat detects missing protection | validates silent protection recovery | `Risk_`, `Debug_` |

## Priority Order

Run these first:

1. `PB-01`
2. `PB-02`
3. `PB-03`
4. `PB-07`
5. `PB-08`
6. `PB-11`

After those are stable, move to:

1. `PB-04`
2. `PB-05`
3. `PB-06`
4. `PB-09`
5. `PB-10`
6. `PB-12`
7. `PB-13`
8. `PB-14`
9. `PB-15`

## Session Capture Format

For each scenario, capture:

- `scenario_id`
- date and session
- instrument and timeframe
- expected behavior
- observed behavior
- log files consulted
- verdict:
  - `pass`
  - `fail`
  - `inconclusive`
- next action

Use:

- [playback-issue-template.md](C:/Users/bessp/Documents/NinjaTrader 8/bin/Custom/Strategies/SecondLegAdvancedMESStrategy/docs/runbooks/playback-issue-template.md)

for anything that fails or looks suspicious.
