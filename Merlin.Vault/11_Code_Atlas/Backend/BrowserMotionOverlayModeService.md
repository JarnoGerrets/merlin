---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserMotionOverlayModeService

## File

`Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserMotionOverlayModeService.cs`

Verified present in current repo.

## Purpose

Maintains browser pointer overlay mode by mapping normalized vision coordinates to BrowserHost-local overlay coordinates and publishing render state.

## Related Features

- [[Browser Control]]
- [[Browser Workspace]]
- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]
- [[Browser Page-Aware Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserMotionOverlayModeService` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `EnableAsync` | public | Checks workspace active/bounds, marks mode enabled, emits initial overlay state, subscribes to workspace changes. | workspace state; `RaiseStateChangedAsync` | BrowserWorkspaceMotionProfile/start pointer route | Returns start result. |
| `DisableAsync` | public | Clears enabled state, resets mapper, sends inactive overlay. | mapper reset; `RaiseStateChangedAsync` | profile deactivation/close | Reason logged. |
| `UpdatePointerAsync` | public | Maps `gesture.pointer.move` to overlay render state. | `BrowserPointerMapper.Map`; workspace update | browser profile/legacy router | Stores latest state. |
| `SetClickVisualStateAsync` | public | Re-emits latest state with armed/fired/blocked/scroll visual state. | `RaiseStateChangedAsync` | BrowserPinchClickController | Visual feedback only. |
| `OnBrowserWorkspaceStateChangedAsync` | private | Hides/resizes overlay on BrowserWorkspace lifecycle/bounds changes. | inactive state; state raise | workspace event | Must tolerate host shutdown. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_enabled` | `bool` | Whether pointer updates are accepted. | methods | enable/disable/close | false on disable |
| `_latestState` | `BrowserPointerRenderState` | Last emitted overlay state. | click visual/pinch controller | pointer update/disable | inactive when disabled |
| `_gate` | `object` | Protects mode/latest state. | all methods | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IBrowserWorkspaceService` | Sends overlay state to BrowserHost and supplies bounds. |
| `BrowserPointerMapper` | Coordinate mapping and smoothing. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `StateChanged` | BrowserPinchClickController/tests | latest render state. |
| `browser_pointer_state` | BrowserHost | overlay active/pinched/inside/x/y/radius/visual state. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | browser profile/legacy router | `UpdatePointerAsync` |
| `BrowserWorkspaceStateChanged` | BrowserWorkspaceService | private handler |

## External Side Effects

Shows/hides native BrowserHost pointer overlay through workspace commands.

## Safety / Guardrails

Keep BrowserWorkspace active/bounds checks strict. DOM/page actions must pass safety/confirmation where applicable; raw pointer actions must remain BrowserHost-scoped.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserMotionOverlayModeServiceTests.cs` | enable/disable, bounds, mapping, inactive behavior. | No native overlay. |
| `BrowserPinchClickControllerTests.cs` | consumes pointer state. | No real click. |

## Known Risks / Fragility

Stale bounds cause cursor/click offset. Overlay updates during host shutdown must be ignored quietly.

## Change Notes for Agents

Read source and linked browser flow notes before editing. Do not mix DOM action safety with raw motion click coordinate ownership without an explicit design.
