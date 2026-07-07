---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserPointerRenderState

## File

`Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPointerRenderState.cs`

Verified present in current repo.

## Purpose

Immutable message describing browser pointer overlay state and click visual phase.

## Related Features

- [[Browser Control]]
- [[Browser Workspace]]
- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]
- [[Browser Page-Aware Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserPointerRenderState` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| record constructor | public | Carries Active, IsPinched, IsInsideBounds, OverlayX, OverlayY, Radius, VisualState. | n/a | mapper/overlay service/host serialization | Value object. |
| `BrowserPointerClickVisualStates` | public static | Defines `normal`, `pinch_candidate`, `pinch_armed`, `click_sent`, `scroll_candidate`, `scrolling`, `cooldown`, `low_confidence`. | constants | click controller and overlay service | Must match host rendering. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| record properties | primitive values | One render-state snapshot. | host/click controller | mapper/overlay service | per pointer event |

## Dependencies

| Dependency | Used For |
| --- | --- |
| BrowserPointerMapper | Creates mapped states. |
| BrowserMotionOverlayModeService | Stores/transmits states. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `browser_pointer_state` payload | BrowserHost | serialized state. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| pointer mapping input / click visual state | overlay/click services | record creation |

## External Side Effects

No external side effects.

## Safety / Guardrails

Keep BrowserWorkspace active/bounds checks strict. DOM/page actions must pass safety/confirmation where applicable; raw pointer actions must remain BrowserHost-scoped.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| Browser motion/click tests | Verify state flows through services. | No direct serialization contract test. |

## Known Risks / Fragility

Changing semantics affects both visual cursor and click coordinate eligibility.

## Change Notes for Agents

Read source and linked browser flow notes before editing. Do not mix DOM action safety with raw motion click coordinate ownership without an explicit design.
