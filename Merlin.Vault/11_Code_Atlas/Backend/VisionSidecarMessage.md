---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# VisionSidecarMessage

## File

`Merlin.Backend/Services/Vision/VisionSidecarMessage.cs`

Verified present in current repo.

## Purpose

Parsed JSON stdout message from Python vision worker.

## Fields / Members

- lifecycle/camera: `Type`, `Version`, `CameraName`, actual width/height/fps.
- diagnostics: `Message`, `Error`, `Code`, `Status`.
- gesture: `PointerId`, `X`, `Y`, `Confidence`, `Source`.
- pinch calibration: ratios and sample counts/path.
- motion region calibration: control region bounds, corner sample counts/path.

## Created By

VisionSidecarClient deserializes worker stdout JSON into this class.

## Consumed By

VisionSidecarHost.HandleOutputLineAsync interprets by Type, routes gestures, completes calibration, logs errors.

## Flow

Python stdout -> client parse -> host handler -> gesture router/calibration awaiters/logs.

## What Breaks If Changed

Changing JSON names breaks the sidecar protocol. Removing calibration fields prevents calibration result reporting.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `VisionSidecarClientTests.cs`
