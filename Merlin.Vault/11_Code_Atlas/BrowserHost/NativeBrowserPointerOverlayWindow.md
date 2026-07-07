---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# NativeBrowserPointerOverlayWindow

## File

`Merlin.BrowserHost/NativeBrowserPointerOverlayWindow.cs`

Verified present in current repo.

## Purpose

Transparent always-on-top overlay window that renders the browser motion pointer, click visual states, and exposes the current screen click coordinate.

## Related Features

- [[Browser Workspace]]
- [[Browser Control]]
- [[Browser Pointer Overlay]]
- [[Browser Page-Aware Control]]

## Main Types / Classes

- `NativeBrowserPointerOverlayWindow` and related command/script models in BrowserHost.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| constructor | public | Configures borderless transparent topmost tool window and paint styles. | WinForms properties | BrowserWorkspaceForm | Transparency key is Windows-specific. |
| `ApplyState` | public | Positions overlay over browser bounds, stores state, shows/invalidates overlay or hides when inactive. | WinForms bounds/show/invalidate | BrowserWorkspaceForm pointer state handler | Render path. |
| `HideOverlay` | public | Hides overlay and resets state. | WinForms hide | workspace close/disable | Safe cleanup. |
| `TryGetCurrentScreenClickPoint` | public | Converts overlay-local pointer state to screen point. | stored bounds/state | BrowserWorkspaceForm.FireBrowserPointerClick | Host owns final click location. |
| `WndProc` | protected override | Makes overlay transparent to mouse activation/clicks. | Win32 message handling | OS | Prevents focus stealing. |
| `OnPaint` | protected override | Draws rings/chevrons/colors based on visual state. | GDI+ drawing helpers | WinForms paint | Visual feedback. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_state` | `BrowserPointerOverlayState` | Latest render info. | paint/click point | `ApplyState` | inactive on hide |
| `_browserBounds` | `Rectangle` | Screen bounds of browser content. | click point/position | `ApplyState` | updated per bounds event |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WinForms/GDI+ | Native transparent drawing. |
| BrowserPointerOverlayState | Backend render payload. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| painted overlay | user screen | cursor/rings/scroll/click state. |
| click point | BrowserWorkspaceForm | screen coordinate for native input. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| browser_pointer_state command | BrowserWorkspaceForm | `ApplyState` |

## External Side Effects

Creates a topmost transparent WinForms window and draws with GDI+.

## Safety / Guardrails

BrowserHost should stay a scoped browser executor. It should not decide assistant intent or safety policy; backend services decide that before sending commands.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| Browser motion tests | Indirect payload behavior. | No native drawing test. |

## Known Risks / Fragility

Transparency/focus behavior depends on Windows styles. Incorrect bounds create visible/click offset.

## Change Notes for Agents

Keep BrowserHost command JSON compatible with `BrowserWorkspaceService`. If a payload shape changes, update [[Backend BrowserHost Commands]] and backend tests/atlas notes together.
