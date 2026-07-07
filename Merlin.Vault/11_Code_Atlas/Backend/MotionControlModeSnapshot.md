---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlModeSnapshot

## File

`Merlin.Backend/Services/Motion/MotionControlModeSnapshot.cs`

Verified present in current repo.

## Purpose

Immutable snapshot of global motion mode state.

## Fields / Members

- `State`: Disabled/Enabling/Enabled/SwitchingProfile/Disabling/Faulted.
- `IsEnabled`: quick boolean.
- `ActiveProfileId`, `ActiveProfileDisplayName`: selected profile.
- `ActiveSurface`: surface the profile was selected for.
- `EnabledUtc`, `UpdatedUtc`: lifecycle timestamps.
- `Reason`: diagnostic transition reason.
- `Disabled(...)`: factory for disabled dashboard/unknown snapshots.

## Created By

MotionControlModeService creates snapshots on enable/disable/profile switch/failure.

## Consumed By

CommandRouter responses, tests, status/debug code, profile context.

## Flow

Mode lifecycle updates snapshot; snapshot is exposed via interface and passed into gesture contexts.

## What Breaks If Changed

Changing fields breaks router status wording, tests, and profile diagnostics.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlModeServiceTests.cs`
