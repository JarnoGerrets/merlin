---
type: agent-report
status: completed
related_features:
  - Voice Interruption System
  - Responsive Feedback
  - Streaming Responses and TTS
related_bug: AskClarification Live Dead-End
---

# AskClarification PR10.4 Prerequisite Investigation

## Summary

The full ConversationalInterruption PR10.4 AskClarification recovery is not ready for implementation yet.

The PR10.4a safe fallback is in place: stale ownerless live `AskClarification` outcomes no longer fall through the old PR7 branch, short fragments such as `in the pool` resume or clean up the held playback path, and focused live interruption tests cover the observed wedge.

The larger system still lacks the durable owner needed to ask an unclear-interruption clarification, wait for the user's answer, bind that answer to the interrupted turn, time out safely, and leave `InterruptionState=handling` reliably.

## Go / No-Go Result

Result: No-Go for full PR10.4 implementation until prerequisites below are implemented.

This is a No-Go for runtime behavior changes, not a No-Go for planning. The next safe implementation unit is PR10.4b: a narrow pending unclear-interruption clarification owner.

## Runtime Code Changes

None.

## Current Mitigation

PR10.4a already does the following:

- Removes the stale live branch that logged `Ask-user-to-clarify is not executed live in PR7`.
- Resolves ownerless `AskUserToClarifyInterruption` and unsupported recomposition-like live strategies as terminal fallback outcomes.
- Resumes provisional holds when possible instead of suppressing resume and returning to legacy semantic routing.
- Suppresses legacy semantic routing for handled terminal fallback outcomes.
- Covers the observed `in the pool` wedge in `ConversationalInterruptionLiveIntegrationTests`.

This mitigation prevents the known dead end, but it does not implement a conversational clarification loop.

## Missing Prerequisites

### 1. Pending unclear-interruption clarification owner

Findings:

No durable owner exists today for an unclear live interruption that Merlin asks the user to clarify. Existing components can classify, route, speak a bridge phrase, and resume or flush playback, but none owns "we are awaiting the answer to this unclear interruption."

Evidence:

- `LiveInterruptionIntegrationService.HandleAskUserToClarifyInterruptionAsync` either maps to sequential recomposition when all prerequisites are enabled or falls back terminally. It does not create a pending clarification record.
- `ResponsiveFeedbackInterruptionPort.RequestBridgeFeedbackAsync` can emit bridge/wait feedback and marks requests as unclear via `IsUnclear`, but it does not store pending state.
- `LiveUtteranceGate` has `AskClarification` as a decision kind and maps it to route action `AskClarification`, but the gate is stateless beyond short pending fragments.
- `ConfirmationService` owns pending confirmations with expiry and consume semantics, but that model is for safety confirmations, not live interruption clarification.
- Repository searches found no production `PendingClarification`, `AwaitingInterruption`, `DeferredClarification`, or durable pending interruption clarification owner.

Recommended owner:

Create a new singleton service under `Merlin.Backend/Services/InterruptionIntelligence/`, for example:

- `IPendingInterruptionClarificationService`
- `PendingInterruptionClarificationService`
- `PendingInterruptionClarification`

Required files/classes:

- `PendingInterruptionClarification` model with clarification id, active turn id, correlation id, capture id, original transcript, normalized transcript, checkpoint metadata if available, provisional hold id if relevant, created/expiry timestamps, and status.
- Service APIs:
  - `CreatePendingAsync(...)`
  - `TryGetLatestPending(...)`
  - `TryConsumeResponse(...)`
  - `CancelForTurn(...)`
  - `ExpireDue(...)`
  - `HasActivePendingForTurn(...)`
- Callers:
  - `LiveInterruptionIntegrationService` creates pending state when it genuinely asks for an unclear-interruption clarification.
  - `BargeInCoordinator` or the live utterance route path checks and consumes pending state before normal command routing.
  - `LiveUtteranceGate` may receive pending state as input, but should not own the state.

Services that must not own it:

- `AssistantSpeechPlaybackService`: owns audio playback/hold mechanics only.
- `ResponsiveFeedbackInterruptionPort`: owns bridge feedback emission only.
- `LiveUtteranceGate`: should remain an evaluator/router, not a durable workflow store.
- `BargeInCoordinator`: can orchestrate the capture flow, but should not become the long-lived pending state store.

Risks:

- If pending state is stored in BargeIn only, idle/background captures can bypass it or lose it across route paths.
- If pending state is stored in playback, clarification ownership becomes coupled to audio hold lifetime.
- If pending state is stored in responsive feedback, silent/no-feedback clarification cases become impossible to own correctly.

### 2. AwaitingInterruptionClarification state

Findings:

No explicit awaiting-interruption-clarification state exists today.

Evidence:

- `AssistantUiStateEvent` carries string `BaseState` and string `InterruptionState`.
- Barge-in emits `interruptionState: "handling"` when a captured utterance interrupted speaking playback.
- `AssistantSpeechPlaybackService.BeginProvisionalAudioHoldAsync` emits base state `listening` with `interruptionState: "held_for_user_speech"`.
- `AssistantSpeechPlaybackService.ResumeProvisionalAudioHoldAsync` emits base state `speaking` with `interruptionState: "none"`.
- Searches found no `AwaitingInterruption`, `PendingClarification`, or `AwaitingInterruptionClarification` production state.

Recommended state model:

Keep using the existing `AssistantUiStateEvent.InterruptionState` string contract, but add canonical constants for interruption states before broadening usage. The new state should be:

```text
awaiting_interruption_clarification
```

Required transitions:

- `handling` -> `awaiting_interruption_clarification` after a clarification prompt is successfully queued or spoken.
- `awaiting_interruption_clarification` -> `handling` when the user's clarification answer is captured and being recomposed or routed.
- `awaiting_interruption_clarification` -> `none` on timeout, cancellation, stop request, browser/surface route takeover, or explicit "never mind".
- `held_for_user_speech` must remain reserved for the low-level provisional audio hold window before the yielded utterance is classified.
- During clarification prompt playback, base state can be `speaking` with speech item `interruption_clarification`; after prompt completion, base state should become `listening` with interruption state `awaiting_interruption_clarification`.

Risks:

- Reusing `handling` while waiting for the user's answer recreates the wedge class.
- Reusing `listening` alone makes the UI look healthy while the router silently needs a bound clarification answer.
- Adding a new state without a timeout creates a more explicit but still stale failure mode.

### 3. Timeout/recovery

Findings:

Merlin has timeout patterns, but none for pending unclear-interruption clarification.

Evidence:

- `AssistantSpeechPlaybackService.RefreshHoldTimeoutLocked` resumes provisional audio holds after `InterruptionHandlingOptions.ProvisionalAudioHoldTimeoutMs`, deferring briefly while interruption STT is pending.
- `ConfirmationService` uses an expiry duration and removes expired pending confirmations on access.
- `InterruptionHandlingOptions` includes `ClarificationTimeoutMs`, but current live PR10.4a code uses it for model call limits, not durable user-response pending state.
- No pending clarification expiry service exists.

Recommended owner:

The pending clarification service should own expiry of pending clarification records. It may use a passive cleanup-on-access approach first, then add an active timer only if UI cleanup requires it.

Config:

Add config to `InterruptionHandlingOptions`, for example:

```text
PendingInterruptionClarificationTimeoutMs = 15000
```

Recommended duration: 15 seconds initially. This gives the user human time to answer after a spoken clarification prompt, while still bounding stale state.

Timeout behavior:

- Clear the pending clarification record.
- Emit/log a timeout event such as `pending_interruption_clarification_timeout`.
- Exit `awaiting_interruption_clarification` to `none`.
- Do not route the expired answer later as a clarification response.
- Do not cancel unrelated current speech.
- Do not resume an original final answer if the full PR10.4 path already flushed/cancelled it to ask the clarification.
- If a provisional hold is still active due to a failure before clarification ownership was established, use the existing playback port resume path rather than touching playback internals.

Risks:

- If timeout lives in playback, it can only understand holds, not clarification answers.
- If timeout lives only in UI state, the backend can still consume an expired answer.
- If timeout cancels playback blindly, it can kill newly queued interruption-owned clarification speech.

### 4. Stale handling watchdog

Findings:

There is no general stale `InterruptionState=handling` watchdog.

Evidence:

- `BargeInCoordinator` tracks active capture/handling via `_handlingTrigger` and `_activeCaptureSession`.
- `AssistantUiStateBroadcaster` emits and coalesces UI states but does not own recovery decisions.
- `AssistantSpeechPlaybackService` has provisional hold timeout recovery, but only for active held playback.
- No production search result showed a general stale interruption handling watchdog.

Recommended owner:

Add a narrow watchdog around BargeIn/live interruption handling, likely owned by `BargeInCoordinator` or a small service it calls, because BargeIn owns `_handlingTrigger`, capture sessions, and the first transition into `interruptionState: "handling"`.

The pending clarification service should expose state to the watchdog, but should not itself monitor capture/session internals.

Config:

- `EnableLiveInterruptionStaleHandlingWatchdog`
- `LiveInterruptionHandlingWatchdogTimeoutMs`
- Optional `LiveInterruptionHandlingWatchdogPollMs`

What it monitors:

- Barge-in handling trigger active.
- Active capture session.
- Active provisional hold or held playback snapshot.
- Active pending interruption clarification.
- Active interruption-owned speech output.
- Active model call/recomposition task if available.

Safe recovery action:

- If no active capture, no active hold, no pending clarification, no active interruption speech, and no active model call exists past the timeout, emit a recovery UI state with `interruptionState: "none"`.
- Log recovery with enough state to debug the lost owner.
- Do not cancel playback or flush queues unless a specific owner reports stale ownership.

Tests needed:

- Handling state clears when no owner remains.
- Handling state does not clear while pending clarification exists.
- Handling state does not clear while provisional hold is active.
- Handling state does not clear while interruption clarification speech is queued/playing.

Risks:

- A watchdog that clears too aggressively can hide real long-running model or speech work.
- A watchdog that cancels instead of only clearing ownerless state can introduce new audio/turn corruption.

### 5. PR10 clarification/recomposition ownership

Findings:

Several PR10 pieces exist, but full ownership is partial.

Evidence:

- `ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint` exists.
- `LiveInterruptionIntegrationService.HandleSequentialClarificationRecompositionAsync` can create a checkpoint, flush held/original speech, suppress normal progress, cancel current playback, generate clarification, and optionally generate continuation.
- Required flags exist in `InterruptionHandlingOptions`: `EnableLiveModelCalls`, `EnableClarificationCalls`, `EnableContinuationRecomposition`, `EnableSequentialRecomposition`, `EnableLiveSpokenAnswerTracking`, and `EnableLivePlaybackActions`.
- `LiveSpokenAnswerTrackingService` and `SpokenAnswerTracker` exist for checkpoint creation.
- `AssistantSpeechInterruptionSpeechOutputPort` marks `"clarification"` as `SpeechPlaybackItemType.InterruptionClarification` and `"recomposed_continuation"` as `SpeechPlaybackItemType.InterruptionContinuation`.
- `ResponsiveFeedbackInterruptionPort` can suppress normal progress and emit bridge feedback.

Missing:

- Durable pending clarification owner for user answers.
- Explicit awaiting clarification state.
- Timeout/recovery for pending clarification.
- Watchdog for stale handling.
- Unified full ownership for live `LocalBridgeAndRecomposeFromCheckpoint` and `QueueFollowUpAfterCurrent`; PR10.4a terminally falls back for unsupported live strategies.
- Clear answer-binding path from next captured utterance into the pending clarification/recomposition flow.

Partial:

- Sequential recomposition exists, but it runs synchronously within the yielded utterance handling path when all flags and dependencies are enabled. It is not a durable "ask now, wait for answer, then continue" workflow.
- Responsive feedback can bridge an unclear action, but bridge feedback is not the same as owning a pending clarification question.

Ready:

- Playback port abstractions.
- Model port abstractions.
- Speech output port and interruption-owned speech item types.
- Live integration test seams with fake playback/model/speech ports.

### 6. Hold/resume/flush ownership

Findings:

`AssistantSpeechPlaybackService` owns provisional audio holds. Full PR10.4 should only interact with holds through the existing playback port abstractions.

Evidence:

- `BeginProvisionalAudioHoldAsync` pauses active final-answer playback, creates a hold id, refreshes hold timeout, and emits `held_for_user_speech`.
- `ResumeProvisionalAudioHoldAsync` resumes only a matching active held playback and emits `speaking` with `interruptionState: "none"`.
- `FlushProvisionalAudioHoldAsync` clears the hold, increments final answer generation, cancels the held playback cancellation token, and logs `conversational_interruption_speech_channel_flushed`.
- `LiveInterruptionIntegrationService.ResolveProvisionalAudioHoldAsync` logs missing hold ids as unavailable and does not throw.
- `AssistantSpeechInterruptionPlaybackPort` already gates resume/flush through `EnableLivePlaybackActions`.

Required API usage:

- Use `IInterruptionPlaybackPort.ResumeProvisionalAudioHoldAsync` for ignore/backchannel/fallback outcomes.
- Use `IInterruptionPlaybackPort.FlushProvisionalAudioHoldAsync` when the original answer is being replaced by interruption-owned speech.
- Use `FlushFinalAnswerSpeechForTurnAsync`, `CancelCurrentAsync`, or `StopCurrentAsync` only through the playback port.
- Never manipulate wave players, queue state, generation ids, or hold ids outside `AssistantSpeechPlaybackService`.

Risks:

- Bypassing playback service risks stale generation ids and cancelled-but-still-speaking audio.
- Keeping a provisional hold while waiting for a longer clarification answer can cause timeout resume over the top of an interruption-owned prompt.

### 7. Legacy cleanup risk

Findings:

Legacy cleanup can still run after live gate/classifier paths unless a live outcome explicitly suppresses it. Any full PR10.4 path that queues interruption-owned speech must prevent legacy cleanup and semantic routing from cancelling or replacing that speech.

Evidence:

- `BargeInCoordinator` emits `interruption_handling_started`, evaluates the live gate, calls the live conversational interruption seam, then falls through to legacy classifier/routing when allowed.
- `LiveInterruptionHandlingOutcome` has `AllowLegacyCleanup` and `AllowLegacySemanticRouting` flags.
- PR10.4a terminal fallback uses `AllowLegacyCleanup=true`, `AllowLegacySemanticRouting=false`, and `ShouldResumeOrContinuePlaybackIfPossible=true`.
- Sequential recomposition uses `AllowLegacyCleanup=false`, `AllowLegacySemanticRouting=false`, and queues `InterruptionClarification` / `InterruptionContinuation` speech through the speech output port.
- `BargeInCoordinator` can clear queues and cancel turns in legacy accepted paths.

Required guard:

- Any outcome that queues interruption-owned clarification or continuation speech must set `AllowLegacyCleanup=false` and `AllowLegacySemanticRouting=false`.
- Once a pending clarification owner is created, the next utterance must be consumed by that owner before generic command routing.
- If an owner fails to create, fall back terminally and resume/cleanup through existing playback APIs.

Risks:

- If legacy cleanup runs after a prompt is queued, Merlin can ask a clarification and immediately cancel the prompt.
- If semantic routing consumes the clarification answer as a new command, Merlin can appear to ignore the clarification workflow.

## Proposed Implementation Sequence

### PR10.4b - Pending unclear-interruption clarification owner

Scope:

Add the durable pending clarification owner only. Do not build the watchdog yet. Do not enable full multi-turn recomposition yet.

Files/classes:

- `Merlin.Backend/Services/InterruptionIntelligence/PendingInterruptionClarification.cs`
- `Merlin.Backend/Services/InterruptionIntelligence/IPendingInterruptionClarificationService.cs`
- `Merlin.Backend/Services/InterruptionIntelligence/PendingInterruptionClarificationService.cs`
- `Merlin.Backend/Configuration/InterruptionHandlingOptions.cs`
- `Merlin.Backend/Program.cs`
- Narrow integration points in `LiveInterruptionIntegrationService` and BargeIn/live route handling only if needed to create/consume state.

Tests:

- `PendingInterruptionClarificationServiceTests.CreateStoresPendingWithExpiry`
- `PendingInterruptionClarificationServiceTests.ConsumeResponseRemovesPending`
- `PendingInterruptionClarificationServiceTests.ExpiredPendingIsNotReturned`
- `ConversationalInterruptionLiveIntegrationTests.AskClarificationCreatesPendingOwnerWhenEnabled`
- `BargeInCoordinatorTests.PendingInterruptionClarificationResponseBypassesNormalCommandRouting`

Non-goals:

- No stale handling watchdog.
- No full recomposition.
- No broad UI redesign.
- No changes to unrelated correction regeneration tests.

Acceptance criteria:

- A pending unclear-interruption clarification can be created and consumed.
- Expired pending state is not consumed.
- The owner is independent of playback holds and responsive feedback.
- Existing PR10.4a fallback remains intact when owner creation is disabled or unavailable.

### PR10.4c - Awaiting state + timeout recovery

Scope:

Add explicit UI/backend state transitions for awaiting an interruption clarification answer and timeout recovery for the pending owner.

Files/classes:

- `AssistantUiStateEvent` usage sites, preferably via canonical state constants.
- `AssistantUiStateBroadcaster` callers in BargeIn/live interruption paths.
- `PendingInterruptionClarificationService`
- `InterruptionHandlingOptions`

Tests:

- `ConversationalInterruptionLiveIntegrationTests.AskClarificationTransitionsToAwaitingInterruptionClarification`
- `BargeInCoordinatorTests.PendingClarificationTimeoutClearsInterruptionState`
- `AssistantSpeechPlaybackServiceTests.InterruptionClarificationSpeechDoesNotReuseHeldForUserSpeechState`

Non-goals:

- No watchdog for arbitrary stale handling.
- No broad state-machine rewrite.

Acceptance criteria:

- Awaiting state appears only while a live pending clarification exists.
- Timeout clears pending state and exits awaiting state.
- Timeout does not cancel unrelated playback.

### PR10.4d - Stale handling watchdog

Scope:

Add owner-aware stale handling recovery for `InterruptionState=handling` without changing normal interruption behavior.

Files/classes:

- Barge-in watchdog code or small `IInterruptionHandlingWatchdog` service.
- `BargeInCoordinator` integration.
- `InterruptionHandlingOptions` or `BargeInOptions` config.

Tests:

- `BargeInCoordinatorTests.HandlingWatchdogClearsOwnerlessHandlingState`
- `BargeInCoordinatorTests.HandlingWatchdogDoesNotClearWhilePendingClarificationExists`
- `BargeInCoordinatorTests.HandlingWatchdogDoesNotClearWhileProvisionalHoldExists`
- `BargeInCoordinatorTests.HandlingWatchdogDoesNotClearWhileInterruptionSpeechActive`

Non-goals:

- No correction regeneration refactor.
- No playback queue rewrites.

Acceptance criteria:

- Ownerless stale handling state is recovered.
- Active owned work is not interrupted.
- Recovery is logged and configurable.

### PR10.4e - Full clarification/recomposition outcome ownership

Scope:

Complete the multi-turn AskClarification and clarification/recomposition paths once the pending owner, awaiting state, timeout recovery, and watchdog exist.

Files/classes:

- `LiveInterruptionIntegrationService`
- `PendingInterruptionClarificationService`
- `InterruptionOrchestrator`
- `LiveUtteranceGate` input/context plumbing if needed
- `BargeInCoordinator`
- model/speech output port tests

Tests:

- `ConversationalInterruptionLiveIntegrationTests.AskClarificationResponseGeneratesContinuation`
- `ConversationalInterruptionLiveIntegrationTests.LocalBridgeAndRecomposeHasExecutableOwner`
- `ConversationalInterruptionLiveIntegrationTests.QueueFollowUpHasExecutableOwnerOrExplicitFallback`
- `LiveUtteranceGateTests.PendingInterruptionClarificationResponseRoutesToOwner`
- `BargeInCoordinatorTests.PendingClarificationAnswerDoesNotRouteToCommandRouter`

Non-goals:

- No new prompt UX unrelated to unclear interruptions.
- No fallback implementation if prerequisites regress.

Acceptance criteria:

- Merlin can ask an unclear-interruption clarification, wait for the user's answer, and continue/recompose without legacy cleanup killing prompt or continuation speech.
- Unsupported branches fail closed through terminal fallback rather than stale live deferral.

## Suggested Next Prompt

```text
You are working in the Merlin repository.

Task: implement PR10.4b, the pending unclear-interruption clarification owner.

Before changing runtime code, read:
- Merlin.Vault/07_Agent_Reports/AskClarification PR10.4 Prerequisite Investigation.md
- Merlin.Vault/13_Implementation_Plans/Voice/AskClarification Live Dead-End Recovery Plan.md
- Merlin.Vault/09_Bugs/AskClarification Live Dead-End.md
- Merlin.Vault/11_Code_Atlas/Backend/LiveInterruptionIntegrationService.md
- Merlin.Vault/11_Code_Atlas/Flows/Live Utterance Flow.md

Scope:
- Add a durable pending unclear-interruption clarification owner under Services/InterruptionIntelligence.
- Add model/interface/service and DI registration.
- Add expiry/consume APIs.
- Wire only the minimal create/consume path needed for unclear live interruption clarification ownership.
- Preserve PR10.4a terminal fallback when pending owner creation is disabled or unavailable.

Non-goals:
- Do not add stale handling watchdog.
- Do not implement full PR10.4 recomposition.
- Do not refactor BargeIn architecture.
- Do not change unrelated correction regeneration behavior.

Required tests:
- pending service create/consume/expiry unit tests
- live integration test proving AskClarification can create a pending owner when enabled
- route test proving pending clarification response bypasses generic command routing
- regression test proving `in the pool` safe fallback still resumes and suppresses semantic routing when owner is not enabled/applicable

Validation:
- dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
- dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification" -p:UseSharedCompilation=false

Final response:
- files changed
- behavior changed
- tests run
- whether PR10.4b owner exists
- whether full PR10.4 is still blocked
```

## Open Questions

- Should the clarification answer be accepted only in the next capture, or for any capture before timeout?
- Should a user say "never mind" to cancel the pending clarification and resume, or should that be handled as a stop/cancel command?
- Should pending clarification state be exposed in active surface context for routing/debugging?
- Should `ClarificationTimeoutMs` be reused, or should a new `PendingInterruptionClarificationTimeoutMs` be added to avoid model-call timeout ambiguity?
- Should the awaiting state be visible in frontend UI, or only used for backend routing and diagnostics at first?

## Related Vault Updates

- [[AskClarification Live Dead-End Recovery Plan]]
- [[AskClarification Live Dead-End]]
- [[Voice Pipeline Progress]]
- [[Current Work Dashboard]]
- [[2026 Change Log]]
- [[RUN-2026-07-07-005 AskClarification PR10.4 Prerequisite Investigation]]
