---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserWorkspaceMotionProfile

## File

`Merlin.Backend/Services/Motion/Profiles/BrowserWorkspaceMotionProfile.cs`

Verified present in current repo.

## Purpose

Wraps browser pointer overlay, pinch click, and scroll services as the BrowserWorkspace motion profile. It is selected only while the active surface is BrowserWorkspace.

## Related Features

- [[Browser Control]]
- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]
- [[Motion Control Profile Layer]]

## Main Types / Classes

- `BrowserWorkspaceMotionProfile` implements `IMotionControlProfile`.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Descriptor` | public property | Profile id `motion.browser_workspace`, BrowserWorkspace kind, priority, and browser pointer/click/scroll capabilities. | object initializer | registry/service/tests | Signals that sidecar tracking is needed. |
| `CanHandle` | public | Returns true for BrowserWorkspace surface. | `ActiveSurfaceSnapshot.Kind` | registry | Keeps browser gestures out of dashboard mode. |
| `ActivateAsync` | public | Ensures BrowserWorkspace is active and enables `BrowserMotionOverlayModeService`. | `_browserWorkspace`; `_browserMotionOverlayModeService.EnableAsync` | motion service | Does not launch browser itself. |
| `DeactivateAsync` | public | Disables overlay and resets pinch click state. | overlay service; pinch controller | motion service | Stops visual pointer when surface changes or mode disables. |
| `HandleGestureAsync` | public | Sends pointer movement to overlay, pinch gestures to click controller, and lets click controller coordinate scroll-hold state. | `UpdatePointerAsync`; `BrowserPinchClickController.HandleGestureAsync` | motion service | Consumes pointer move, pinch start/move/end. |
| `OnActiveSurfaceChangedAsync` | public | Logs/accepts surface updates; switching is owned by motion service. | logger | motion service | Profile does not self-switch. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `Descriptor` | `MotionControlProfileDescriptor` | Profile identity/capabilities for selection and tracking. | registry/service | object initializer | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IBrowserWorkspaceService` | Reads workspace state and sends overlay/click/scroll commands. |
| `BrowserMotionOverlayModeService` | Maps webcam pointer to browser overlay coordinates. |
| `BrowserPinchClickController` | Converts pinch phases into click or scroll-hold. |
| `ILogger<BrowserWorkspaceMotionProfile>` | Diagnostics. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `browser_pointer_state` | BrowserHost via BrowserWorkspaceService | Render state after pointer move or visual click state. |
| `browser_pointer_click` / `browser_scroll_by_pixels` | BrowserHost via lower services | Native click/scroll actions from pinch handling. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | MotionControlModeService | `HandleGestureAsync` |
| `BrowserWorkspaceStateChanged` | lower browser services | overlay/click services subscribe directly |

## External Side Effects

Updates BrowserHost native pointer overlay, may fire native click through BrowserHost, may scroll WebView content.

## Safety / Guardrails

Keep raw click/scroll behavior constrained to BrowserWorkspace. Page action safety does not currently guard raw native clicks, so do not route dashboard or unknown gestures here.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `MotionControlProfileRegistryTests.cs` | BrowserWorkspace profile selection. | Does not test BrowserHost process. |
| `BrowserMotionOverlayModeServiceTests.cs` | Overlay mapping and state publication. | No real overlay window. |
| `BrowserPinchClickControllerTests.cs` | Pinch click/scroll phases and bounds guards. | No OS-level click verification. |

## Known Risks / Fragility

Raw click coordinates depend on BrowserHost bounds and overlay state. If BrowserHost closes without state reset, this profile can try to update a missing host unless lower services quietly ignore inactive host updates.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
