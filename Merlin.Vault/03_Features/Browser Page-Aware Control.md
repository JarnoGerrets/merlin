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

# Browser Page-Aware Control

## Summary

DOM/snapshot-based browser actions with safety guard and confirmation.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- BrowserPageSnapshotService interface and BrowserWorkspaceService snapshot implementation exist.
- `Merlin.BrowserHost/PageSnapshotScript.cs, ClickElementScript.cs, SearchFieldScript.cs, CommonActionScript.cs exist.`
- BrowserPageSafetyGuard.cs and tests exist.

## What Exists Today

Page snapshot, visible elements, click/search/action scripts, and safety guard exist; robust site-specific behavior remains partial.

## Current Behavior

Can target visible links/buttons/inputs in limited cases and classify risky page actions for confirmation.

## Planned Behavior

Reliable generic page action layer first, learned site profiles later.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/Snapshot/BrowserPageSnapshot.cs` | BrowserPageSnapshot | Snapshot model | Visible page elements. |
| `Merlin.BrowserHost/PageSnapshotScript.cs` | PageSnapshotScript | DOM extractor | Runs in WebView2. |
| `Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPageSafetyGuard.cs` | BrowserPageSafetyGuard | Safety classifier | Risk/confirmation decisions. |

## Code Atlas

- [[BrowserWorkspaceService]]
- [[BrowserPageSafetyGuard]]
- [[PageSnapshotScript]]
- [[CommonActionScript]]
- [[Browser Page Action Safety Flow]]

## Related Systems

- [[Browser Workspace]]
- [[Control Profile DB]]
- [[Safety and Confirmation]]
- [[Site Control Profiles]]

## Dependencies

- [[Browser Workspace]]
- [[Safety and Confirmation]]

## Dependents

- [[Control Profile DB]]
- [[Site Control Profiles]]

## Readiness

Ready for implementation: yes

Reason:
Generic safety and snapshot hardening is ready.

Blocked by:
- Learned/site control depends on motion control and correction stability.

Next safe action:
Improve generic element selection/revalidation; do not add app-specific shortcuts.

## Non-Goals / Do Not Build Yet

- Do not build YouTube profile here.
- Do not skip confirmation for risky actions.

## Known Bugs / Fragility

- Click target resolution can drift after page changes.
- Some media buttons are icon/data-title based, not plain text.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrowserPageSafetyGuardTests.cs` | Safety rules | DOM extraction is mostly host/runtime validation. |

## Relevant Implementation Plans

- [[Site Control Profiles Learning Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
