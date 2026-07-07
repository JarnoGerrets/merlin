---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# NativeBrowserInputService

## File

`Merlin.BrowserHost/NativeBrowserInputService.cs`

Verified present in current repo.

## Purpose

Tiny Win32 input wrapper used by BrowserHost to perform a left click at a screen coordinate selected by the native pointer overlay.

## Related Features

- [[Browser Workspace]]
- [[Browser Control]]
- [[Browser Pointer Overlay]]
- [[Browser Page-Aware Control]]

## Main Types / Classes

- `NativeBrowserInputService` and related command/script models in BrowserHost.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `LeftClick` | public static | Calls `SetCursorPos`, then sends left-down and left-up with `SendInput`. | P/Invoke | BrowserWorkspaceForm.FireBrowserPointerClick | Throws `Win32Exception` on failure. |
| `MouseInput` | private static | Creates Win32 input structs. | struct constructors | `LeftClick` | Internal helper. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| no mutable state | n/a | Stateless P/Invoke helper. | n/a | n/a | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `SetCursorPos` | Moves OS cursor before click. |
| `SendInput` | Emits mouse down/up. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| native mouse click | Windows input subsystem | Left click at screen coordinate. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| screen coordinate | BrowserWorkspaceForm | `LeftClick` |

## External Side Effects

Moves the system cursor and emits native mouse input.

## Safety / Guardrails

BrowserHost should stay a scoped browser executor. It should not decide assistant intent or safety policy; backend services decide that before sending commands.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| BrowserPinchClickControllerTests.cs | Backend decides when to request click. | No P/Invoke test. |

## Known Risks / Fragility

This affects the real desktop. It must remain scoped to BrowserHost-owned click points, not arbitrary backend coordinates.

## Change Notes for Agents

Keep BrowserHost command JSON compatible with `BrowserWorkspaceService`. If a payload shape changes, update [[Backend BrowserHost Commands]] and backend tests/atlas notes together.
