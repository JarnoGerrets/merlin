---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# BrowserPageSafetyDecision

## File

`Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPageSafetyDecision.cs`

Verified present in current repo.

## Purpose

Decision returned by BrowserPageSafetyGuard for a candidate page action.

## Fields / Members

- `Level`: safe, confirm, or blocked.
- `Reason`: diagnostic/user-facing reason.
- `Risks`: risk categories detected from element/context.

## Created By

`BrowserPageSafetyGuard.Evaluate` creates it.

## Consumed By

`BrowserWorkspaceService` decides whether to execute, create a confirmation, or return a blocked result.

## Flow

Page action candidate and context enter guard; decision controls BrowserWorkspace action execution.

## What Breaks If Changed

Changing levels/risks breaks safety policy, confirmations, and user response wording.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `BrowserPageSafetyGuardTests.cs`
- `CommandRouterTests.cs`
