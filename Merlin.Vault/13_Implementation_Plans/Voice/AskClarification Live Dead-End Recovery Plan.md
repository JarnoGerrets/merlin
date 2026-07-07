---
type: implementation-plan
status: implemented
ready_for_agent: false
system: Voice
related_feature: Voice Interruption System
source: Merlin.ToDo/askclarification_implementation/merlin_askclarification_dead_end_fix.md
last_updated: 2026-07-07
---

# AskClarification Live Dead-End Recovery Plan

## Purpose

Fix the live interruption dead end where an utterance such as `in the pool` could be classified as `AskClarification`, suppress playback resume, enter `InterruptionState=handling`, and then fall through a stale PR7 branch with no executable live owner.

## Go / No-Go Inspection

Result: PR10.4a originally started as a safe fallback. PR10.4b through PR10.4e have now completed the prerequisite chain and full pending clarification/recomposition ownership.

## Go / No-Go Lesson

A previous attempt found missing prerequisites and implemented a minimal safe fallback anyway.

Going forward, this is not allowed unless the prompt explicitly approves a fallback scope.

If pending unclear-interruption clarification owner or stale handling watchdog is missing, the correct behavior is:

- stop runtime changes,
- mark this plan blocked/partial,
- report prerequisites,
- propose prerequisite implementation plan.

Verified present:

| Prerequisite | Status | Evidence |
| --- | --- | --- |
| PR10 sequential clarification/recomposition path | present | `LiveInterruptionIntegrationService.HandleSequentialClarificationRecompositionAsync` and related live tests. |
| AssistantSpeechPlaybackService provisional hold resume/flush support | present | Playback port methods and tests cover provisional hold resume/flush. |
| InterruptionState handling/cleanup state | partial | Barge-in emits handling/cleanup states, but no stale live clarification owner/watchdog exists. |
| Test seams for live interruption outcomes | present | `ConversationalInterruptionLiveIntegrationTests` has fake playback/model/speech ports and live outcome assertions. |

Missing or partial:

| Prerequisite | Status | Consequence |
| --- | --- | --- |
| ResponsiveFeedback unclear interruption prompt support | present | Existing feedback can bridge, and pending owner/recomposition ownership now binds the follow-up answer. |
| Awaiting live interruption clarification state and response execution | present | PR10.4c added awaiting state and timeout/cancel cleanup; PR10.4e added executable response ownership. |
| Stale `InterruptionState=handling` watchdog | present | PR10.4d clears ownerless stale handling without touching active capture, pending clarification, held playback, or interruption-owned speech. |

## Implemented in PR 10.4a

- Removed stale PR7 live AskClarification dead branch.
- Added safe terminal fallback for ownerless AskClarification.
- Short fragments such as `in the pool` now resume/cleanup.
- Added regression test for observed wedge.

Additional safe fallback coverage:

- Sequential recomposition disabled/unavailable paths now resume/cleanup instead of deferring.
- Unsupported recomposition-like live strategies now produce a handled terminal fallback outcome.

## Implemented in PR 10.4b

- Added durable pending unclear-interruption clarification owner.
- Added pending clarification create/get/consume/cancel/expire APIs.
- Registered pending owner in DI.
- Added opt-in live AskClarification pending-owner creation.
- Added BargeIn consume hook so pending clarification responses bypass normal backend voice request routing.
- Added service, live integration, and BargeIn route tests.

## Implemented in PR 10.4c

- Added canonical `awaiting_interruption_clarification` interruption state.
- Pending AskClarification owner creation now emits awaiting clarification UI/backend state.
- Pending clarification response consumption now emits `handling` before routing the bound response.
- Pending clarification expiry and cancellation now clear awaiting state back to `none`.
- Added active timeout recovery for pending clarification records using `PendingInterruptionClarificationTimeoutMs`.
- Added focused tests for pending creation, consumption, expiry, cancellation, timeout recovery, and default config behavior.

## Implemented in PR 10.4d

- Added configurable stale `InterruptionState=handling` watchdog in BargeIn.
- Ownerless stale handling now clears to `none` with reason `stale_interruption_handling_watchdog_recovered`.
- The watchdog defers cleanup while active capture, pending clarification, held playback, or interruption-owned speech still owns the interruption.
- Added focused watchdog tests for cleanup and non-cleanup of valid active states.

## Implemented in PR 10.4e

- Added executable pending clarification response ownership in `LiveInterruptionIntegrationService`.
- Pending clarification records now retain spoken-answer checkpoint context needed for recomposition.
- `BargeInCoordinator` now hands consumed pending clarification responses to the live owner before generic command routing.
- Successful pending responses generate and speak a recomposed continuation while suppressing legacy cleanup and semantic routing.
- Failed or incomplete pending response ownership clears interruption state and fails closed without routing the answer as a generic command.
- Added focused tests for recomposition, failed recomposition cleanup, incomplete checkpoint safety, and BargeIn owner handoff.

## PR10.4 Prerequisite Investigation

Status: implemented for the AskClarification dead-end recovery sequence.

Closure review: [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]] confirmed PR10.4a-e is implementation-complete. Manual live UX validation is still pending before the bug should be marked verified.

The prerequisite investigation is captured in [[AskClarification PR10.4 Prerequisite Investigation]].

Required implementation sequence:

1. PR10.4b - pending unclear-interruption clarification owner. Implemented.
2. PR10.4c - awaiting clarification state and timeout recovery. Implemented.
3. PR10.4d - stale handling watchdog. Implemented.
4. PR10.4e - full clarification/recomposition outcome ownership. Implemented.

## Non-Goals

- Do not create a new pending clarification subsystem.
- Do not invent a separate ResponsiveFeedback clarification state.
- Do not add a second interruption router.
- Do not change the global behavior when live minimal behavior is fully disabled by config.

## Code Map

| File | Role |
| --- | --- |
| `Merlin.Backend/Services/InterruptionIntelligence/LiveInterruptionIntegrationService.cs` | Owns live interruption outcome resolution and terminal fallback behavior. |
| `Merlin.Backend/Services/InterruptionIntelligence/PendingInterruptionClarificationService.cs` | Owns PR10.4b pending unclear-interruption clarification records. |
| `Merlin.Backend.Tests/ConversationalInterruptionLiveIntegrationTests.cs` | Regression tests for live interruption outcomes. |
| `Merlin.Backend.Tests/PendingInterruptionClarificationServiceTests.cs` | Tests pending owner create/consume/expiry/cancel behavior. |
| `Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs` | Consumes pending clarification responses, emits response-handling state, and hands the response to the live owner. |
| `Merlin.Backend/Configuration/BargeInOptions.cs` | Owns stale handling watchdog enable/timeout config. |

## Validation

| Check | Result |
| --- | --- |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter ConversationalInterruptionLiveIntegrationTests` | passed, 36 tests |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` | passed; existing `FloorYieldController._yieldedCurrentPlayback` warning remains |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification" -p:UseSharedCompilation=false` | passed, 42 tests |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification" -p:UseSharedCompilation=false` | passed, 46 tests after PR10.4c |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "PendingInterruptionClarificationResponseTransitionsToHandling\|PendingInterruptionClarificationResponseBypassesBackendVoiceRequest" -p:UseSharedCompilation=false` | passed, 2 tests |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification\|BargeIn" -p:UseSharedCompilation=false` | failed with the same 4 known BargeIn idle-capture failures listed below; PR10.4c-specific tests passed |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "InterruptionHandlingWatchdog" -p:UseSharedCompilation=false` | passed, 4 PR10.4d tests |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "InterruptionHandlingWatchdog\|BargeInOptions_DefaultProviderIsWebRtcApm" -p:UseSharedCompilation=false` | passed, 5 tests including default watchdog config |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification\|BargeIn" -p:UseSharedCompilation=false` | latest run passed 195 tests and failed 5 BargeIn tests; 4 are known idle-capture failures and `BurstPromotion_AllowsMixedAllowUncertainFrames` passed individually afterward |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "FullyQualifiedName~BargeInCoordinatorTests.BurstPromotion_AllowsMixedAllowUncertainFrames" -p:UseSharedCompilation=false` | passed individually |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "TryHandlePendingClarificationResponseAsync\|PendingInterruptionClarificationResponseHandledByOwner\|AskClarificationCreatesPendingOwnerWhenEnabled\|AskClarificationShortFragmentInThePool" -p:UseSharedCompilation=false` | passed, 6 PR10.4e-focused tests |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification" -p:UseSharedCompilation=false` | passed, 52 tests |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification\|BargeIn" -p:UseSharedCompilation=false` | passed 200 tests and failed 4 known BargeIn idle-capture tests after PR10.4e |
| Previously failing tests run individually | reproduced 9 failures; classified below. |
| Full backend test project | failed with 9 correction/barge-in failures; focused live interruption regression remains green. |

## Failing Test Classification

| Test | Result | Classification | Notes |
| --- | --- | --- | --- |
| `Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest` data row `i mean family car` | fails individually | pre-existing unrelated failure | Correction regeneration dispatch path; does not enter `LiveInterruptionIntegrationService`. |
| `Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest` data row `i mean what is the purpose of a voice` | fails individually | pre-existing unrelated failure | Same correction regeneration dispatch path. |
| `OldCancelledCorrelationIdCannotSuppressNewCorrectionCorrelationId` | fails individually | pre-existing unrelated failure | Correction correlation cancellation behavior. |
| `Correction_NewCorrectedResponseCanEnqueueSpeech` | fails individually | pre-existing unrelated failure | Corrected response playback enqueue behavior. |
| `Correction_CancelsOldTurnAndDispatchesNewRequest_WithNewCorrelationId` | fails individually | pre-existing unrelated failure | Correction dispatcher output behavior. |
| `ProcessMicrophoneFrame_BackendIdleVoice_AddsCaptureIdAndTimelineLogs` | fails individually | related but not caused | Adjacent BargeIn capture/timeline behavior, but failure occurs before live interruption outcome handling and no BargeIn production code changed. |
| `ProcessMicrophoneFrame_IdleAecOnlyEnergy_DoesNotExtendCapture` | fails individually | related but not caused | Adjacent idle capture acoustic behavior, not touched by PR 10.4a. |
| `ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest` | fails individually | related but not caused | Backend idle voice request capture behavior, not live interruption fallback. |
| `ProcessMicrophoneFrame_IdleRawMicSpeech_KeepsCaptureActive` | fails individually | related but not caused | Adjacent idle raw mic capture behavior, not touched by PR 10.4a. |

## Follow-Up Needed

- Run [[AskClarification PR10.4 Live UX Validation Checklist]].
- Fix known BargeIn idle-capture test failures using [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]].
- Fix known correction regeneration failures using [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]].
- Revisit wording/UX around unclear-interruption prompts after live validation.

## Related Notes

- [[Voice Interruption System]]
- [[Voice Pipeline Architecture]]
- [[LiveInterruptionIntegrationService]]
- [[AskClarification Live Dead-End]]
- [[Voice Pipeline Progress]]
