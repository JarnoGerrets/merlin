---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
  - layer/backend
---

# Voice Interruption System

## Summary

Barge-in, live utterance gate, interruption handling, and playback control.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- LiveUtteranceGate.cs exists.
- BargeInCoordinator and InterruptionIntelligence services exist.
- LiveUtteranceGateTests.cs and BargeInTests.cs exist but full suite currently has failing BargeIn tests.

## What Exists Today

System listens during playback, classifies interruption/correction/commands, and coordinates playback stop/pause/resume.

## Current Behavior

Works in many flows but current full tests show timing fragility.

Live `AskUserToClarifyInterruption` no longer uses the stale PR7 deferred branch. PR 10.4a ownerless unclear live outcomes now resolve to a terminal fallback that resumes provisional playback when possible and suppresses legacy semantic routing.

PR10.4b adds a durable pending unclear-interruption clarification owner. When enabled, non-short live AskClarification outcomes can create a pending owner, and BargeIn consumes the pending response before normal backend voice routing.

PR10.4c adds explicit `awaiting_interruption_clarification` UI/backend state and timeout recovery. Pending owner creation emits awaiting state, pending response consumption returns to `handling`, and expiry/cancellation clears the state to `none`.

PR10.4d adds an owner-aware stale `InterruptionState=handling` watchdog. Ownerless stale handling now clears to `none`, while active capture, pending clarification, held playback, and interruption-owned speech are preserved.

PR10.4e adds full pending clarification response ownership. Consumed pending clarification answers are handed to `LiveInterruptionIntegrationService`, which binds them to the stored spoken-answer checkpoint, generates a recomposed continuation, suppresses legacy cleanup/generic routing, and clears interruption state on success or failure.

## Planned Behavior

Run manual AskClarification PR10.4 live UX validation, then stabilize failing barge-in/correction tests before broad unrelated voice behavior.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/LiveUtterance/LiveUtteranceGate.cs` | LiveUtteranceGate | Live utterance decision | Accept/clarify/route decisions. |
| `Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs` | BargeInCoordinator | Capture/STT coordination and stale handling watchdog | Idle/speaking capture; clears ownerless stale `handling` state without touching active owners. |
| `Merlin.Backend/Services/AssistantSpeechPlaybackService.cs` | AssistantSpeechPlaybackService | Playback control | Pause/resume/stop/drain. |
| `Merlin.Backend/Services/InterruptionIntelligence/PendingInterruptionClarificationService.cs` | PendingInterruptionClarificationService | Pending unclear-interruption clarification owner | Owns pending records, expiry, cancellation cleanup, and timeout recovery. |
| `Merlin.Backend/Models/AssistantUiStateEvent.cs` | AssistantUiStateEvent | UI/backend state contract | Defines canonical interruption state constants including `awaiting_interruption_clarification`. |

## Code Atlas

- [[LiveUtteranceGate]]
- [[AssistantSpeechPlaybackService]]
- [[PendingInterruptionClarificationService]]
- [[Voice Command Flow]]
- [[Assistant Speech Playback Flow]]

## Related Systems

- STT
- TTS
- [[Browser Control]]
- [[Command Routing Architecture]]
- [[Correction Layer]]

## Dependencies

- STT
- TTS
- [[Command Routing Architecture]]

## Dependents

- [[Correction Layer]]
- [[Browser Control]]

## Readiness

Ready for implementation: partial

Reason:
The AskClarification PR10.4 dead-end recovery sequence is implementation-complete. Remaining voice readiness work is manual live UX validation and adjacent test stabilization.

Blocked by:
- Current failing BargeIn idle-capture tests remain adjacent known failures.

Next safe action:
Run [[AskClarification PR10.4 Live UX Validation Checklist]], then fix correction/barge-in test failures in isolation.

## Non-Goals / Do Not Build Yet

- Do not mix voice timing fixes with browser/motion feature work.

## Known Bugs / Fragility

- Correction/barge-in timing fragility.
- Pause/play/stop ambiguity with active browser media.
- Unclear-interruption clarification ownership is implemented, but live wording/UX should still be validated with [[AskClarification PR10.4 Live UX Validation Checklist]].
- Existing correction regeneration and BargeIn capture tests remain red and are tracked separately from the AskClarification fallback.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/LiveUtteranceGateTests.cs` | Gate decisions | Surface/media edge cases need more. |
| `Merlin.Backend.Tests/BargeInTests.cs` | Barge-in | Currently failing cases. |
| `Merlin.Backend.Tests/PendingInterruptionClarificationServiceTests.cs` | Pending clarification owner | Owner, expiry, cancellation cleanup, and timeout recovery. |
| `Merlin.Backend.Tests/BargeInTests.cs` | Barge-in and watchdog | Stale handling watchdog cleanup/non-cleanup coverage plus known unrelated idle-capture failures. |

## Relevant Implementation Plans

- [[Always-On Interruption And Live Utterance Routing Plan]]
- [[Conversational Interruption Redesign V2 Plan]]
- [[AskClarification Live Dead-End Recovery Plan]]
- [[PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan]]
- [[PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan]]
- [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]]
- [[Responsive Feedback Migration V2 Plan]]
- [[Conversational Interruption Redesign Original Plan]]
- [[Responsive Feedback Migration Original Plan]]
- [[Echo Aware Self Speech Suppression Plan]]
- [[Fast Near-End Ducking Path Plan]]
- [[Instant Ducking And Natural Hard Stop Plan]]
- [[Playback Clock Aligned Reference Tap Plan]]
- [[Playback Mic Correlation Self Echo Suppression Plan]]
- [[Voice Correction Learning Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (25 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
