---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# MotionControlModeService

## File

`Merlin.Backend/Services/Motion/MotionControlModeService.cs`

Verified present in current repo.

## Purpose

Coordinates global motion-control mode. It owns enabled/disabled state, active motion profile, ActiveSurface subscriptions, and VisionSidecar tracking start/stop decisions.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Active Surface Layer]]

## Main Types / Classes

- `MotionControlModeService` implements `IMotionControlModeService`.
- Uses `MotionControlModeSnapshot`, `MotionControlProfileRegistry`, and `IMotionControlProfile` implementations.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `EnableAsync` | public | Locks `_gate`, gets current ActiveSurface, resolves/activates profile, starts vision tracking if profile needs pointer input, subscribes to surface changes, and updates snapshot. | `IActiveSurfaceService.GetCurrentAsync`; `SwitchProfileLockedAsync`; `StartTrackingIfNeededLockedAsync` | `CommandRouter`; tests | Handles optional `MotionControlProfileOverride`. |
| `DisableAsync` | public | Locks, deactivates active profile, stops sidecar tracking, unsubscribes surface events, and returns disabled snapshot. | `DeactivateProfileLockedAsync`; `StopTrackingLockedAsync` | `CommandRouter`; tests | Failure moves state to faulted only for tracked failures. |
| `HandleGestureAsync` | public | Drops gestures when disabled, otherwise builds `MotionControlGestureContext` and dispatches to active profile. | `_activeProfile.HandleGestureAsync` | `VisionGestureEventRouter` | Gesture processing is serialized through `_gate`. |
| `OnActiveSurfaceChangedAsync` | public | When enabled, switches profiles if the surface changes. | `SwitchProfileLockedAsync` | `ActiveSurfaceService.ActiveSurfaceChanged`; tests | Keeps dashboard/browser motion semantics aligned with focus. |
| `SwitchProfileLockedAsync` | private | Resolves profile, deactivates old profile when id changes, activates new profile, starts/stops tracking based on capabilities, updates snapshot. | `_profileRegistry.Resolve`; `ActivateResolvedProfileLockedAsync` | enable/surface change | Must be called under `_gate`. |
| `StartTrackingIfNeededLockedAsync` / `StopTrackingLockedAsync` | private | Lazily resolves `IVisionSidecarHost` and starts/stops camera tracking. | service provider; `IVisionSidecarHost` | enable/profile switch/disable | Sidecar is only active for profiles that need pointer stream. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_current` | `MotionControlModeSnapshot` | Public mode state, active profile id/display name, active surface, timestamps, reason. | `Current`, `IsEnabled`, routing/tests | enable/disable/profile switch | lifetime of service |
| `_activeProfile` | `IMotionControlProfile?` | Profile currently receiving gestures. | `HandleGestureAsync` | activation/deactivation | null when disabled |
| `_trackingStarted` | `bool` | Whether VisionSidecar tracking has been started by this service. | start/stop helpers | start/stop helpers | false on disable/failure |
| `_subscribedToSurfaceChanges` | `bool` | Prevents duplicate ActiveSurface event subscriptions. | enable/disable | enable/disable | false on disable |
| `_gate` | `SemaphoreSlim` | Serializes mode/profile/gesture operations. | all async public methods | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IActiveSurfaceService` | Provides current surface and change events. |
| `IMotionControlProfileRegistry` | Chooses dashboard/browser/neutral profile. |
| `IServiceProvider` | Lazily resolves `IVisionSidecarHost`. |
| `IMotionControlProfile` | Executes profile-specific activation and gesture handling. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `DashboardGestureForwarded` | frontend/WebSocket compatibility subscribers | Dashboard gestures forwarded by dashboard profile path for old UI control. |
| logs `MotionControlMode*` | application log | State transitions, profile switches, start/stop failures. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | `VisionGestureEventRouter` | `HandleGestureAsync` |
| `ActiveSurfaceChanged` | `ActiveSurfaceService` | `OnActiveSurfaceChangedAsync` |

## External Side Effects

Starts/stops the Python vision sidecar via `IVisionSidecarHost`. It also invokes profile activation/deactivation code that can update BrowserHost overlays or frontend UI control state.

## Safety / Guardrails

Keep sidecar ownership here, not in individual profiles. Profiles describe capabilities; this service decides whether tracking runs. Do not bypass `_gate` when changing profile state.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `MotionControlModeServiceTests.cs` | Enable/disable, profile switch, sidecar start/stop, event forwarding, fault behavior. | No real camera process. |
| `VisionGestureEventRouterTests.cs` | Verifies router delegates to motion service when registered. | Does not cover all profile actions. |

## Known Risks / Fragility

Legacy direct gesture paths still exist in `VisionGestureEventRouter` for compatibility if the motion service is absent. Deadlocks are possible if callbacks call into services while locks are held, so preserve current async lock boundaries.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
