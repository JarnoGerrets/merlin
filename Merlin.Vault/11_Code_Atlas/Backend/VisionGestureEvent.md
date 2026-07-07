---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# VisionGestureEvent

## File

`Merlin.Backend/Services/Vision/VisionGestureEvent.cs`

Verified present in current repo.

## Purpose

Backend model for a parsed sidecar gesture event.

## Fields / Members

- `Type`: `gesture.pointer.move`, `gesture.pinch.start`, `gesture.pinch.move`, or `gesture.pinch.end`.
- `PointerId`: logical pointer such as primary/secondary.
- `X`, `Y`: normalized pointer coordinates when present.
- `Confidence`: MediaPipe/worker confidence.
- `Source`: usually `webcam`.

## Created By

VisionSidecarHost creates it from `VisionSidecarMessage` fields; tests create instances directly.

## Consumed By

VisionGestureEventRouter, MotionControlModeService, profiles, frontend compatibility forwarding, browser motion services.

## Flow

worker stdout -> sidecar message -> gesture event -> router -> motion profile/frontend/browser.

## What Breaks If Changed

Changing JSON names breaks worker protocol; changing nullable coordinate behavior breaks pinch-end handling.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `VisionGestureEventRouterTests.cs`
- `MotionControlModeServiceTests.cs`
- `BrowserPinchClickControllerTests.cs`
