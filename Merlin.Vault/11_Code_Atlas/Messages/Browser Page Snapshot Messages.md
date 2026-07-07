---
type: protocol
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Browser Page Snapshot Messages

| Event / Message | Direction | Payload / Notes |
| --- | --- | --- |
| `page_snapshot` | backend -> BrowserHost | Request id and snapshot options sent by `BrowserWorkspaceService.GetSnapshotAsync`. |
| snapshot result event | BrowserHost -> backend | Completed by `BrowserWorkspaceService.CompletePageSnapshot` into a pending snapshot task. |
| `click_element` | backend -> BrowserHost | Element id/candidate data for `ClickElementScript`. |
| page action result event | BrowserHost -> backend | Completed by `BrowserWorkspaceService.CompletePageAction`. |
| common action command | backend -> BrowserHost | Media/common actions such as pause/play/fullscreen/skip-ad through `CommonActionScript`. |

## Related Notes

- [[Browser Page-Aware Control]]
- [[Browser Page Snapshot Flow]]
- [[Backend BrowserHost Commands]]
