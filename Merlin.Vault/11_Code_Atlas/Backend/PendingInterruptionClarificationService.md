---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
  - voice
---

# PendingInterruptionClarificationService

## File

`Merlin.Backend/Services/InterruptionIntelligence/PendingInterruptionClarificationService.cs`

## Purpose

Owns durable pending unclear-interruption clarification records for PR10.4b/PR10.4c/PR10.4e.

This service is the backend owner for "Merlin has asked, or is about to ask, what the user meant by an unclear live interruption, and the next answer should bind to that pending clarification instead of becoming a normal command."

## Main Types

| Type | File | Role |
| --- | --- | --- |
| `PendingInterruptionClarification` | `PendingInterruptionClarification.cs` | Stored pending clarification record. |
| `PendingInterruptionClarificationCreateRequest` | `PendingInterruptionClarification.cs` | Creation request from live interruption handling. |
| `PendingInterruptionClarificationResponse` | `PendingInterruptionClarification.cs` | Consumed user response bound to pending clarification. |
| `IPendingInterruptionClarificationService` | `IPendingInterruptionClarificationService.cs` | Service contract. |
| `PendingInterruptionClarificationService` | `PendingInterruptionClarificationService.cs` | In-memory pending owner with expiry/consume/cancel APIs. |

## State Owned

| State | Meaning | Lifetime |
| --- | --- | --- |
| pending clarification records | Unclear interruption awaiting a response | Until consumed, cancelled, replaced, or expired |
| timeout recovery | Scheduled expiry for pending clarification records | Runs after `PendingInterruptionClarificationTimeoutMs` unless already consumed/cancelled |

Records include turn id, correlation id, capture id, original transcript, normalized transcript, route metadata, hold metadata, created time, expiry time, and the spoken-answer checkpoint context needed for PR10.4e recomposition.

## Main APIs

| Method | Role |
| --- | --- |
| `CreatePending` | Creates or replaces the pending clarification for a turn. |
| `TryGetLatestPending` | Gets latest non-expired pending clarification. |
| `TryGetForTurn` | Gets latest non-expired pending clarification for a turn. |
| `HasActivePendingForTurn` | Checks whether a turn has active pending clarification state. |
| `TryConsumeResponse` | Consumes the latest pending clarification with a user response. |
| `CancelForTurn` | Cancels pending records for a turn. |
| `ExpireDue` | Expires overdue records and clears awaiting UI/backend state when a broadcaster is available. |

## Integration Points

| Caller | Behavior |
| --- | --- |
| `LiveInterruptionIntegrationService` | Creates pending owner when opt-in PR10.4b pending clarification is enabled for non-short `AskUserToClarifyInterruption`; owns PR10.4e response recomposition after BargeIn consumes a response. |
| `BargeInCoordinator` | Consumes pending clarification responses before awake gate/live gate/normal backend voice request routing, emits response-handling state, and delegates to the live owner. |
| DI in `Program.cs` | Registers the service as singleton. |
| `AssistantUiStateBroadcaster` | Optional dependency used to clear `awaiting_interruption_clarification` to `none` on timeout/cancel. |

## Config

`InterruptionHandlingOptions`:

- `EnablePendingInterruptionClarification`
- `PendingInterruptionClarificationTimeoutMs`

## Non-Goals

- Does not implement stale handling watchdog.
- Does not generate clarification prompts.
- Does not itself recompose or continue the interrupted answer; `LiveInterruptionIntegrationService` owns that.

## Tests

| Test File | Coverage |
| --- | --- |
| `PendingInterruptionClarificationServiceTests.cs` | create, consume, expiry, cancel, timeout recovery, default timeout. |
| `ConversationalInterruptionLiveIntegrationTests.cs` | opt-in AskClarification creates pending owner and awaiting state; short-fragment fallback remains intact; pending responses can generate recomposed continuation. |
| `BargeInTests.cs` | pending clarification response bypasses normal backend voice request routing, transitions to handling, and delegates to the live owner. |

## Known Gaps

PR10.4d stale handling watchdog is implemented in `BargeInCoordinator`. Known adjacent BargeIn idle-capture failures remain outside the pending owner.

## Related Notes

- [[AskClarification PR10.4 Prerequisite Investigation]]
- [[AskClarification Live Dead-End Recovery Plan]]
- [[LiveInterruptionIntegrationService]]
- [[Live Utterance Flow]]
