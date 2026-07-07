---
type: feature
status: partial
area: frontend
tags:
  - merlin
  - feature
  - status/partial
  - layer/frontend
---

# Dashboard UI Control

## Summary

Godot dashboard gesture cursor and window manipulation.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.Frontend/Scripts/Main.gd handles UI_CONTROL_MODE_STARTED/STOPPED and GESTURE_* events.`
- Main.gd owns _gesture_pointer_move, _gesture_pinch_start, _gesture_pinch_end, resize, drag, crumple logic.
- DashboardMotionProfile.cs forwards dashboard profile gestures to frontend.

## What Exists Today

Dashboard gesture behavior works but is centralized in a large Godot script.

## Current Behavior

Pointer move updates a gesture cursor. Pinch can grab, drag, resize, select, and crumple/dismiss dashboard surfaces.

## Planned Behavior

Extract focused frontend gesture helpers and profile-specific sensitivity after behavior is stable.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Frontend/Scripts/Main.gd` | Main.gd | Dashboard gesture owner | Gesture cursor and window interactions. |
| `Merlin.Backend/Services/Motion/Profiles/DashboardMotionProfile.cs` | DashboardMotionProfile | Backend adapter | Forwards gestures to frontend path. |

## Code Atlas

- [[Main.gd]]
- [[DashboardMotionProfile]]
- [[Dashboard Motion Profile Flow]]
- [[Frontend Gesture Constants]]

## Related Systems

- Future widget motion control
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]

## Dependencies

- [[Vision Sidecar]]
- [[Motion Control Profile Layer]]

## Dependents

- Future widget motion control

## Readiness

Ready for implementation: yes

Reason:
Hardening and extraction can happen incrementally.

Blocked by:
- Big refactors should wait until baseline behavior is documented/testable.

Next safe action:
Document/extract one gesture behavior at a time, starting with resize sensitivity.

## Non-Goals / Do Not Build Yet

- Do not rewrite dashboard motion in backend.
- Do not add widget-specific gestures before widget behavior exists.

## Known Bugs / Fragility

- Centralized Main.gd gesture logic is hard to reason about.
- Camera reach can make screen edges inaccessible.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Manual validation` | Dashboard gestures | No Godot automated gesture tests discovered. |

## Relevant Implementation Plans

- [[Universal UI Control Layer Design Plan]]

## Relevant Reports

- `Merlin.Vault/12_Source_Material/Imported_Merlin_ToDo/done/DONE frontend_ui/merlin_frontend_ui_current_architecture_report.md`

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (5 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
