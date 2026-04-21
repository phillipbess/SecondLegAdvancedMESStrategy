# ADR-0005: Promotion Gates

- Status: Proposed
- Date: 2026-04-20

## Context

This repo needs explicit gates for moving from concept to implementation, from playback
to walk-forward, and from simulation evidence to any live-use recommendation.

## Decision Placeholder

Pending review. The working direction is:

- define stage gates that align with the repo's validation rules
- require source safety, playback evidence, walk-forward durability, and shadow/sim review
- block promotion when entry quality is weak but exits make aggregate results look better
- keep the go/no-go standard stable across future strategy revisions

## Consequences to Confirm

- fewer premature promotions based on narrow historical wins
- stronger discipline around evidence quality and parameter robustness
- easier handoff from research to implementation to operator review

## Open Questions

- what exact metrics or scorecards should each gate require
- which failures are hard blockers versus caution flags
- who owns final promotion sign-off at each stage
