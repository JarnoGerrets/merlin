---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# BrowserWorkspaceForm

## File

`Merlin.BrowserHost/BrowserWorkspaceForm.cs`

Verified present in current repo.

## Purpose

WinForms/WebView2 host window for BrowserWorkspace. It reads JSON commands from stdin, drives WebView2, manages the native pointer overlay, and writes host events/results to stdout.

## Related Features

- [[Browser Workspace]]
- [[Browser Control]]
- [[Browser Pointer Overlay]]
- [[Browser Page-Aware Control]]

## Main Types / Classes

- `BrowserWorkspaceForm` and related command/script models in BrowserHost.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `OnLoad` | protected override | Initializes WebView2, starts stdin command loop, navigates initial URL, flushes queued commands. | `InitializeWebViewAsync`; `ReadCommandsAsync` | WinForms lifecycle | Startup can timeout. |
| `ReadCommandsAsync` | private | Reads stdin lines, deserializes `BrowserWorkspaceCommand`, dispatches on UI thread. | JSON deserialize; `HandleCommand` | background task | Backend command receive loop. |
| `HandleCommand` | private | Switches command types: navigate/back/forward/refresh/scroll/zoom/search/common action/page snapshot/click element/pointer state/pointer click/close. | command handlers | stdin loop | Protocol dispatcher. |
| `ApplyBrowserPointerState` | private | Applies backend render state to native overlay using current browser surface bounds. | `_pointerOverlay.ApplyState` | `browser_pointer_state` command | Draws cursor overlay. |
| `FireBrowserPointerClick` | private | Asks overlay for current screen click point and calls native input click. | `TryGetCurrentScreenClickPoint`; `NativeBrowserInputService.LeftClick` | `browser_pointer_click` command | Host owns final coordinate. |
| `PerformCommonActionAsync` | private | Executes `CommonActionScript` in WebView and writes page action result. | `ExecuteScriptAsync`; `WritePageActionResult` | common_action command | Pause/play/fullscreen/skip-ad path. |
| `ClickElementAsync` / `FillSearchAndSubmitAsync` | private | Execute JS scripts for page-aware click/search and write result. | ClickElementScript/SearchFieldScript | backend page actions | DOM-scoped. |
| `CapturePageSnapshotAsync` | private | Executes PageSnapshotScript and writes snapshot/result event. | WebView2 script | page_snapshot command | Backend awaits request id. |
| `ReportBoundsChanged` | private | Writes BrowserWorkspace bounds/focus/active state to stdout. | `GetBrowserSurfaceScreenBounds`; `WriteHostEvent` | move/resize/show/focus/nav events | Drives backend pointer mapping. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_webView` | `WebView2` | Browser surface. | handlers | constructor/WinForms | form lifetime |
| `_pendingCommands` | queue | Commands received before WebView ready. | queue/flush | stdin loop | emptied after init |
| `_pointerOverlay` | `NativeBrowserPointerOverlayWindow` | Transparent pointer overlay. | pointer handlers | constructor | form lifetime |
| `_closed` | `CancellationTokenSource` | Stops stdin loop on close. | command loop | close/dispose | canceled on form close |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WebView2 | Browser rendering and JavaScript execution. |
| script classes | DOM snapshot/click/search/common action scripts. |
| NativeBrowserPointerOverlayWindow | Visual pointer overlay and click coordinate. |
| NativeBrowserInputService | Native mouse click. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `browser_workspace_bounds` and navigation/log events | backend stdout reader | active/focused bounds, URL/title. |
| `page_snapshot_result` | backend | Snapshot JSON or error by request id. |
| `page_action_result` | backend | DOM/common-action success/error data. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| backend JSON commands | stdin | `ReadCommandsAsync`/`HandleCommand` |
| WebView navigation/focus events | WebView2/WinForms | event handlers |

## External Side Effects

Creates a visible WinForms window, embeds WebView2, executes JavaScript, draws native overlay, moves OS cursor/clicks for pointer click.

## Safety / Guardrails

BrowserHost should stay a scoped browser executor. It should not decide assistant intent or safety policy; backend services decide that before sending commands.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| Backend tests | BrowserWorkspaceService protocol and scoring with fake host. | No BrowserHost unit/E2E tests in repo. |

## Known Risks / Fragility

WebView2 initialization/focus timing can make the window black/unresponsive. Pointer overlay transparency/focus styles are platform-sensitive.

## Change Notes for Agents

Keep BrowserHost command JSON compatible with `BrowserWorkspaceService`. If a payload shape changes, update [[Backend BrowserHost Commands]] and backend tests/atlas notes together.
