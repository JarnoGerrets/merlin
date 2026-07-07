---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/askclarification_implementation/merlin_askclarification_dead_end_fix.md
related_features:
  - Correction Layer
status: current
ready_for_agent: true
---

## Plan Status

Status: current
Ready for agent use: yes
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: [[Correction Layer]]
Related architecture: [[Correction Architecture]]
Related code atlas: [[CorrectionRequestBuilder]]
Original source: `Merlin.ToDo/askclarification_implementation/merlin_askclarification_dead_end_fix.md`

# AskClarification Dead End Fix Plan

## Purpose

This document is a focused implementation spec for fixing a live voice wedge where Merlin hears the user, runs STT, classifies the utterance as clarification-related, suppresses normal resume, cancels or interrupts the current answer, and then never returns to a valid runtime state.

This is not a new conversational-interruption redesign. The redesign already exists. This PR should close the remaining live integration gap where `AskClarification` / `AskUserToClarifyInterruption` / deferred clarification branches can still behave like a PR7-era dead end.

Working title:

```text
ConversationalInterruption PR 10.4 - AskClarification Live Dead-End Recovery
```

Alternative title:

```text
ConversationalInterruption PR 10.4 - Total Live Outcome Resolution
```

The second title is more accurate if the fix also audits every live yielded-utterance decision branch, not only `AskClarification`.

---

# Observed Failure

A live voice log showed this pattern:

```text
BackendIdleVoiceCaptureStarted
voice_capture_stt_completed
Transcript: "in the pool.."
LiveUtteranceGate Decision: AskClarification
Reason: Ask-user-to-clarify is not executed live in PR7..
PlaybackResumeSuppressedByGate
StreamingFinalAnswerSessionCancelled Reason: stop_current_requested
Final UI state:
  BaseState: thinking
  Reason: interruption_handling_started
  InterruptionState: handling
```

There was no later clean recovery to:

```text
idle
listening
speaking
awaiting clarification
completed
cancelled
```

From the user's point of view this looks like Merlin stopped listening. Technically it still captured and transcribed audio, but the conversation pipeline wedged in interruption handling.

---

# Root Cause Hypothesis

The likely root cause is that a live yielded utterance still reached a stale/deferred PR7 branch:

```text
Ask-user-to-clarify is not executed live in PR7
```

That branch selected a behavior that was conceptually valid but not executable in the current live path.

The dangerous combination was:

```text
1. Merlin yielded to user speech.
2. Gate/CI selected AskClarification or a clarification-like strategy.
3. Normal playback resume was suppressed.
4. Current final answer speech/generation was cancelled or invalidated.
5. No live clarification/recompose/unclear-question/fallback path executed.
6. InterruptionState remained handling.
```

This violates the core runtime rule:

```text
Every live yielded-utterance decision must produce a terminal outcome.
```

Terminal means one of:

```text
- resume held playback
- ignore and clean up
- stop/cancel and clean up
- redirect/replacement routed
- clarification spoken
- recomposed continuation spoken
- unclear-interruption prompt spoken and state moved to awaiting clarification
- safe fallback to listening/idle
- explicit failure recovery completed
```

No live branch may leave Merlin in:

```text
BaseState: thinking
InterruptionState: handling
```

without an active handler, active playback item, active hold, or pending awaited user clarification.

---

# Existing Design This Must Respect

The existing interruption design already says:

```text
Interruption decides.
ResponsiveFeedback phrases.
DeepInfra answers/recomposes.
SpeechPlayback serializes.
```

This PR must preserve that boundary.

## ConversationalInterruption owns

```text
- yielded utterance ownership
- classification result interpretation
- live interruption outcome selection
- state cleanup
- hold resolution
- checkpoint lookup
- clarification/recomposition routing
- fallback behavior
```

## ResponsiveFeedback owns

```text
- reusable bridge phrases
- unclear interruption prompt phrases
- recompose wait phrases
- cooldowns
- cache keys
- phrase selection
```

## AssistantSpeechPlaybackService owns

```text
- speech serialization
- provisional hold resume/flush
- stale final-answer generation invalidation
- actual audio queue behavior
```

Do not hard-code a new phrase library in CI for unclear interruptions.

Allowed local emergency fallback if ResponsiveFeedback fails:

```text
No speech, clean recovery.
```

or one minimal non-card fallback only behind an explicit emergency path. Prefer no phrase over duplicating ResponsiveFeedback.

---

# Main Goal

Fix the live wedge by making `AskClarification` and all clarification-like live outcomes executable, recoverable, and impossible to leave stuck.

The fix is successful when this class of log can no longer happen:

```text
Decision: AskClarification
Reason: not executed live
PlaybackResumeSuppressedByGate
InterruptionState: handling
(no recovery)
```

Instead, the same situation must resolve to one of these paths:

```text
A. likely noise/fragment/backchannel -> resume/ignore and clear handling
B. meaningful clarification/follow-up -> PR10 clarification + recomposition
C. truly unclear directed interruption -> ask a short unclear-interruption question and await user answer
D. unsupported/missing-context -> safe fallback cleanup to listening/idle or resume hold
E. exception/timeout -> forced recovery cleanup
```

---

# Non-Goals

Do not change Layer 1 acoustic behavior in this PR.

Do not change:

```text
- VAD thresholds
- AEC thresholds
- self-echo suppression
- endpointing rules
- raw mic vs residual policy
- STT model or beam settings
- Chatterbox TTS generation logic
- frontend gesture/browser control behavior
```

Do not implement PR11 parallel recomposition unless it already exists and this bug is specifically in its outcome wiring.

Do not add a second clarification system beside the existing PR10 sequential clarification/recomposition path.

Do not make Merlin ask clarification too often. Random fragments such as:

```text
"in the pool.."
"this video.."
"I'm Dick Overslan.."
```

should not automatically trigger spoken clarification unless the utterance is clearly directed at Merlin and contextually meaningful.

---

# Key Runtime Rule

Add or enforce this invariant:

```text
For every yielded utterance that enters Layer 2 / ConversationalInterruption:
  exactly one owner must decide the semantic outcome,
  and the outcome must always resolve cleanup.
```

Cleanup includes:

```text
- clear InterruptionState=handling, or move to a valid explicit state
- resolve provisional hold: resume or flush
- either suppress legacy semantic routing or explicitly allow it
- mark final answer generation/session state consistently
- clear UI thinking/handling state unless real work remains
- emit final timeline completion marker
```

Invalid outcomes:

```text
- handled=false + resumeSuppressed=true + no fallback
- deferred=true + hold unresolved
- AskClarification selected + no live executor
- exception thrown before cleanup
- cancellation token aborts cleanup entirely
- legacy cleanup cancels newly queued interruption speech
```

---

# Terms

## AskClarification

In this spec, `AskClarification` means any live gate or CI result that says Merlin should ask the user to clarify what they meant, instead of immediately treating the transcript as a normal command or recomposition input.

It may appear as one or more of:

```text
LiveUtteranceGateDecision.AskClarification
InterruptionHandlingStrategy.AskUserToClarifyInterruption
ConversationFocusAction.AskUserToClarify
LiveHandlingOutcome.DeferredClarification
Reason: Ask-user-to-clarify is not executed live in PR7
```

The agent must search the codebase for all variants.

## Clarification/follow-up

This is different from `AskClarification`.

A user clarification/follow-up is content from the user, such as:

```text
"But the water itself too, right?"
"What do you mean by liner?"
"How does depth make it blue?"
```

Those should normally execute PR10 sequential clarification + recomposition.

## Unclear interruption

An unclear interruption is directed enough that Merlin should probably not ignore it, but not clear enough to route as correction, stop, or clarification/follow-up.

Example:

```text
"No wait, I mean..."
"Actually..."
"That thing there..."
```

This can use ResponsiveFeedback's unclear interruption card, for example:

```text
"Did you want me to change direction, or should I keep going?"
```

Use this sparingly.

---

# Required Investigation

Before coding, inspect the exact current path from the log.

Search for these strings and related enum values:

```text
Ask-user-to-clarify is not executed live in PR7
AskClarification
AskUserToClarifyInterruption
PlaybackResumeSuppressedByGate
interruption_handling_started
InterruptionState
handling
stop_current_requested
EnableLiveMinimalBehavior
EnableLiveModelCalls
EnableClarificationCalls
EnableContinuationRecomposition
ClarifyThenRecomposeFromCheckpoint
```

Likely areas to inspect:

```text
Merlin.Backend/Services/BargeIn/
Merlin.Backend/Services/SpeechPresence/
Merlin.Backend/Services/Interruption/
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend/Services/LiveUtterance/
Merlin.Backend/Services/Feedback/
```

Likely classes/components to inspect by name or behavior:

```text
LiveUtteranceGate
LiveInterruptionIntegrationService
InterruptionOrchestrator
ConversationFocusManager
InterruptionClassifier
BargeInCoordinator
FloorYieldController
AssistantSpeechPlaybackService
LiveAssistantTurnService
SpokenAnswerTracker
ResponsiveFeedbackOrchestrator
InterruptionFeedbackAdapter
```

---

# Implementation Plan

## Step 1 - Add a central outcome finalization guard

Add a single cleanup/finalization mechanism around live interruption handling.

Preferred shape:

```csharp
await using var scope = await _liveInterruptionState.BeginHandlingAsync(context, cancellationToken);

try
{
    var outcome = await HandleYieldedUtteranceCoreAsync(context, cancellationToken);
    await ResolveOutcomeAsync(outcome, context, cancellationToken);
    await scope.CompleteAsync(outcome, cancellationToken);
    return outcome;
}
catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
{
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Live interruption handling failed. CaptureId={CaptureId} TurnId={TurnId}", context.CaptureId, context.TurnId);
    var recovery = await RecoverLiveInterruptionAsync(context, ex, CancellationToken.None);
    await scope.CompleteAsync(recovery, CancellationToken.None);
    return recovery;
}
finally
{
    await _liveInterruptionState.EnsureNotStuckAsync(context, CancellationToken.None);
}
```

Adapt names to the existing project style.

The important behavior is:

```text
try -> resolve outcome
catch -> recover outcome
finally -> enforce no stuck handling state
```

The finalizer must not depend on the original request cancellation token if that token was cancelled by interruption cleanup. Use a bounded cleanup token or `CancellationToken.None` for last-resort state cleanup.

## Step 2 - Replace stale PR7 deferred outcome with executable mapping

Find the branch that logs:

```text
Ask-user-to-clarify is not executed live in PR7
```

Replace it with a real decision map.

Suggested mapping:

```text
If CI strategy is ClarifyThenRecomposeFromCheckpoint:
  route to existing PR10 sequential clarification + recomposition executor.

If CI strategy is AskUserToClarifyInterruption:
  route to new unclear-interruption executor, not PR10 factual clarification.

If LiveGate decision is AskClarification but CI classifier says Noise/Backchannel/Empty:
  resume hold / ignore and cleanup.

If LiveGate decision is AskClarification but transcript is too short or fragment-like:
  safe ignore/resume unless strong directed-at-Merlin markers exist.

If LiveGate decision is AskClarification and no active answer/recent yielded context exists:
  skip CI and allow normal idle request handling or safe idle cleanup.
```

Do not allow:

```text
return Deferred without cleanup
return Handled=false while suppressing resume
return AskClarification with no executor
```

## Step 3 - Add explicit `UnclearInterruption` executor

Add a small executor for `AskUserToClarifyInterruption`.

Behavior:

```text
1. Resolve provisional hold intentionally.
2. Do not route the raw transcript as a command.
3. Do not call DeepInfra unless needed by existing design.
4. Request ResponsiveFeedback unclear-interruption bridge/prompt.
5. Speak one short question through the normal speech service.
6. Move live state to AwaitingInterruptionClarification or equivalent.
7. Store pending clarification context with timeout.
8. If feedback/speech fails, recover to listening/idle or resume hold depending on safety.
```

Suggested pending context:

```csharp
public sealed class PendingInterruptionClarification
{
    public string Id { get; init; } = "";
    public string CaptureId { get; init; } = "";
    public string OriginalTurnId { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public string OriginalUserQuestion { get; init; } = "";
    public string? LastCompletedSentence { get; init; }
    public string? DiscardedPartialSentence { get; init; }
    public string AmbiguousTranscript { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public string? HoldId { get; init; }
    public bool CanResumeOriginalAnswer { get; init; }
}
```

If an equivalent pending clarification model already exists, extend it instead of creating another one.

Recommended timeout:

```text
5-10 seconds for live voice clarification context
```

Timeout behavior:

```text
If original held playback is still resumable:
  resume it and clear state.
Else:
  clear state and return to listening/idle.
```

## Step 4 - Route the next user utterance while awaiting clarification

When state is awaiting interruption clarification, the next yielded/idle transcript should be handled as the answer to Merlin's clarification question, not as an unrelated command by default.

Simple first version:

```text
If user says stop/cancel:
  cancel pending clarification and active turn.

If user gives correction-like content:
  cancel original and route corrected request.

If user says continue/keep going:
  resume original answer if safe, else recompose from checkpoint.

If user provides substantive content:
  treat it as additional interruption context and run PR10 clarification/recomposition or correction route depending on classifier.

If timeout expires before next transcript:
  clear pending clarification and recover.
```

Avoid over-engineering. The key is that the `AwaitingInterruptionClarification` state has an exit.

## Step 5 - Harden fragment/noise handling before asking clarification

The transcript in the observed log was:

```text
"in the pool.."
```

That should usually not cause a spoken clarification prompt by itself.

Add conservative fragment gating before `AskUserToClarifyInterruption` is spoken.

Treat as fragment/noise/ignore-resume when all or most are true:

```text
- very short transcript
- no wake/name/direct-address marker
- no correction marker
- no explicit question marker
- no stop/playback-control marker
- no clear reference to current answer
- low semantic confidence
- transcript resembles TV/random pickup
```

Examples that should normally ignore/resume during active answer:

```text
"in the pool"
"this video"
"over there"
"that one"
"yeah"
"mhm"
```

Examples that may ask unclear-interruption question:

```text
"no wait"
"no what I mean is"
"actually hold on"
"that's not what"
"wait, change"
```

Examples that should not ask unclear-interruption question because they are executable:

```text
"stop" -> StopPlayback
"no, I meant what is X" -> CancelAndRedirect
"but the water itself too, right" -> ClarifyThenRecomposeFromCheckpoint
"what do you mean by liner" -> ClarifyThenRecomposeFromCheckpoint
```

## Step 6 - Make resume suppression safe

Any code path that emits/logs:

```text
PlaybackResumeSuppressedByGate
```

must be paired with an executable semantic owner.

Add a guard:

```text
If resume is suppressed by gate:
  outcome must be one of:
    StopPlayback
    CancelAndRedirect
    ClarifyThenRecomposeFromCheckpoint
    LocalBridgeAndRecomposeFromCheckpoint
    QueueFollowUpAfterCurrent
    AskUserToClarifyInterruption with pending state created

If outcome is unsupported/deferred/unknown:
  do not suppress resume; resume hold or perform safe cleanup.
```

This is critical. Suppressing resume without an executable replacement path causes the wedge.

## Step 7 - Guarantee provisional hold resolution

Audit hold resolution for all outcomes.

Required mapping:

```text
Noise / Empty / Backchannel:
  Resume hold.

Stop / Cancel:
  Flush hold.

Correction / Redirect:
  Flush hold before replacement route.

Clarification / RelatedFollowUp:
  Flush hold before generated clarification + recomposed continuation.

SideComment / AdditionalContext:
  Flush hold before bridge/recomposition.

AskUserToClarifyInterruption:
  Prefer keep hold only if the clarification question is expected to be answered quickly and hold timeout is safe.
  Otherwise flush or explicitly pause with timeout.
  In all cases, record hold id and guarantee timeout recovery.

Unknown / Deferred / Failure:
  Resume hold if safe; otherwise flush and clear state.
```

If the hold is missing, log it and continue safe cleanup.

Do not throw because a hold id is missing.

## Step 8 - Add stale-state watchdog

Add a recovery watchdog as a seatbelt, not as the main fix.

Rule:

```text
If InterruptionState=handling for more than N seconds
and no active handler task exists
and no active interruption speech exists
and no active provisional hold exists
and no pending awaited clarification exists,
then force recovery to listening/idle and log a high-severity diagnostic.
```

Suggested threshold:

```text
3-5 seconds in development
8-10 seconds in production
```

Make threshold configurable.

Suggested option:

```csharp
public int LiveInterruptionHandlingStaleTimeoutMs { get; set; } = 8000;
public bool EnableLiveInterruptionStaleRecovery { get; set; } = true;
```

## Step 9 - Improve diagnostics

Add high-level logs for this fix.

Required logs:

```text
LiveInterruptionHandlingStarted
LiveInterruptionDecisionMapped
LiveInterruptionOutcomeExecuting
LiveInterruptionOutcomeResolved
LiveInterruptionCleanupCompleted
LiveInterruptionCleanupForcedRecovery
AskClarificationMappedToUnclearPrompt
AskClarificationMappedToPr10Recomposition
AskClarificationIgnoredAsFragment
AskClarificationFallbackResume
ProvisionalHoldResolvedForInterruptionOutcome
InterruptionStateCleared
```

Each should include where available:

```text
CaptureId
TurnId
ResolvedActiveTurnId
ObservedTurnId
CorrelationId
GateDecision
CIType
CIStrategy
OutcomeKind
HoldId
WasResumeSuppressed
WasSemanticRoutingSuppressed
CheckpointAvailable
PendingClarificationId
FinalBaseState
FinalInterruptionState
```

Do not spam frame-level acoustic logs.

---

# Suggested Outcome Model

If current outcome models are too loose, add explicit fields.

Example:

```csharp
public sealed class LiveInterruptionResolvedOutcome
{
    public string CaptureId { get; init; } = "";
    public string TurnId { get; init; } = "";

    public LiveInterruptionOutcomeKind Kind { get; init; }

    public bool WasEvaluated { get; init; }
    public bool WasHandled { get; init; }
    public bool SuppressLegacySemanticRouting { get; init; }
    public bool AllowLegacyCleanup { get; init; }

    public bool ResumePlayback { get; init; }
    public bool FlushPlayback { get; init; }
    public bool CancelOriginalTurn { get; init; }
    public bool RequiresPendingClarificationState { get; init; }
    public bool CleanupCompleted { get; init; }

    public string Reason { get; init; } = "";
}
```

Example enum:

```csharp
public enum LiveInterruptionOutcomeKind
{
    None = 0,
    IgnoredNoise,
    BackchannelResume,
    StopCancelled,
    ReplacementRouted,
    ClarificationAndRecompositionStarted,
    SideCommentRecompositionStarted,
    FollowUpQueued,
    UnclearPromptAsked,
    FallbackResumed,
    FallbackClearedToIdle,
    FailedRecovered
}
```

This is only needed if the current outcome model does not make terminal state explicit.

---

# Decision Mapping Table

| Input decision / strategy | Required live behavior | Hold behavior | Legacy semantic routing | Final state |
|---|---|---|---|---|
| Empty / useless | ignore | resume | suppress | speaking/listening |
| NoiseOrFalsePositive | ignore | resume | suppress | speaking/listening |
| Backchannel | continue | resume | suppress | speaking |
| StopRequest / StopPlayback | stop + optional stop confirmation | flush | suppress | listening/idle |
| CancelRequest | cancel + cleanup | flush | suppress | listening/idle |
| Correction / CancelAndRedirect | route replacement once | flush | suppress | processing/speaking/listening |
| ClarificationQuestion | PR10 clarification + recomposition | flush | suppress | speaking/listening |
| RelatedFollowUpQuestion | PR10 clarification + recomposition | flush | suppress | speaking/listening |
| SideComment / AdditionalContext | bridge + recompose | flush | suppress | speaking/listening |
| QueueFollowUpAfterCurrent | queue + bridge; continue/recompose | usually resume or recompose | suppress | speaking/listening |
| AskUserToClarifyInterruption | ask unclear prompt; await answer | explicit pause/flush/resume timeout | suppress | awaiting clarification |
| Unknown low-confidence fragment | ignore/resume fallback | resume | suppress | speaking/listening |
| Unknown high-confidence directed at Merlin | unclear prompt or safe clarification | explicit | suppress | awaiting clarification/listening |
| Unsupported/deferred | fallback recovery | resume if safe else flush | do not suppress unless handled | valid non-handling state |

---

# State Invariants

Add assertions/tests around these invariants.

## Invariant 1

```text
InterruptionState=handling may only exist while there is an active handler operation.
```

## Invariant 2

```text
If InterruptionState=handling exits, it must transition to exactly one valid next state.
```

Valid examples:

```text
IdleListening
CapturingUserSpeech
ProcessingTurn
Speaking
AwaitingInterruptionClarification
Completed
Cancelled
FailedRecovered
```

Use actual project enum names.

## Invariant 3

```text
A provisional hold must never be left unresolved by a terminal interruption outcome.
```

## Invariant 4

```text
If playback resume is suppressed, another owner must own recovery.
```

## Invariant 5

```text
A final answer TTS cancellation must not by itself leave the live turn in thinking/handling.
```

## Invariant 6

```text
Legacy cleanup may not cancel newly queued interruption-owned speech after CI already handled the outcome.
```

This protects the stop-confirmation style bug from reappearing for clarification prompts or recomposed continuation.

---

# Test Plan

## Unit tests: decision mapping

Add or update tests around the live yielded-utterance mapper.

Test cases:

```text
AskClarification + classifier ClarificationQuestion -> PR10 sequential clarification outcome
AskClarification + classifier RelatedFollowUpQuestion -> PR10 sequential clarification outcome
AskClarification + classifier SideComment -> bridge/recompose outcome
AskClarification + classifier NoiseOrFalsePositive -> ignore/resume outcome
AskClarification + short fragment "in the pool" -> ignore/resume outcome unless explicit directed markers exist
AskUserToClarifyInterruption -> unclear prompt outcome with pending clarification state
Unsupported deferred clarification -> fallback resume/cleanup, not stuck
```

## Unit tests: cleanup finalizer

Test:

```text
handler succeeds -> InterruptionState cleared
handler throws -> recovery runs and InterruptionState cleared
handler cancellation after stop_current_requested -> cleanup still runs
cleanup with missing hold id -> no throw, state cleared
resume suppressed but no executable outcome -> fallback resume/cleanup
```

## Unit tests: provisional hold resolution

Test:

```text
noise resumes hold
backchannel resumes hold
short fragment resumes hold
clarification flushes hold before generated clarification
side-comment flushes hold before recompose
unclear prompt creates pending state and resolves hold explicitly
failure resumes hold if safe
failure flushes if hold not resumable
```

## Unit tests: awaiting clarification state

Test:

```text
AskUserToClarifyInterruption creates pending clarification context
next utterance "keep going" resumes/recomposes original answer
next utterance "no I meant X" routes replacement
next utterance with substantive detail runs PR10 recomposition
next utterance "stop" cancels and clears pending clarification
pending clarification timeout recovers safely
```

## Integration/regression tests

Add a regression matching the observed bug.

### Regression 1: exact dead-end prevention

Given:

```text
- Merlin is speaking a final answer.
- Floor-yield/provisional hold starts.
- STT transcript: "in the pool"
- LiveGate returns AskClarification.
- Resume would previously be suppressed.
```

Expected:

```text
- No log/reason says "not executed live in PR7".
- No permanent InterruptionState=handling.
- Hold is resumed or safely cleaned.
- Final state is speaking/listening/idle, not thinking+handling.
- Timeline completion marker is emitted.
```

### Regression 2: real clarification still works

Given:

```text
- Merlin is explaining pool water color.
- User says: "But the water itself too, right?"
```

Expected:

```text
- CI owns the utterance.
- Old playback is not raw-resumed mid-sentence.
- Clarification model call runs if enabled.
- Clarification speech is queued/spoken.
- Continuation recomposition is queued/spoken.
- Legacy semantic routing is suppressed.
- Final state recovers cleanly.
```

### Regression 3: unclear directed correction prefix

Given:

```text
- Merlin is speaking.
- User says: "No, what I meant is"
```

Expected:

```text
- Does not route malformed command.
- Does not wedge.
- Either asks unclear prompt and awaits clarification, or safely holds briefly then recovers.
```

### Regression 4: exception during clarification model call

Given:

```text
- Classifier chooses ClarifyThenRecomposeFromCheckpoint.
- Clarification model port throws.
```

Expected:

```text
- No stuck handling state.
- Hold resolved.
- Either ResponsiveFeedback wait/recover phrase or silent cleanup.
- Final state valid.
```

### Regression 5: legacy cleanup does not kill interruption prompt

Given:

```text
- AskUserToClarifyInterruption queues unclear prompt speech.
- Legacy cleanup path runs afterward.
```

Expected:

```text
- Legacy cleanup does not clear/cancel the newly queued interruption-owned speech.
- Prompt either plays or is intentionally skipped with cleanup logged.
```

---

# Manual Test Script

Use a long spoken answer so interruption happens mid-answer.

Suggested prompt:

```text
Merlin, explain why swimming pool water often looks blue, including the liner, depth, sunlight, and water clarity.
```

Manual cases:

## Case 1: random fragment

While Merlin speaks, say:

```text
in the pool
```

Expected:

```text
Merlin should not wedge.
Most likely it should resume or continue.
It should not remain thinking/handling.
```

## Case 2: real related clarification

While Merlin speaks, say:

```text
But the water itself too, right?
```

Expected:

```text
Merlin should briefly answer that point and continue naturally.
It should not resume the cut-off partial sentence.
```

## Case 3: correction prefix

While Merlin speaks, say:

```text
No, what I meant is
```

Expected:

```text
Merlin should not route "what I meant is" as a command.
It should ask a short clarification or safely recover.
```

## Case 4: stop

While Merlin speaks, say:

```text
Stop.
```

Expected:

```text
Merlin stops, optionally gives stop confirmation, and returns to listening/idle.
```

## Case 5: failed model simulation

Temporarily force the interruption model port to fail during PR10 clarification.

Expected:

```text
Merlin recovers cleanly without getting stuck in handling.
```

---

# Config Requirements

Use existing config flags where possible.

Check these options:

```json
"InterruptionHandling": {
  "Enabled": true,
  "EnableLiveBargeInIntegration": true,
  "EnableLiveShadowMode": false,
  "EnableLiveMinimalBehavior": true,
  "EnableLivePlaybackActions": true,
  "EnableLiveRedirectRouting": true,
  "EnableLiveResponsiveFeedbackBridge": true,
  "EnableLiveModelCalls": true,
  "EnableClarificationCalls": true,
  "EnableContinuationRecomposition": true,
  "EnableLiveInterruptionStaleRecovery": true,
  "LiveInterruptionHandlingStaleTimeoutMs": 8000
}
```

Do not blindly add options if equivalent ones already exist.

Important behavior:

```text
If model calls are disabled, clarification/recomposition must degrade gracefully.
Disabled model calls must not produce a stuck AskClarification branch.
```

Fallback when model calls disabled:

```text
- related real clarification -> bridge/recompose disabled fallback OR safe resume/cleanup
- AskUserToClarifyInterruption -> ResponsiveFeedback unclear prompt if enabled
- if ResponsiveFeedback disabled too -> silent cleanup/resume/idle
```

---

# Acceptance Criteria

The PR is acceptable when:

```text
1. The exact stale PR7 log branch is removed or no longer reachable in live mode.
2. AskClarification never leaves InterruptionState=handling without an active pending state.
3. PlaybackResumeSuppressedByGate cannot suppress resume unless a terminal CI owner exists.
4. Provisional holds are always resolved by terminal outcomes.
5. Random short fragments during speech do not wedge Merlin.
6. Real related clarifications still run PR10 clarification + recomposition.
7. Unclear directed interruptions can ask one short ResponsiveFeedback-based question and await the user's answer.
8. Timeout or failure while awaiting clarification recovers to a valid state.
9. Exceptions in interruption handling do not leave UI state as thinking/handling.
10. Legacy cleanup does not cancel newly queued interruption-owned speech.
11. Full backend tests pass.
12. New regression tests prove the observed failure cannot recur.
```

Hard fail if any test can produce:

```text
BaseState: thinking
InterruptionState: handling
```

with no active handler, hold, speech, model call, or pending clarification.

---

# Recommended Agent Prompt

Use this prompt for the coding agent:

```text
You are working in the Merlin repository.

Task: implement ConversationalInterruption PR 10.4 - AskClarification Live Dead-End Recovery.

Problem:
A live voice log showed Merlin captured audio, completed STT, mapped a yielded utterance to AskClarification, logged "Ask-user-to-clarify is not executed live in PR7", suppressed playback resume, cancelled/interrupted final answer speech, and then remained stuck in BaseState=thinking with InterruptionState=handling. This must become impossible.

Read first:
- Merlin.ToDo/interruption_intelligence/merlin_conversational_interruption_redesign_v2.md
- Merlin.ToDo/responsive_feedback/merlin_responsive_feedback_migration_plan_v2.md, if present
- Current code around LiveUtteranceGate, LiveInterruptionIntegrationService, InterruptionOrchestrator, BargeInCoordinator, FloorYieldController, AssistantSpeechPlaybackService, LiveAssistantTurnService, SpokenAnswerTracker, ResponsiveFeedbackOrchestrator, and InterruptionFeedbackAdapter.

Search the codebase for:
- Ask-user-to-clarify is not executed live in PR7
- AskClarification
- AskUserToClarifyInterruption
- PlaybackResumeSuppressedByGate
- interruption_handling_started
- InterruptionState
- EnableLiveMinimalBehavior
- EnableLiveModelCalls
- ClarifyThenRecomposeFromCheckpoint

Implement the smallest complete fix that guarantees every live yielded-utterance decision has a terminal outcome.

Required behavior:
1. Replace any stale/deferred PR7 AskClarification live branch with executable mapping.
2. If the utterance is a real clarification/follow-up, route to the existing PR10 sequential clarification + recomposition path.
3. If the utterance is an unclear directed interruption, ask one short ResponsiveFeedback-based unclear prompt and enter an explicit awaiting-clarification state with timeout/recovery.
4. If the utterance is a short fragment/noise such as "in the pool", ignore/resume/cleanup instead of asking clarification.
5. If model calls or feedback are disabled, degrade gracefully and never wedge.
6. If resume is suppressed by the gate, some other owner must resolve the hold/state. Otherwise resume must not be suppressed.
7. Add a finalization/recovery guard so exceptions/cancellations during interruption handling always clear or transition InterruptionState safely.
8. Ensure provisional audio holds are always resolved: resume for noise/backchannel/fragment, flush for stop/correction/clarification/recompose, explicit timeout behavior for awaiting clarification.
9. Add a stale handling watchdog as a seatbelt.
10. Do not change VAD/AEC/STT/self-echo/acoustic thresholds in this PR.
11. Do not create a second hard-coded bridge phrase library. Use ResponsiveFeedback for unclear/recompose/queue bridge phrases where speech is needed.
12. Do not allow legacy cleanup to cancel newly queued interruption-owned speech after CI has handled an outcome.

Required tests:
- AskClarification + "in the pool" while speaking does not wedge; state returns to speaking/listening/idle and hold is resolved.
- AskClarification + real related follow-up routes to PR10 clarification + recomposition.
- AskUserToClarifyInterruption creates pending clarification state and timeout recovery.
- Next utterance while awaiting clarification can stop, correct, continue, or provide substantive context.
- Handler exception still clears InterruptionState.
- Resume suppressed without executable semantic outcome falls back to resume/cleanup.
- Missing hold id does not throw or wedge.
- Legacy cleanup does not kill interruption-owned unclear prompt/clarification speech.
- Full backend test suite passes.

Add useful structured logs with CaptureId, TurnId, CorrelationId, gate decision, CI strategy, outcome kind, hold id, cleanup result, and final live state.

Do not mark this done until the observed stuck state is covered by a regression test.
```

---

# Final Note

This PR should be treated as a reliability fix, not a UX enhancement.

The important standard is:

```text
Merlin may ignore an ambiguous interruption.
Merlin may ask for clarification.
Merlin may recompose.
Merlin may stop.
But Merlin may never get stuck because a selected live decision has no executable path.
```
