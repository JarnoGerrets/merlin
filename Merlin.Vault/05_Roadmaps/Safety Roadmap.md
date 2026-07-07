---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Safety Roadmap

## Scope

Confirmation, browser page guard, active-surface routing boundaries, and risky action prevention.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant feature notes | Relevant code atlas notes | Next safe action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Preserve browser page safety guard | implemented | BrowserPageSafetyGuard | page-aware control | yes | Guard returns safe/confirm/block decisions. | [[Safety and Confirmation]], [[Browser Page-Aware Control]] | [[BrowserPageSafetyGuard]], [[BrowserPageSafetyDecision]], [[BrowserPagePendingConfirmation]] | Keep tests for risky phrases/fields. |
| Fix raw motion click safety gap | partial | BrowserPinchClickController, NativeBrowserInputService | learned controls | yes | Native click path is separate from DOM safety. | [[Browser Pinch Click]], [[Safety and Confirmation]] | [[BrowserPinchClickController]], [[NativeBrowserInputService]], [[BrowserPageSafetyGuard]] | Add guard/confirmation strategy for raw clicks. |
| Confirm stale page actions safely | partial | BrowserWorkspaceService snapshots | reliable browser click | yes | Pending confirmations carry URL/timestamp but dynamic pages remain hard. | [[Browser Page-Aware Control]] | [[BrowserPagePendingConfirmation]], [[BrowserWorkspaceService]], [[BrowserPageSnapshot]] | Strengthen stale confirmation checks. |
| Deep external/app control safety | future | active surface, trusted registry, confirmation policy | external app automation | no | Broad app control is not current runtime. | [[External App Control]], [[Control Profile DB]] | [[CommandRouter]], [[Safety and Confirmation Architecture]] | Do not build without app-specific safety model. |
