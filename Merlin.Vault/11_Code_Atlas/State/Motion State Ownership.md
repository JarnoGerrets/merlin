---
type: state
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Motion State Ownership

| State | Owner | Readers | Writers | Lifetime | Reset Conditions | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Motion enabled/current profile | MotionControlModeService | VisionGestureEventRouter, tests | CommandRouter, ActiveSurfaceChanged handler | Backend service lifetime | DisableAsync/profile activation failure | Stale profile if ActiveSurface is wrong |
| Camera tracking state | VisionSidecarHost/vision_worker.py | MotionControlModeService | Start/Stop tracking commands | Sidecar process lifetime | StopTracking/Shutdown | Process leak or camera lock |

## Related Notes

- [[Current System Map]]
- [[Code Atlas Index]]
