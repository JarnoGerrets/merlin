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

# Site Control Profiles

## Summary

Future learned/per-site action profiles.

## Status

future

## Verified Against Code

Status verified: yes

Evidence:
- Site profile plan exists under Merlin.ToDo.
- `No production site profile DB/runtime found.`

## What Exists Today

Not implemented.

## Current Behavior

Generic browser control only.

## Planned Behavior

After full motion control, let user teach controls and store selectors per site/action.

Near-term targeted exception:
- [[YouTube Site Control Profile Media Commands]] defines a small seeded YouTube media profile for fullscreen confirmations and 10-second seek commands. This is not the full learned Control Profile DB.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| Missing | Missing | Not implemented | Future/unknown. |

## Code Atlas

- None yet.

## Related Systems

- Future YouTube/media/site controls
- [[Browser Page-Aware Control]]
- [[Control Profile DB]]
- [[Correction Layer]]

## Dependencies

- [[Control Profile DB]]
- [[Browser Page-Aware Control]]
- [[Correction Layer]]

## Dependents

- Future YouTube/media/site controls

## Readiness

Ready for implementation: no

Reason:
User already decided this comes after full motion control.

Blocked by:
- [[Motion Control]]
- [[Control Profile DB]]
- [[Safety and Confirmation]]

Next safe action:
Do not implement yet; continue generic motion control.

## Non-Goals / Do Not Build Yet

- Do not clutter generic browser control with site-specific commands.

## Known Bugs / Fragility

- N/A - future only.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Missing` | No implementation | All tests future. |

## Relevant Implementation Plans

- [[Site Control Profiles Learning Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
