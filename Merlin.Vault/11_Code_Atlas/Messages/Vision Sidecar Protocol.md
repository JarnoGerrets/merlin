---
type: protocol
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Vision Sidecar Protocol

| Event / Message | Direction | Payload / Notes |
| --- | --- | --- |
| `vision.start_tracking` | backend -> Python | camera index/name/backend/profile, width/height/fps, model path, emit rate, pinch thresholds, pointer mapping, calibration paths. |
| `vision.stop_tracking` | backend -> Python | Stop frame loop, release camera, release active pinch states. |
| `vision.calibrate_pinch` | backend -> Python | lead/open/pinch/release timings, phase pause seconds, calibration output path. |
| `vision.calibrate_motion_region` | backend -> Python | lead/corner timings, phase pause, padding, calibration output path. |
| `vision.shutdown` | backend -> Python | Stop worker loop and release resources. |
| `vision.ready` / log events | Python -> backend | Worker lifecycle and camera/profile diagnostics. |
| `VisionCameraProfileBenchmarkResult` log | Python -> backend | Backend/profile candidate metrics: open success, startup, actual resolution/fps/fourcc, measured fps, read ms, black frames, failures. |
| `vision.tracking_started` / `vision.tracking_stopped` | Python -> backend | Tracking lifecycle and actual camera metadata. |
| `gesture.pointer.move` | Python -> backend | pointerId, x, y, confidence, source. |
| `gesture.pinch.start` / `gesture.pinch.move` / `gesture.pinch.end` | Python -> backend | pointerId and coordinates/confidence where available. |
| `vision.pinch_calibration_started/completed` | Python -> backend | status, thresholds, open/pinch/release samples, calibration path, message. |
| `vision.motion_region_calibration_started/completed` | Python -> backend | status, control region bounds, corner samples, calibration path, message. |
| `vision.error` | Python -> backend | error code/message for camera, tracking, protocol, or calibration failures. |

## Related Notes

- [[VisionSidecarHost]]
- [[VisionSidecarClient]]
- [[VisionSidecarMessage]]
- [[vision_worker.py]]
- [[Motion Gesture Dispatch Flow]]
