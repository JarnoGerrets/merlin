# Merlin Live Turn Correction Regeneration Implementation

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Live_Turn_Correction_Regeneration_Implementation.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Finish the second phase of Merlin's interruption system: **correction regeneration**.

The first-cut live turn cancellation flow is already implemented. Hard stops and corrections now cancel the active backend turn, clear playback, and suppress stale final responses before WebSocket send, dev visual flow start, and speech enqueue.

The remaining missing behavior is:

```text
Correction interruption
↓
cancel old live turn
↓
clear old speech
↓
suppress old late response
↓
create a new corrected user request
↓
route that corrected request through Merlin's normal request pipeline
↓
Merlin answers the corrected request
```

The new corrected request must go through the normal backend pipeline:

```text
WebSocketHandler
↓
CommandRouter
↓
HybridIntentParser
↓
ToolRegistry / ITool
↓
GeneralConversationTool / DeepInfra when appropriate
```

Do **not** directly call DeepInfra from the interruption/correction code.

---

## Why normal routing matters

A correction may be a brainstorming correction:

```text
No, I meant the orb should feel more magical, not more technical.
```

That should naturally route to:

```text
general_conversation
↓
GeneralConversationTool
↓
LocalAIChatService
↓
DeepInfra
```

But a correction may also be a tool request:

```text
No, open Firefox instead.
```

That should route to:

```text
open_application
↓
OpenApplicationTool
```

A correction may also be unsafe or unsupported:

```text
Actually delete those files instead.
```

That must route through the existing missing/unsupported/safety/confirmation behavior, not directly to chat.

Therefore, correction regeneration must submit a **new user request** into the same routing path normal text/voice requests use.

---

## Current known state

The first-cut live turn cancellation work reportedly added/changed:

```text
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/LocalAIChatService.cs
Merlin.Backend/Program.cs
Merlin.Backend/Models/LiveAssistantTurn.cs
Merlin.Backend/Models/AssistantTurnContext.cs
Merlin.Backend/Services/Interfaces/ILiveAssistantTurnService.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend.Tests/LiveAssistantTurnServiceTests.cs
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/CommandRouterTests.cs
Merlin.Backend.Tests/WebSocketHandlerTests.cs
```

The first-cut behavior is:

```text
hard stop:
  clear playback
  cancel active live turn
  suppress old answer

correction:
  clear playback
  cancel active live turn
  record correction text on live turn
  suppress old answer
  no regenerated answer yet

backchannel/clarification:
  preserve current non-cancelling behavior
```

This task should build on that foundation without reworking it.

---

## Important constraints

Do not redesign the routing system.

Do not implement web search, web research, capability routing, file access, email, calendar, Codex integration, memory redesign, or tool architecture changes as part of this task.

Do not change the successful hard-stop cancellation behavior except where needed to keep correction regeneration clean.

Do not make `BargeInCoordinator` call DeepInfra directly.

Do not bypass:

- `CommandRouter`
- `HybridIntentParser`
- `ToolRegistry`
- confirmation handling
- missing capability handling
- unsupported action handling
- live turn stale-response suppression

Do not reuse the cancelled old correlation id for the regenerated request.

Do not let the old cancelled response suppress the new corrected response.

Do not start correction regeneration for hard stops, backchannels, or clarifications.

---

## Desired user experience

### Scenario 1 - Brainstorming correction

```text
User:
"Help me design the external-open animation."

Merlin:
starts brainstorming

User interrupts:
"No, I mean it should feel magical and tactile, not technical."

Expected:
- old answer stops
- old turn is cancelled
- old answer cannot return later
- Merlin routes the corrected request normally
- parser chooses general_conversation
- DeepInfra answers the corrected brainstorming direction
```

### Scenario 2 - Tool correction

```text
User:
"Open Chrome."

Merlin:
starts/opening/responding

User interrupts:
"No, open Firefox instead."

Expected:
- old turn is cancelled
- correction routes normally
- parser chooses app opening
- OpenApplicationTool handles Firefox
- confirmation still applies if needed
```

### Scenario 3 - URL correction

```text
User:
"Open facebook.com."

User interrupts:
"No, open github.com instead."

Expected:
- old response/speech suppressed
- corrected request routes to URL opening
- OpenUrlTool handles github.com
```

### Scenario 4 - Unsafe/destructive correction

```text
User:
"Find duplicate files."

User interrupts:
"Actually delete them all."

Expected:
- old turn cancelled
- correction routes normally
- destructive/unsupported/confirmation handling is preserved
- no direct DeepInfra bypass
```

### Scenario 5 - Hard stop

```text
User:
"Explain all of Merlin's architecture."

Merlin starts answering.

User:
"Stop."

Expected:
- old answer stops
- old turn cancelled
- no regenerated request
```

### Scenario 6 - Backchannel

```text
Merlin is talking.

User:
"mhm"
```

Expected:

```text
- do not cancel active turn
- do not start a new correction request
- preserve current pause/resume behavior
```

---

## Main design principle

Correction regeneration should behave as if the user submitted a new message after interrupting.

Conceptually:

```text
old assistant turn is dead
new corrected user request is born
new request goes through the normal pipeline
```

The correction pipeline must not be a special second assistant.

---

## Recommended implementation shape

Add a small dispatcher/builder layer instead of putting too much logic inside `BargeInCoordinator`.

Suggested new services:

```text
Merlin.Backend/Services/Interfaces/ICorrectionRequestBuilder.cs
Merlin.Backend/Services/CorrectionRequestBuilder.cs

Merlin.Backend/Services/Interfaces/ICorrectionRequestDispatcher.cs
Merlin.Backend/Services/CorrectionRequestDispatcher.cs
```

If the repo structure suggests fewer services, one combined service is acceptable:

```text
Merlin.Backend/Services/CorrectionRegenerationService.cs
Merlin.Backend/Services/Interfaces/ICorrectionRegenerationService.cs
```

The key is to keep responsibilities clean:

```text
BargeInCoordinator:
  classify interruption
  clear playback
  cancel old live turn
  trigger correction regeneration request

CorrectionRequestBuilder:
  build corrected user message / AssistantRequest

CorrectionRequestDispatcher:
  submit corrected request through normal backend request path

WebSocketHandler / shared request processor:
  register new live turn
  call CommandRouter
  guard stale responses
  send/speak response
```

---

## Best integration point

Prefer reusing the same method that normal text/voice requests use after deserialization.

The first-cut work reportedly added something like:

```text
WebSocketHandler.ProcessLiveRequestAsync
```

or equivalent.

If such a method exists, correction regeneration should call into that shared request processing method rather than duplicating routing/sending/speech logic.

Target shape:

```text
WebSocketHandler receives normal request
↓
ProcessLiveRequestAsync(request, websocket/session/context, cancellationToken)

Correction regeneration creates corrected AssistantRequest
↓
ProcessLiveRequestAsync(correctedRequest, same websocket/session/context, cancellationToken)
```

If `WebSocketHandler` is not the right dependency target for `BargeInCoordinator`, extract the shared logic into an application service:

```text
IAssistantRequestDispatcher
AssistantRequestDispatcher
```

Suggested interface:

```csharp
public interface IAssistantRequestDispatcher
{
    Task DispatchAsync(
        AssistantRequest request,
        AssistantRequestDispatchContext context,
        CancellationToken cancellationToken);
}
```

Then both `WebSocketHandler` and correction regeneration can use the dispatcher.

However, avoid a large refactor unless needed. If the current `WebSocketHandler` can safely expose/reuse a private method internally for correction processing, keep the first pass smaller.

---

## Correlation id rules

The corrected request must get a **new correlation id**.

Do not reuse the old cancelled correlation id.

Suggested format:

```text
{oldCorrelationId}:correction:{shortGuid}
```

or simply a new GUID.

The response for the new corrected request should use the new correlation id so:

- old stale-response guard cannot suppress the new answer
- frontend can distinguish old cancelled turn from new corrected turn
- logs can trace correction lineage

Store lineage metadata if possible:

```text
OriginalCorrelationId
CorrectionCorrelationId
CorrectionText
CorrectionReason
```

If current models do not support metadata, logging is enough for first pass.

---

## Request building strategy

This is the hardest part. The corrected request must preserve enough context without confusing the parser.

There are three possible strategies.

### Strategy A - Route correction text directly

Example:

```text
Correction transcript:
"No, open Firefox instead."

New request message:
"open Firefox instead"
```

Pros:

- simple
- works well for self-contained tool corrections

Cons:

- poor for non-self-contained corrections

Example problem:

```text
Previous request:
"How much VRAM does Whisper large use?"

Correction:
"No, I meant medium.en with beam 5."
```

Routing only `"medium.en with beam 5"` may not be enough.

### Strategy B - Wrap previous request and correction

Example new request message:

```text
Previous user request: "How much VRAM does Whisper large use?"
Correction: "No, I meant medium.en with beam 5."
Handle the corrected request. The correction supersedes the previous request.
```

Pros:

- preserves context
- works better for brainstorming and partial corrections

Cons:

- may confuse rule-based/tool parsers
- `OpenApplicationTool` may not understand wrapper text

### Strategy C - Hybrid builder

Recommended first implementation.

Use direct correction text when it appears self-contained or tool-like.

Use a compact contextual correction when it appears partial.

Suggested heuristic:

Direct route if correction contains strong command/action target:

```text
open
launch
start
go to
browse to
visit
search
look up
find
show
what
why
how
explain
tell me
delete
install
change
set
```

Contextual route if correction starts with partial correction markers:

```text
no, I meant
I meant
not X, Y
actually use
instead use
with beam
medium.en
large.en
that one
the other one
```

But keep this simple. Do not build a massive correction NLP engine.

Suggested builder output:

```csharp
public sealed record CorrectionRequestBuildResult(
    string CorrectedMessage,
    string Strategy,
    string? PreviousRequest,
    string CorrectionText,
    string OriginalCorrelationId,
    string NewCorrelationId);
```

For first pass, Strategy B is acceptable if it works with current tests. But include tests for tool corrections to ensure wrappers do not break app/URL routing.

---

## Recommended message format for hybrid contextual correction

For contextual corrections, use a compact format that still reads like a user request:

```text
Correct my previous request using this correction.

Previous request:
{previousRequest}

Correction:
{correctionText}
```

Avoid verbose system-like instructions if they confuse the parser.

Alternative:

```text
{previousRequest}

Correction from user:
{correctionText}
```

Test which works best with the current `HybridIntentParser`.

---

## Where to get the previous request

Correction regeneration should try to know the previous user request.

Possible sources:

1. Current live turn metadata if stored.
2. WebSocket/session request context.
3. Recent conversation/session state.
4. The active assistant turn context.
5. Last routed user message held by `WebSocketHandler`.

If previous request is not available, fall back to routing the correction text directly.

Do not block regeneration if previous text is missing.

Suggested behavior:

```text
if previousRequest exists:
  build contextual correction request
else:
  route correctionText directly
```

---

## BargeInCoordinator responsibilities

`BargeInCoordinator` should not become the full dispatcher.

It should:

```text
on Correction:
  clear playback
  cancel active live turn
  record correction text
  notify/trigger correction regeneration
```

It should not:

```text
call DeepInfra directly
bypass CommandRouter
build complex prompts itself
send WebSocket responses itself unless already its responsibility
```

If `BargeInCoordinator` does not have access to WebSocket/session context, it may emit an event/result that `WebSocketHandler` observes.

Possible implementation patterns:

### Pattern 1 - BargeInCoordinator returns a result

```csharp
public sealed record BargeInHandlingResult(
    bool CancelledTurn,
    bool ShouldRegenerateCorrection,
    string? CorrectionText,
    string? OriginalCorrelationId);
```

`WebSocketHandler` receives this result and dispatches correction.

### Pattern 2 - BargeInCoordinator uses callback/event

```csharp
public event Func<CorrectionRequested, Task>? CorrectionRequested;
```

or an injected service.

### Pattern 3 - Shared correction regeneration service

`BargeInCoordinator` calls:

```csharp
await _correctionRegenerationService.RequestCorrectionAsync(...);
```

The service queues/dispatches through normal request pipeline.

Prefer the least invasive pattern that fits the current code.

---

## Avoid correction loops

A regenerated correction request should not be treated as another interruption transcript.

Mark the request source/metadata as correction-generated.

Possible field values:

```text
InteractionSource = "voice_correction"
ClientMode = existing mode
IsCorrectionRegeneration = true
OriginalCorrelationId = old id
```

If models do not support these fields, log them at least.

If the regenerated answer is interrupted again, that should create a new normal interruption flow, but the regeneration request itself should not instantly re-trigger correction handling.

---

## Frontend/WebSocket behavior

The corrected request should produce a normal assistant response packet.

The frontend should see:

```text
old turn stops / no old final answer
new correlation id produces a new assistant response
speech plays for new answer if enabled
orb states move through thinking/tool/speaking normally
```

Do not require frontend changes in first pass unless the backend protocol needs an explicit correction event.

Optional helpful event:

```json
{
  "type": "correction_regeneration_started",
  "originalCorrelationId": "...",
  "correctionCorrelationId": "..."
}
```

Only add this if current frontend event handling makes it easy. Not required.

---

## Live turn service integration

Use the existing `ILiveAssistantTurnService`.

Correction flow should be:

```text
old turn:
  CancelTurnAsync(oldCorrelationId, UserCorrection, correctionText)

new turn:
  BeginTurn(newCorrelationId, ...)
  route normally
  ShouldEmit(newCorrelationId) before final send/speech
  CompleteTurn(newCorrelationId)
```

Important:

```text
ShouldEmit(oldCorrelationId) == false
ShouldEmit(newCorrelationId) == true while active
```

Tests must prove old and new turns do not conflict.

---

## Cancellation token behavior

The old turn token should be cancelled.

The new corrected request should get a fresh cancellation token.

The new request may itself be cancelled by a later interruption.

Do not reuse the old cancelled token for the new request.

If the original request's outer socket/client cancellation token is still valid, link the new turn token to that outer token.

---

## Safety behavior

Because the correction routes normally, existing safety behavior should apply automatically.

Add tests for at least one unsafe/unsupported correction if current test infrastructure allows.

Example:

```text
Original:
"Find duplicate files."

Correction:
"Actually delete them all."
```

Expected:

```text
does not call DeepInfra directly
routes to unsupported/destructive handling or missing capability
does not execute deletion
```

If destructive detection is currently limited, assert that the correction goes through `CommandRouter`/parser rather than direct chat.

---

## Tests to add

Use existing test style.

Likely files:

```text
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/WebSocketHandlerTests.cs
Merlin.Backend.Tests/CommandRouterTests.cs
Merlin.Backend.Tests/LiveAssistantTurnServiceTests.cs
```

Add a new test file if cleaner:

```text
Merlin.Backend.Tests/CorrectionRegenerationTests.cs
```

### Unit tests for builder

If adding `CorrectionRequestBuilder`, test:

```text
Builds_new_correlation_id
Preserves_original_correlation_id
Uses_direct_strategy_for_open_firefox_instead
Uses_contextual_strategy_for_no_i_meant_medium_en_with_beam_5
Falls_back_to_correction_text_when_previous_request_missing
Does_not_return_empty_message_for_empty_or_whitespace_correction
```

### Dispatcher/request flow tests

Required:

```text
Correction_cancels_old_turn_and_dispatches_new_request
Correction_new_request_uses_new_correlation_id
Correction_old_late_response_is_suppressed
Correction_new_response_is_sent
Correction_new_response_can_enqueue_speech
Hard_stop_does_not_dispatch_new_request
Backchannel_does_not_dispatch_new_request
Clarification_does_not_dispatch_new_request_unless_classified_as_correction
```

### Routing tests for correction types

Add tests proving regenerated corrections route through normal pipeline:

```text
Brainstorming_correction_routes_to_general_conversation
Tool_correction_open_firefox_routes_to_open_application
Url_correction_open_github_routes_to_open_url
Unsafe_correction_delete_files_routes_to_unsupported_or_missing_not_direct_chat
Missing_capability_correction_routes_to_missing_capability
```

### Race tests

Required:

```text
Old_model_response_completes_after_correction_new_response_still_sent
Old_cancelled_correlation_does_not_suppress_new_correlation
Second_interruption_can_cancel_regenerated_turn
Correction_dispatch_failure_does_not_revive_old_turn
```

### Regression tests

Ensure existing first-cut behavior still works:

```text
Hard_stop_cancels_active_turn
Hard_stop_suppresses_late_response
Correction_cancels_active_turn
Backchannel_does_not_cancel
All_existing_backend_tests_pass
```

---

## Example test scenario with delayed fake tool/model

Create a fake delayed tool or fake delayed chat service.

Flow:

```text
1. User sends original request.
2. Fake model/tool blocks.
3. Correction arrives.
4. Old turn is cancelled.
5. New corrected request is dispatched.
6. Fake old model/tool completes.
7. Assert old response not sent/spoken.
8. New corrected response completes.
9. Assert new response sent/spoken.
```

This test is critical. It proves the feature works under the race condition that matters most.

---

## Implementation phases

### Phase 1 - Builder only

- Add correction request builder.
- Add tests for direct/contextual/fallback request construction.
- No dispatch yet.

### Phase 2 - Dispatch through normal request pipeline

- Identify/extract reusable request dispatch method.
- Ensure normal text/voice requests and correction-generated requests use the same routing/sending/speech path.
- Add new correlation id and fresh live turn.
- Add tests.

### Phase 3 - Connect BargeIn correction to dispatcher

- On correction, after cancelling old turn, dispatch the new corrected request.
- Preserve hard stop behavior.
- Preserve backchannel behavior.
- Add tests.

### Phase 4 - Race/stale tests

- Delayed old response after correction.
- New response still sends.
- Old correlation cannot suppress new correlation.

### Phase 5 - Optional frontend event

Only if easy:

- send `correction_regeneration_started` event
- include original and new correlation ids

Not required for first working version.

---

## Acceptance criteria

This task is complete when:

- [ ] Correction interruptions still cancel the old active live turn.
- [ ] Correction interruptions still clear old playback.
- [ ] Stale old responses remain suppressed.
- [ ] Correction interruptions create a new corrected user request.
- [ ] The corrected request uses a new correlation id.
- [ ] The corrected request gets a fresh live turn.
- [ ] The corrected request routes through the normal request pipeline.
- [ ] Brainstorming corrections can reach `GeneralConversationTool`/DeepInfra naturally.
- [ ] Tool corrections can reach normal tools.
- [ ] Unsafe/missing/unsupported corrections do not bypass safety/missing handling.
- [ ] Hard stops do not generate new requests.
- [ ] Backchannels/clarifications do not generate new requests unless classified as correction.
- [ ] Old cancelled responses cannot suppress new corrected responses.
- [ ] Existing backend tests still pass.
- [ ] New tests cover correction dispatch, routing, stale old response suppression, and correlation id separation.

---

## Known acceptable limitations

It is acceptable if first-pass correction merging is simple.

For example, this may be acceptable:

```text
Previous request:
"How much VRAM does whisper large use?"

Correction:
"No, I meant medium.en with beam 5."

Generated corrected request:
"Correct my previous request using this correction.

Previous request:
How much VRAM does whisper large use?

Correction:
No, I meant medium.en with beam 5."
```

A future improvement can use a small LLM/request-rewrite step to turn it into:

```text
How much VRAM does whisper medium.en with beam 5 use?
```

Do not add that LLM rewrite step in the first pass unless it is clearly isolated and tested.

---

## Important design warning: do not directly call DeepInfra

This task exists partly to avoid this mistake:

```text
Correction detected
↓
call DeepInfra directly
```

That would break Merlin's tool/safety architecture.

Correct flow:

```text
Correction detected
↓
build corrected AssistantRequest
↓
normal routing
↓
parser decides:
  - general conversation -> DeepInfra
  - tool command -> tool
  - missing capability -> limitation
  - unsupported/destructive -> safety/unsupported
```

This preserves the system's architecture and keeps brainstorming behavior correct.

---

## Suggested final implementation report

When finished, report:

1. Files changed.
2. Files added.
3. How correction requests are built.
4. How the corrected request is dispatched through the normal pipeline.
5. How new correlation ids are created.
6. How old and new live turns are separated.
7. Where hard stop behavior is preserved.
8. Where backchannel/clarification behavior is preserved.
9. How brainstorming corrections reach DeepInfra naturally.
10. How tool corrections route to tools naturally.
11. Tests added.
12. Tests run and results.
13. Known limitations.
14. Next recommended improvement.

Do not simply say "implemented."

Explain the actual lifecycle with the classes and methods changed.

---

## Recommended first implementation cut

If the full version is too large, implement this valuable first cut:

```text
1. Add CorrectionRequestBuilder.
2. Add tests for request building.
3. Extract or reuse a normal request dispatch method.
4. On correction, dispatch a new corrected request with a new correlation id.
5. Prove old response is suppressed and new response is sent.
6. Prove hard stop does not dispatch.
7. Prove brainstorming correction reaches general_conversation.
8. Prove tool correction reaches OpenApplicationTool/OpenUrlTool.
```

Leave advanced semantic correction rewriting for a later task.
