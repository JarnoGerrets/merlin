---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileCapabilities

## File

`Merlin.Backend/Services/Motion/MotionControlProfileCapabilities.cs`

Verified present in current repo.

## Purpose

String constants describing what a motion profile can do.

## Fields / Members

- `Pointer`, `Hover`, `Select`, `Drag`, `Resize`, `Dismiss`: dashboard capabilities.
- `BrowserPointerOverlay`, `BrowserClick`, `BrowserScroll`: browser motion capabilities.
- `SafeNoop`: neutral profile capability.

## Created By

Profile descriptors reference these constants.

## Consumed By

MotionControlModeService uses capabilities to decide whether tracking should start; diagnostics/listing use descriptors.

## Flow

Profile descriptor -> registry/mode service -> sidecar start/stop and UI diagnostics.

## What Breaks If Changed

Mistyped constants can stop sidecar from starting or misrepresent profile features.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlProfileRegistryTests.cs`
- `MotionControlModeServiceTests.cs`
