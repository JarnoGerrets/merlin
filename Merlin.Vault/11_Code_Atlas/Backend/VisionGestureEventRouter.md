---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# VisionGestureEventRouter

## File

`Merlin.Backend/Services/Vision/VisionGestureEventRouter.cs`

Verified present in current repo.

## Purpose

Routes parsed gesture events from the vision sidecar. In the current architecture it delegates to `IMotionControlModeService` when available; otherwise it retains legacy dashboard/browser forwarding paths for compatibility tests and old runtime wiring.

## Related Features

- [[Vision Sidecar]]
- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Browser Control]]

## Main Types / Classes

- `VisionGestureEventRouter`
- `VisionGestureEventForwarded` event is exposed as `GestureEventForwarded`.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `RouteAsync` | public | Logs input, delegates to `IMotionControlModeService.HandleGestureAsync` when present, otherwise forwards to legacy UI-control/browser services and raises `GestureEventForwarded` for frontend. | motion service; UI controller; browser overlay; pinch controller; event subscribers | `VisionSidecarHost.HandleOutputLineAsync`; tests | Motion service is preferred path. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `GestureEventForwarded` | event | Compatibility frontend forwarding for dashboard gestures. | WebSocketHandler/tests | `RouteAsync` | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IMotionControlModeService` | Preferred dispatch into active motion profile. |
| `UiControlModeController` | Legacy dashboard gesture forwarding when motion service is absent. |
| `BrowserMotionOverlayModeService` / `BrowserPinchClickController` | Legacy browser pointer/click path when motion service is absent. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `GestureEventForwarded` | WebSocketHandler/test subscribers | Gesture event sent to frontend visual event bridge. |
| motion-profile dispatch | MotionControlModeService | `VisionGestureEvent` routed by active profile. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | `VisionSidecarHost` | `RouteAsync` |

## External Side Effects

No direct external I/O. Side effects happen through motion profiles, UI-control forwarding, or BrowserWorkspace services.

## Safety / Guardrails

Keep the motion service delegation first. Compatibility fallbacks should not diverge from profile behavior and should eventually be removed only after all wiring is profile-based.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `VisionGestureEventRouterTests.cs` | Dashboard forwarding, browser forwarding, multi-pointer handling, and motion service delegation. | No real sidecar process. |

## Known Risks / Fragility

Two paths can exist: new motion profiles and legacy direct consumers. Changes risk duplicate gesture handling if both are accidentally active.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
