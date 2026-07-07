---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileOverride

## File

`Merlin.Backend/Services/Motion/MotionControlProfileOverride.cs`

Verified present in current repo.

## Purpose

Optional request to force a specific motion profile for enable/resolve.

## Fields / Members

- `ProfileId`: requested profile id.
- `Reason`: diagnostic reason for override.

## Created By

CommandRouter/debug/future settings can create it; tests construct it directly.

## Consumed By

MotionControlModeService passes it to registry; registry selects matching profile if registered.

## Flow

User/debug command -> enable -> registry override resolution -> active profile.

## What Breaks If Changed

Changing fields breaks manual override and future site/app profile debugging.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlProfileRegistryTests.cs`
- `MotionControlModeServiceTests.cs`
