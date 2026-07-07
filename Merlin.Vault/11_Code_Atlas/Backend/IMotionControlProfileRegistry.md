---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# IMotionControlProfileRegistry

## File

`Merlin.Backend/Services/Motion/IMotionControlProfileRegistry.cs`

Verified present in current repo.

## Purpose

Contract for resolving/listing motion profiles for a current ActiveSurface.

## Fields / Members

- `Resolve(activeSurface, profileOverride)`: selected profile, confidence, reason.
- `ListProfiles()`: descriptors for diagnostics/future UI.

## Created By

Implemented by MotionControlProfileRegistry.

## Consumed By

MotionControlModeService and tests.

## Flow

ActiveSurface snapshot enters registry; output resolution drives profile activation.

## What Breaks If Changed

Changing resolution contract breaks mode service and all profile tests.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `MotionControlProfileRegistryTests.cs`
