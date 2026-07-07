---
type: feature
status: partial
area: cross-cutting
tags:
  - merlin
  - feature
  - status/partial
  - layer/cross-cutting
---

# Motion Control

## Summary

Voice-driven hand tracking control for dashboard and browser surfaces.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- `CommandRouter.cs routes eyes open/closed and browser pointer commands.`
- `VisionSidecarHost.cs starts/stops Python tracking.`
- Main.gd owns dashboard gesture behavior.
- BrowserMotionOverlayModeService.cs, BrowserPinchClickController.cs, and BrowserScrollCommandService.cs own browser motion behavior.

## What Exists Today

Motion works for dashboard UI control and BrowserWorkspace pointer/click/scroll, but behavior is split across Python, backend, Godot, and BrowserHost.

## Current Behavior

`eyes open`, `open your eyes`, `start ui control`, and `start browser pointer` start tracking paths. `eyes closed` stops normal motion. Browser pointer stop remains an active context/routing concern.

## Planned Behavior

Unify configuration, diagnostics, and safety for profile-specific motion.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/CommandRouter.cs` | CommandRouter | Voice command entry | Routes motion commands. |
| `Merlin.Backend/Services/Vision/VisionSidecarHost.cs` | VisionSidecarHost | Tracking lifecycle | Starts Python worker tracking. |
| `Merlin.Frontend/Scripts/Main.gd` | Main.gd gesture functions | Dashboard consumer | Hover/select/drag/resize/crumple. |

## Code Atlas

- [[MotionControlModeService]]
- [[VisionSidecarHost]]
- [[VisionGestureEventRouter]]
- [[Main.gd]]
- [[BrowserMotionOverlayModeService]]
- [[Motion Control Enable Flow]]
- [[Motion Gesture Dispatch Flow]]

## Related Systems

- [[Browser Pinch Click]]
- [[Browser Pointer Overlay]]
- [[Browser Scroll Gestures]]
- [[Dashboard UI Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]

## Dependencies

- [[Vision Sidecar]]
- [[Motion Control Profile Layer]]

## Dependents

- [[Dashboard UI Control]]
- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]

## Readiness

Ready for implementation: yes

Reason:
Partial system exists; hardening work is ready when scoped to current profiles.

Blocked by:
- New app profiles are blocked by [[Control Profile DB]].

Next safe action:
Tune profile-specific config and keep raw clicks behind safety policy.

## Non-Goals / Do Not Build Yet

- Do not build full learned motion control yet.
- Do not move all Godot dashboard logic in one pass.

## Known Bugs / Fragility

- Lower-left corner physical reach issue.
- Duplicated smoothing/config thresholds.
- DPI/multi-monitor click uncertainty.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/VisionGestureEventRouterTests.cs` | Backend routing | No end-to-end camera/Godot automation. |
| `Merlin.Backend.Tests/BrowserMotionOverlayModeServiceTests.cs` | Browser overlay mode | Native overlay visual validation is manual. |

## Relevant Implementation Plans

- [[Motion Control Profile Layer Plan]]

## Relevant Reports

- `Merlin.Vault/12_Source_Material/Imported_Merlin_ToDo/motion_control/report/current_motion_structure_report.md`

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (3 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
