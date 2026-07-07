---
type: state
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Browser Workspace State Ownership

| State | Owner | Readers | Writers | Lifetime | Reset Conditions | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Host active/bounds | BrowserWorkspaceService | Browser motion/page services | Host stdout/open/close | Host process lifetime | Close/host exit | UI may not restore if stale |
| Native overlay render state | BrowserHost overlay | Native input service | browser_pointer_state command | BrowserHost window lifetime | Hide/close/minimize | Z-order/DPI issues |

## Related Notes

- [[Current System Map]]
- [[Code Atlas Index]]
