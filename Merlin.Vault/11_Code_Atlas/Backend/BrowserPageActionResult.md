---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# BrowserPageActionResult

## File

`Merlin.Backend/Services/BrowserWorkspace/PageControl/BrowserPageActionResult.cs`

Verified present in current repo.

## Purpose

Result contract returned by BrowserWorkspace page-aware operations and common actions.

## Fields / Members

- `Success`: whether requested page action completed.
- `Message`: user/log-facing explanation.
- `ErrorCode`: machine-readable failure such as missing element or safety block.
- `ElementId`, `ElementText`, `ElementHref`: selected DOM element identity.
- `CandidateCount`: how many candidates were considered.
- `Confirmation`: pending confirmation when action needs user approval.

## Created By

`BrowserWorkspaceService` creates this for snapshot search, click-visible-element, common actions, confirmations, and BrowserHost page action result completion.

## Consumed By

`CommandRouter` formats it into assistant responses; tests/fakes implement it; confirmation flow stores `Confirmation`.

## Flow

BrowserHost stdout page-action JSON is converted to this shape, then CommandRouter decides user text like success, blocked, not found, or confirmation required.

## What Breaks If Changed

Changing fields breaks CommandRouter response formatting, confirmation handling, BrowserHost result parsing, and tests that assert error codes.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `CommandRouterTests.cs`
- `BrowserWorkspaceScoringTests.cs`
- `BrowserPageSafetyGuardTests.cs` indirectly
