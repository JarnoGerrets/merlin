---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserPinchClickStateMachine

## File

`Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPinchClickStateMachine.cs`

Verified present in current repo.

## Purpose

Pure state machine that converts pinch activity, movement, eligibility, and time into click/scroll/cooldown/blocked phases.

## Related Features

- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]
- [[Browser Pointer Overlay]]
- [[Browser Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserPinchClickStateMachine` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| constructors | public | Configure arm duration, scroll-hold duration, cooldown, and movement threshold. | field assignments | DI/tests | Defaults encode current UX. |
| `Update` | public | Advances phase from pinch/eligibility/time/overlay Y and returns snapshot with click or scroll instructions. | `CalculateDeltaY`; phase helpers | BrowserPinchClickController | Pure decision logic. |
| `Cancel` | public | Resets phase and visual state. | field reset | controller reset/workspace close | Clears pending click/scroll. |
| `CalculateDeltaY` | private | Computes movement from pinch anchor to current overlay Y. | stored anchor | `Update` | Used to enter scroll mode. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_phase` | `BrowserPinchClickPhase` | Current click/scroll state. | `Update` | `Update`, `Cancel` | normal after reset/cooldown |
| timer fields | `DateTimeOffset?` | Arm/cooldown timing. | `Update` | `Update`, `Cancel` | null after reset |
| `_anchorOverlayY` | `double?` | Y coordinate captured at pinch start. | `CalculateDeltaY` | `Update`, `Cancel` | null after reset |

## Dependencies

| Dependency | Used For |
| --- | --- |
| eligibility input | Blocks unsafe click before firing. |
| current time | Drives arm/cooldown behavior. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `BrowserPinchClickStateSnapshot` | controller | Phase, visual state, click/scroll booleans, scroll delta. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| pinch active / overlay Y / eligibility | BrowserPinchClickController | `Update` |

## External Side Effects

No external side effects.

## Safety / Guardrails

Raw browser motion actions are not the same as page-aware DOM actions. Keep BrowserWorkspace active, bounds, focus, and confidence checks conservative.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserPinchClickControllerTests.cs` | Exercises click, blocked, cooldown, and scroll phases through controller. | No standalone state-machine-only test. |

## Known Risks / Fragility

Timing thresholds tune accidental click avoidance. Movement threshold decides whether a pinch becomes click or scroll.

## Change Notes for Agents

Read source and linked browser motion/safety flow notes before editing. Keep pure decision logic separate from BrowserHost I/O.
