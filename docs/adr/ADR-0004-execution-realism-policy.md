# ADR-0004: Execution Realism Policy

- Status: Proposed
- Date: 2026-04-20

## Context

A strategy can look strong in idealized tests and fail once realistic fills, latency, and
cancellation behavior are applied. This repo needs an explicit realism policy early.

## Decision Placeholder

Pending review. The likely baseline is:

- prefer playback and shadow/sim evidence over optimizer-only outcomes
- define conservative assumptions for slippage, stop-entry fills, and stale cancel logic
- separate raw-entry validity from improvements caused by trade management
- require the same realism assumptions across promotion reviews

## Consequences to Confirm

- less risk of approving a fragile setup based on perfect-fill artifacts
- more honest comparison across playback, walk-forward, and shadow/sim stages
- cleaner audit trail for why a promising model was promoted or rejected

## Open Questions

- what default slippage and fill assumptions are acceptable for MES evaluation
- how will realism assumptions differ by session, volatility regime, or instrument
- what evidence pack is mandatory before live-readiness discussions
