---
type: feature
status: partial
area: cross-cutting
tags:
  - merlin
  - feature
  - status/partial
---

# Browser Control

## Summary

Voice and motion control of Browser Workspace, implemented in phases.

## Status

partial

## What Exists Today

- Phase 1 spoken navigation exists.
- Phase 2 pointer overlay exists.
- Phase 3 pinch click exists.
- Phase 4 scroll gesture exists.
- Phase 5 page-aware control/safety exists partially.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/Web/WebDestinationParser.cs` | Spoken browser parser | Navigation/control phrases. |
| `Merlin.Backend/Services/CommandRouter.cs` | Execution | Browser action switch. |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/*` | Pointer/click/scroll | Motion control. |
| `Merlin.BrowserHost/ClickElementScript.cs` | DOM click | Click visible element. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]

## Phase Status

| Phase | Status | Implemented files | Known bugs | Future work |
| --- | --- | --- | --- | --- |
| Phase 1 spoken navigation | implemented | `WebDestinationParser`, `CommandRouter`, `BrowserWorkspaceService` | phrase ambiguity | surface-aware expansion |
| Phase 2 pointer overlay | implemented | `BrowserMotionOverlayModeService`, `NativeBrowserPointerOverlayWindow` | z-order/DPI | safety integration |
| Phase 3 pinch click | implemented | `BrowserPinchClickController`, host click command | raw click safety gap | safe click abstraction |
| Phase 4 scroll gesture | implemented | `BrowserScrollCommandService`, pinch state machine | gesture tuning | per-profile sensitivity |
| Phase 5 page-aware control | partial | snapshot/click/common action/safety guard | dynamic pages, Dutch variants | site profiles later |


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Generic browser control works for navigation, common actions, pointer/click/scroll, and page-aware actions.

## Planned Behavior

After profile and page-aware foundations stabilize, add Control Profile DB and site learning.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- YouTube/media controls expose language/selector edge cases.
- Generic action routing can feel like guessing.
- Confirmation UX can be too heavy for search-result opening.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Do not add YouTube-specific profile before profile DB foundations.
