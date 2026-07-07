---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserPointerMapper

## File

`Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPointerMapper.cs`

Verified present in current repo.

## Purpose

Maps normalized camera pointer coordinates into BrowserWorkspace overlay pixels with gain, reach compensation, clamping, smoothing, and radius calculation.

## Related Features

- [[Browser Control]]
- [[Browser Workspace]]
- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]
- [[Browser Page-Aware Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserPointerMapper` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Map` | public | Validates input, clamps normalized coordinates, applies mapping/tuning, smooths against previous output, returns `BrowserPointerRenderState`. | clamp helpers; previous state | BrowserMotionOverlayModeService | Feeds cursor and click. |
| `Reset` | public | Clears previous smoothed coordinates. | field reset | overlay disable/profile switch | Prevents jumps between sessions. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| previous overlay coordinates | `double?` | Last smoothed x/y. | `Map` | `Map`, `Reset` | null after reset |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `BrowserPointerMappingInput` | Bounds, normalized pointer, confidence, pinch flag, visual state. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `BrowserPointerRenderState` | overlay service | mapped overlay output. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| normalized pointer | vision gesture path | `Map` |

## External Side Effects

No external side effects.

## Safety / Guardrails

Keep BrowserWorkspace active/bounds checks strict. DOM/page actions must pass safety/confirmation where applicable; raw pointer actions must remain BrowserHost-scoped.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserMotionOverlayModeServiceTests.cs` | Indirect mapping checks. | No camera-angle calibration test. |

## Known Risks / Fragility

Too much smoothing feels laggy; too much gain makes resize/click unstable. Coordinate-space confusion creates click offset.

## Change Notes for Agents

Read source and linked browser flow notes before editing. Do not mix DOM action safety with raw motion click coordinate ownership without an explicit design.
