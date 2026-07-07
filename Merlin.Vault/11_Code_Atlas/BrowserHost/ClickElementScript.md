---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# ClickElementScript

## File

`Merlin.BrowserHost/ClickElementScript.cs`

Verified present in current repo.

## Purpose

Builds JavaScript to locate an element by Merlin element id/snapshot expectations and dispatch a realistic click sequence in the page.

## Related Features

- [[Browser Page-Aware Control]]
- [[Browser Control]]
- [[Browser Workspace]]

## Main Types / Classes

- `ClickElementScript` static script factory.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Create` | public static | Embeds target id, snapshot id, expected text/href, finds matching DOM element, validates expectations when present, scrolls/focuses, dispatches pointer/mouse/click events, returns result JSON. | `JsonSerializer.Serialize` | BrowserWorkspaceForm.ClickElementAsync | DOM click executor. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `SerializerOptions` | JSON options | Safely embeds command data into script. | `Create` | static init | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WebView2 ExecuteScriptAsync | Runs DOM click script. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| page action result JSON | BrowserWorkspaceForm/backend | success, element id/text/href/error. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| click element command | BrowserWorkspaceForm | `Create` |

## External Side Effects

Executes DOM events and can navigate/change page state.

## Safety / Guardrails

These scripts execute only after backend routing/safety chooses the command. Keep script output structured and bounded; do not move assistant intent or safety policy into JavaScript.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| CommandRouterTests/BrowserWorkspaceScoringTests | Backend candidate/safety behavior before this script. | No live DOM click test. |

## Known Risks / Fragility

If expected text/href checks are too weak, stale snapshots can click the wrong element. If too strict, dynamic pages fail valid clicks.

## Change Notes for Agents

Keep result JSON stable for BrowserWorkspaceService. Update [[Backend BrowserHost Commands]] if command/result fields change.
