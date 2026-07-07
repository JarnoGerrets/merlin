---
type: feature
status: future
area: cross-cutting
tags:
  - merlin
  - feature
  - status/future
  - layer/cross-cutting
---

# File Browser

## Summary

Future file browsing/control surface.

## Status

future

## Verified Against Code

Status verified: yes

Evidence:
- No FileBrowser production feature found from required search.
- `Capability docs mention file access/destructive actions as future/unknown.`

## What Exists Today

Not implemented as a UI/browser feature.

## Current Behavior

No dedicated file browser surface.

## Planned Behavior

Needs safety policy for file reads/writes/deletes before UI work.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| Missing | Missing | Not implemented | Future/unknown. |

## Code Atlas

- None yet.

## Related Systems

- Future file motion profile
- [[Safety and Confirmation]]
- file access capability policy

## Dependencies

- [[Safety and Confirmation]]
- file access capability policy

## Dependents

- Future file motion profile

## Readiness

Ready for implementation: no

Reason:
High-risk file actions need safety and scope policy first.

Blocked by:
- Safety policy
- capability design

Next safe action:
Define safe read-only file browsing contract.

## Non-Goals / Do Not Build Yet

- Do not add destructive file control without confirmation/undo policy.

## Known Bugs / Fragility

- N/A - future only.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Missing` | No implementation | All tests future. |

## Relevant Implementation Plans

- None currently promoted for this feature.

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
