# Merlin Live Turn Cancellation And Interruption Implementation

## Purpose

Implement true live assistant turn cancellation in Merlin.

Merlin currently appears to support **playback-live interruption**: when the user interrupts, speech playback can be paused, resumed, or cleared. However, the active assistant request itself is not fully live yet. That means DeepInfra/local generation, routing, polishing, tool execution, and final outbound response handling may continue after the user has already interrupted.

The goal of this task is to make interruptions cancel the **active assistant turn**, not only the currently playing audio.

This should prevent old answers from returning after the user says something like:

```text
stop
cancel
wait
no, I meant ...
```

The user experience we want:

```text
Merlin starts answering
↓
User interrupts
↓
Speech stops immediately
↓
The active backend turn is cancelled or marked interrupted
↓
Any late model/tool/polishing response is ignored
↓
No old answer is sent over WebSocket
↓
No old answer is queued for speech
↓
A later correction can start as a new clean turn
```

This task should be implemented as a focused extension of the current Merlin backend. Do not redesign the whole routing system.

---

## Important Current Diagnosis

Based on current inspection and previous agent findings, the current state is believed to be:

- `BargeInCoordinator` can detect/classify interruptions.
- It can pause/resume/clear speech playback.
- Hard stop/correction calls `IAssistantSpeechPlaybackService.ClearQueueAsync`.
- Correction logs indicate DeepInfra regeneration is deferred.
- Playback uses `correlationId` as `AssistantTurnId`.
- `AssistantTurnTracker` and persisted turn state exist.
- Normal conversation/model calls are not fully wired to persisted/live turn state yet.
- DeepInfra/local generation already accepts `CancellationToken`, so the lower-level provider path is likely cancellable.
- The missing piece is a live request lifecycle boundary that owns cancellation and stale-response suppression.

The phrase for the current bug is:

```text
playback-live, but not turn-live
```

This implementation should make Merlin:

```text
playback-live + turn-live
```

---

## Definitions

### Playback-live

Merlin can stop, pause, resume, or clear audio that is currently being spoken.

This controls the speech output queue/player only.

### Turn-live

Merlin can cancel or invalidate the entire active assistant turn:

- intent parsing
- command routing
- tool execution where cancellation is safe/cooperative
- DeepInfra/local model generation
- response polishing
- speech enqueueing
- outbound WebSocket response
- orb/frontend final-state messages

### Stale response

A response from a previous assistant turn that completes after that turn was cancelled or superseded.

Stale responses must not be sent to the frontend and must not be spoken.

### Correction

An interruption where the user is not merely stopping Merlin, but replacing/refining the request.

Example:

```text
No, I meant faster-whisper medium, not large.
```

Correction regeneration should be implemented only after hard cancellation and stale-response suppression are working.

---

## Main Implementation Rule

Cancellation tokens are necessary but not sufficient.

Some async operations may:

- ignore cancellation
- complete after cancellation
- throw late
- finish successfully despite token cancellation
- perform partial side effects

Therefore, Merlin must use both:

1. cooperative cancellation through `CancellationToken`
2. explicit stale-response suppression before sending/speaking any final result

The final guard is essential:

```csharp
if (!liveTurnService.ShouldEmit(correlationId))
{
    return;
}
```

or equivalent.

---

## High-Level Architecture

Add a small backend service that tracks currently active assistant turns.

Suggested names:

- `ILiveAssistantTurnService`
- `LiveAssistantTurnService`

Alternative acceptable names:

- `IActiveAssistantTurnRegistry`
- `ActiveAssistantTurnRegistry`
- `IAssistantTurnCancellationService`

Prefer names that clearly communicate live request lifecycle control.

The service should:

- register an active turn when a user text/voice request starts
- create/own a linked `CancellationTokenSource`
- expose a `CancellationToken` to the routing/model/tool pipeline
- mark a turn cancelled/interrupted
- answer whether a turn is still active/emittable
- unregister/complete the turn when finished
- optionally call `IAssistantTurnTracker.MarkInterruptedAsync` if a persisted turn id exists
- be safe for concurrent requests
- avoid cancelling unrelated sessions/conversations

---

## Suggested Data Model

Create a lightweight runtime-only model.

Suggested file:

```text
Merlin.Backend/Models/LiveAssistantTurn.cs
```

Suggested shape:

```csharp
public sealed class LiveAssistantTurn : IAsyncDisposable, IDisposable
{
    public string ConversationId { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public string AssistantTurnId { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public CancellationTokenSource CancellationTokenSource { get; init; } = default!;
    public CancellationToken CancellationToken => CancellationTokenSource.Token;
    public LiveAssistantTurnStatus Status { get; private set; }
    public LiveAssistantTurnCancelReason? CancelReason { get; private set; }
    public string? CorrectionText { get; private set; }

    public void MarkCancelled(
        LiveAssistantTurnCancelReason reason,
        string? correctionText = null)
    {
        // idempotent
    }
}
```

Suggested enum:

```csharp
public enum LiveAssistantTurnStatus
{
    Active,
    Cancelled,
    Completed
}
```

Suggested cancel reason enum:

```csharp
public enum LiveAssistantTurnCancelReason
{
    Unknown,
    UserHardStop,
    UserCorrection,
    SupersededByNewTurn,
    ClientDisconnected,
    Timeout,
    SystemShutdown
}
```

Do not persist this runtime object directly. Persisted turn tracking can be updated separately through existing `AssistantTurnTracker`.

---

## Suggested Turn Context

Avoid coupling everything directly to `WebSocketHandler`.

Create or reuse a small context object that can be passed through routing and services.

Suggested file:

```text
Merlin.Backend/Models/AssistantTurnContext.cs
```

Suggested shape:

```csharp
public sealed record AssistantTurnContext(
    string ConversationId,
    string CorrelationId,
    string AssistantTurnId,
    CancellationToken CancellationToken);
```

If Merlin already has an equivalent request/session context, extend that instead of duplicating it.

Important:

- Do not pass `LiveAssistantTurnService` everywhere if a simple context is enough.
- Most services only need `CancellationToken`, `CorrelationId`, and maybe `AssistantTurnId`.
- Only lifecycle owners should call begin/cancel/complete.

---

## Suggested Service Interface

Suggested file:

```text
Merlin.Backend/Services/Interfaces/ILiveAssistantTurnService.cs
```

Suggested shape:

```csharp
public interface ILiveAssistantTurnService
{
    LiveAssistantTurn BeginTurn(
        string conversationId,
        string correlationId,
        string? assistantTurnId = null,
        CancellationToken requestAborted = default);

    bool TryGetActiveTurn(string correlationId, out LiveAssistantTurn turn);

    CancellationToken GetTurnCancellationToken(
        string correlationId,
        CancellationToken fallback = default);

    Task<bool> CancelTurnAsync(
        string correlationId,
        LiveAssistantTurnCancelReason reason,
        string? correctionText = null,
        CancellationToken cancellationToken = default);

    bool IsActive(string correlationId);

    bool IsCancelled(string correlationId);

    bool ShouldEmit(string correlationId);

    void CompleteTurn(string correlationId);
}
```

Implementation notes:

- Use `ConcurrentDictionary<string, LiveAssistantTurn>`.
- Key by `correlationId` for the first implementation because playback already uses correlation id as turn id.
- If conversation/session support requires more precision, use a composite key later.
- `BeginTurn` should cancel/supersede an existing active turn with the same correlation id if that can happen.
- `CancelTurnAsync` should be idempotent.
- `CompleteTurn` should dispose the linked `CancellationTokenSource`.
- `ShouldEmit` should return false if the turn is missing, cancelled, or completed depending on timing.
- Be deliberate about whether `Completed` should still allow a final emit. Usually the final emit check should happen before `CompleteTurn`.

A safer pattern:

```csharp
try
{
    // process request
    if (!liveTurnService.ShouldEmit(correlationId))
    {
        return;
    }

    // send response / enqueue speech
}
finally
{
    liveTurnService.CompleteTurn(correlationId);
}
```

---

## Suggested Implementation File

Suggested file:

```text
Merlin.Backend/Services/LiveAssistantTurnService.cs
```

Implementation requirements:

- Thread-safe.
- Idempotent cancellation.
- Does not throw if turn is already missing/completed.
- Logs begin/cancel/complete.
- If existing `IAssistantTurnTracker` is available, optionally mark interrupted.
- Does not block the interruption path on slow persistence.
- Does not cancel all turns globally unless explicitly requested.
- Does not clear speech playback itself. Keep that in `BargeInCoordinator` or the interruption handler.

Suggested logging events:

```text
Live turn started. ConversationId=..., CorrelationId=...
Live turn cancelled. CorrelationId=..., Reason=...
Live turn completed. CorrelationId=...
Late response suppressed. CorrelationId=...
Cancel requested for unknown turn. CorrelationId=...
```

---

## Dependency Injection

Register the service in the backend DI container.

Likely location:

```text
Merlin.Backend/Program.cs
```

or existing service registration extension if present.

Suggested registration:

```csharp
builder.Services.AddSingleton<ILiveAssistantTurnService, LiveAssistantTurnService>();
```

It should be singleton because it tracks active live turns across requests.

---

## Wiring Points To Inspect

Before changing code, inspect these files/classes if they exist:

```text
Merlin.Backend/Program.cs
Merlin.Backend/Services/BargeInCoordinator.cs
Merlin.Backend/Services/WebSocketHandler.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/IntentParser.cs
Merlin.Backend/Services/ResponsePolisher.cs
Merlin.Backend/Services/AssistantTurnTracker.cs
Merlin.Backend/Services/Interfaces/IAssistantSpeechPlaybackService.cs
Merlin.Backend/Services/Interfaces/IAssistantTurnTracker.cs
Merlin.Backend/Services/Interfaces/ILocalAiClient.cs
Merlin.Backend/Tools/
Merlin.Backend.Tests/
```

Search terms:

```text
CorrelationId
AssistantTurnId
ClearQueueAsync
Pause
Resume
BargeIn
HardCancel
Correction
CancellationToken
DeepInfra
LocalAI
Polish
CommandRouter
WebSocket
SendAsync
Enqueue
Speak
TurnTracker
MarkInterrupted
```

---

## Phase 1: Implement Hard Cancellation Boundary

### Goal

When the user hard-stops Merlin, the active backend turn is cancelled and late responses are suppressed.

### Required behavior

```text
User asks a long question
↓
DeepInfra/model request starts
↓
Merlin begins speaking or thinking
↓
User says "stop"
↓
BargeInCoordinator classifies HardCancel
↓
Speech queue is cleared
↓
Live turn is cancelled
↓
Model/tool/routing token is cancelled where possible
↓
If old work completes late, it is ignored
↓
No final old answer is sent/spoken
```

### Work items

- [ ] Add `ILiveAssistantTurnService`.
- [ ] Add `LiveAssistantTurnService`.
- [ ] Add runtime models/enums.
- [ ] Register service in DI.
- [ ] Begin live turn at the entry point for incoming text/voice requests.
- [ ] Pass the live cancellation token into routing/model/tool/polishing calls.
- [ ] On hard cancellation, call `CancelTurnAsync`.
- [ ] Before sending final response, call `ShouldEmit`.
- [ ] Before enqueueing final speech, call `ShouldEmit`.
- [ ] Complete/unregister turn in `finally`.

### Do not do yet

- Do not implement correction regeneration yet.
- Do not redesign router.
- Do not add new capability routing.
- Do not modify unrelated ToDo/capability spec folders.

---

## Phase 2: Wire BargeInCoordinator To Live Turn Cancellation

### Goal

Interruption classification should affect the active assistant turn lifecycle.

### Existing behavior to preserve

Barge-in should continue to:

- pause playback for temporary clarification/backchannel if currently supported
- resume playback when appropriate
- clear playback on hard stop/correction
- avoid cancelling on backchannel/clarification if current behavior says resume

### Required behavior by interruption type

#### HardCancel

Examples:

```text
stop
cancel
shut up
never mind
```

Expected:

```text
clear speech playback
cancel active live turn
suppress stale responses
mark turn interrupted if possible
do not generate replacement answer
```

#### Correction

Examples:

```text
no, I meant medium.en
wait, I said folder not file
actually use Godot 4.5
```

Expected first implementation:

```text
clear speech playback
cancel active live turn
record correction text if available
suppress stale responses
do not regenerate yet
```

Expected later implementation:

```text
clear speech playback
cancel old turn
record correction text
create new request from correction
route as new turn
```

#### Backchannel

Examples:

```text
yes
mhm
okay
right
```

Expected:

```text
do not cancel active turn
pause/resume if current design does that
do not suppress final answer
```

#### Clarification

Examples:

```text
what do you mean?
which one?
```

Expected:

```text
preserve current intended behavior
do not hard-cancel unless classifier marks it as correction/hard stop
```

### Work items

- [ ] Inject `ILiveAssistantTurnService` into `BargeInCoordinator` or the nearest interruption handling coordinator.
- [ ] Identify current active `correlationId`/`AssistantTurnId`.
- [ ] On HardCancel, call `CancelTurnAsync(..., UserHardStop)`.
- [ ] On Correction, call `CancelTurnAsync(..., UserCorrection, correctionText)`.
- [ ] Do not cancel for Backchannel/Clarification.
- [ ] Add tests for each interruption class.

---

## Phase 3: Pass CancellationToken Through Request Pipeline

### Goal

The active live turn token should flow through the entire request.

### Inspect current signatures

Find current signatures for:

```text
WebSocketHandler request handling
CommandRouter route/execute
IntentParser parse/classify
Local AI client calls
DeepInfra client calls
Response polishing
Tool execution
Speech enqueue/TTS generation
```

### Required behavior

Where possible, change signatures from:

```csharp
Task<Result> HandleAsync(Request request)
```

to:

```csharp
Task<Result> HandleAsync(Request request, CancellationToken cancellationToken)
```

or, if context is better:

```csharp
Task<Result> HandleAsync(Request request, AssistantTurnContext turnContext)
```

### Important rule

Prefer passing the plain `CancellationToken` to low-level services.

Only pass `AssistantTurnContext` where correlation/turn metadata is actually needed.

Suggested layering:

```text
WebSocketHandler
- owns BeginTurn / CompleteTurn
- creates AssistantTurnContext

CommandRouter
- receives AssistantTurnContext or CancellationToken

IntentParser
- receives CancellationToken

Tools
- receive CancellationToken

DeepInfra/local model
- receive CancellationToken

ResponsePolisher
- receives CancellationToken

Speech output enqueue
- checked against ShouldEmit before enqueue
```

### Tool execution note

For read-only tools, cancellation can stop work.

For side-effect tools, cancellation cannot undo actions already performed.

Examples of side-effect tools:

```text
open app
open URL
install package
delete file
change setting
send email
```

The implementation must not claim an action was undone if it was already executed.

---

## Phase 4: Stale Outbound Response Suppression

### Goal

No cancelled/superseded assistant response may reach the user.

### Required suppression points

Add `ShouldEmit` checks before:

- sending assistant text response over WebSocket
- sending frontend/orb final response state
- queueing speech playback
- queueing TTS generation if separate
- writing final assistant message to conversation history, if cancellation means it should not be persisted
- updating active speaking state after cancellation

### Suggested helper

If response sending is centralized, add a helper:

```csharp
private bool CanEmitTurn(string correlationId)
{
    if (!_liveTurnService.ShouldEmit(correlationId))
    {
        _logger.LogInformation(
            "Suppressing stale assistant response for cancelled turn {CorrelationId}",
            correlationId);

        return false;
    }

    return true;
}
```

### Important

Suppression should happen even if the lower-level LLM call does not throw `OperationCanceledException`.

This is the main protection against haunted old answers.

---

## Phase 5: Persist Interruption State Carefully

### Goal

Where possible, reflect turn interruption in existing persisted turn tracking.

### Current known state

There appears to be an `AssistantTurnTracker` and persisted turn state.

Normal conversation/model calls may not yet be fully wired to persisted turn records.

### First-pass rule

Use `correlationId` as the live turn id.

If a real persisted turn id exists, call:

```csharp
IAssistantTurnTracker.MarkInterruptedAsync(...)
```

or equivalent.

If no persisted turn exists yet:

- do not force a large persistence refactor
- log the interruption
- keep persisted integration optional
- make the live cancellation work first

### Do not block cancellation on persistence

Cancellation path should be fast.

If marking interruption fails, log the failure but do not prevent cancellation.

---

## Phase 6: Correction Regeneration

Only start this phase after hard stop cancellation and stale-response suppression are tested.

### Goal

A correction interruption should cancel the old turn and start a new turn based on the correction text.

Example:

```text
User: "Tell me how much VRAM whisper large uses."
Merlin starts answering.
User: "No, I meant medium.en with beam 5."
Merlin stops old answer.
Merlin answers the corrected question.
```

### Required behavior

```text
Correction detected
↓
Cancel old turn
↓
Clear playback
↓
Record correction text
↓
Build compact replacement request
↓
Start a new live turn
↓
Route as normal
```

### Correction request builder

Add a small service only if needed.

Suggested name:

```text
CorrectionRequestBuilder
```

Suggested behavior:

- include the previous user request if available
- include correction transcript
- include short instruction that the correction supersedes the old request
- avoid sending huge old conversation context
- treat correction as a new user request/turn

Suggested replacement prompt shape:

```text
The user interrupted the previous assistant answer with a correction.

Previous user request:
"{previousRequest}"

Correction:
"{correctionText}"

Handle the corrected request. The correction supersedes the previous request.
```

### Safety

If the corrected request changes into a risky action, route through normal confirmation flow.

Example:

```text
Actually delete those files
```

should require destructive confirmation.

---

## Phase 7: Tests

Add focused tests before broad refactors.

Likely test project:

```text
Merlin.Backend.Tests/
```

Suggested test files:

```text
LiveAssistantTurnServiceTests.cs
BargeInLiveTurnCancellationTests.cs
WebSocketLateResponseSuppressionTests.cs
CommandRouterCancellationTests.cs
CorrectionInterruptionTests.cs
```

Use existing test naming/style where possible.

### Required tests

#### Live service basics

- [ ] `BeginTurn_registers_active_turn`
- [ ] `CancelTurn_marks_turn_cancelled_and_cancels_token`
- [ ] `CancelTurn_is_idempotent`
- [ ] `CompleteTurn_removes_or_marks_completed_turn`
- [ ] `ShouldEmit_returns_false_after_cancel`
- [ ] `ShouldEmit_returns_true_for_active_turn`
- [ ] `Cancel_unknown_turn_does_not_throw`

#### Hard stop flow

- [ ] hard stop cancels active live turn
- [ ] hard stop clears playback queue
- [ ] hard stop suppresses late DeepInfra response
- [ ] hard stop does not enqueue final speech
- [ ] hard stop does not send final WebSocket assistant message

#### Correction flow first cut

- [ ] correction cancels active live turn
- [ ] correction clears playback queue
- [ ] correction records correction text if available
- [ ] correction suppresses old answer
- [ ] correction does not regenerate until regeneration phase is enabled

#### Backchannel / clarification flow

- [ ] backchannel does not cancel active turn
- [ ] clarification does not cancel active turn unless classified as correction
- [ ] paused speech can still resume if current behavior supports resume

#### Cancellation token propagation

- [ ] DeepInfra/local provider receives cancellation token
- [ ] response polisher receives cancellation token
- [ ] read-only tool receives cancellation token
- [ ] cancelled model call does not enqueue speech
- [ ] cancellation during polishing suppresses final response

#### Multiple turn/session safety

- [ ] cancelling one correlation id does not cancel another
- [ ] new turn after cancellation can complete normally
- [ ] cancelled previous turn cannot overwrite newer turn output

#### Side effect behavior

- [ ] if an open-app/open-url tool already executed, cancellation does not retry it
- [ ] cancellation suppresses post-action narration if the turn was cancelled
- [ ] future destructive tools must still use confirmation

---

## Acceptance Criteria

The implementation is complete when all of the following are true:

- [ ] Each incoming text/voice user request is registered as a live assistant turn.
- [ ] The live turn owns a cancellation token.
- [ ] Routing/model/tool/polishing paths receive the live turn cancellation token where practical.
- [ ] Hard cancel interruption clears speech playback and cancels the active turn.
- [ ] Correction interruption clears speech playback and cancels the active turn.
- [ ] Backchannel/clarification does not incorrectly cancel the active turn.
- [ ] Late DeepInfra/local responses after cancellation are suppressed.
- [ ] Cancelled responses are not sent over WebSocket.
- [ ] Cancelled responses are not queued for speech.
- [ ] Cancelled responses do not switch the orb/frontend back into speaking/responding state.
- [ ] The cancellation path is idempotent and does not crash if a turn is already completed.
- [ ] Existing tests pass.
- [ ] New tests cover hard stop, correction, backchannel, stale response suppression, and token propagation.
- [ ] Correction regeneration is either implemented in a separate phase or explicitly left as a documented next step.

---

## Non-Goals

Do not implement these as part of the first cut:

- full router redesign
- web research capability
- file/email/calendar capability routing
- Codex integration
- new memory architecture
- destructive file actions
- software installation
- major persistence refactor
- frontend visual redesign
- large conversation-context rewrite

This task is about turn lifecycle and interruption correctness only.

---

## Safety And Side Effects

Cancellation does not undo side effects.

If a tool has already opened a browser, launched an app, changed a setting, or performed an action, cancellation only stops the remaining assistant response path.

Future risky tools should use action phases:

```text
planned
awaiting_confirmation
confirmed
executing
executed
completed
cancelled
failed
```

Cancellation is strongest before `executing`.

Do not mark a completed side-effect action as undone.

---

## Recommended Implementation Order

Use this exact order unless repo structure suggests a better local fit.

### Step 1: Inspect current flow

Before editing, inspect:

```text
BargeInCoordinator
WebSocketHandler
CommandRouter
DeepInfra/local AI clients
ResponsePolisher
AssistantSpeechPlaybackService
AssistantTurnTracker
current tests around interruption/barge-in
```

Write down:

- where correlation ids are created
- where assistant responses are sent
- where speech is enqueued
- where `ClearQueueAsync` is called
- where cancellation tokens already exist
- whether `WebSocketHandler` has access to request-aborted tokens

### Step 2: Add service and models

Add:

```text
ILiveAssistantTurnService
LiveAssistantTurnService
LiveAssistantTurn
LiveAssistantTurnStatus
LiveAssistantTurnCancelReason
AssistantTurnContext if no existing equivalent exists
```

Register service in DI.

### Step 3: Add unit tests for the service

Do this before wiring into the live pipeline.

### Step 4: Register turns at request entry

At the text/voice request entry point:

```csharp
var turn = _liveTurnService.BeginTurn(
    conversationId,
    correlationId,
    assistantTurnId: correlationId,
    requestAborted: cancellationToken);

try
{
    var turnContext = new AssistantTurnContext(
        conversationId,
        correlationId,
        turn.AssistantTurnId,
        turn.CancellationToken);

    // route/process with turnContext or turn.CancellationToken
}
finally
{
    _liveTurnService.CompleteTurn(correlationId);
}
```

### Step 5: Pass cancellation token through the pipeline

Update method signatures carefully.

Prefer minimal changes:

```csharp
Task<X> DoAsync(..., CancellationToken cancellationToken)
```

Do not introduce `AssistantTurnContext` everywhere unless metadata is needed.

### Step 6: Connect barge-in hard cancel/correction

On hard cancel/correction:

```csharp
await _speechPlayback.ClearQueueAsync(...);

await _liveTurnService.CancelTurnAsync(
    correlationId,
    LiveAssistantTurnCancelReason.UserHardStop,
    cancellationToken: cancellationToken);
```

For correction:

```csharp
await _liveTurnService.CancelTurnAsync(
    correlationId,
    LiveAssistantTurnCancelReason.UserCorrection,
    correctionText,
    cancellationToken);
```

### Step 7: Add stale response guards

Before sending/speaking:

```csharp
if (!_liveTurnService.ShouldEmit(correlationId))
{
    _logger.LogInformation(
        "Suppressing stale response for turn {CorrelationId}",
        correlationId);

    return;
}
```

Add this to all known final output points.

### Step 8: Add integration-style tests

Test with fake delayed model response:

```text
request starts
fake model waits
hard cancel fires
fake model completes
assert no WebSocket send
assert no speech enqueue
```

### Step 9: Only then add correction regeneration

Correction regeneration is optional for this first task unless explicitly requested after cancellation is proven.

---

## Example Scenarios

### Scenario 1: Hard stop during model generation

```text
User: "Explain the entire Merlin voice architecture."
Merlin starts DeepInfra request.
User: "stop."
```

Expected:

```text
speech queue cleared
live turn cancelled
DeepInfra token cancelled where possible
late DeepInfra result ignored
no final answer spoken
no final answer sent
```

### Scenario 2: Hard stop after model returns but before speech finishes

```text
User asks question.
Model response completed.
Speech playback starts.
User says "stop."
```

Expected:

```text
speech queue cleared
live turn cancelled or marked no longer emittable
remaining queued speech removed
no further chunks spoken
```

### Scenario 3: Correction during answer

```text
User: "How much VRAM does Whisper large use?"
Merlin starts answering.
User: "No, I meant medium.en with beam 5."
```

First cut expected:

```text
old speech cleared
old turn cancelled
correction text recorded/logged
old answer suppressed
no regeneration yet unless Phase 6 is implemented
```

Later expected:

```text
old turn cancelled
new corrected turn starts
Merlin answers corrected question
```

### Scenario 4: Backchannel

```text
Merlin explains something.
User says "mhm" or "okay."
```

Expected:

```text
do not cancel
preserve current pause/resume behavior
do not suppress final answer
```

### Scenario 5: Side-effect tool

```text
User: "open Chrome."
Chrome opens.
User: "stop."
```

Expected:

```text
Chrome remains open
Merlin stops speaking
post-action narration may be suppressed
do not retry open Chrome
do not claim Chrome was closed
```

---

## Logging Requirements

Add enough logging to debug timing/race conditions.

At minimum log:

- turn start
- turn cancel requested
- turn cancellation token triggered
- turn complete
- stale response suppressed
- cancel requested for unknown/missing turn
- correction text captured, if present
- failed persisted interruption mark, if applicable

Avoid logging sensitive full transcripts unless current Merlin logging policy already allows it.

For correction text, prefer shortened/sanitized logs:

```text
Correction captured. Length=42, CorrelationId=...
```

---

## Race Conditions To Handle

### Cancel arrives before turn registered

Should not crash.

Log:

```text
Cancel requested for unknown turn.
```

### Cancel arrives after turn completed

Should not crash.

### Model completes at the same moment cancel arrives

Final `ShouldEmit` guard decides.

### User starts a new request immediately after cancelling old request

Old response must not override the new response.

### Multiple sessions/conversations

Cancel only the relevant turn.

If current app only has one active conversation, still design the service so it can support multiple later.

---

## Review Checklist For Agent

Before finishing, verify:

- [ ] No old response can be spoken after hard cancel.
- [ ] No old response can be sent after hard cancel.
- [ ] Correction cancellation does not accidentally resume old speech.
- [ ] Backchannel does not hard-cancel.
- [ ] Cancellation token is passed to DeepInfra/local clients.
- [ ] Tests use delayed fake services to prove suppression.
- [ ] The service is singleton and thread-safe.
- [ ] The implementation does not touch unrelated capability docs/TODO files.
- [ ] Existing behavior for open app/open URL still works.
- [ ] Existing tests still pass.

---

## Final Agent Output Required

After implementation, report:

1. Files changed.
2. New files added.
3. How live turns are registered.
4. How hard cancellation reaches active backend work.
5. Where stale responses are suppressed.
6. Which tests were added.
7. Which tests were run and their result.
8. Any known limitations.
9. Whether correction regeneration was implemented or left as next step.

Do not simply say "implemented". Explain the lifecycle with the actual files/classes used.

---

## Preferred Final State Summary

When done, Merlin should be able to honestly say:

```text
Interruptions now cancel the active assistant turn, not just the audio playback.
Hard stops and corrections clear speech, cancel active backend work where possible, and suppress late responses from old turns.
Correction regeneration is either implemented as a new turn or left as a clean next step.
```
