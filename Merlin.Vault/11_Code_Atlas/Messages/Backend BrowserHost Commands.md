---
type: protocol
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Backend BrowserHost Commands

| Event / Message | Direction | Payload / Notes |
| --- | --- | --- |
| `navigate` | backend -> BrowserHost | URL navigation; handled by `BrowserWorkspaceForm.Navigate`. |
| `back` / `forward` / `refresh` | backend -> BrowserHost | WebView history/reload commands. |
| `scroll` / `scroll_to_top` / `scroll_to_bottom` | backend -> BrowserHost | Generic browser scroll commands. |
| `browser_scroll_by_pixels` | backend -> BrowserHost | Pixel delta from browser motion scroll gestures. |
| `zoom_in` / `zoom_out` / `reset_zoom` | backend -> BrowserHost | WebView zoom factor changes. |
| `search` | backend -> BrowserHost | Browser-level search/navigation query path. |
| `page_snapshot` | backend -> BrowserHost | DOM snapshot request with request id/options; result returns on stdout. |
| `click_element` | backend -> BrowserHost | DOM element click by Merlin element id plus optional stale-snapshot expectations. |
| `search_field` | backend -> BrowserHost | Find/fill/submit page search input. |
| `common_action` | backend -> BrowserHost | Media/common control action such as pause/play/fullscreen/skip-ad. |
| `browser_pointer_state` | backend -> BrowserHost | Native overlay render state: active, pinched, inside bounds, overlay x/y, radius, visual state. |
| `browser_pointer_click` | backend -> BrowserHost | Ask host to click current overlay-owned screen point. |
| `close` | backend -> BrowserHost | Close BrowserWorkspace host window/process. |

## Host -> Backend Events

| Event / Message | Direction | Payload / Notes |
| --- | --- | --- |
| bounds/state events | BrowserHost -> backend | Active/focused/minimized screen bounds used by frontend and pointer mapping. |
| navigation events | BrowserHost -> backend | URL/title/loading changes used for snapshot freshness and ActiveSurface metadata. |
| `page_snapshot_result` | BrowserHost -> backend | Snapshot payload or error keyed by request id. |
| `page_action_result` | BrowserHost -> backend | Result payload for click/search/common-action command. |
| log/error lines | BrowserHost -> backend | Diagnostics from host process and WebView2. |

## Related Notes

- [[BrowserWorkspaceService]]
- [[BrowserWorkspaceForm]]
- [[BrowserWorkspaceCommand]]
- [[Browser Workspace Flow]]
- [[Browser Pointer Flow]]
