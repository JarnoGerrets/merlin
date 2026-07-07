---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlModeState

## File

`Merlin.Backend/Services/Motion/MotionControlModeState.cs`

Verified present in current repo.

## Purpose

Enum naming lifecycle states for global motion control mode.

## Fields / Members

- `Disabled`: no gesture dispatch.
- `Enabling`: enable flow in progress.
- `Enabled`: active profile receives gestures.
- `SwitchingProfile`: active surface transition in progress.
- `Disabling`: disable flow in progress.
- `Faulted`: enable/dispatch/lifecycle failure state.

## Created By

MotionControlModeService assigns these values inside snapshots.

## Consumed By

CommandRouter/status paths and tests read them through `MotionControlModeSnapshot`.

## Flow

Mode service transition -> snapshot -> responses/status/tests.

## What Breaks If Changed

Reordering enum values can break serialized/int comparisons if any are introduced; renaming breaks status/tests.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlModeServiceTests.cs`
