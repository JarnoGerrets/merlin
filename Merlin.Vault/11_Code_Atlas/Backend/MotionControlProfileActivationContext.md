---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileActivationContext

## File

`Merlin.Backend/Services/Motion/MotionControlProfileActivationContext.cs`

Verified present in current repo.

## Purpose

Activation payload passed to a profile when motion mode selects it.

## Fields / Members

- `ActiveSurface`: surface being activated for.
- `Reason`: why activation happened.
- `ActivatedUtc`: activation timestamp.

## Created By

MotionControlModeService creates it in `ActivateResolvedProfileLockedAsync`.

## Consumed By

All `IMotionControlProfile.ActivateAsync` implementations.

## Flow

Surface resolution -> profile activation context -> profile logs/setup.

## What Breaks If Changed

Profiles lose diagnostics and surface metadata if fields change.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlModeServiceTests.cs`
- `MotionControlProfileRegistryTests.cs` indirectly
