---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# BrowserWorkspaceStateChanged

## File

`Merlin.Backend/Services/BrowserWorkspace/BrowserWorkspaceStateChanged.cs`

Verified present in current repo.

## Purpose

Event payload for BrowserWorkspace lifecycle/bounds/focus changes.

## Fields / Members

- `Active`: whether workspace host is active.
- `Bounds`: browser surface screen rectangle and focus/minimized state.
- `Reason`: diagnostic reason.
- `BrowserWorkspaceBounds.X/Y/Width/Height`: screen bounds.
- `IsMinimized`, `IsFocused`: host state used by pointer mode.

## Created By

`BrowserWorkspaceService.PublishStateChangedAsync` creates it from host bounds/open/close events.

## Consumed By

Frontend WebSocket state sender, BrowserMotionOverlayModeService, BrowserPinchClickController, tests/fakes.

## Flow

BrowserHost reports bounds -> service updates current state -> publishes event -> frontend and motion services adapt.

## What Breaks If Changed

If bounds semantics change, pointer overlay mapping and native click coordinate ownership break.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `BrowserMotionOverlayModeServiceTests.cs`
- `BrowserPinchClickControllerTests.cs`
