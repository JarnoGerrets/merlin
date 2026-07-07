---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserPinchClickController

## File

`Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPinchClickController.cs`

Verified present in current repo.

## Purpose

Coordinates BrowserWorkspace pinch gestures by combining latest overlay pointer state, the click state machine, BrowserHost click commands, scroll commands, and visual feedback.

## Related Features

- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]
- [[Browser Pointer Overlay]]
- [[Browser Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserPinchClickController` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `HandleGestureAsync` | public | Filters pinch gestures, evaluates eligibility, updates state machine, fires click/scroll actions, and updates visual state. | state machine; workspace; scroll service; pointer mode | BrowserWorkspaceMotionProfile/legacy router | Main gesture-to-action path. |
| `ResetAsync` | public | Cancels state machine, resets scroll service, restores normal visual state. | state machine; scroll service; pointer mode | profile deactivation/workspace close/tests | Clears stuck phases. |
| `OnBrowserPointerStateChangedAsync` | private | Stores latest pointer render state and BrowserHost-owned click coordinate. | lock | overlay service event | Pinch events do not carry final click point. |
| `OnBrowserWorkspaceStateChangedAsync` | private | Resets controller when browser becomes inactive/unavailable. | `ResetAsync` | workspace event | Prevents post-close clicks. |
| `GetEligibility` | private | Blocks low confidence, inactive workspace, missing coordinate, or outside-bounds click. | latest state/workspace | `HandleGestureAsync` | Drives blocked visual state. |
| `SetVisualStateAsync` | private | Sends click phase to overlay service. | overlay service | gesture/reset paths | Visual feedback. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_stateMachine` | `BrowserPinchClickStateMachine` | Phase/click/scroll decisions. | handler | handler/reset | service lifetime |
| latest pointer/click fields | render state and coordinates | Last overlay state/click target. | eligibility/action | pointer state event/reset | cleared on reset/inactive |
| `_gate` | `object` | Protects latest pointer state. | handlers | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IBrowserWorkspaceService` | Sends click and observes workspace lifecycle. |
| `BrowserMotionOverlayModeService` | Supplies pointer state and visual feedback. |
| `BrowserScrollCommandService` | Sends rate-limited scroll. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `browser_pointer_click` | BrowserHost via workspace service | Native click at current overlay point. |
| `browser_scroll_by_pixels` | BrowserHost via scroll service | WebView scroll delta. |
| `browser_pointer_state` | BrowserHost via overlay service | Visual phase. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` pinch events | browser profile/legacy router | `HandleGestureAsync` |
| `BrowserPointerRenderState` | overlay service event | pointer state handler |
| `BrowserWorkspaceStateChanged` | workspace service | workspace state handler |

## External Side Effects

Can cause BrowserHost native click and WebView scrolling.

## Safety / Guardrails

Raw browser motion actions are not the same as page-aware DOM actions. Keep BrowserWorkspace active, bounds, focus, and confidence checks conservative.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserPinchClickControllerTests.cs` | Arming, click, cooldown, low confidence, inactive workspace, scroll mode, visual state. | No OS SendInput verification. |

## Known Risks / Fragility

Overlay/click coordinate drift is the largest UX risk. A missed pinch-end can leave scroll or cooldown state until reset.

## Change Notes for Agents

Read source and linked browser motion/safety flow notes before editing. Keep pure decision logic separate from BrowserHost I/O.
