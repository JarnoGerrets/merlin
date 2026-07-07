---
type: feature
status: implemented
area: python
tags:
  - merlin
  - feature
  - status/implemented
  - layer/python
---

# Vision Sidecar

## Summary

Python OpenCV/MediaPipe worker plus backend host for camera and gesture events.

## Status

implemented

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.Backend/VisionScripts/vision_worker.py exists.`
- VisionSidecarHost.cs and VisionSidecarClient.cs exist.
- `VisionSidecarClientTests.cs covers protocol/client behavior.`

## What Exists Today

Backend starts Python worker, sends JSON commands, receives gesture JSON lines, and emits VisionGestureEvent to routing.

## Current Behavior

Supports camera warmup, adaptive capture profile selection, pointer move, pinch start/move/end, pinch calibration, motion region calibration, and error reporting.

## Planned Behavior

Better diagnostics, fewer hardcoded thresholds, and profile-specific config ownership.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/VisionScripts/vision_worker.py` | vision_worker | Camera/MediaPipe loop | Adaptive OpenCV profile selection and gesture emit. |
| `Merlin.Backend/Services/Vision/VisionSidecarHost.cs` | VisionSidecarHost | Process/protocol owner | WarmAsync, StartTrackingAsync, StopTrackingAsync. |
| `Merlin.Backend/Services/Vision/VisionGestureEvent.cs` | VisionGestureEvent | Gesture DTO | Pointer/pinch event fields. |

## Code Atlas

- [[vision_worker.py]]
- [[VisionSidecarHost]]
- [[VisionSidecarClient]]
- [[VisionGestureEventRouter]]
- [[Vision Sidecar Protocol]]

## Related Systems

- MediaPipe
- OpenCV
- Python runtime
- [[Browser Control]]
- [[Dashboard UI Control]]
- [[Motion Control]]

## Dependencies

- MediaPipe
- OpenCV
- Python runtime

## Dependents

- [[Motion Control]]
- [[Dashboard UI Control]]
- [[Browser Control]]

## Readiness

Ready for implementation: yes

Reason:
Implemented and usable; future work should be targeted.

Blocked by:
- Full reliability depends on camera placement and config tuning.

Next safe action:
Add explicit diagnostics for selected capture profile and gesture confidence drift.

## Non-Goals / Do Not Build Yet

- Do not make the Python worker own UI actions.
- Do not add app-specific behavior to the sidecar.

## Known Bugs / Fragility

- Sidecar lifecycle leaks and slow/failing camera start are known risks.
- Camera angle can make corners unreachable.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/VisionSidecarClientTests.cs` | Client/protocol | No real camera in automated tests. |

## Relevant Docs / Reports / Prompts

- [[08_Implementation_Prompts/Index|Implementation Prompts Index]]
- [[07_Agent_Reports/Index|Agent Reports Index]]
- [[Current Test Coverage]]

## Open Questions

- Which runtime observations should be added after the next live validation?
