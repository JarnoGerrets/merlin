---
type: feature
status: implemented
area: cross-cutting
tags:
  - merlin
  - feature
  - status/implemented
  - layer/cross-cutting
---

# Browser Workspace

## Summary

Backend-managed BrowserHost/WebView2 workspace with commands, snapshots, pointer overlay, click, scroll, and active-surface updates.

## Status

implemented

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.Backend/Services/BrowserWorkspace/BrowserWorkspaceService.cs exists.`
- `Merlin.BrowserHost/BrowserWorkspaceForm.cs exists.`
- `Merlin.BrowserHost/BrowserWorkspaceCommand.cs defines host command DTOs.`
- `BrowserWorkspaceScoringTests.cs and browser motion/page safety tests exist.`

## What Exists Today

Backend opens/closes BrowserHost, sends stdin JSON commands, reads stdout state, updates ActiveSurface, and supports navigation/page actions/motion overlay.

## Current Behavior

Open browser/search/navigation work. Closing closes host but UI restore has known stale-state risk. Page-aware actions and safety are partial.

## Planned Behavior

Tighten close/reset, page action revalidation, and safety integration for raw pointer actions.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/BrowserWorkspaceService.cs` | BrowserWorkspaceService | Backend owner | Host lifecycle and command protocol. |
| `Merlin.BrowserHost/BrowserWorkspaceForm.cs` | BrowserWorkspaceForm | WebView2 host | Command loop and browser UI. |
| `Merlin.BrowserHost/NativeBrowserPointerOverlayWindow.cs` | NativeBrowserPointerOverlayWindow | Native pointer overlay | Transparent overlay and click point. |

## Code Atlas

- [[BrowserWorkspaceService]]
- [[BrowserWorkspaceForm]]
- [[BrowserWorkspaceCommand]]
- [[NativeBrowserPointerOverlayWindow]]
- [[Browser Workspace Flow]]
- [[Backend BrowserHost Commands]]

## Related Systems

- [[Active Surface Layer]]
- [[Browser Control]]
- [[Browser Page-Aware Control]]
- [[Browser Pointer Overlay]]
- [[Safety and Confirmation]]

## Dependencies

- [[Active Surface Layer]]
- [[Safety and Confirmation]]

## Dependents

- [[Browser Control]]
- [[Browser Pointer Overlay]]
- [[Browser Page-Aware Control]]

## Readiness

Ready for implementation: yes

Reason:
Implemented; next work should fix lifecycle and safety gaps.

Blocked by:
- Learned site controls blocked by [[Control Profile DB]].

Next safe action:
Fix browser close/reset stale state and raw pointer safety gap.

## Non-Goals / Do Not Build Yet

- Do not treat arbitrary website-specific behavior as generic browser control.

## Known Bugs / Fragility

- Browser close/reset stale state.
- Overlay lifecycle/z-order fragility.
- DPI/multi-monitor uncertainty.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrowserWorkspaceScoringTests.cs` | Scoring | No full WebView2 automation. |
| `Merlin.Backend.Tests/BrowserPageSafetyGuardTests.cs` | Safety guard | Raw motion clicks bypass page safety. |

## Relevant Implementation Plans

- [[Browser Control Phases 2-5 Plan]]
- [[Site Control Profiles Learning Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (4 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
