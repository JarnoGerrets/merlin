---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/Merlin_Correction_Classification_And_Semantic_Rewrite_Implementation.md
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
Original source: `Merlin.ToDo/Merlin_Correction_Classification_And_Semantic_Rewrite_Implementation.md`

# Correction Classification And Semantic Rewrite Plan

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Correction_Classification_And_Semantic_Rewrite_Implementation.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Improve Merlin's interruption/correction understanding so it can distinguish:

```text
"I meant X"     -> usually correction
"I mean X"      -> often continuation/elaboration, not always correction
"No, I mean X"  -> correction
"Actually X"    -> correction or replacement
"To clarify..." -> clarification/continuation
```

The current correction regeneration foundation reportedly works:

```text
Correction interruption
↓
old live turn cancelled
↓
old playback cleared
↓
old late response suppressed
↓
new corrected request created
↓
new request routed through normal pipeline
```

This task improves the **classification and rewriting** before that regeneration happens.

The goal is to prevent Merlin from:

- treating every "I mean..." as correction
- missing obvious "I meant..." corrections
- routing partial corrections poorly
- losing context in corrections like "medium.en with beam 5"
- bypassing normal routing/safety by calling DeepInfra directly

---

## Current known context

Merlin now appears to have:

- live turn cancellation
- stale response suppression
- correction regeneration
- `CorrectionRequestBuilder`
- normal dispatch through:
  - `WebSocketHandler`
  - `CommandRouter`
  - `HybridIntentParser`
  - `ToolRegistry`
  - `ITool`
- correction-generated requests using new correlation ids
- heuristic correction building:
  - direct routing for command-like corrections
  - contextual wrapping for partial corrections with previous request text

The known limitation is:

```text
Correction rewriting is heuristic, not semantic.
```

The next improvement is:

```text
Add better correction-vs-continuation classification and optional bounded semantic rewrite for partial/contextual corrections.
```

---

## Important distinction

This is **not** the same task as instant ducking or hard-stop recognition.

Ducking/hard-stop task:

```text
stop
abort
please stop
cancel that
```

This task:

```text
I meant...
I mean...
No, I mean...
Actually...
Instead...
Not X, Y...
To clarify...
```

Do not mix the tasks unless both TODOs are intentionally being implemented together.

---

## Non-goals

Do not implement these in this task:

- speaker ducking fixes
- natural hard-stop phrase expansion
- web search
- web research
- Codex integration
- file/email/calendar capabilities
- router/capability redesign
- new TTS/STT providers
- full memory redesign
- destructive action support

This task should only improve:

```text
correction classification
correction/continuation distinction
semantic correction rewrite
tests around "I meant" vs "I mean"
```

---

## Core problem

Speech corrections are nuanced.

These are usually corrections:

```text
No, I meant medium.en.
Actually use Firefox.
Not Chrome, Firefox.
I meant the downloads folder, not documents.
No, I mean GitHub, not Facebook.
Wait, I meant beam 5.
```

These are often continuation or elaboration:

```text
I mean the orb should feel alive, but not chaotic.
What I mean is that it should pull the window outward.
I mean, we could make it more tactile.
To clarify, I want the animation to feel intentional.
```

But "I mean" can still be a correction when combined with replacement cues:

```text
No, I mean Firefox, not Chrome.
I mean medium.en instead of large.en.
I mean the API project, not the frontend.
```

So the rule cannot be:

```text
"I meant" = correction
"I mean" = not correction
```

The classifier needs contextual cues.

---

## Desired behavior

### Case 1 - Clear correction

Previous request:

```text
How much VRAM does whisper large.en with beam 5 use?
```

Interruption:

```text
No, I meant medium.en.
```

Expected classification:

```text
Correction
```

Expected regenerated request:

```text
How much VRAM does whisper medium.en with beam 5 use?
```

---

### Case 2 - "I mean" as continuation

Previous request:

```text
Help me design the external-open animation.
```

Interruption:

```text
I mean it should feel magical and tactile, not technical.
```

Expected classification:

```text
Continuation or clarification, not correction
```

Expected behavior:

- do not treat it like an emergency replacement unless current barge-in policy intentionally does
- if Merlin is currently speaking and this is an interruption, it may still stop/adjust the active response depending current correction-regeneration design
- but semantically it should preserve brainstorming context and continue the thought
- it should not be classified as a hard stop

Suggested regenerated/continued request if current pipeline requires regeneration:

```text
Continue the previous brainstorming request with this clarification:
I mean it should feel magical and tactile, not technical.
```

---

### Case 3 - "I mean" with replacement cue

Previous request:

```text
Open Chrome.
```

Interruption:

```text
No, I mean Firefox.
```

Expected classification:

```text
Correction
```

Expected regenerated request:

```text
Open Firefox.
```

---

### Case 4 - Tool correction

Previous request:

```text
Open facebook.com.
```

Interruption:

```text
Actually GitHub.
```

Expected classification:

```text
Correction
```

Expected regenerated request:

```text
Open github.com.
```

Expected route:

```text
open_url / OpenUrlTool
```

---

### Case 5 - Unsafe correction

Previous request:

```text
Find duplicate files.
```

Interruption:

```text
Actually delete them.
```

Expected classification:

```text
Correction
```

Expected regenerated request:

```text
Delete the duplicate files.
```

Expected route:

```text
existing unsupported/destructive/safety handling
```

It must not execute destructive action directly.

---

## Required investigation before coding

Inspect these files/classes if they exist:

```text
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/BargeInInterfaces.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/CorrectionRequestBuilder.cs
Merlin.Backend/Services/Interfaces/ICorrectionRequestBuilder.cs
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/HybridIntentParser.cs
Merlin.Backend/Services/LocalAIChatService.cs
Merlin.Backend/Services/Interfaces/ILocalAIChatService.cs
Merlin.Backend/Configuration/
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/CorrectionRegenerationTests.cs
Merlin.Backend.Tests/BargeInTests.cs
```

Search for:

```text
CorrectionRequestBuilder
Correction
BargeInAction.Correction
BargeInAction.Clarification
BargeInAction.Backchannel
CorrectionText
PreviousRequest
DispatchCorrection
ProcessAndEmitLiveRequestAsync
LocalAIChatService
DeepInfra
Semantic
Rewrite
I meant
I mean
actually
instead
not
```

Before editing, determine:

1. Where correction/backchannel/clarification classification currently happens.
2. Whether "I mean" is currently classified as correction.
3. Whether "I meant" is currently recognized.
4. How `CorrectionRequestBuilder` decides direct vs contextual strategy.
5. Whether previous request text is reliably available.
6. Whether correction generation currently calls DeepInfra directly. It should not.
7. Whether generated corrected requests route through normal pipeline.
8. How to add semantic rewrite without bypassing routing/safety.

Mention the answers in the final report.

---

# Part 1 - Add correction intent classification

## Goal

Create a clearer distinction between:

```text
Correction
Continuation
Clarification
Backchannel
HardStop
Unknown
```

This task mainly focuses on `Correction`, `Continuation`, and `Clarification`.

If the current models already have similar concepts, extend them instead of adding duplicates.

---

## Suggested models

Add or adapt:

```text
Merlin.Backend/Models/CorrectionIntent.cs
Merlin.Backend/Models/CorrectionClassificationResult.cs
```

Suggested enum:

```csharp
public enum CorrectionIntent
{
    Unknown,
    Correction,
    Continuation,
    Clarification,
    Backchannel,
    HardStop
}
```

Suggested result:

```csharp
public sealed record CorrectionClassificationResult(
    CorrectionIntent Intent,
    double Confidence,
    string Reason,
    bool ShouldCancelCurrentTurn,
    bool ShouldRegenerateRequest,
    bool NeedsSemanticRewrite);
```

If this belongs in `BargeInModels.cs`, put it there.

---

## Suggested service

Add:

```text
Merlin.Backend/Services/Interfaces/ICorrectionIntentClassifier.cs
Merlin.Backend/Services/CorrectionIntentClassifier.cs
```

Suggested interface:

```csharp
public interface ICorrectionIntentClassifier
{
    CorrectionClassificationResult Classify(
        string transcript,
        string? previousUserRequest,
        bool assistantIsSpeaking);
}
```

This classifier should be deterministic first. Semantic/LLM classification can be a future addition if needed.

---

## Deterministic classification rules

### Strong correction cues

Classify as correction when transcript includes:

```text
no, I meant
no I meant
I meant
I actually meant
actually use
actually open
actually search
instead
not X, Y
not X but Y
rather
replace
use X instead
open X instead
go to X instead
wait, I meant
sorry, I meant
```

Examples:

```text
No, I meant medium.en.
I meant Firefox, not Chrome.
Actually use GitHub.
Use the API project instead.
Not documents, downloads.
```

### "I mean" correction cues

`I mean` should classify as correction only when paired with replacement/negation/action cues:

```text
no, I mean
I mean X instead
I mean X, not Y
I mean open X
I mean use X
I mean go to X
```

Examples:

```text
No, I mean Firefox.
I mean medium.en instead of large.en.
I mean the backend project, not the frontend.
```

### "I mean" continuation cues

Classify as continuation/clarification when `I mean` appears without replacement cues and sounds like elaboration:

```text
I mean it should feel...
I mean we could...
I mean the idea is...
I mean what I want is...
What I mean is...
To clarify...
Basically...
More specifically...
```

Examples:

```text
I mean it should feel magical, not technical.
What I mean is the animation should pull outward.
I mean we could make the orb react first.
To clarify, I want it more tactile.
```

### Backchannel cues

Preserve existing behavior for:

```text
mhm
yeah
yes
okay
right
sure
```

These should not trigger correction rewrite.

### Hard stop cues

Do not duplicate hard-stop logic too much. If hard stop is already handled earlier, leave it there.

If this classifier sees obvious hard stop phrases, it may return `HardStop`, but hard-stop handling should remain the emergency path.

---

## Confidence policy

Use confidence to avoid over-eager correction.

Suggested thresholds:

```text
>= 0.80: act confidently
0.55 - 0.79: use contextual fallback or clarification behavior
< 0.55: do not regenerate as correction
```

Avoid treating every interruption as correction.

---

# Part 2 - Add semantic correction rewrite

## Goal

For partial/contextual corrections, use a bounded semantic rewrite service to create a clean corrected user request.

This should improve cases like:

```text
Previous:
How much VRAM does whisper large.en with beam 5 use?

Correction:
No, I meant medium.en.

Output:
How much VRAM does whisper medium.en with beam 5 use?
```

instead of:

```text
Correct my previous request using this correction...
```

---

## Important safety rule

The semantic rewriter must **not answer the user**.

It only rewrites the request.

It must not execute tools.

It must not call `CommandRouter`.

It must not bypass safety.

The rewritten request must still go through normal routing:

```text
rewritten corrected request
↓
WebSocketHandler / request dispatcher
↓
CommandRouter
↓
HybridIntentParser
↓
ToolRegistry / GeneralConversationTool / safety/missing handling
```

---

## Suggested service

Add:

```text
Merlin.Backend/Services/Interfaces/ISemanticCorrectionRewriteService.cs
Merlin.Backend/Services/SemanticCorrectionRewriteService.cs
Merlin.Backend/Models/SemanticCorrectionRewriteResult.cs
```

Suggested interface:

```csharp
public interface ISemanticCorrectionRewriteService
{
    Task<SemanticCorrectionRewriteResult> RewriteAsync(
        string previousUserRequest,
        string correctionTranscript,
        CancellationToken cancellationToken);
}
```

Suggested result:

```csharp
public sealed record SemanticCorrectionRewriteResult(
    bool Success,
    string CorrectedMessage,
    double Confidence,
    string Strategy,
    string Reason,
    string? ErrorMessage);
```

---

## Config

Add config if practical:

```json
"CorrectionRegeneration": {
  "SemanticRewriteEnabled": true,
  "MinimumRewriteConfidence": 0.72,
  "RewriteTimeoutSeconds": 4,
  "BypassSemanticRewriteForDirectCommands": true,
  "BypassSemanticRewriteForHighConfidenceToolCorrections": true,
  "FallbackToContextualWrapperOnFailure": true
}
```

If there is existing barge-in/correction config, extend it instead.

Default can be `false` if you want the first pass to be opt-in. But tests should verify behavior with it enabled.

---

## Prompt for semantic rewrite

Use a strict JSON prompt.

Do not send huge conversation context.

Only send:

```text
previous user request
correction transcript
maybe current assistant topic if already available cheaply
```

Suggested prompt:

```text
You rewrite interrupted user requests.

Return JSON only.

Task:
Given the previous user request and the user's correction, produce one clean corrected user request.

Rules:
- Do not answer the request.
- Do not execute tools.
- Do not add new facts.
- Preserve all constraints from the previous request unless the correction replaces them.
- If the correction replaces a specific value, substitute it.
- If the correction is a continuation rather than a correction, return intent "continuation".
- If uncertain, set confidence below 0.72.
- Keep the corrected request natural, as if the user had said it directly.
- Do not include explanations outside JSON.

JSON shape:
{
  "intent": "correction" | "continuation" | "clarification" | "unknown",
  "correctedMessage": "...",
  "confidence": 0.0,
  "reason": "..."
}

Previous user request:
"..."

Correction transcript:
"..."
```

---

## Semantic rewrite examples

### Example 1

Previous:

```text
How much VRAM does whisper large.en with beam 5 use?
```

Correction:

```text
No, I meant medium.en.
```

Expected JSON:

```json
{
  "intent": "correction",
  "correctedMessage": "How much VRAM does whisper medium.en with beam 5 use?",
  "confidence": 0.9,
  "reason": "The correction replaces large.en with medium.en while preserving the VRAM and beam size question."
}
```

### Example 2

Previous:

```text
Open Chrome.
```

Correction:

```text
No, I mean Firefox.
```

Expected JSON:

```json
{
  "intent": "correction",
  "correctedMessage": "Open Firefox.",
  "confidence": 0.92,
  "reason": "The correction replaces Chrome with Firefox."
}
```

### Example 3

Previous:

```text
Help me brainstorm the external-open animation.
```

Correction:

```text
I mean it should feel magical and tactile, not technical.
```

Expected JSON:

```json
{
  "intent": "continuation",
  "correctedMessage": "Continue brainstorming the external-open animation with this direction: it should feel magical and tactile, not technical.",
  "confidence": 0.82,
  "reason": "The user is elaborating the desired direction rather than replacing a previous target."
}
```

### Example 4

Previous:

```text
Find duplicate files in downloads.
```

Correction:

```text
Actually delete them.
```

Expected JSON:

```json
{
  "intent": "correction",
  "correctedMessage": "Delete the duplicate files in downloads.",
  "confidence": 0.86,
  "reason": "The correction changes the requested action from finding duplicate files to deleting them."
}
```

This rewritten message must still route through normal safety handling.

---

## Provider choice

Use the existing AI abstraction if possible.

Possible options:

```text
LocalAIChatService
ILocalAIClient
DeepInfra provider
local fallback
```

Prefer a small dedicated method/prompt rather than using general conversation.

Do not directly answer the user.

If using DeepInfra, keep:

- low max tokens
- short timeout
- strict JSON
- no memory context unless explicitly needed
- no tool execution

If the AI call fails or times out, fall back to heuristic/contextual builder.

---

# Part 3 - Integrate with CorrectionRequestBuilder

## Goal

`CorrectionRequestBuilder` should become strategy-based:

```text
direct command
deterministic continuation
semantic rewrite
contextual fallback
```

Suggested strategies:

```text
direct_command
deterministic_correction
semantic_rewrite
continuation_context
contextual_fallback
raw_correction
```

Suggested build result shape if not already present:

```csharp
public sealed record CorrectionRequestBuildResult(
    string CorrectedMessage,
    string Strategy,
    string? PreviousRequest,
    string CorrectionText,
    string OriginalCorrelationId,
    string NewCorrelationId,
    CorrectionIntent Intent,
    double Confidence,
    string Reason);
```

---

## Strategy selection

### Direct command bypass

Do not waste an LLM call for obvious command corrections:

```text
open Firefox instead
go to github.com instead
search the web for Godot docs
cancel that
stop
```

If it is clearly a tool-like command, route directly.

### Semantic rewrite

Use semantic rewrite when:

- previous request exists
- correction is partial/contextual
- classifier says correction or continuation with enough confidence
- config enables semantic rewrite
- not an obvious direct command

Examples:

```text
No, I meant medium.en.
I mean Firefox, not Chrome.
Actually GitHub.
More magical, less technical.
```

### Contextual fallback

Use existing contextual wrapper when:

- semantic rewrite disabled
- semantic rewrite times out
- semantic rewrite returns invalid JSON
- confidence below threshold
- previous request exists

### Raw correction fallback

Use raw correction when:

- no previous request exists
- correction text is self-contained
- contextual wrapper would be worse

---

## Dispatch behavior

Regardless of strategy:

```text
corrected message
↓
normal request pipeline
```

Do not route semantic results directly to DeepInfra.

---

# Part 4 - Recent request cache with expiry

The previous request must be reliable.

The agent that implemented correction regeneration noted:

```text
best next step is a bounded recent-request cache with expiry
```

Implement this if current previous-request access is weak.

Suggested service:

```text
Merlin.Backend/Services/Interfaces/IRecentUserRequestStore.cs
Merlin.Backend/Services/RecentUserRequestStore.cs
```

Suggested behavior:

- store recent user requests by conversation/session/client id
- include correlation id
- include timestamp
- include interaction source
- ignore empty transcripts
- bounded size
- expiry, for example 2-5 minutes
- do not store huge content
- do not store sensitive content beyond current conversation logging policy

Suggested model:

```csharp
public sealed record RecentUserRequest(
    string ConversationId,
    string CorrelationId,
    string Message,
    DateTimeOffset CreatedAt,
    string? InteractionSource);
```

This store helps corrections like:

```text
No, I meant medium.en.
```

know what they are correcting.

If existing session/conversation state already reliably provides previous request, reuse that instead.

---

# Part 5 - Tests

Add focused tests.

Likely files:

```text
Merlin.Backend.Tests/CorrectionIntentClassifierTests.cs
Merlin.Backend.Tests/SemanticCorrectionRewriteServiceTests.cs
Merlin.Backend.Tests/CorrectionRequestBuilderTests.cs
Merlin.Backend.Tests/CorrectionRegenerationTests.cs
```

Use fake AI/rewrite service for deterministic tests.

Do not make unit tests depend on live DeepInfra.

---

## Correction intent classifier tests

Required:

```text
"I meant medium.en" -> Correction
"No, I meant medium.en" -> Correction
"Wait, I meant medium.en" -> Correction
"I mean medium.en instead of large.en" -> Correction
"No, I mean Firefox" -> Correction
"I mean Firefox, not Chrome" -> Correction
"I mean it should feel magical" -> Continuation
"What I mean is it should feel magical" -> Continuation
"To clarify, I want it more tactile" -> Clarification/Continuation
"mhm" -> Backchannel
"okay" -> Backchannel
"stop" -> HardStop if classifier handles hard stop, or ignored if hard stop handled earlier
```

---

## Correction request builder tests

Required:

```text
Direct_tool_correction_bypasses_semantic_rewrite
Partial_i_meant_correction_uses_semantic_rewrite_when_enabled
I_mean_with_replacement_uses_semantic_rewrite
I_mean_elaboration_routes_as_continuation_context
Semantic_rewrite_low_confidence_falls_back_to_contextual_wrapper
Semantic_rewrite_timeout_falls_back_to_contextual_wrapper
No_previous_request_falls_back_to_raw_correction
New_correlation_id_is_created
Original_correlation_id_is_preserved
```

---

## Semantic rewrite tests with fake service

Required:

```text
Rewrites_large_to_medium_en
Rewrites_open_chrome_to_open_firefox
Rewrites_facebook_to_github
Continuation_rewrite_preserves_brainstorming_context
Unsafe_delete_rewrite_still_only_returns_message_for_normal_routing
Invalid_json_returns_failure
Timeout_returns_failure
Low_confidence_returns_failure_or_low_confidence_result
```

---

## End-to-end correction regeneration tests

Required:

```text
I_meant_medium_en_dispatches_clean_corrected_request
I_mean_magical_dispatches_continuation_request_to_general_conversation
No_i_mean_firefox_routes_to_OpenApplicationTool
Actually_github_routes_to_OpenUrlTool
Actually_delete_them_routes_to_unsupported_or_safety_not_direct_chat
Old_response_still_suppressed
New_response_still_sent
Hard_stop_still_does_not_regenerate
Backchannel_still_does_not_regenerate
```

---

# Part 6 - Safety requirements

Semantic rewrite can produce risky messages.

That is okay only if the message is routed through normal safety handling.

Do not execute the rewritten request directly.

Do not treat a rewrite as confirmation.

Do not silently upgrade safe requests into destructive execution.

Examples:

```text
Previous: "Find duplicate files"
Correction: "Actually delete them"
Rewrite: "Delete duplicate files"
Route: unsupported/destructive/confirmation path
```

Add tests for this.

---

# Part 7 - Logging and diagnostics

Add logs for:

```text
Correction classified. Intent=..., Confidence=..., Reason=...
Correction rewrite requested. Strategy=...
Correction rewrite succeeded. Confidence=..., Strategy=...
Correction rewrite failed. Reason=...
Correction builder fallback used. Strategy=...
Correction regenerated request dispatched. OriginalCorrelationId=..., NewCorrelationId=...
```

Avoid logging full sensitive text unless current Merlin logging policy allows it.

Prefer lengths/snippets if needed.

---

# Acceptance criteria

This task is complete when:

- [ ] "I meant X" is treated as correction.
- [ ] "No, I meant X" is treated as correction.
- [ ] "I mean X" is not always treated as correction.
- [ ] "I mean X" with replacement cues is treated as correction.
- [ ] "I mean..." as elaboration can route as continuation/context, not hard correction.
- [ ] Partial corrections can be semantically rewritten into clean corrected requests.
- [ ] Semantic rewrite is bounded, JSON-only, timeout-limited, and confidence-gated.
- [ ] Semantic rewrite failure falls back safely to existing heuristic/contextual behavior.
- [ ] Rewritten requests still go through normal pipeline.
- [ ] Tool corrections still route to tools.
- [ ] Brainstorming continuations still reach general conversation/DeepInfra naturally.
- [ ] Unsafe rewritten requests still route through safety/missing/unsupported handling.
- [ ] Existing live turn cancellation and correction regeneration still work.
- [ ] Hard stop behavior remains separate and is not broken.
- [ ] Tests cover "I meant" vs "I mean".
- [ ] Full backend test suite passes.

---

# Manual verification checklist

After implementation, manually test while Merlin is speaking.

## Correction

```text
No, I meant medium.en.
I meant Firefox, not Chrome.
Actually GitHub.
Not documents, downloads.
```

Expected:

```text
old answer stops
corrected request is clean
new answer/action uses corrected target
```

## Continuation / elaboration

```text
I mean it should feel magical, not technical.
What I mean is the orb should pull the tile outward.
To clarify, the animation should feel tactile.
```

Expected:

```text
Merlin continues/responds in the same conceptual direction
does not misclassify as hard stop
does not produce weird wrapper text if semantic rewrite is enabled
```

## Tool correction

```text
No, open Firefox instead.
No, go to github.com.
```

Expected:

```text
routes through OpenApplicationTool / OpenUrlTool
```

## Unsafe correction

```text
Actually delete them.
```

Expected:

```text
routes through existing safety/unsupported handling
does not execute destructive action directly
```

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. How "I meant" is classified.
4. How "I mean" is classified.
5. How replacement cues are detected.
6. How continuation/elaboration is handled.
7. How semantic rewrite works.
8. Which provider/service is used for semantic rewrite.
9. How timeout/confidence/fallback works.
10. How recent previous request context is retrieved.
11. How rewritten requests still go through normal routing.
12. How unsafe rewritten requests are kept safe.
13. Tests added.
14. Tests run and results.
15. Known limitations.
16. Next recommended improvement.

Do not simply say "implemented." Explain the actual lifecycle with classes/methods changed.

---

# Recommended first implementation cut

If the full version is too large, implement this smaller first cut:

```text
1. Add deterministic CorrectionIntentClassifier.
2. Improve CorrectionRequestBuilder strategy selection for "I meant" vs "I mean".
3. Add recent previous-request cache if current previous request context is unreliable.
4. Add fake semantic rewrite interface and tests, but keep real AI rewrite disabled by default.
5. Route all generated messages through the existing normal correction regeneration path.
6. Add tests for "I meant medium.en", "I mean magical", and "No, I mean Firefox".
```

Then implement real AI semantic rewrite in a second cut.
