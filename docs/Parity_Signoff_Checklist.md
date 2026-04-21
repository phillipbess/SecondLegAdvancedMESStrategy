# Parity Signoff Checklist

This checklist defines the exact gates for signoff.

It is a readiness document only.

It does **not** mean any gate is complete yet.

## Status Vocabulary

Use the following words consistently:

- `defined`: the gate, docs, and source-level tests exist, but required manual evidence is still missing
- `evidence-in-progress`: some manual evidence exists, but one or more required files are still missing or inconclusive
- `complete`: all required evidence exists and the dated signoff summary says the gate passed

Until a dated evidence folder contains the required files for a gate, that gate stays `defined` or `evidence-in-progress`, never `complete`.

## Current Repo Posture

- Source-level signoff/evidence contracts may be green while manual evidence is still absent.
- Local tests and external harness bridge/adapter tests may be green while real compile, playback, and full harness evidence are still absent.
- If `docs/artifacts/` contains templates only and no dated evidence folder, the honest repo-level posture is:
  - entry gate: `defined`
  - runtime parity gate: `defined`
- In that state, the strongest allowed claim is source-level and bridge-test readiness only, not completed signoff.

## Entry Contract Complete

All of the following must be true before calling the entry contract complete:

- `docs/Entry_Brain_V1_Contract.md` matches the implemented strategy surface with no known drift.
- The entry brain is limited to:
  - trend context
  - ATR regime
  - impulse
  - pullback state machine
  - signal validation
  - planned entry / stop / qty / expiry
  - structure-room gate
  - entry expiry / reset semantics
- The entry brain does not own:
  - exits
  - trail movement
  - flatten behavior
  - realized `R`
  - recovery state
- Long and short logic are mirrored for:
  - trend
  - impulse
  - leg separation
  - retracement
  - second-leg validation
  - signal bar
  - entry
  - initial stop
  - structure room
- The required deterministic strategy families are implemented and green:
  - trend context
  - ATR regime and session guards
  - impulse qualification
  - pullback state machine
  - entry qualification
  - armed-entry lifecycle
  - long golden case
  - short golden case
  - invalid continuous pullback case
  - stale / expired trigger case
- NT8 compile succeeds with the current entry brain.
- NT8 compile succeeds with the current unmanaged transport shell in place.
- Playback smoke can demonstrate:
  - one valid long setup reaches resting entry
  - one valid short setup reaches resting entry
  - stale trigger cancellation works
  - invalid setups are blocked for the expected reason

Evidence pack required for this gate:

- green deterministic strategy test summary in `docs/artifacts/YYYY-MM-DD/test-summary.md`
- NT8 compile note in `docs/artifacts/YYYY-MM-DD/compile.md`
- entry/playback smoke note in `docs/artifacts/YYYY-MM-DD/playback-smoke.md`
- optional screenshots or curated traces under `docs/artifacts/YYYY-MM-DD/screenshots/`

Allowed claim before this evidence exists:

- source-level entry-contract wiring is defined

Blocked claim before this evidence exists:

- entry contract complete

## Mancini Runtime Parity Complete

All of the following must be true before claiming Mancini runtime parity:

- Runtime authority remains donor-shaped in:
  - `OnOrderUpdate`
  - `OnExecutionUpdate`
  - `OnPositionUpdate`
  - `OnMarketData`
- The entry brain hands off through one narrow `PlannedEntry` seam and does not bypass runtime ownership.
- Entry order identity, fill truth, cancel handling, and protective-stop ownership behave the same way by design as the donor runtime model.
- The live management surface is simple trail only:
  - no fixed target
  - no breakeven mode
  - no alternate exit engine
- Long and short runtime plumbing is mirrored for:
  - stop-entry submit side
  - fill anchoring
  - protective stop placement
  - favorable-price tracking
  - trail tightening
  - flatten routing
  - session-close handling
  - trade counting and loss accounting
- Source-level contract tests are green for:
  - host shell / runtime ownership
  - simple-trail only behavior
  - short-capable plumbing
  - session-close / flatten behavior
  - persistence / recovery shape
- NT8 compile evidence confirms the unmanaged transport cutover compiles cleanly for:
  - primary entry submit
  - protective stop submit/change
  - flatten submit
- NT8 Playback smoke can demonstrate for both long and short:
  - submit
  - fill
  - protective stop present after fill
  - protective stop can change through the active trail path
  - simple trail progression
  - flat reset
- NT8 Playback can also demonstrate:
  - no duplicate entry behavior
  - no orphaned working entry after cancel / expiry
  - flatten submit is coherent through the unmanaged transport path
  - flatten-before-close works
  - no unprotected live trade after fill
- External runtime-harness scenarios are green for the touched parity lanes before any formal parity claim.
- External runtime-harness evidence should specifically cover the touched protective-coverage/finalization lanes after unmanaged cutover.
- Green external harness bridge/adapter tests do not satisfy this requirement by themselves.

Evidence pack required for this gate:

- green runtime contract summary in `docs/artifacts/YYYY-MM-DD/test-summary.md`
- NT8 compile note in `docs/artifacts/YYYY-MM-DD/compile.md`
- runtime playback smoke note in `docs/artifacts/YYYY-MM-DD/playback-smoke.md`
- runtime-harness note in `docs/artifacts/YYYY-MM-DD/runtime-harness.md`
- signoff rollup in `docs/artifacts/YYYY-MM-DD/signoff-summary.md`

Allowed claim before this evidence exists:

- runtime parity gates are defined, the source-level contract lane is green, and bridge-test wiring may be green

Blocked claim before this evidence exists:

- Mancini runtime parity complete

## Signoff Rule

Do not mark either section complete from code inspection alone.

Use:

- green tests
- NT8 compile
- NT8 Playback evidence
- runtime-harness evidence where parity is claimed

Only then move a gate from "defined" to "complete."

Green repo tests alone are never enough to move a gate to `complete`.

The recommended operator path is:

1. collect green local test evidence
2. record NT8 compile evidence
3. record playback smoke evidence
4. record runtime-harness evidence for touched parity lanes
5. summarize the gate outcome in one dated signoff summary
