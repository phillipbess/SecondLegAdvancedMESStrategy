# Runbooks

This folder holds operator and validation runbooks for the strategy lifecycle.

## Scope

- capture repeatable validation flows for the stripped `v1` strategy
- separate playback, walk-forward, shadow/sim, and rollback procedures
- keep future evidence collection consistent across reviewers

## Current Runbooks

- `playback.md`
- `playback-preflight.md`
- `playback-scenario-matrix.md`
- `log-review-guide.md`
- `golden-log-sequences.md`
- `playback-issue-template.md`
- `signoff-evidence.md`
- `walk-forward.md`
- `shadow-sim.md`
- `rollback-disable.md`

The immediate operator path is now:

1. `playback-preflight.md`
2. `playback-scenario-matrix.md`
3. `playback.md`
4. `log-review-guide.md`

That sequence should make the first Playback pass much more disciplined and much more
useful.
