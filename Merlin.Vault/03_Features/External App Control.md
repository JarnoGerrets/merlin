---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
  - layer/backend
---

# External App Control

## Summary

Opening trusted apps/URLs exists; deep external app control is future.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- ApplicationResolver and DefaultProcessLauncher are registered.
- `Trusted app/url stores and OpenApplicationTool/OpenUrlTool exist.`
- `No general external app UI/motion control layer found.`

## What Exists Today

Merlin can launch trusted apps/URLs through tools and trusted registry.

## Current Behavior

Launch/open is partial external control. App detection, active external surfaces, and motion profiles are not implemented.

## Planned Behavior

External app active surface detection and control profiles later.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Program.cs` | OpenApplicationTool/OpenUrlTool registrations | App/url launch | Trusted stores and tools. |

## Code Atlas

- [[CommandRouter]]

## Related Systems

- Future external app motion/control profiles
- Trusted registry
- [[Safety and Confirmation]]

## Dependencies

- Trusted registry
- [[Safety and Confirmation]]

## Dependents

- Future external app motion/control profiles

## Readiness

Ready for implementation: no

Reason:
Launch behavior exists, but broader control needs active-surface/app detection.

Blocked by:
- External app detection
- [[Control Profile DB]]
- safety policy

Next safe action:
Document launch-only boundary and avoid deep control until requested.

## Non-Goals / Do Not Build Yet

- Do not automate Discord/WhatsApp/Steam yet.

## Known Bugs / Fragility

- External app status can be overclaimed if launch-only is confused with control.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Command/tool tests` | Launch routing partial | No external app automation E2E. |

## Relevant Implementation Plans

- [[External Open Overlay And Animation Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (13 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
