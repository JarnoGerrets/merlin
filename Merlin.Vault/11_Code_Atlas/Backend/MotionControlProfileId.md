---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileId

## File

`Merlin.Backend/Services/Motion/MotionControlProfileId.cs`

Verified present in current repo.

## Purpose

Stable profile id constants.

## Fields / Members

- `Dashboard`: `motion.dashboard`.
- `BrowserWorkspace`: `motion.browser_workspace`.
- `Neutral`: `motion.neutral`.

## Created By

Profile descriptors and overrides use these constants.

## Consumed By

MotionControlProfileRegistry override matching and tests.

## Flow

Command/debug override -> registry compares id -> selected profile.

## What Breaks If Changed

Renaming ids breaks overrides, tests, and persisted/future config references.

## Related Features

- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Vision Sidecar]]
- [[Browser Control]]

## Tests

- `MotionControlProfileRegistryTests.cs`
