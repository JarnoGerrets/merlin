---
type: flow
status: partial
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Browser Scroll Gesture Flow

## Summary

Pinch-hold movement scrolls WebView2 through BrowserHost.

## Current Flow

1. pinch hold/move
2. BrowserPinchClickStateMachine scrolling phase
3. BrowserScrollCommandService.ScrollAsync
4. BrowserWorkspaceService.ScrollByPixelsAsync
5. browser_scroll_by_pixels
6. WebView2 scroll

## Mermaid Diagram

```mermaid
flowchart LR
    N0[pinch hold/move] --> N1[BrowserPinchClickStateMachine scrolling phase]
    N1[BrowserPinchClickStateMachine scrolling phase] --> N2[BrowserScrollCommandService.ScrollAsync]
    N2[BrowserScrollCommandService.ScrollAsync] --> N3[BrowserWorkspaceService.ScrollByPixelsAsync]
    N3[BrowserWorkspaceService.ScrollByPixelsAsync] --> N4[browser_scroll_by_pixels]
    N4[browser_scroll_by_pixels] --> N5[WebView2 scroll]
```

## Related Feature And Architecture Notes

- [[Browser Scroll Gestures]]
- [[BrowserScrollCommandService]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
