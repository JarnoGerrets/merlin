---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# VisionSidecarHost

## File

`Merlin.Backend/Services/Vision/VisionSidecarHost.cs`

Verified present in current repo.

## Purpose

Owns the Python vision worker process. It starts the process, writes JSON commands to stdin, reads JSON events from stdout, routes gesture events, and completes calibration tasks.

## Related Features

- [[Vision Sidecar]]
- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Browser Control]]

## Main Types / Classes

VisionSidecarHost and its directly referenced protocol/model types.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `WarmAsync` | public | Starts worker without tracking. | `EnsureProcessStartedLockedAsync` | `VisionWarmupHostedService` | Pays startup cost early. |
| `StartTrackingAsync` | public | Ensures process, builds `vision.start_tracking` from `VisionOptions`, sends command, tracks requested state. | `SendCommandLockedAsync` | `MotionControlModeService` | Worker opens camera. |
| `CalibratePinchAsync` | public | Sends `vision.calibrate_pinch` and waits for `vision.pinch_calibration_completed`. | pending TCS; `SendCommandLockedAsync` | WebSocket calibration flow | Returns thresholds/sample counts. |
| `CalibrateMotionRegionAsync` | public | Sends `vision.calibrate_motion_region` and waits for completion. | pending TCS; `SendCommandLockedAsync` | WebSocket calibration flow | Saves focus/control region. |
| `StopTrackingAsync` | public | Sends `vision.stop_tracking` and clears tracking request. | `SendCommandLockedAsync` | Motion mode disable/profile switch | Worker releases camera. |
| `ShutdownAsync` | public | Sends shutdown if possible, cancels output loops, disposes/kills process. | `DisposeProcess`; `TryKillProcess` | app shutdown/dispose | Best effort cleanup. |
| `HandleOutputLineAsync` | private | Parses stdout JSON, logs errors/status, routes gestures, completes calibration results. | `VisionSidecarClient.TryParseMessage`; `_router.RouteAsync` | output reader | Central protocol receive handler. |
| `SendCommandLockedAsync` | private | Serializes command and writes one stdin line. | `VisionSidecarClient.SerializeCommand` | all command methods | Called under `_gate`. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_process` | `Process?` | Active Python worker. | lifecycle/send/output | start/dispose/exit | null after shutdown |
| `_gate` | `SemaphoreSlim` | Serializes process lifecycle and stdin writes. | public methods | constructor | service lifetime |
| `_trackingRequested` | `bool` | Backend-side tracking intent. | start/stop/fault | start/stop/fault | false after stop |
| calibration TCS fields | `TaskCompletionSource` | Await current pinch or motion-region calibration. | calibration methods/output handler | calibration start/completion/failure | null when idle |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `VisionSidecarClient` | JSON command/message protocol. |
| `VisionGestureEventRouter` | Dispatches parsed gesture messages. |
| `IOptionsMonitor<VisionOptions>` | Supplies camera, profile, pinch, and region settings. |
| `IWebHostEnvironment` | Resolves worker/model/calibration paths. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `vision.*` commands | Python stdin | Start/stop/shutdown/calibration commands. |
| `VisionGestureEvent` | Motion router | `gesture.pointer.move` and `gesture.pinch.*`. |
| calibration results | awaiting command/WebSocket code | Success/failure and sample counts. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| stdout JSON | Python worker | `HandleOutputLineAsync` |
| stderr/process exit | Python worker | drain/exit handling |

## External Side Effects

Starts/kills Python, writes stdin, reads stdout/stderr, and indirectly owns webcam access through worker commands.

## Safety / Guardrails

Keep lifecycle ownership centralized and preserve existing guards. This component is part of live camera/browser routing and should fail closed rather than emit stale actions.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `VisionSidecarClientTests.cs` | Protocol parsing and worker source invariants. | No live process/camera. |
| `MotionControlModeServiceTests.cs` | Sidecar start/stop via abstraction. | Host faked. |

## Known Risks / Fragility

Uncompleted calibration tasks can hang voice workflows. Duplicate tracking starts can fight over camera ownership.

## Change Notes for Agents

Read source and linked flow notes before editing. Do not move safety or process ownership into lower-level helpers.
