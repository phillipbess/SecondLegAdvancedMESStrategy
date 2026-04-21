# AGENTS.md — SecondLegAdvancedMESStrategy Safe-Build Contract

This repo is a sibling strategy, not a rewrite of `ManciniMESStrategy`.

## Core Rule

Preserve the current strategy's hardened order-management lessons, but do not couple this repo to Adam failed-breakdown entry logic.

## Build Priorities

- Keep the change surface elegant and minimal.
- Prefer NinjaTrader 8 event-driven design.
- Reuse hardened fill/exit/safety patterns where they can be ported cleanly.
- Keep strategy thesis, parameters, and validation explicit.
- Avoid overfitting-by-construction.

## Reuse Rules

- Reuse order/fill/exit authority patterns before rewriting them.
- Do not blindly copy entry-analysis code tied to failed breakdowns.
- Treat all logging formats as intentional contracts once defined here.
- Keep persistence symmetry if new durable state is added.

## Validation Rules

- Prove entry edge before optimizing exits.
- Separate raw signal validation from trade-management tuning.
- Prefer broad parameter regions over single magic values.
- Require playback and walk-forward validation before any live-use recommendation.

## Default Planning Phrase

`elegant, nt8 best practice aligned, event driven, no downstream side effects (verified with code traced end to end)`
