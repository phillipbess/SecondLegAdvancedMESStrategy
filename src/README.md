# Source Notes

This folder holds the new strategy implementation.

Current source layout:

- `strategy/`
  - new strategy host shell
  - parameter surface
  - second-leg state machine scaffold
  - bar/event flow scaffold
- `runtime-core/`
  - imported hardened order/fill/exit infrastructure from the donor strategy

Porting rule:

- rebuild the entry brain here
- freeze the reused runtime core as early as possible
- adapt execution events, persistence, and logging identity carefully
