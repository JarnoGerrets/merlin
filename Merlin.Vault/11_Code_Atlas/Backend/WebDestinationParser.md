---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# WebDestinationParser

## File

`Merlin.Backend/Services/Web/WebDestinationParser.cs`

Verified present in current repo.

## Purpose

Parses spoken browser destination/control phrases into `WebDestinationCommand` values before CommandRouter executes BrowserWorkspace navigation or common actions.

## Related Features

- [[Vision Sidecar]]
- [[Motion Control]]
- [[Motion Control Profile Layer]]
- [[Browser Control]]

## Main Types / Classes

WebDestinationParser and its directly referenced protocol/model types.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `TryParse` | public | Normalizes input, matches browser navigation/control phrases, destination names, URLs, searches, and common media actions. | phrase tables; destination helpers; media normalizer | `CommandRouter.HandleWebDestinationCommandAsync` | Broad browser request parser. |
| destination helpers | private | Convert spoken names/queries into URLs or search URLs. | string parsing | `TryParse` | Handles named sites and web search. |
| common-action parsing | private/static | Maps pause/play/skip/fullscreen phrases into common action keys. | `BrowserMediaCommandNormalizer` | `TryParse` | Overlaps with browser media normalizer. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| phrase tables | arrays/dictionaries | Browser open/close/back/forward/search/common-action aliases. | parser | static initialization | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `BrowserMediaCommandNormalizer` | Reuses active-surface media parsing concepts. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `WebDestinationCommand` | CommandRouter | Action, URL/query/common action and reason. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| spoken text | CommandRouter | `TryParse` |

## External Side Effects

No side effects; execution occurs in CommandRouter and BrowserWorkspaceService.

## Safety / Guardrails

Keep lifecycle ownership centralized and preserve existing guards. This component is part of live camera/browser routing and should fail closed rather than emit stale actions.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `CommandRouterTests.cs` | Destination/browser route behavior. | Parser mostly tested indirectly. |
| `BrowserMediaCommandNormalizerTests.cs` | Shared media phrase behavior. | Not every destination phrase. |

## Known Risks / Fragility

Phrase overlap is high: `go forward` can mean browser history, video seek, or ordinary language. Keep site-specific commands out of this parser unless delegated to profiles.

## Change Notes for Agents

Read source and linked flow notes before editing. Do not move safety or process ownership into lower-level helpers.
