# ADRs

This folder holds Architecture Decision Records for repo-level strategy choices that
should stay explicit as the runtime evolves.

## M0 Scope

- record boundary and reuse decisions before implementation drifts
- define which runtime behaviors are authoritative vs strategy-specific
- keep execution realism and promotion expectations written down early

## Current ADR Set

- `ADR-0001` - repo boundary and reuse policy
- `ADR-0002` - runtime-core authority
- `ADR-0003` - second-leg state machine
- `ADR-0004` - execution realism policy
- `ADR-0005` - promotion gates

Each ADR is currently an M0 placeholder and should be promoted from `Proposed` to
`Accepted` only after the decision is reviewed against playback and validation needs.
