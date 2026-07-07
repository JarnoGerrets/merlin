---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileResolution

## File

`Merlin.Backend/Services/Motion/MotionControlProfileResolution.cs`

Verified present in current repo.

## Purpose

Result of resolving a motion profile for an ActiveSurface.

## Fields / Members

- `Profile`: selected profile instance.
- `Confidence`: selection confidence for diagnostics.
- `Reason`: why selected, fallback, or override.

## Created By

MotionControlProfileRegistry creates it.

## Consumed By

MotionControlModeService activates the profile and writes reason/confidence to logs/snapshots.

## Flow

ActiveSurface -> registry -> resolution -> profile activation.

## What Breaks If Changed

Changing it breaks mode service activation and tests.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlProfileRegistryTests.cs`
- `MotionControlModeServiceTests.cs`
