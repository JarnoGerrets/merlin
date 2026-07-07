---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserWorkspaceService

## File

`Merlin.Backend/Services/BrowserWorkspace/BrowserWorkspaceService.cs`

Verified present in current repo.

## Purpose

Backend owner of the BrowserWorkspace host process, stdin/stdout protocol, page snapshots/actions, safety confirmations, pointer overlay commands, and ActiveSurface updates.

## Related Features

- [[Browser Control]]
- [[Browser Workspace]]
- [[Browser Pointer Overlay]]
- [[Browser Pinch Click]]
- [[Browser Page-Aware Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserWorkspaceService` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `OpenAsync` | public | Starts BrowserHost if needed, navigates initial URL, publishes active state, and sets ActiveSurface to BrowserWorkspace. | process start; `PublishStateChangedAsync`; `SetBrowserWorkspaceActiveSurfaceAsync` | CommandRouter/web destination routes | Host lifecycle entry. |
| `NavigateAsync` / `BackAsync` / `ForwardAsync` / `RefreshAsync` | public | Mark snapshot stale, send navigation command, and wait a settle period. | `SendCommandAsync`; `SettlePageActionAsync` | CommandRouter | Requires active host. |
| `ScrollAsync`, `ScrollByPixelsAsync`, zoom/search methods | public | Send scoped browser commands to host. | `SendCommandAsync` | CommandRouter and browser motion services | Pixel scroll is motion path. |
| `GetSnapshotAsync` / `GetFreshSnapshotAsync` | public | Return cached snapshot or send `page_snapshot` and await matching stdout event. | pending snapshot TCS map; host command | page read/click/search routes | Freshness policy controls reuse. |
| `ClickVisibleElementAsync` | public | Score visible elements from snapshot, evaluate `BrowserPageSafetyGuard`, create confirmation when needed, send `click_element`. | candidate builder; safety guard; confirmation service | CommandRouter page click/open-result routes | Core page-aware click path. |
| `PerformCommonActionAsync` | public | Executes common media actions through direct host script or snapshot candidates. | common-action scoring; host command | CommandRouter media routes | Used for pause/play/fullscreen/skip-ad. |
| `UpdateBrowserPointerOverlayAsync` | public | Sends `browser_pointer_state` to host when active. | `SendCommandAsync` | BrowserMotionOverlayModeService | Should quietly ignore inactive host. |
| `FireBrowserPointerClickAsync` | public | Sends `browser_pointer_click` to host. | `SendCommandAsync` | BrowserPinchClickController | Host owns final coordinate. |
| `CloseAsync` | public | Sends close/kills host, clears pending operations, publishes inactive, resets ActiveSurface to dashboard. | `SendCommandAsync`; reset helpers | CommandRouter/app shutdown | Must restore Merlin UI. |
| `HandleHostOutputLineAsync` | private | Parses host stdout events for bounds, navigation, snapshot, page action, and lifecycle. | JSON deserialize; completion helpers | output drain | Protocol receive loop. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_process` | `Process?` | BrowserHost process. | lifecycle/send | open/close/exit | null after close |
| `_pendingSnapshots` | concurrent dictionary | Snapshot request waiters. | snapshot methods/output | request/output/close | cleared on close |
| `_pendingPageActions` | concurrent dictionary | Page action waiters. | page action methods/output | request/output/close | cleared on close |
| `_latestSnapshot` | `BrowserPageSnapshot?` | Last DOM snapshot. | page actions/readout | snapshot completion/stale marking | null/stale on nav/close |
| `_currentBounds` | `BrowserWorkspaceBounds?` | Browser surface screen bounds/focus state. | motion/frontend | host bounds events/close | null on close |
| `_sync` | `SemaphoreSlim` | Serializes host lifecycle and stdin writes. | public methods | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IBrowserPageSafetyGuard` | Classifies risky page actions. |
| `IConfirmationService` | Stores pending confirmations. |
| `IActiveSurfaceService` | BrowserWorkspace/Dashboard surface updates. |
| BrowserHost process | WebView2 UI, DOM scripts, native pointer overlay/click. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| BrowserHost commands | host stdin | navigation, snapshot, click element, common action, pointer state/click, scroll. |
| `StateChanged` | frontend and browser motion services | Active flag, bounds, reason. |
| ActiveSurface updates | ActiveSurfaceService | browser metadata/capabilities or dashboard reset. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| BrowserHost stdout JSON | BrowserHost | `HandleHostOutputLineAsync` |
| confirmation responses | ConfirmationService/CommandRouter | pending action continuation |

## External Side Effects

Starts/kills BrowserHost, writes stdin, reads stdout/stderr, changes WebView navigation/DOM, updates ActiveSurface, and can cause native click/scroll through host.

## Safety / Guardrails

Keep BrowserWorkspace active/bounds checks strict. DOM/page actions must pass safety/confirmation where applicable; raw pointer actions must remain BrowserHost-scoped.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `CommandRouterTests.cs` | Browser route responses into workspace methods. | Host faked. |
| `BrowserWorkspaceScoringTests.cs` | click/common-action candidate scoring. | No live DOM. |
| `BrowserPageSafetyGuardTests.cs` | safety decisions used here. | No confirmation UI. |
| browser motion tests | pointer/click interactions through interface. | No process/native click. |

## Known Risks / Fragility

Host lifecycle is fragile: stale state after close can leave ActiveSurface in browser mode, while sending overlay commands after host exit can produce warnings. Snapshot-based click scoring can select wrong sidebar/result elements if page snapshot is stale.

## Change Notes for Agents

Read source and linked browser flow notes before editing. Do not mix DOM action safety with raw motion click coordinate ownership without an explicit design.
