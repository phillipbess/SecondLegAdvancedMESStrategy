# Test Notes

Testing stays layered:

1. source-level contract tests
2. strategy-logic tests
3. realistic execution playback
4. walk-forward validation

## Scaffold

- `contracts/` defines source-level safety and reuse contracts before heavier runtime testing.
- `strategy/` defines strategy-thesis and setup-behavior test families before playback tuning.

Start with the manifests in each folder. The first-pass strategy families are now implemented; the remaining placeholder surface is mostly contract-side infrastructure that should only be promoted when the repo defines those behaviors intentionally.
