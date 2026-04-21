# ADR-0001: Repo Boundary and Reuse Policy

- Status: Proposed
- Date: 2026-04-20

## Context

This repo exists as a sibling to `ManciniMESStrategy`, not as a branch, rewrite, or
hidden extension of it. We want to reuse hardened runtime lessons without inheriting
entry logic that belongs to Adam failed-breakdown behavior.

## Decision Placeholder

Pending review. The likely baseline is:

- keep this repo strategy-thesis independent
- reuse proven order/fill/exit safety patterns only when they port cleanly
- treat borrowed runtime concepts as adapted code, not implicit upstream coupling
- document every major reuse lane in `docs/Architecture_Reuse_Map.md`

## Consequences to Confirm

- clear ownership boundary between donor runtime patterns and new entry logic
- lower risk of accidental strategy drift back toward Mancini behavior
- easier validation because entry edge and runtime safety remain separable

## Open Questions

- which components are shared by concept only versus by near-direct port
- how will divergence from the donor runtime be tracked over time
- what review standard is required before copying any hardened subsystem
