---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# SearchFieldScript

## File

`Merlin.BrowserHost/SearchFieldScript.cs`

Verified present in current repo.

## Purpose

Builds JavaScript that finds a search field, fills a query, dispatches input/change events, and submits via Enter/form submit.

## Related Features

- [[Browser Page-Aware Control]]
- [[Browser Control]]
- [[Browser Workspace]]

## Main Types / Classes

- `SearchFieldScript` static script factory.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Create` | public static | Embeds query and optional preferred element id, selects search-like input/contenteditable, fills it, dispatches events, submits, and returns result JSON. | `JsonSerializer.Serialize` | BrowserWorkspaceForm.FillSearchAndSubmitAsync | Page search-field executor. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `SerializerOptions` | JSON options | Embeds query/id. | `Create` | static init | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WebView2 ExecuteScriptAsync | Runs script in page. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| page action result JSON | BrowserWorkspaceForm/backend | success/error and selected element. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| search_field command | BrowserWorkspaceForm | `Create` |

## External Side Effects

Mutates page input and may submit/navigate.

## Safety / Guardrails

These scripts execute only after backend routing/safety chooses the command. Keep script output structured and bounded; do not move assistant intent or safety policy into JavaScript.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| CommandRouterTests.cs | Backend route/search behavior. | No live DOM field test. |

## Known Risks / Fragility

Search fields vary widely; contenteditable and shadow DOM cases can fail. Filling the wrong field is a UX risk, so backend safety/selection matters.

## Change Notes for Agents

Keep result JSON stable for BrowserWorkspaceService. Update [[Backend BrowserHost Commands]] if command/result fields change.
