# Signoff Evidence Runbook

## Purpose

Create one dated, reviewable evidence pack for signoff work without mixing compile,
playback, and harness results into ad hoc notes.

## Output Folder

Create:

- `docs/artifacts/YYYY-MM-DD/`

Recommended contents:

- `test-summary.md`
- `compile.md`
- `playback-smoke.md`
- `runtime-harness.md`
- `signoff-summary.md`
- `screenshots/`

If `docs/artifacts/` still contains templates only and no dated folder for the current attempt, then no manual signoff evidence has been recorded yet.

Green external harness bridge/adapter tests are useful source-level evidence, but they do not replace the real `runtime-harness.md` evidence file.

## Minimum Flow

1. run the relevant local tests and record the exact command plus outcome in `test-summary.md`
2. record NT8 compile evidence in `compile.md`
3. record playback smoke evidence in `playback-smoke.md`
4. record runtime-harness evidence in `runtime-harness.md` when parity is claimed
5. summarize gate outcomes and remaining blockers in `signoff-summary.md`

For the current unmanaged-cutover posture, the compile/playback/harness notes should explicitly say whether they covered:

- primary entry submit
- protective stop submit/change
- flatten submit

## Gate Mapping

Use the parity checklist as the source of truth:

- entry contract completeness requires test, compile, and playback evidence
- runtime parity requires test, compile, playback, and harness evidence

Do not mark a gate complete if the required evidence file is missing.

## Required Summary Language

Every dated `signoff-summary.md` should answer these explicitly:

1. What is the strongest claim currently allowed?
2. What is the blocked claim?
3. Which exact missing manual evidence files are still missing or inconclusive?

Green tests without compile/playback/harness notes must be summarized as source-level readiness only, not completed signoff.
Green external harness bridge/adapter tests without a real harness note must be summarized as bridge-test readiness only, not completed runtime parity.

## Exact Manual Blockers By Gate

Before `entry contract complete` can be claimed, the dated evidence pack must contain:

- `test-summary.md`
- `compile.md`
- `playback-smoke.md`

Before `Mancini runtime parity complete` can be claimed, the dated evidence pack must contain:

- `test-summary.md`
- `compile.md`
- `playback-smoke.md`
- `runtime-harness.md`
- `signoff-summary.md`

And those files must make the unmanaged-cutover checks explicit for compile/playback/harness, not just say "passed" generically.
