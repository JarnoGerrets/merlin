---
type: flow
status: partial
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Browser Page Action Safety Flow

## Summary

Page-aware actions evaluate snapshot/action risk through BrowserPageSafetyGuard before confirmation/action execution; raw native motion clicks remain outside this guard.

## Current Flow

1. page-aware command
2. BrowserWorkspaceService.GetFreshSnapshotAsync
3. BrowserPageSafetyGuard
4. pending confirmation when risky
5. ConfirmBrowserPageClickAsync
6. ClickElementScript/CommonActionScript

## Mermaid Diagram

```mermaid
flowchart LR
    N0[page-aware command] --> N1[BrowserWorkspaceService.GetFreshSnapshotAsync]
    N1[BrowserWorkspaceService.GetFreshSnapshotAsync] --> N2[BrowserPageSafetyGuard]
    N2[BrowserPageSafetyGuard] --> N3[pending confirmation when risky]
    N3[pending confirmation when risky] --> N4[ConfirmBrowserPageClickAsync]
    N4[ConfirmBrowserPageClickAsync] --> N5[ClickElementScript/CommonActionScript]
```

## Related Feature And Architecture Notes

- [[Safety and Confirmation]]
- [[BrowserPageSafetyGuard]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
