---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/always-on-interruption-and-live-utterance-routing.md
related_features:
  - Voice Interruption System
status: current
ready_for_agent: true
---

## Plan Status

Status: current
Ready for agent use: yes
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: [[Voice Interruption System]]
Related architecture: [[Voice Pipeline Architecture]]
Related code atlas: [[AssistantSpeechPlaybackService]]
Original source: `Merlin.ToDo/always-on-interruption-and-live-utterance-routing.md`

# Always-On Interruption And Live Utterance Routing Plan

## Goal

Merlin currently has an interruption system that mostly behaves like a TTS/barge-in feature: it is strongest while Merlin is speaking. The next architecture step is to extend that same interruption foundation so Merlin can hear meaningful user speech during every active flow state, including silent processing, local intent routing, tool planning, awaiting tool execution, and tool execution.

The goal is not to make every utterance immediately kill the active task. The goal is:

```text
Always listen.
Never blindly interrupt.
Always route.
```

Merlin should not have silent dead zones where the user can speak but the speech is ignored.

## Core User Problem

Example:

```text
User: open facebook
User, immediately after: sorry, I meant Google, can you open Google?
```

In the current architecture this may fail because Merlin may already have closed the original capture and may be silently processing, planning, or executing. The second sentence may not be captured as meaningful input unless Merlin is speaking and the existing barge-in logic is active.

This creates the bad UX feeling:

```text
I can talk, but Merlin is not really listening.
```

For a natural assistant, that should basically never happen.

## High-Level Architecture Change

Do not replace the current interruption logic. Extend it.

The current model is probably close to:

```text
if assistantIsSpeaking:
    enable interruption detection
else:
    use normal idle listening / maybe no interruption capture
```

The new model should become:

```text
if assistantIsSpeaking
or activeTurnIsProcessing
or toolIsPlanning
or toolIsExecuting
or awaitingToolCommit:
    enable live utterance monitor
```

The same underlying pieces should be reused where possible:

```text
VAD detects user speech
→ capture audio
→ transcribe
→ create UserUtterance event
→ classify utterance relationship to active flow
→ route it
```

The important change is where this system is allowed to run, and what happens after it captures speech.

## Key Principle: Separate Speech Intake From Turn Ownership

Audio capture and turn ownership should not be the same concern.

### Speech Intake Layer

Responsible for:

- Receiving microphone frames.
- Running VAD / speech detection.
- Maintaining a small ring buffer so the beginning of speech is not clipped.
- Capturing utterance audio segments.
- Running STT only when speech is detected.
- Emitting `UserUtterance` events.

### Conversation / Flow Layer

Responsible for:

- Knowing the current active turn.
- Knowing whether Merlin is idle, processing, planning, speaking, or executing.
- Deciding whether new user speech cancels, replaces, pauses, extends, queues behind, or starts a new request.

The speech intake layer should be capable of creating utterance events even if the conversation layer is busy.

## Important GPU / Resource Constraint

Always-on listening must not mean Whisper is constantly transcribing on the GPU.

The intended pipeline is:

```text
Always-on mic intake:
  CPU audio capture
  CPU RMS / noise-floor tracking
  CPU VAD / lightweight speech gate
  ring buffer of recent audio

Only when speech is detected:
  freeze utterance audio segment
  send segment to Whisper / STT
```

This keeps the always-on part cheap.

Chatterbox TTS and Whisper STT may both use CUDA. They should not fight for GPU resources without coordination.

Introduce or reuse a simple GPU work scheduler:

```text
Whisper STT job → GPU scheduler
Chatterbox TTS job → GPU scheduler
```

Priority rule:

```text
Highest priority: interruption STT
Medium priority: normal STT
Lower priority: TTS generation
```

When user speech starts during TTS:

```text
1. Stop audio playback immediately.
2. Cancel or skip future TTS chunks if possible.
3. Give STT priority for the interruption transcript.
4. Route the new utterance.
```

## Required System States

Add or formalize these states. Names may be adjusted to fit the current codebase, but the concepts should exist.

```text
IdleListening
CapturingUserSpeech
Interpreting
ProcessingTurn
PlanningTool
AwaitingToolCommit
ExecutingTool
Speaking
PausedByUser
Interrupted
Completed
Failed
```

Important: these are conversation/task states, not mic states.

The mic intake should remain available in all active states unless there is a specific protected section.

## UserUtterance Event

Every meaningful captured speech segment should become a structured event.

Example:

```json
{
  "type": "user_utterance",
  "text": "sorry I meant Google, can you open Google?",
  "timestampUtc": "2026-06-19T10:44:00Z",
  "activeTurnId": "turn-open-facebook-123",
  "stateWhenCaptured": "PlanningTool",
  "assistantWasSpeaking": false,
  "speechSource": "live_utterance_monitor",
  "confidence": 0.82
}
```

The event should be logged even if routing is still primitive.

First milestone: no speech disappears. Even before Merlin handles every case perfectly, the logs should show that the user spoke and how Merlin classified it.

## Interruption Router

Create or extend a router that receives every `UserUtterance` captured during an active flow.

Initial categories:

```text
PauseAndClarify
CancelActiveTurn
ReplaceActiveTurn
AddToActiveTurn
QueueAfterActiveTurn
StatusQuestion
BackgroundOrNoOp
Unknown
```

Start rule-based. Do not overbuild with complex AI at first.

### Phrase Examples

#### PauseAndClarify

```text
stop
wait
hold on
pause
hang on
```

#### CancelActiveTurn

```text
cancel that
never mind
forget it
don't do that
stop doing that
abort
```

#### ReplaceActiveTurn

```text
actually Google
I mean Google
I meant Google
sorry I meant Google
no, open Google
not Facebook, Google
open Google instead
```

#### AddToActiveTurn

```text
also open YouTube
and open YouTube too
include YouTube as well
```

#### QueueAfterActiveTurn

```text
after that open Discord
then open Discord
when you are done, open Discord
```

#### StatusQuestion

```text
what are you doing?
what are you working on?
did you hear me?
are you still opening Facebook?
```

#### BackgroundOrNoOp

```text
thanks
okay
random low-confidence background speech
uncertain TV-like audio
```

## Stop Behavior Must Be State-Aware

Plain `stop` should not mean the same thing in every state.

### Rule

```text
If Merlin is Speaking:
  "stop" = immediately stop TTS/playback
  no spoken confirmation

If Merlin is AwaitingToolCommit / PlanningTool:
  "stop" = pause before action
  ask contextual confirmation if needed

If Merlin is ExecutingTool:
  "stop" = attempt pause/cancel if possible
  ask only if there is ambiguity or a safe undo choice

If Merlin is Processing silently:
  "stop" = pause/cancel pending response quietly or with a very short acknowledgement
```

### Why

If Merlin is giving a long spoken answer and the user says `stop`, Merlin should stop talking immediately. It should not say: `Do you want me to stop?` because that violates the user's request.

During speech:

```text
User: stop
Merlin: [immediately stops speaking]
```

No confirmation.

During a pending tool action:

```text
User: open Facebook
Merlin: [planning / awaiting commit]
User: stop
Merlin: Do you want me to stop and not open Facebook?
```

During an already executed tool action, future compensation can be added later:

```text
User: stop
Merlin: Facebook is already open. Do you want me to close it?
```

But closing/rollback is not part of this first milestone.

## Stop Versus Cancel

The assistant should distinguish between pause language and final cancellation language.

```text
"stop" while speaking = stop output
"stop" before action = pause and clarify
"wait" / "hold on" = pause active task
"cancel that" = cancel active task
"never mind" = cancel active task
"don't do that" = cancel active task
```

Core principle:

```text
Immediate pause/stop first.
Final cancellation only when clear or confirmed.
```

## Pending Command State

For side-effect tools, introduce a lightweight pending command concept.

States:

```text
Captured
Interpreting
Planned
AwaitingCommit
Executing
Executed
Completed
Superseded
Cancelled
Failed
```

Correction behavior:

```text
If state = Captured / Interpreting / Planned:
  replace old command

If state = AwaitingCommit:
  replace old command

If state = Executing:
  attempt cancel if supported, otherwise route as follow-up / future compensation

If state = Executed / Completed:
  do not undo yet in this milestone
  optionally perform corrected request as follow-up
```

## Tool Commit Gate

Add a tiny grace window before executing small side-effect tools.

Example:

```text
User: open Facebook
Merlin: creates pending OpenUrlTool(facebook.com)
State: AwaitingCommit for 300–700 ms
User: sorry I meant Google
Router: ReplaceActiveTurn
Action: cancel pending Facebook, create pending Google, execute Google
```

Suggested defaults:

```text
Safe read-only request: no delay
Small external side effect: 300–700 ms delay
Risky action: explicit confirmation
```

Examples:

```text
What time is it?        → immediate
Tell me a joke          → immediate
Open Facebook           → short correction window
Open Discord            → short correction window
Send this WhatsApp      → explicit confirmation
Delete a file           → explicit confirmation or reversible workflow
Run terminal command    → explicit confirmation unless whitelisted
```

Do not overdo this. The correction window should be short enough that the assistant still feels fast.

## Initial Scope

Implement now:

- Extend current interruption/live capture logic so it runs during silent active states, not only during TTS playback.
- Add/propagate active turn state into logs.
- Emit `UserUtterance` events during active flows.
- Add simple router categories.
- Implement state-aware `stop` behavior.
- Add cancellation token checks before major pipeline steps.
- Add a lightweight pending-command grace window for small side-effect tools such as `OpenUrlTool`.
- Add tests/log verification for no-lost-speech scenarios.

Do not implement yet:

- Closing wrong browser tabs.
- Browser tab ownership.
- Full rollback/compensation system.
- Complex task queues.
- Multi-agent task management.
- Advanced LLM-based correction classification.
- Permanent memory or preference learning.

Those are later steps after reliable always-on utterance capture exists.

## Cancellation Token Requirements

Each active turn should have a cancellation token or equivalent cancellation handle.

Check cancellation before:

```text
calling LLM
starting local intent execution
executing tool
entering AwaitingCommit
committing side-effect tool
starting TTS
continuing TTS chunks
```

If cancellation is requested:

```text
log cancellation reason
mark active turn as Cancelled / Superseded / PausedByUser
stop further side effects
avoid starting new TTS unless the route requires a tiny acknowledgement or clarification
```

## TTS Chunking Requirement

If possible, TTS should be generated and played in chunks rather than as one giant audio job.

Bad:

```text
generate full 30-second response
play it
```

Better:

```text
generate sentence/chunk 1
play it
maybe generate chunk 2
if interruption happens:
  stop playback
  cancel/skip remaining chunks
```

For this milestone, do not rewrite all TTS if that is too large. But ensure the interruption path can stop playback immediately and prevent future chunks from being played.

## Logging Requirements

Add explicit logs for these events:

```text
LiveUtteranceMonitorStarted
LiveUtteranceMonitorActiveState
UserSpeechDetectedDuringState
UserUtteranceCaptured
UserUtteranceRouted
ActiveTurnPaused
ActiveTurnCancelled
ActiveTurnSuperseded
PendingCommandCreated
PendingCommandAwaitingCommit
PendingCommandCommitted
PendingCommandCancelledBeforeCommit
TtsStoppedByUser
GpuJobQueued
GpuJobStarted
GpuJobCompleted
GpuJobCancelled
```

Example log payload:

```json
{
  "event": "UserUtteranceRouted",
  "text": "sorry I meant google",
  "activeTurnId": "turn-open-facebook-123",
  "stateWhenCaptured": "AwaitingToolCommit",
  "route": "ReplaceActiveTurn",
  "confidence": 0.88,
  "action": "CancelPendingCommandAndStartReplacement"
}
```

## Acceptance Tests

### Test 1: Speech During Silent Processing Is Captured

Steps:

```text
1. Trigger a slow request.
2. While Merlin is silently processing, say: stop.
```

Expected:

```text
- Logs show speech detected during ProcessingTurn.
- Transcript is captured.
- Route is PauseAndClarify or CancelActiveTurn depending on phrase.
- Original turn does not proceed to TTS if cancellation was final.
```

### Test 2: Correction Before Tool Execution

Steps:

```text
1. Say: open facebook.
2. Immediately say: sorry I meant google.
```

Expected:

```text
- First request creates pending OpenUrlTool(facebook.com).
- Second utterance is captured during PlanningTool or AwaitingToolCommit.
- Second utterance is routed as ReplaceActiveTurn.
- Facebook pending command is cancelled before commit if still pending.
- Google command starts.
```

### Test 3: Stop During Long TTS

Steps:

```text
1. Ask Merlin for a long answer.
2. While Merlin is speaking, say: stop.
```

Expected:

```text
- TTS stops immediately.
- Merlin does not ask for confirmation.
- Remaining TTS chunks are not played.
- Event is logged as TtsStoppedByUser.
```

### Test 4: Stop Before Opening Website

Steps:

```text
1. Say: open facebook.
2. During AwaitingCommit, say: stop.
```

Expected:

```text
- Pending command pauses.
- Merlin asks: Do you want me to stop and not open Facebook?
- If user says yes, command is cancelled.
- If user says no/continue, command resumes.
- If user says open Google instead, command is replaced.
```

### Test 5: Explicit Cancel Before Opening Website

Steps:

```text
1. Say: open facebook.
2. Say: cancel that.
```

Expected:

```text
- Pending command is cancelled without extra confirmation.
- Facebook does not open if not already committed.
```

### Test 6: New Request During Active Flow

Steps:

```text
1. Ask a slow question.
2. While processing, say: after that open Discord.
```

Expected:

```text
- Utterance is captured.
- Route is QueueAfterActiveTurn.
- If queueing is not implemented yet, log NotImplementedQueue and do not lose the utterance.
```

### Test 7: GPU Scheduling Does Not Collide Badly

Steps:

```text
1. Trigger TTS generation.
2. Interrupt while TTS is active.
```

Expected:

```text
- Playback stops immediately.
- Remaining TTS work is cancelled/skipped where possible.
- Interruption STT gets priority.
- Logs show GPU job ordering.
```

## Suggested Class / Component Names

Use names that fit the current codebase. These are suggestions:

```text
LiveUtteranceMonitor
SpeechIntakeCoordinator
InterruptionRouter
ActiveTurnManager
PendingCommandGate
GpuWorkScheduler
UserUtterance
UtteranceRouteDecision
TurnState
PendingCommandState
```

If the existing code has services with similar responsibilities, extend those instead of creating duplicate systems.

## Suggested C# Shapes

These are conceptual. Adapt to the actual architecture.

```csharp
public enum MerlinState
{
    IdleListening,
    CapturingUserSpeech,
    Interpreting,
    ProcessingTurn,
    PlanningTool,
    AwaitingToolCommit,
    ExecutingTool,
    Speaking,
    PausedByUser,
    Interrupted,
    Completed,
    Failed
}
```

```csharp
public enum UtteranceRouteKind
{
    PauseAndClarify,
    CancelActiveTurn,
    ReplaceActiveTurn,
    AddToActiveTurn,
    QueueAfterActiveTurn,
    StatusQuestion,
    BackgroundOrNoOp,
    Unknown
}
```

```csharp
public sealed class UserUtterance
{
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; init; }
    public string? ActiveTurnId { get; init; }
    public MerlinState StateWhenCaptured { get; init; }
    public bool AssistantWasSpeaking { get; init; }
    public double? Confidence { get; init; }
    public string Source { get; init; } = "live_utterance_monitor";
}
```

```csharp
public sealed class UtteranceRouteDecision
{
    public UtteranceRouteKind Kind { get; init; }
    public double Confidence { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ReplacementText { get; init; }
}
```

```csharp
public sealed class ActiveTurn
{
    public string TurnId { get; init; } = Guid.NewGuid().ToString("N");
    public string OriginalText { get; init; } = string.Empty;
    public MerlinState State { get; set; }
    public CancellationTokenSource Cancellation { get; } = new();
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
```

```csharp
public enum StopDecision
{
    StopSpeechOnlyNoConfirmation,
    PauseAndConfirmCancel,
    TryCancelThenClarifyIfNeeded,
    CancelPendingResponseQuietly,
    NoActiveTask
}
```

```csharp
public StopDecision DecideStopBehavior(MerlinState state)
{
    return state switch
    {
        MerlinState.Speaking => StopDecision.StopSpeechOnlyNoConfirmation,
        MerlinState.AwaitingToolCommit => StopDecision.PauseAndConfirmCancel,
        MerlinState.PlanningTool => StopDecision.PauseAndConfirmCancel,
        MerlinState.ExecutingTool => StopDecision.TryCancelThenClarifyIfNeeded,
        MerlinState.ProcessingTurn => StopDecision.CancelPendingResponseQuietly,
        _ => StopDecision.NoActiveTask
    };
}
```

```csharp
public sealed class GpuWorkScheduler
{
    private readonly SemaphoreSlim _gpu = new(1, 1);

    public async Task<T> RunAsync<T>(
        string jobName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await _gpu.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            _gpu.Release();
        }
    }
}
```

## Implementation Milestones

### Milestone 1: No Lost Speech

- Live utterance monitor can run during active states.
- Captured utterances include current state and active turn id.
- Logs prove speech is captured during silent processing.

### Milestone 2: State-Aware Stop

- Stop during TTS stops speech immediately with no confirmation.
- Stop during pending tool action pauses and asks contextual confirmation.
- Explicit cancel phrases cancel without confirmation.

### Milestone 3: Correction Before Commit

- Add short pending-command window for `OpenUrlTool` or similar low-risk side-effect tools.
- Correction phrases replace pending command.
- Acceptance test: Facebook request can be replaced with Google before Facebook opens.

### Milestone 4: Cancellation Propagation

- Active turns have cancellation tokens.
- Pipeline checks tokens before major expensive/side-effect steps.
- TTS chunks and pending tools respect cancellation where possible.

### Milestone 5: GPU Coordination

- STT and TTS use a shared GPU scheduler if both use CUDA.
- Interruption STT gets priority over continued TTS.
- Logs show GPU jobs are not colliding uncontrollably.

## What Success Feels Like

The final behavior should feel like this:

```text
User: open facebook
Merlin: [begins planning]
User: sorry I meant google
Merlin: [does not ignore it]
Merlin: opens Google instead
```

And:

```text
User: explain dependency injection in detail
Merlin: [starts long spoken answer]
User: stop
Merlin: [stops speaking immediately]
```

And:

```text
User: open facebook
User: stop
Merlin: Do you want me to stop and not open Facebook?
```

The critical UX outcome:

```text
There are no unknown silences where the user can talk but Merlin throws the speech away.
```

## Final Reminder

This is an extension of the existing interruption system, not a rewrite.

Reuse the current:

- VAD logic.
- Audio buffering.
- STT handoff.
- Self-echo / playback suppression.
- Hard-stop during TTS.
- Interruption logging.

Expand it so it applies during all active flow states, then route the captured utterance based on context.
