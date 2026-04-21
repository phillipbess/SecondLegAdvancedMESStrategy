# Contract Test Ladder

Purpose: fail fast on source-level drift in the hardened runtime lanes this repo plans to reuse, while keeping signoff evidence requirements explicit.

These tests should stay narrow and structural. They are not trade-performance tests.

## Implemented Parity-Facing Families

- `execution_event_contract`: protects the authoritative fill/recovery lane and the runtime sequencing around coverage, persistence, and reset.
- `runtime_snapshot_contract`: freezes the harness-facing runtime truth surface and the parity-relevant semantics it must continue to report.
- `persistence_symmetry_contract`: catches drift in durable and trade-scoped recovery state, especially stop/coverage fields.
- `signoff_evidence_contract`: keeps the checklist, runbooks, and artifact templates aligned so manual compile/playback/harness evidence stays reviewable.

## Structural Families

- `host_shell_contract`: required host fields/methods exist for lifted runtime components.
- `exit_authority_contract`: all exit flows stay centralized and no alternate exit lane appears.
- `runtime_scenario_pack_contract`: the first external runtime-harness scenario pack and semantic anchors stay explicit.
- `runtime_harness_adapter_contract`: the thin local adapter surface for driving first-pack harness scenarios stays stable.
- `runtime_harness_projection_contract`: the flat harness-shaped snapshot projection stays aligned with the local runtime scaffold.
- `logging_contract`: once logs are defined here, schema/file-name drift is caught early.

Use `contract_test_manifest.json` as the planning surface for filenames, scope, and status.
