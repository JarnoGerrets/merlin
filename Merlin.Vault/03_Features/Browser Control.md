---
type: feature
status: partial
area: cross-cutting
tags:
  - merlin
  - feature
  - status/partial
  - layer/cross-cutting
---

# Browser Control

## Summary

Spoken and motion control over BrowserWorkspace across navigation, pointer, click, scroll, and page-aware phases.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- CommandRouter.cs routes browser commands.
- BrowserWorkspaceService.cs implements host actions.
- BrowserMotionOverlayModeService.cs, BrowserPinchClickController.cs, and BrowserScrollCommandService.cs implement motion phases.
- BrowserPageSafetyGuard.cs and BrowserHost scripts implement parts of page-aware control.

## What Exists Today

Phase 1 navigation, Phase 2 pointer, Phase 3 pinch click, Phase 4 scroll, and Phase 5 page-aware primitives exist, but the combined UX remains partial.

## Current Behavior

Can open/close browser, navigate/search, start pointer overlay, use pinch click/scroll, click/read/search page elements in limited forms.

## Planned Behavior

Motion-first generic control, reliable page actions, then site control profiles after full motion control.

Near-term media-control prompt:
- [[YouTube Site Control Profile Media Commands]] covers correct fullscreen confirmations and a small YouTube-only 10-second seek profile.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/CommandRouter.cs` | CommandRouter | Routes spoken browser commands | Open/close/navigation/page actions. |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserMotionOverlayModeService.cs` | BrowserMotionOverlayModeService | Pointer overlay mode | Maps vision pointer to overlay state. |
| `Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPageSafetyGuard.cs` | BrowserPageSafetyGuard | Page action safety | Classifies risky actions. |

## Code Atlas

- [[CommandRouter]]
- [[BrowserWorkspaceService]]
- [[BrowserMotionOverlayModeService]]
- [[BrowserPinchClickController]]
- [[BrowserScrollCommandService]]
- [[Browser Page Action Safety Flow]]

## Related Systems

- [[Active Surface Layer]]
- [[Browser Workspace]]
- [[Control Profile DB]]
- [[Site Control Profiles]]
- [[Vision Sidecar]]

## Dependencies

- [[Browser Workspace]]
- [[Active Surface Layer]]
- [[Vision Sidecar]]

## Dependents

- [[Site Control Profiles]]
- [[Control Profile DB]]

## Readiness

Ready for implementation: yes

Reason:
Generic browser control hardening is ready; site-specific behavior is not.

Blocked by:
- Learned/site controls need [[Control Profile DB]] and correction stability.

Next safe action:
Fix generic lifecycle/safety and avoid adding site-specific command clutter.

## Non-Goals / Do Not Build Yet

- Do not add YouTube-specific command routing into generic browser control.

## Known Bugs / Fragility

- Pause/play/stop ambiguity.
- Dutch STT variants need central handling.
- Click target resolution can be unreliable.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrowserMediaCommandNormalizerTests.cs` | Media phrase variants | Does not verify actual DOM click. |
| `Merlin.Backend.Tests/BrowserMotionOverlayModeServiceTests.cs` | Pointer mode | No WebView2 end-to-end coverage. |

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
