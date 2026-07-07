---
type: prompt-extension
id: PE-0005
status: active
applies_to:
  - all systems
required_for:
  - implementation
  - bugfix
  - refactor
  - documentation
---

# PE-0005 Vault Writeback Rules

## Core Rule

If code changes but the vault does not, the task is incomplete.

## Required Writeback

1. Create an agent run report in `14_Agent_Runs`.
2. Update affected feature notes.
3. Update affected architecture notes.
4. Update affected code atlas notes.
5. Update roadmaps/current-state notes if status changed.
6. Add or update bug notes.
7. Update progress reports.
8. Add changelog entry.

## Final Response Must Include

- code files changed
- vault notes changed
- tests run
- bugs found
- remaining work
