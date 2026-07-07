---
type: state
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Active Surface State Ownership

| State | Owner | Readers | Writers | Lifetime | Reset Conditions | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Current surface snapshot | ActiveSurfaceService | CommandRouter, LiveUtteranceGate, MotionControlProfileRegistry | BrowserWorkspaceService/future producers | app lifetime | ResetToDashboard | Stale surface routes commands wrong |

## Related Notes

- [[Current System Map]]
- [[Code Atlas Index]]
