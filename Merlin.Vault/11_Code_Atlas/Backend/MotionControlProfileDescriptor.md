---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileDescriptor

## File

`Merlin.Backend/Services/Motion/MotionControlProfileDescriptor.cs`

Verified present in current repo.

## Purpose

Immutable metadata for a motion profile.

## Fields / Members

- `ProfileId`: stable id.
- `DisplayName`: user/debug label.
- `SurfaceKind`: dashboard/browser/unknown target.
- `Priority`: selection ordering.
- `Capabilities`: set of motion capabilities.
- `Metadata`: optional extension data.

## Created By

Each IMotionControlProfile implementation initializes its Descriptor.

## Consumed By

MotionControlProfileRegistry, MotionControlModeService, tests/status surfaces.

## Flow

Profile implementation -> descriptor -> registry selection/listing -> mode snapshot.

## What Breaks If Changed

Changing ids/kinds/priorities changes profile selection and can route gestures to wrong surface.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlProfileRegistryTests.cs`
