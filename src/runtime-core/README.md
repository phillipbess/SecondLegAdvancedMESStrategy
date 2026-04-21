# Runtime Core

This folder is reserved for the hardened runtime engine imported from `ManciniMESStrategy`.

Target imports:

- signal and order identity helpers
- exit authority
- submission guards
- control lane
- trade manager
- coverage and safety
- order maintenance and recovery
- persistence patterns
- logging/reporting infrastructure

Porting rule:

- prefer mechanical class-binding changes
- avoid behavioral rewrites during first import
- freeze the imported runtime core before tuning the new strategy brain

Current status:

- low-risk support helpers imported
- host-shell contract documented
- port order frozen for the next implementation passes
