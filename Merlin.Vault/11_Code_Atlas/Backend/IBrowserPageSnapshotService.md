---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# IBrowserPageSnapshotService

## File

`Merlin.Backend/Services/BrowserWorkspace/Snapshot/IBrowserPageSnapshotService.cs`

Verified present in current repo.

## Purpose

Interface for reading cached or fresh BrowserWorkspace page snapshots.

## Fields / Members

- `LatestSnapshot`: cached snapshot or null.
- `GetSnapshotAsync`: cached/current snapshot path.
- `GetFreshSnapshotAsync(policy)`: explicit freshness request with robustness policy.

## Created By

Implemented by `BrowserWorkspaceService`; faked in tests through `IBrowserWorkspaceService` implementations.

## Consumed By

CommandRouter page read/find/click paths and browser service tests.

## Flow

Router asks interface for snapshot; implementation may return cache or request BrowserHost page snapshot.

## What Breaks If Changed

Changing method names/contracts breaks CommandRouter and BrowserWorkspace abstractions.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Vision Sidecar]]
- [[Safety and Confirmation]]

## Tests

- `CommandRouterTests.cs`
- browser workspace tests/fakes
