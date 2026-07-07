---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# ActiveSurfaceService

## File

`Merlin.Backend/Services/Context/ActiveSurface/ActiveSurfaceService.cs`

Verified present in current repo.

## Purpose

Owns the backend's current interaction surface snapshot: dashboard, browser workspace, or unknown. It publishes surface changes so command routing and motion profile selection can move between dashboard and browser semantics.

## Related Features

- [[Active Surface Layer]]
- [[Motion Control Profile Layer]]
- [[Browser Control]]

## Main Types / Classes

- `ActiveSurfaceService` implements `IActiveSurfaceService`.
- Uses `ActiveSurfaceSnapshot`, `ActiveSurfaceUpdate`, and `KnownSurfaces`.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Current` | public property | Returns the current snapshot under lock. | `_sync`; `_current` | routers, tests, motion service | Starts at `KnownSurfaces.Dashboard`. |
| `GetCurrentAsync` | public | Returns current snapshot as a task-friendly API. | `Current` | `CommandRouter`; `MotionControlModeService`; tests | No I/O. |
| `SetActiveSurfaceAsync` | public | Builds a new snapshot from update data, clamps confidence, swaps `_current`, logs, then invokes `ActiveSurfaceChanged` outside the lock. | `ActiveSurfaceUpdate`; `ActiveSurfaceChanged` | `BrowserWorkspaceService`; future surface owners | Avoids raising events while holding `_sync`. |
| `ResetToDashboardAsync` | public | Replaces current surface with `KnownSurfaces.Dashboard` for browser close/reset or recovery. | `SetActiveSurfaceAsync` | `BrowserWorkspaceService`; tests | Restores assistant playback/dashboard capabilities. |
| `CurrentSupports` | public | Checks whether current snapshot contains a capability. | `_current.Capabilities` | `LiveUtteranceGate`; tests | Used for quick capability guards. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_current` | `ActiveSurfaceSnapshot` | Current surface kind, id, confidence, capabilities, metadata, and timestamp. | `Current`, `GetCurrentAsync`, `CurrentSupports` | `SetActiveSurfaceAsync`, `ResetToDashboardAsync` | process lifetime; reset to dashboard on browser close |
| `_sync` | `object` | Protects `_current`. | all members | constructor | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `ILogger<ActiveSurfaceService>` | Emits diagnostics when surface changes. |
| `KnownSurfaces` | Provides canonical dashboard/browser capability sets. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `ActiveSurfaceChanged` | subscribers such as `MotionControlModeService` | New snapshot and reason after state change. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `ActiveSurfaceUpdate` | BrowserWorkspaceService and future surface owners | `SetActiveSurfaceAsync` |

## External Side Effects

No external I/O beyond logging and subscriber callbacks.

## Safety / Guardrails

Always publish changes after releasing `_sync`; subscribers may call back into services. Surface capabilities should reflect routing/safety intent because LiveUtteranceGate and motion profiles depend on them.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `ActiveSurfaceServiceTests.cs` | Default dashboard, browser update, reset, confidence clamping, event behavior. | No concurrency stress test. |
| `MotionControlProfileRegistryTests.cs` | Indirectly verifies snapshots drive profile selection. | Does not verify every capability. |

## Known Risks / Fragility

A stale browser surface after close causes commands to route to the wrong context. Incorrect capabilities can turn ambiguous phrases like `pause` into the wrong action.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
