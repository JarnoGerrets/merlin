---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# BrowserPageSnapshot

## File

`Merlin.Backend/Services/BrowserWorkspace/Snapshot/BrowserPageSnapshot.cs`

Verified present in current repo.

## Purpose

Snapshot of current browser page metadata and categorized visible DOM elements.

## Fields / Members

- `SnapshotId`, `Url`, `Title`, `CapturedAtUtc`, `PageVersion`: identity/freshness metadata.
- `IsStale`, `IsLoading`: reuse guards.
- `Inputs`, `SearchFields`, `Buttons`, `Links`, `Headings`, `Results`, `TextBlocks`: categorized elements.
- `TotalElementCount`, `IsTruncated`, `Error`: snapshot completeness/failure info.

## Created By

BrowserHost `PageSnapshotScript` emits JSON; `BrowserWorkspaceService.CompletePageSnapshot` materializes/stores it.

## Consumed By

`CommandRouter` formats page readouts/find results; `BrowserWorkspaceService` scores click/search/common-action candidates.

## Flow

Backend requests snapshot -> BrowserHost executes script -> stdout event completes pending TCS -> latest snapshot powers page-aware commands.

## What Breaks If Changed

Changing categories or freshness fields breaks candidate scoring, page read responses, stale confirmation rejection, and tests.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `BrowserWorkspaceScoringTests.cs`
- `CommandRouterTests.cs`
