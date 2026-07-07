---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# IBrowserWorkspaceService

## File

`Merlin.Backend/Services/BrowserWorkspace/IBrowserWorkspaceService.cs`

Verified present in current repo.

## Purpose

Main backend interface for BrowserWorkspace lifecycle, navigation, page-aware actions, common media actions, pointer overlay, native click, and scroll.

## Fields / Members

- `StateChanged`, `IsActive`, `CurrentBounds`, `OpenUrlsInsideWorkspaceWhenActive`: lifecycle state.
- Navigation methods: open/navigate/back/forward/refresh.
- Scroll/zoom/search methods.
- Page methods: search current page, click visible element, common action, confirm click.
- Motion methods: update pointer overlay, fire pointer click, scroll pixels.
- `CloseAsync`: host shutdown.

## Created By

Implemented by `BrowserWorkspaceService`; numerous tests define fakes.

## Consumed By

CommandRouter, BrowserWorkspaceMotionProfile, BrowserMotionOverlayModeService, BrowserPinchClickController, BrowserScrollCommandService, WebSocketHandler state sender.

## Flow

Command routing and motion profiles depend on this interface instead of BrowserHost process details.

## What Breaks If Changed

Changing defaults for motion methods can break older fakes; changing method signatures affects router/profile tests widely.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `CommandRouterTests.cs`
- `BrowserMotionOverlayModeServiceTests.cs`
- `BrowserPinchClickControllerTests.cs`
