---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# DashboardMotionProfile

## File

`Merlin.Backend/Services/Motion/Profiles/DashboardMotionProfile.cs`

Verified present in current repo.

## Purpose

Wraps the existing dashboard UI-control gesture path as a motion profile. It forwards webcam pointer/pinch gestures to `UiControlModeController` only when ActiveSurface is Dashboard.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control Profile Layer]]
- [[Motion Control]]

## Main Types / Classes

- `DashboardMotionProfile` implements `IMotionControlProfile`.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Descriptor` | public property | Identifies profile id `motion.dashboard`, display name, Dashboard surface kind, priority, and pointer/hover/select/drag/resize/dismiss capabilities. | constants | registry/tests | Used for profile listing and sidecar need detection. |
| `CanHandle` | public | Returns true for `ActiveSurfaceKind.Dashboard`. | `ActiveSurfaceSnapshot.Kind` | `MotionControlProfileRegistry` | Prevents browser gestures from flowing to dashboard UI. |
| `ActivateAsync` | public | Logs activation and starts/keeps dashboard UI control compatibility. | logger; context | `MotionControlModeService` | Does not start sidecar itself. |
| `DeactivateAsync` | public | Logs deactivation and lets controller state be stopped by command/lifecycle paths. | logger | `MotionControlModeService` | Kept lightweight. |
| `HandleGestureAsync` | public | Forwards `VisionGestureEvent` to `UiControlModeController.HandleGestureAsync`. | `_uiControlModeController` | `MotionControlModeService` | This is the old dashboard motion path under profile control. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `Descriptor` | `MotionControlProfileDescriptor` | Profile identity and capabilities. | registry/service | object initializer | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `UiControlModeController` | Sends dashboard gesture visual events to frontend and owns dashboard UI-control enable/disable state. |
| `ILogger<DashboardMotionProfile>` | Activation/action diagnostics. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| dashboard gesture forwarding | `UiControlModeController` / frontend | Pointer and pinch events converted to visual events by controller. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | MotionControlModeService | `HandleGestureAsync` |

## External Side Effects

No direct I/O; side effects happen through `UiControlModeController` and frontend WebSocket visual events.

## Safety / Guardrails

Only handle dashboard surface. Do not let this profile consume BrowserWorkspace gestures, because browser pointer/click/scroll safety lives in browser motion services.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `MotionControlProfileRegistryTests.cs` | Dashboard profile selection. | Does not validate frontend visuals. |
| `MotionControlModeServiceTests.cs` | Gesture dispatch through active profile. | No full Godot integration. |

## Known Risks / Fragility

This profile is a compatibility wrapper around older UI-control behavior. Changing it can break `open your eyes`, `start ui control`, and dashboard drag/resize gestures.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
