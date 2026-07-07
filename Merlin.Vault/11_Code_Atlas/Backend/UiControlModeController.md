---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# UiControlModeController

## File

`Merlin.Backend/Services/Vision/UiControlModeController.cs`

Verified present in current repo.

## Purpose

Keeps dashboard UI-control compatibility state and forwards vision gestures to frontend visual events while UI control mode is active.

## Related Features

- [[Vision Sidecar]]
- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Browser Control]]

## Main Types / Classes

UiControlModeController and its directly referenced protocol/model types.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `StartAsync` | public | Marks UI control active and stores visual-event callback. | lock/state update | `CommandRouter` eyes-open/start-ui-control paths | Does not own sidecar start in profile architecture. |
| `StopAsync` | public | Clears active flag and callback. | lock/state update | `CommandRouter` eyes-closed/stop-ui-control paths | Stops frontend forwarding. |
| `HandleGestureAsync` | public | Converts `VisionGestureEvent` to frontend gesture visual events when active. | stored callback | `DashboardMotionProfile` or legacy router | Drops gestures when inactive. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| active flag | `bool` | Whether dashboard forwarding is enabled. | gesture handler | start/stop | false after stop |
| visual sender | callback | WebSocket visual-event sink. | gesture handler | start/stop | null after stop |

## Dependencies

| Dependency | Used For |
| --- | --- |
| frontend visual-event callback | Sends `AssistantVisualEvent` packets. |
| logger | Diagnostics. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `GESTURE_POINTER_MOVE`, `GESTURE_PINCH_START`, `GESTURE_PINCH_MOVE`, `GESTURE_PINCH_END` | Frontend WebSocket | Dashboard gesture visual events. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | Dashboard profile or legacy router | `HandleGestureAsync` |

## External Side Effects

No direct device I/O; emits frontend visual events via callback.

## Safety / Guardrails

Keep lifecycle ownership centralized and preserve existing guards. This component is part of live camera/browser routing and should fail closed rather than emit stale actions.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `VisionGestureEventRouterTests.cs` | Legacy dashboard forwarding. | No Godot end-to-end. |
| `CommandRouterTests.cs` | UI control route compatibility. | Visual packet details limited. |

## Known Risks / Fragility

A stale WebSocket callback can make active mode appear alive while visual events are lost. Sidecar start/stop belongs to MotionControlModeService, not here.

## Change Notes for Agents

Read source and linked flow notes before editing. Do not move safety or process ownership into lower-level helpers.
