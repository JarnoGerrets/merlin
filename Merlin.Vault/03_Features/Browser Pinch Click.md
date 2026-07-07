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

# Browser Pinch Click

## Summary

Pinch gesture converts current browser pointer position into a native browser click.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- BrowserPinchClickController.cs and BrowserPinchClickStateMachine.cs exist.
- NativeBrowserInputService.cs injects native input.
- BrowserPinchClickControllerTests.cs exists.

## What Exists Today

Pinch can fire a browser pointer click, but raw clicks bypass page action safety.

## Current Behavior

Controller checks browser active, overlay active, bounds, minimized state, hand lost, confidence, and inside bounds before click.

## Planned Behavior

Route risky raw click through safety-aware adapter or restrict contexts.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPinchClickController.cs` | BrowserPinchClickController | Pinch click owner | Eligibility and click fire. |
| `Merlin.BrowserHost/NativeBrowserInputService.cs` | NativeBrowserInputService | Native input | SendInput click. |

## Code Atlas

- [[BrowserPinchClickController]]
- [[BrowserPinchClickStateMachine]]
- [[NativeBrowserInputService]]
- [[Browser Pinch Click Flow]]

## Related Systems

- Future site control learning
- [[Browser Pointer Overlay]]

## Dependencies

- [[Browser Pointer Overlay]]

## Dependents

- Future site control learning

## Readiness

Ready for implementation: yes

Reason:
Safety hardening is ready; learned behavior is not.

Blocked by:
- Risk-aware raw pointer policy is missing.

Next safe action:
Add safety-aware raw click adapter.

## Non-Goals / Do Not Build Yet

- Do not add page-specific click hacks here.

## Known Bugs / Fragility

- Raw motion clicks bypass BrowserPageSafetyGuard.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrowserPinchClickControllerTests.cs` | Pinch click state | Native click validation is manual. |

## Relevant Docs / Reports / Prompts

- [[08_Implementation_Prompts/Index|Implementation Prompts Index]]
- [[07_Agent_Reports/Index|Agent Reports Index]]
- [[Current Test Coverage]]

## Open Questions

- Which runtime observations should be added after the next live validation?
