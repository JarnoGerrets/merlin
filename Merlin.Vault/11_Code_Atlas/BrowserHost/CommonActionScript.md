---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# CommonActionScript

## File

`Merlin.BrowserHost/CommonActionScript.cs`

Verified present in current repo.

## Purpose

Builds JavaScript for common media/browser controls such as pause, play, stop, fullscreen, exit fullscreen, and skip ad.

## Related Features

- [[Browser Page-Aware Control]]
- [[Browser Control]]
- [[Browser Workspace]]

## Main Types / Classes

- `CommonActionScript` static script factory.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Create` | public static | Embeds action name, searches selectors/labels for known controls, dispatches pointer/mouse/click events, returns result JSON. | `JsonSerializer.Serialize` | BrowserWorkspaceForm.PerformCommonActionAsync | Used by common-action route. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `SerializerOptions` | JSON options | Safely embeds action string. | `Create` | static init | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WebView2 ExecuteScriptAsync | Runs common action script. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| page action result JSON | BrowserWorkspaceForm/backend | success/error, element id, action. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| common action command | BrowserWorkspaceForm | `Create` |

## External Side Effects

Clicks DOM controls and can affect media playback/fullscreen/navigation.

## Safety / Guardrails

These scripts execute only after backend routing/safety chooses the command. Keep script output structured and bounded; do not move assistant intent or safety policy into JavaScript.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| BrowserWorkspaceScoringTests.cs | Backend candidate fallback/scoring. | No live YouTube/WebView test. |
| CommandRouterTests.cs | Common action route responses. | Host script not executed. |

## Known Risks / Fragility

Selector/label matching is site-sensitive. YouTube localization (`Pauzeren`, `Overslaan`) and dynamic controls can break generic matching.

## Change Notes for Agents

Keep result JSON stable for BrowserWorkspaceService. Update [[Backend BrowserHost Commands]] if command/result fields change.
