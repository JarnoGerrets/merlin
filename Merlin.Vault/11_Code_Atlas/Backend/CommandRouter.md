---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# CommandRouter

## File

`Merlin.Backend/Services/CommandRouter.cs`

Verified present in current repo.

## Purpose

Main backend command dispatcher. It normalizes user requests, starts acknowledgement/progress speech, routes browser/motion/memory/tool requests, coordinates safety/confirmation responses, and formats final assistant responses.

## Related Features

- [[Browser Control]]
- [[Motion Control]]
- [[Voice Command Flow]]
- [[Command Routing Architecture]]
- [[Safety and Confirmation]]
- [[Memory System]]

## Main Types / Classes

- `CommandRouter`
- private `BrowserWorkspaceCommandMatcher`
- uses `AssistantRequest`, `AssistantResponse`, `IntentParseResult`, `WebDestinationCommand`, motion and browser services.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `RouteAsync(string)` | public | Wraps raw text into `AssistantRequest` and delegates to request overload. | `RouteAsync(AssistantRequest)` | WebSocketHandler/tests | Simple entry point. |
| `RouteAsync(AssistantRequest)` | public | Normalizes speech, logs command, decides acknowledgement, evaluates browser/motion/UI-control/memory/tool paths, starts/ends progress speech, and polishes response. | speech normalizer; web parser; active surface; motion service; browser service; tool registry | WebSocketHandler, live utterance routes, tests | Central branch point. |
| motion command handling | internal in route | Handles `eyes open`, `open your eyes`, `start ui control`, `start browser pointer`, `eyes closed`, and related compatibility phrases. | `IMotionControlModeService.EnableAsync/DisableAsync`; `UiControlModeController`; `IVisionSidecarHost` legacy paths | voice/text commands | Motion service is preferred sidecar owner. |
| `HandleWebDestinationCommandAsync` | private | Opens/navigates BrowserWorkspace or executes web destination/common browser commands from parser output. | `IWebDestinationParser`; `IBrowserWorkspaceService` | `RouteAsync` | Keeps URLs inside workspace when configured/active. |
| `HandleBrowserWorkspaceCommandAsync` | private | Executes browser open/close/back/forward/refresh/scroll/zoom/search/page read/page click/common media/pointer commands. | BrowserWorkspaceService; BrowserMediaCommandNormalizer | `RouteAsync` | Applies browser-not-open response and media confirmations. |
| `TryMatchBrowserWorkspaceCommand` | private static | Matches direct browser commands such as open/close browser and start pointer. | `BrowserWorkspaceCommandMatcher` | browser handler | Handles polite wrappers and suffixes. |
| `PolishAsync` | private | Applies response polishing when appropriate. | response polisher | route branches | Skipped/suppressed for some command confirmations. |
| acknowledgement/progress helpers | private | Start immediate acknowledgement/responsive feedback/progress speech. | feedback services; speech service | `RouteAsync` | Protects perceived latency. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| service dependencies | many readonly fields | Router collaborators for tools, speech, feedback, browser, motion, active surface, memory. | route helpers | constructor | service lifetime |
| current turn state fields | private values | Last tool/intent/correlation context for progress/feedback/correction. | route helpers | `SetTurnState` | per request |
| `_speechCommandNormalizer` | `SpeechCommandNormalizer` | Cleans voice command text. | `RouteAsync` | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IWebDestinationParser` | Browser destination/search/common action parsing. |
| `IBrowserWorkspaceService` | BrowserWorkspace lifecycle/actions/page control/motion pointer. |
| `IActiveSurfaceService` | Current surface for media and motion context. |
| `IMotionControlModeService` | Profile-based motion enable/disable/dispatch. |
| `UiControlModeController` / `IVisionSidecarHost` | Legacy UI-control compatibility and calibration paths. |
| `ToolRegistry` / intent parser | Non-browser tool routing. |
| speech/feedback services | Acknowledgement, progress, responsive feedback, TTS suppression decisions. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `AssistantResponse` | WebSocketHandler/frontend | Final command/tool/browser response and metadata. |
| browser/motion commands | BrowserWorkspaceService / MotionControlModeService | Side effects selected by route branch. |
| acknowledgement/progress speech | speech playback service | Short spoken feedback before long work. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `AssistantRequest` | WebSocketHandler/live utterance path | `RouteAsync` |
| active surface snapshot | request or ActiveSurfaceService | browser/motion/media matching |
| pending confirmation state | confirmation service/tool gates | safety branch handling |

## External Side Effects

Can open/close BrowserHost, start/stop camera tracking through motion mode, execute tools, queue TTS, update runtime state, and emit frontend responses.

## Safety / Guardrails

Do not bypass BrowserPageSafetyGuard or ConfirmationService from this router. ActiveSurface chooses context; lower safety services decide whether a page action may execute. Motion sidecar ownership should stay in MotionControlModeService except legacy compatibility paths.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `CommandRouterTests.cs` | Browser commands, media actions, motion/UI control, tool routing, confirmations, page read/click responses. | Full live voice/STT path covered elsewhere. |
| `AcknowledgementIntegrationTests.cs` | Acknowledgement interactions. | No browser host process. |
| `WebSocketHandlerTests.cs` | End-to-end WebSocket route plumbing. | Uses fake services. |

## Known Risks / Fragility

This file is broad and phrase-heavy. Adding site-specific commands here can clutter generic browser control. Response suppression/acknowledgement changes can break natural voice timing.

## Change Notes for Agents

This is a central live path. Read the source plus linked flow/state notes before changing behavior, then run targeted and full backend tests.
