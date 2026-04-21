# Strategy Test Scaffold

Purpose: validate the second-leg continuation thesis before trade-management tuning obscures whether the entry edge is real.

These tests should stay strategy-specific and explainable. Prefer small deterministic fixtures before large playback suites.

## First Families

- `trend_context`: EMA/ATR/session context qualification.
- `impulse_scoring`: impulse quality and rejection of weak pushes.
- `pullback_state_machine`: leg counting, invalidation, and stale setup handling.
- `entry_qualification`: signal-bar, stop-entry, and room-to-structure gating.
- `regime_and_session_filters`: session participation and ATR regime acceptance.
- `golden_cases`: labeled bar-sequence fixtures for the first canonical setup/non-setup examples.

Implemented first-pass families:

- `trend_context`
- `impulse_scoring`
- `entry_qualification`
- `regime_and_session_filters`
- `pullback_state_machine`
- `armed_entry_lifecycle`
- `golden_cases`

All first-pass strategy families now have deterministic fixtures and should stay explainable before any larger playback-only expansion.
