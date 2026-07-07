---
type: feature
status: future
area: backend
tags:
  - merlin
  - feature
  - status/future
  - layer/backend
---

# Control Profile DB

## Summary

Future learned database of surface/site/app control mappings.

## Status

future

## Verified Against Code

Status verified: yes

Evidence:
- No production Control Profile DB implementation found.
- `Merlin.ToDo/site_control_profiles/merlin_site_control_profiles_learning_v1.md is a future plan.`

## What Exists Today

No durable learned profile database exists.

## Current Behavior

Not implemented. Current profiles are code-based motion profiles only.

## Planned Behavior

Store learned site/app/action selectors after full motion control and correction stability.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| Missing | Missing | Not implemented | Future/unknown. |

## Code Atlas

- [[Site Control Profiles]]

## Related Systems

- [[Browser Page-Aware Control]]
- [[Correction Layer]]
- [[Motion Control Profile Layer]]
- [[Safety and Confirmation]]
- [[Site Control Profiles]]
- future app-specific motion profiles

## Dependencies

- [[Motion Control Profile Layer]]
- [[Browser Page-Aware Control]]
- [[Correction Layer]]
- [[Safety and Confirmation]]

## Dependents

- [[Site Control Profiles]]
- future app-specific motion profiles

## Readiness

Ready for implementation: no

Reason:
Foundations exist, but learning/correction/safety are not stable enough.

Blocked by:
- [[Correction Layer]]
- [[Browser Page-Aware Control]]
- [[Safety and Confirmation]]

Next safe action:
Finish generic motion/browser safety before schema design.

## Non-Goals / Do Not Build Yet

- Do not build learning DB now.
- Do not add per-site hacks as a substitute.

## Known Bugs / Fragility

- N/A - future only.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Missing` | No implementation | All tests future. |

## Relevant Implementation Plans

- [[Motion Control Profile Layer Plan]]
- [[Site Control Profiles Learning Plan]]
- [[Voice Correction Learning Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (3 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
