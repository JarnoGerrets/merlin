---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# IMotionControlProfile

## File

`Merlin.Backend/Services/Motion/IMotionControlProfile.cs`

Verified present in current repo.

## Purpose

Profile contract for surface-specific motion behavior.

## Fields / Members

- `Descriptor`: id, display, surface kind, priority, capabilities.
- `CanHandle(surface)`: selection predicate.
- `ActivateAsync`, `DeactivateAsync`: profile lifecycle.
- `HandleGestureAsync`: process gesture context.
- `OnActiveSurfaceChangedAsync`: optional surface update callback.

## Created By

Implemented by DashboardMotionProfile, BrowserWorkspaceMotionProfile, NeutralMotionProfile.

## Consumed By

MotionControlProfileRegistry selects profiles; MotionControlModeService activates/deactivates/dispatches.

## Flow

ActiveSurface -> registry resolution -> mode service lifecycle -> profile handles gestures.

## What Breaks If Changed

Changing lifecycle order or required members affects all profiles and sidecar start/stop decisions.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `MotionControlProfileRegistryTests.cs`
- `MotionControlModeServiceTests.cs`
