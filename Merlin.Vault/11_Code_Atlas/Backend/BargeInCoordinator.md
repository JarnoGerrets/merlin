---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
  - voice
---

# BargeInCoordinator

## File

`Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs`

## Purpose

Coordinates microphone-triggered capture, STT, live utterance routing, interruption handling, and backend voice request emission.

## PR10.4c / PR10.4d / PR10.4e Responsibilities

| Area | Responsibility |
| --- | --- |
| Pending clarification consume seam | Checks `IPendingInterruptionClarificationService.TryConsumeResponse` before awake gate/live gate/normal backend voice routing. |
| Awaiting response transition | Emits `interruptionState=handling` with reason `pending_interruption_clarification_response_captured` when a pending clarification answer is consumed. |
| Routing boundary | Hands consumed pending responses to `LiveInterruptionIntegrationService` and only falls back to routed-event behavior if no owner handles the response. |
| Stale handling watchdog | Observes emitted `interruptionState=handling` state and clears it to `none` only when no active owner remains past the configured timeout. |

## Important Methods

| Method | Role |
| --- | --- |
| `HandleTriggeredSpeechAsync` | Captures speech, runs STT, consumes pending clarification responses before normal routing, and routes utterances. |
| `TryConsumePendingInterruptionClarificationAsync` | PR10.4b/10.4c consume hook for pending clarification answers. |
| `EmitAssistantUiStateImmediateAsync` | Emits immediate assistant UI/backend state events through `AssistantUiStateBroadcaster`. |
| `ObserveInterruptionHandlingState` | PR10.4d hook that starts or clears the stale handling watchdog based on emitted interruption state. |
| `TryRecoverStaleInterruptionHandlingAsync` | Clears ownerless stale `handling` state or defers when capture, pending clarification, held playback, or interruption-owned speech is active. |

## Config

`BargeInOptions`:

- `EnableInterruptionHandlingWatchdog`
- `InterruptionHandlingWatchdogTimeoutMs`

## Non-Goals

- Does not own pending clarification storage.
- Does not own pending timeout recovery.
- Does not implement full AskClarification recomposition.
- Does not own full AskClarification recomposition; it delegates consumed pending responses to `LiveInterruptionIntegrationService`.

## Tests

| Test File | Coverage |
| --- | --- |
| `Merlin.Backend.Tests/BargeInTests.cs` | Pending clarification response bypasses normal backend request routing, delegates to the live owner, transitions from awaiting to handling, and stale handling watchdog cleanup/non-cleanup behavior. |

## Known Gaps

- Existing BargeIn idle-capture tests still fail independently of PR10.4c/PR10.4d.
- Full AskClarification recomposition ownership is implemented in `LiveInterruptionIntegrationService`.

## Related Notes

- [[PendingInterruptionClarificationService]]
- [[LiveInterruptionIntegrationService]]
- [[AskClarification Live Dead-End Recovery Plan]]
