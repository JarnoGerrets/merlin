---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# VisionOptions

## File

`Merlin.Backend/Configuration/VisionOptions.cs`

Verified present in current repo.

## Purpose

Configuration object for vision worker process, camera capture, pointer mapping, pinch thresholds, and calibration timings/paths.

## Fields / Members

- process/model: `Enabled`, `WarmOnStartup`, `PythonExecutable`, `WorkerScriptPath`, `ModelAssetPath`.
- camera/profile: `PreferredCameraName`, `Backend`, `CaptureProfile`, `CameraIndex`, `Width`, `Height`, `Fps`, `MirrorPreview`, `DebugPreview`.
- detection: `EmitRateHz`, `MaxHands`, primary hand thresholds.
- pinch: start/hold/release ratios, debounce, calibration path/timings.
- motion region: calibration path/timings/padding and control region bounds.
- pointer: smoothing, deadzone, gain.

## Created By

ASP.NET options binding from appsettings; tests may instantiate defaults.

## Consumed By

VisionSidecarHost reads it for commands; vision_worker.py consumes serialized config.

## Flow

Configuration -> sidecar start/calibration commands -> Python worker behavior.

## What Breaks If Changed

Changing defaults affects camera startup, FPS, pinch sensitivity, motion reach, and calibration UX.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `VisionSidecarClientTests.cs` source invariants
- `MotionControlModeServiceTests.cs` via sidecar abstraction
