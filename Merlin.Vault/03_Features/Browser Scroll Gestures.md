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

# Browser Scroll Gestures

## Summary

Pinch-hold vertical movement scrolls BrowserWorkspace.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- BrowserScrollCommandService.cs exists.
- BrowserPinchClickStateMachine.cs has scrolling phase.
- BrowserHost handles browser_scroll_by_pixels command.

## What Exists Today

Pinch-hold movement sends scroll commands to BrowserHost/WebView2.

## Current Behavior

Scroll is throttled/amount-controlled but tuning remains empirical.

## Planned Behavior

Tune per profile and add visible diagnostics.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserScrollCommandService.cs` | BrowserScrollCommandService | Scroll throttling | Sends scroll commands. |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPinchClickStateMachine.cs` | BrowserPinchClickStateMachine | Scroll phase | Detects hold/movement. |

## Code Atlas

- [[BrowserScrollCommandService]]
- [[BrowserPinchClickStateMachine]]
- [[Browser Scroll Gesture Flow]]

## Related Systems

- Browser motion UX
- [[Browser Pinch Click]]
- [[Browser Pointer Overlay]]

## Dependencies

- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]

## Dependents

- Browser motion UX

## Readiness

Ready for implementation: yes

Reason:
Implemented enough to tune.

Blocked by:
- Physical camera reach and sensitivity calibration.

Next safe action:
Document/tune scroll thresholds per motion profile.

## Non-Goals / Do Not Build Yet

- Do not create site-specific scroll handling yet.

## Known Bugs / Fragility

- Pinch/click/scroll phase may feel ambiguous without feedback.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Browser motion tests` | Partial state coverage | No WebView2 scroll E2E. |

## Relevant Docs / Reports / Prompts

- [[08_Implementation_Prompts/Index|Implementation Prompts Index]]
- [[07_Agent_Reports/Index|Agent Reports Index]]
- [[Current Test Coverage]]

## Open Questions

- Which runtime observations should be added after the next live validation?
