---
type: feature
status: implemented
area: browserhost
tags:
  - merlin
  - feature
  - status/implemented
  - layer/browserhost
---

# Browser Pointer Overlay

## Summary

Native transparent pointer overlay rendered above BrowserHost.

## Status

implemented

## Verified Against Code

Status verified: yes

Evidence:
- NativeBrowserPointerOverlayWindow.cs exists.
- BrowserMotionOverlayModeServiceTests.cs covers overlay mode state.
- BrowserPointerMapper.cs maps normalized gesture coordinates to browser bounds.

## What Exists Today

Backend sends browser_pointer_state to BrowserHost; BrowserHost renders native pointer overlay.

## Current Behavior

Overlay can hide on minimized/inactive browser and restore with state changes.

## Planned Behavior

Improve DPI diagnostics and lifecycle cleanup.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPointerMapper.cs` | BrowserPointerMapper | Coordinate mapping | Normalized hand to overlay-local pointer. |
| `Merlin.BrowserHost/NativeBrowserPointerOverlayWindow.cs` | NativeBrowserPointerOverlayWindow | Native overlay | Draws pointer and exposes click point. |

## Code Atlas

- [[BrowserPointerMapper]]
- [[NativeBrowserPointerOverlayWindow]]
- [[Browser Pointer Flow]]

## Related Systems

- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]
- [[Browser Workspace]]
- [[Vision Sidecar]]

## Dependencies

- [[Browser Workspace]]
- [[Vision Sidecar]]

## Dependents

- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]

## Readiness

Ready for implementation: yes

Reason:
Implemented; lifecycle/DPI hardening remains.

Blocked by:
- None for hardening.

Next safe action:
Add DPI/multi-monitor diagnostics.

## Non-Goals / Do Not Build Yet

- Do not make overlay capture focus or block listening.

## Known Bugs / Fragility

- Z-order/lifecycle fragility.
- DPI/multi-monitor uncertainty.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrowserMotionOverlayModeServiceTests.cs` | Backend overlay mode | Native transparency is manual. |

## Relevant Docs / Reports / Prompts

- [[08_Implementation_Prompts/Index|Implementation Prompts Index]]
- [[07_Agent_Reports/Index|Agent Reports Index]]
- [[Current Test Coverage]]

## Open Questions

- Which runtime observations should be added after the next live validation?
