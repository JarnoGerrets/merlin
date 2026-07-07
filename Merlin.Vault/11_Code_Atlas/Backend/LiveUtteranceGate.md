---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# LiveUtteranceGate

## File

`Merlin.Backend/Services/LiveUtterance/LiveUtteranceGate.cs`

Verified present in current repo.

## Purpose

Decision gate for live utterances captured while Merlin is speaking or listening. It decides whether speech is a new request, correction, assistant playback control, clarification, garbage/side comment, or should be held.

## Related Features

- [[Voice Interruption System]]
- [[Correction Layer]]
- [[Responsive Feedback]]
- [[Browser Control]]
- [[Active Surface Layer]]

## Main Types / Classes

- `LiveUtteranceGate` implements `ILiveUtteranceGate`.
- Uses `LiveUtteranceGateInput`, `LiveUtteranceGateResult`, `UtteranceRouteDecision`, and internal analysis records.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Evaluate` | public | Normalizes transcript, calls `EvaluateCore`, logs decision. | `Normalize`; `EvaluateCore`; `LogDecision` | BargeInCoordinator/tests | Pure decision stage. |
| `ToRouteDecision` | public | Converts gate result into routing action for barge-in/live utterance coordinator. | `Decision` helper | BargeInCoordinator | Maps Accept/Correction/Clarification/control to routes. |
| `EvaluateCore` | private | Applies pending confirmation handling, explicit playback controls, browser commands/media matching, correction/replacement logic, incomplete/garbage/side-comment checks, active-flow strictness, and request confidence scoring. | many helpers; `IActiveSurfaceService`; `IBrowserMediaCommandNormalizer` | `Evaluate` | Central policy. |
| pending fragment helpers | private | Combine short fragments within hold windows. | `_pendingFragments` | `EvaluateCore` | Reduces false negatives on split STT. |
| correction helpers | private static | Extract `actually`, `i meant`, repeated-no, and replacement text. | string matching | `EvaluateCore` | Drives `AcceptCorrection`. |
| browser wrapper helpers | private static | Strip `please`, `Merlin`, and trailing control wrappers before matching browser commands. | phrase arrays | `EvaluateCore` | Keeps polite browser commands routeable. |
| general request analysis | private static | Detects structured questions/commands, malformed text, fillers, garbage, and incomplete phrases. | word/phrase sets | `EvaluateCore` | Produces positive/negative signals. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_pendingFragments` | concurrent dictionary | Partial utterances awaiting continuation. | EvaluateCore | fragment helpers | expires by time |
| phrase arrays/sets | static collections | Playback controls, browser commands, corrections, fillers, garbage, question/verb markers. | EvaluateCore/helpers | static initialization | process lifetime |
| `_confirmationService` | optional dependency | Used to accept short confirmation replies. | EvaluateCore | constructor | service lifetime |
| `_activeSurfaceService` | optional dependency | Supplies current surface for browser media matching. | EvaluateCore | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IBrowserMediaCommandNormalizer` | Active-surface browser media command matching. |
| `IActiveSurfaceService` | Current surface/capabilities for ambiguous browser media controls. |
| `IConfirmationService` | Detects pending confirmation responses such as `i confirm`. |
| `ILogger<LiveUtteranceGate>` | Decision diagnostics. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `LiveUtteranceGateResult` | BargeInCoordinator/WebSocket route path | Decision kind, confidence, reason, signals, route flags, replacement text. |
| logs `LiveUtteranceGateEvaluated` | diagnostics | Full normalized/stripped text and decision details. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `LiveUtteranceGateInput` | BargeInCoordinator/live STT | `Evaluate` |
| active surface snapshot | ActiveSurfaceService/request context | browser/media matching |
| pending confirmation state | ConfirmationService | confirmation acceptance path |

## External Side Effects

No direct UI/tool side effects. It only returns decisions; BargeInCoordinator and WebSocketHandler perform pause/route/resume actions.

## Safety / Guardrails

This gate deliberately should not execute tools or browser actions. It decides route permission and should remain conservative during assistant speech, especially for garbage, side comments, and malformed STT.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `LiveUtteranceGateTests.cs` | Decision kinds, correction extraction, playback controls, browser/media active-surface matching, confirmation responses, garbage/side comments. | No live microphone audio. |
| `BargeInTests.cs` / live integration tests | Interaction with playback/resume routes. | Timing-sensitive. |

## Known Risks / Fragility

Over-strict matching makes Merlin ignore valid responses; over-permissive matching interrupts speech or opens tools from noise. Browser/media phrases must stay context-aware.

## Change Notes for Agents

This is a central live path. Read the source plus linked flow/state notes before changing behavior, then run targeted and full backend tests.
