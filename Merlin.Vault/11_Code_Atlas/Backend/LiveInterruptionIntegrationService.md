---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
  - voice
---

# LiveInterruptionIntegrationService

## File

`Merlin.Backend/Services/InterruptionIntelligence/LiveInterruptionIntegrationService.cs`

## Purpose

Converts yielded live utterances into executable live interruption outcomes. It bridges classifier decisions, playback control, provisional audio holds, spoken-answer checkpoints, sequential clarification/recomposition, and terminal fallback behavior.

## Important Responsibilities

| Area | Responsibility |
| --- | --- |
| Context validation | Ignores or releases normal requests when no active/recent speech context exists. |
| Stop playback | Flushes holds/final speech and speaks local stop confirmation. |
| Correction redirect | Cancels current playback and routes rewritten user requests when enabled. |
| Sequential clarification/recomposition | Uses spoken-answer checkpoints, model clarification, and continuation speech when all PR10 prerequisites are enabled. |
| Pending clarification owner | Creates pending clarification records for non-short AskClarification when the pending owner option is enabled. |
| Pending clarification response owner | Consumes bound pending responses, reconstructs the stored checkpoint, generates a recomposed continuation, and clears state. |
| Awaiting clarification state | Emits `awaiting_interruption_clarification` after pending owner creation. |
| Terminal fallback | Resolves unsupported or ownerless live decisions by resuming held playback and suppressing legacy semantic routing. |

## AskClarification Behavior

`AskUserToClarifyInterruption` is no longer allowed to fall through the stale PR7 deferred branch in live mode. Current PR 10.4a behavior:

- Short fragments such as `in the pool` become handled fallback outcomes.
- The provisional audio hold is resumed when a hold id exists.
- Legacy semantic routing is suppressed so the old path cannot wedge the UI state.
- If full sequential recomposition prerequisites are available and the utterance is not a short fragment, the service may map to the sequential recomposition path.

This is not the full PR10 pending clarification system. It is a safe terminal fallback for ownerless live clarification decisions.

PR10.4b through PR10.4e add the pending owner path:

- `EnablePendingInterruptionClarification` allows non-short `AskUserToClarifyInterruption` to create a `PendingInterruptionClarification`.
- Pending owner creation emits `awaiting_interruption_clarification`.
- Consumed pending responses are handed back to this service for continuation generation.
- The response path uses the stored spoken-answer checkpoint context instead of running a second clarification prompt model call.
- Failed or incomplete response ownership clears state and suppresses generic routing.
- Short fragments such as `in the pool` continue to use terminal fallback.

## Important Methods

| Method | Role |
| --- | --- |
| `TryHandleYieldedInterruptionAsync` | Main live interruption entry point. |
| `HandleAskUserToClarifyInterruptionAsync` | Resolves AskClarification without the old PR7 dead end. |
| `HandlePendingAskClarificationOwnerAsync` | Creates pending clarification owner when PR10.4b/10.4c is enabled. |
| `TryHandlePendingClarificationResponseAsync` | PR10.4e owner for consumed pending clarification answers and recomposed continuation generation. |
| `EmitAwaitingClarificationAsync` | Emits `awaiting_interruption_clarification` after pending owner creation. |
| `HandleTerminalFallbackAsync` | Safe resume/cleanup terminal outcome for ownerless live strategies. |
| `HandleSequentialClarificationRecompositionAsync` | Full clarification/recomposition path when prerequisites exist. |
| `ResolveProvisionalAudioHoldAsync` | Resume/flush provisional holds and log hold resolution. |

## Tests

| Test File | Coverage |
| --- | --- |
| `Merlin.Backend.Tests/ConversationalInterruptionLiveIntegrationTests.cs` | Live outcome behavior, playback hold resolution, sequential recomposition, and AskClarification regression. |

## Known Gaps

- PR10.4d stale `InterruptionState=handling` watchdog is implemented in `BargeInCoordinator`, not this service.
- Known adjacent BargeIn idle-capture failures remain outside this service.

## Related Notes

- [[AskClarification Live Dead-End]]
- [[AskClarification Live Dead-End Recovery Plan]]
- [[Voice Interruption System]]
- [[AssistantSpeechPlaybackService]]
