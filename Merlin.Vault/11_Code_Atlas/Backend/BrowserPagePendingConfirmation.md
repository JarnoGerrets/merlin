---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# BrowserPagePendingConfirmation

## File

`Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPagePendingConfirmation.cs`

Verified present in current repo.

## Purpose

Serializable details for a risky page action waiting for user confirmation.

## Fields / Members

- `Action`: page action type such as click/search.
- `ElementId`: Merlin DOM id to click after confirmation.
- `ElementText`, `ElementHref`: display and safety context.
- `CurrentUrl`: page URL when confirmation was created.
- `SnapshotCapturedAtUtc`: snapshot timestamp used to reject stale confirmations.
- `Risks`: safety risks that required confirmation.

## Created By

`BrowserWorkspaceService.CreateClickConfirmation` creates it after `BrowserPageSafetyGuard` returns a confirmation decision.

## Consumed By

`ConfirmationService` stores it inside pending confirmation metadata; `BrowserWorkspaceService.ConfirmBrowserPageClickAsync` consumes it.

## Flow

Candidate -> safety decision -> pending confirmation -> user confirms -> service re-validates URL/snapshot/element and sends BrowserHost click.

## What Breaks If Changed

Removing URL/timestamp/element fields weakens stale confirmation protection and can click the wrong page after navigation.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `CommandRouterTests.cs` confirmation paths
- `BrowserPageSafetyGuardTests.cs` for risk sources
