---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# PageSnapshotScript

## File

`Merlin.BrowserHost/PageSnapshotScript.cs`

Verified present in current repo.

## Purpose

Builds JavaScript that snapshots the current DOM into visible actionable/readable elements for backend page read/click/search logic.

## Related Features

- [[Browser Page-Aware Control]]
- [[Browser Control]]
- [[Browser Workspace]]

## Main Types / Classes

- `PageSnapshotScript` static script factory.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Create` | public static | Serializes snapshot options and returns an immediately invoked JS script that walks DOM, extracts text/title/label/href/role/rect/visibility, limits results, and returns JSON. | `JsonSerializer.Serialize` | BrowserWorkspaceForm.CapturePageSnapshotAsync | Main page-awareness collector. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `SerializerOptions` | JSON options | Embeds .NET options as JSON for JS. | `Create` | static init | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WebView2 ExecuteScriptAsync | Runs generated JS in current page. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| snapshot JSON | BrowserWorkspaceForm/backend | elements, metadata, URL/title/request id. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| snapshot options | BrowserWorkspaceCommand | `Create` |

## External Side Effects

Executes JavaScript in the page context and reads DOM metadata.

## Safety / Guardrails

These scripts execute only after backend routing/safety chooses the command. Keep script output structured and bounded; do not move assistant intent or safety policy into JavaScript.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| BrowserWorkspaceScoringTests.cs | Backend scoring over snapshot models. | No JS DOM unit test. |

## Known Risks / Fragility

Dynamic pages can mutate during snapshot. Hidden/overlay elements may appear clickable even when visually obstructed.

## Change Notes for Agents

Keep result JSON stable for BrowserWorkspaceService. Update [[Backend BrowserHost Commands]] if command/result fields change.
