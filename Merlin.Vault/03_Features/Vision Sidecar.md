---
type: feature
status: implemented
area: backend
tags:
  - merlin
  - feature
  - status/implemented
---

# Vision Sidecar

## Summary

Python camera worker for hand tracking, pointer, pinch, and calibration.

## Status

implemented

## What Exists Today

- `IVisionSidecarHost` exists.
- VisionSidecarHost controls worker lifecycle.
- `vision_worker.py` handles capture profiles, gestures, calibration.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/Vision/IVisionSidecarHost.cs` | Interface | Warm/start/stop/calibrate/shutdown. |
| `Merlin.Backend/Services/Vision/VisionSidecarHost.cs` | Host | Process lifecycle and messages. |
| `Merlin.Backend/VisionScripts/vision_worker.py` | Worker | Camera and gesture processing. |
| `Merlin.Backend/Configuration/VisionOptions.cs` | Config | Camera/capture/motion settings. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Starts tracking on motion enable/calibration and emits `VisionGestureEvent`.

## Planned Behavior

Keep camera profile and calibration diagnostics visible.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Webcam backend/profile choice varies by camera.
- Resolution/FOV affects sensitivity.
- Table/camera angle can block lower corners.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Document new camera tuning here.
