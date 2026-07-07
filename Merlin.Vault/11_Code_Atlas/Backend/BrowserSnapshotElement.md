---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# BrowserSnapshotElement

## File

`Merlin.Backend/Services/BrowserWorkspace/Snapshot/BrowserSnapshotElement.cs`

Verified present in current repo.

## Purpose

DOM element model used by browser page snapshots and page-aware action scoring.

## Fields / Members

- `Id`: Merlin-generated stable-ish element id for follow-up actions.
- `Type`: category enum.
- text fields: `Text`, `Label`, `AriaLabel`, `Title`, tooltip/title/name/placeholder/value preview.
- DOM fields: `DomId`, `CssClass`, `Role`, `Href`.
- `Rect`: viewport geometry.
- `IsVisible`, `IsEnabled`, `IsInViewport`: actionability flags.
- `Score`: script/backend relevance score.

## Created By

BrowserHost `PageSnapshotScript` creates JSON elements; tests also construct elements for scoring/safety.

## Consumed By

BrowserWorkspaceService scoring/safety/common-action matching; CommandRouter page text formatting; BrowserPageSafetyGuard.

## Flow

DOM -> snapshot element -> backend candidate scoring -> optional safety/confirmation -> BrowserHost click/search command.

## What Breaks If Changed

Removing label/tooltip fields harms YouTube/media control matching. Changing id/rect/visibility semantics breaks click targeting and readouts.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `BrowserWorkspaceScoringTests.cs`
- `BrowserPageSafetyGuardTests.cs`
