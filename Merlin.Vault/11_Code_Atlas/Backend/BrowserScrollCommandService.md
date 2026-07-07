---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserScrollCommandService

## File

`Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserScrollCommandService.cs`

Verified present in current repo.

## Purpose

Rate-limits and scales vertical pinch movement into BrowserWorkspace pixel scroll commands.

## Related Features

- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]
- [[Browser Pointer Overlay]]
- [[Browser Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserScrollCommandService` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| constructor | public | Configures workspace dependency, pixel scale, and minimum interval. | assignments | DI/tests | Defaults tune scroll feel. |
| `TrySendAsync` | public | Converts overlay delta to pixel scroll, skips tiny/no-op deltas, enforces interval, sends `ScrollByPixelsAsync`. | workspace service | BrowserPinchClickController | Returns whether scroll was sent. |
| `Reset` | public | Clears last send time. | field reset | controller reset | Allows immediate new gesture scroll. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_lastSentUtc` | `DateTimeOffset?` | Last emitted scroll time. | `TrySendAsync` | `TrySendAsync`, `Reset` | null after reset |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IBrowserWorkspaceService` | Sends scoped `browser_scroll_by_pixels`. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `browser_scroll_by_pixels` | BrowserHost | Pixel scroll delta. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| scroll delta request | BrowserPinchClickController | `TrySendAsync` |

## External Side Effects

Sends WebView scroll commands through BrowserWorkspaceService.

## Safety / Guardrails

Raw browser motion actions are not the same as page-aware DOM actions. Keep BrowserWorkspace active, bounds, focus, and confidence checks conservative.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserPinchClickControllerTests.cs` | Indirect scroll hold behavior. | No standalone timing stress test. |

## Known Risks / Fragility

High scale makes scroll uncontrollable; strict rate limiting makes it feel laggy.

## Change Notes for Agents

Read source and linked browser motion/safety flow notes before editing. Keep pure decision logic separate from BrowserHost I/O.
