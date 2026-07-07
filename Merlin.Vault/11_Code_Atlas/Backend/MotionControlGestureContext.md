---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlGestureContext

## File

`Merlin.Backend/Services/Motion/MotionControlGestureContext.cs`

Verified present in current repo.

## Purpose

Per-gesture context passed from motion mode service into the active motion profile.

## Fields / Members

- `GestureEvent`: raw parsed vision event.
- `ActiveSurface`: surface snapshot at dispatch time.
- `ModeSnapshot`: current mode/profile state.
- `ReceivedUtc`: timestamp for diagnostics/timing.

## Created By

`MotionControlModeService.HandleGestureAsync` creates it after receiving a `VisionGestureEvent`.

## Consumed By

DashboardMotionProfile, BrowserWorkspaceMotionProfile, NeutralMotionProfile.

## Flow

Vision worker -> VisionSidecarHost -> VisionGestureEventRouter -> MotionControlModeService -> active profile.

## What Breaks If Changed

Changing fields breaks profile decisions and tests that inspect dispatch context.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlModeServiceTests.cs`
