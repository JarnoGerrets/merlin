---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# IMotionControlModeService

## File

`Merlin.Backend/Services/Motion/IMotionControlModeService.cs`

Verified present in current repo.

## Purpose

Contract for enabling/disabling profile-based motion mode and dispatching vision gestures to the active profile.

## Fields / Members

- `DashboardGestureForwarded`: compatibility event.
- `Current`: current snapshot.
- `IsEnabled`: quick state flag.
- `EnableAsync(reason, override)`, `DisableAsync(reason)`: lifecycle.
- `HandleGestureAsync`: gesture dispatch.
- `OnActiveSurfaceChangedAsync`: surface-driven profile switch.

## Created By

Implemented by `MotionControlModeService`; faked in `VisionGestureEventRouterTests`.

## Consumed By

CommandRouter, VisionGestureEventRouter, tests, future status surfaces.

## Flow

Voice command enables motion mode; sidecar emits gestures; router delegates to mode service; mode service profile dispatches.

## What Breaks If Changed

Changing this interface requires updates to router, DI, fakes, tests, and profile lifecycle.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `MotionControlModeServiceTests.cs`
- `VisionGestureEventRouterTests.cs`
- `CommandRouterTests.cs`
