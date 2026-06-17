# Phase 1 — Current Conversation Memory

## Goal

Implement Merlin's current conversation memory layer.

This phase gives Merlin a lightweight short-term working memory for the active topic.

By the end of this phase, Merlin should be able to answer internally:

- What conversation is active?
- What topic is active?
- What is the user's current goal?
- What concepts are currently active?
- Is this user message a follow-up or a new topic?
- What short summary describes the current topic so far?

This phase must **not** implement long-term memory writing, medium-memory topic closing, DeepInfra prompt compilation, or interruption behavior.

---

## Existing foundation to use

Use the existing persistence foundation. Do not rebuild it.

Expected existing capabilities:

- Conversation persistence.
- Topic persistence.
- Assistant turn persistence.
- Concept storage/search.
- Seed concepts.
- Store interfaces and EF-backed repositories.

Use existing interfaces where possible. If an interface is missing a small method required for this phase, extend it cleanly.

---

## Components to add

Add the following components, names can be adjusted to match existing project style:

```text
CurrentConversationMemoryService
TopicBoundaryDetector
CurrentConversationState
TopicBoundaryDecision
FollowUpCueDetector
ActiveConceptMerger
```

Suggested folder placement:

```text
Merlin.Backend/Core/Memory/Services/
Merlin.Backend/Core/Memory/Models/
Merlin.Backend/Core/Conversation/Services/
Merlin.Backend/Core/Conversation/Models/
```

Keep EF-specific code out of these classes.

---

## CurrentConversationState model

Create a model that represents the current active working memory.

Suggested shape:

```csharp
public sealed record CurrentConversationState
{
    public required string ConversationId { get; init; }
    public string? ActiveTopicId { get; init; }
    public string? ActiveTopicTitle { get; init; }
    public string? CurrentGoal { get; init; }
    public string? RecentSummary { get; init; }
    public IReadOnlyList<string> ActiveConcepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ActiveEntities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnresolvedQuestions { get; init; } = Array.Empty<string>();
    public DateTimeOffset LastUpdatedUtc { get; init; }
}
```

Do not overcomplicate this with embeddings or external AI.

---

## TopicBoundaryDecision model

Create a result object for topic boundary decisions.

Suggested shape:

```csharp
public sealed record TopicBoundaryDecision
{
    public required bool IsNewTopic { get; init; }
    public required bool ShouldClosePreviousTopic { get; init; }
    public required double Confidence { get; init; }
    public string? SuggestedTopicTitle { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<string> DetectedConcepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FollowUpCues { get; init; } = Array.Empty<string>();
}
```

The `Reason` field is important for debugging and future dashboard visibility.

---

## TopicBoundaryDetector MVP

Implement a local, rule-based detector.

Inputs:

- Current user message text.
- Current active topic state.
- Concepts extracted from the user message.

Output:

- Continue current topic.
- Start new topic.
- Optionally close previous topic.

### Continuation signals

Treat message as continuing the current topic if one or more of these are true:

- Message contains follow-up cues such as:
  - `this`
  - `that`
  - `it`
  - `the system`
  - `the thing`
  - `what about`
  - `and then`
  - `okay but`
  - `so now`
  - `can you create the md`
  - `create the todo`
  - `write the prompt`
  - `same`
  - `continue`
- Message shares concepts with active topic.
- Message refers to the same file/artifact/implementation area.
- Message asks for deeper implementation details of the current idea.

### New topic signals

Treat message as a new topic if:

- There is low concept overlap with current topic.
- No follow-up cue exists.
- User explicitly says:
  - `new topic`
  - `different question`
  - `separate thing`
  - `unrelated`
- User switches from Merlin memory architecture to an unrelated area such as Whisper VRAM, weather, finance, etc.

### MVP scoring

Use simple concept overlap.

Pseudo-code:

```csharp
var messageConcepts = conceptExtractor.Extract(userMessage);
var activeConcepts = current.ActiveConcepts;
var overlap = CalculateOverlap(messageConcepts, activeConcepts);
var hasFollowUpCue = followUpCueDetector.HasFollowUpCue(userMessage);

if (hasFollowUpCue || overlap >= 0.35)
{
    return ContinueCurrent(...);
}

return StartNewTopic(...);
```

Keep the threshold configurable.

---

## CurrentConversationMemoryService responsibilities

Implement a service with methods similar to:

```csharp
Task<CurrentConversationState> GetOrCreateCurrentStateAsync(CancellationToken cancellationToken = default);

Task<TopicBoundaryDecision> AnalyzeUserMessageAsync(
    string userMessage,
    CancellationToken cancellationToken = default);

Task<CurrentConversationState> ApplyUserMessageAsync(
    string userMessage,
    CancellationToken cancellationToken = default);

Task<CurrentConversationState> UpdateAfterAssistantResponseAsync(
    string assistantResponse,
    CancellationToken cancellationToken = default);
```

The exact method names can vary.

### GetOrCreateCurrentStateAsync

Should:

1. Find active conversation or create one.
2. Find active topic or create one.
3. Load topic title, summary, and concepts.
4. Return a `CurrentConversationState`.

### AnalyzeUserMessageAsync

Should:

1. Extract concepts from the user message.
2. Compare with active topic concepts.
3. Detect follow-up cues.
4. Return a `TopicBoundaryDecision`.

### ApplyUserMessageAsync

Should:

1. Analyze user message.
2. If same topic:
   - update active concepts.
   - update recent summary lightly.
   - update active topic timestamp.
3. If new topic:
   - for now, start a new topic but do **not** summarize old topic into medium memory yet.
   - medium-memory summarization belongs to Phase 3.
4. Return updated state.

---

## Summary update MVP

Do not call DeepInfra for summaries in this phase.

Use a simple rolling local summary strategy:

- If no summary exists, create a short template summary from the user message and concepts.
- If summary exists, append a compact sentence only when useful.
- Keep summary under a configurable character limit, for example 1200 characters.

Example:

```text
The active topic is Merlin's brain-like memory layer. The user is discussing how to phase implementation safely after the SQLite/EF persistence foundation was completed.
```

This is not meant to be perfect. It is a local working summary only.

---

## Active concept merging

When a new message continues the current topic:

1. Extract concepts from the message.
2. Merge into active topic concepts.
3. Deduplicate by normalized concept name.
4. Keep high-value project/technical concepts.
5. Avoid storing useless common words.
6. Limit active concepts to a reasonable number, for example 25-50.

Do not create a huge concept list.

---

## Tests to add

Add tests for:

### Test: creates active conversation and topic

Input:

```text
Let's design Merlin's memory system.
```

Expected:

- Active conversation exists.
- Active topic exists.
- Active topic title is created.
- Active concepts include memory/Merlin if extractor supports them.

### Test: follow-up continues topic

Existing topic:

```text
Merlin memory architecture
```

Input:

```text
okay but how should the prompt compiler work?
```

Expected:

- Same topic continues.
- `IsNewTopic = false`.
- Active concepts add prompt compiler.

### Test: unrelated message starts new topic

Existing topic:

```text
Merlin memory architecture
```

Input:

```text
what does beam do in Whisper?
```

Expected:

- New topic decision.
- `IsNewTopic = true`.
- Previous topic is not summarized yet in this phase.

### Test: summary stays bounded

Send multiple follow-up messages.

Expected:

- Recent summary does not grow indefinitely.
- Summary remains below configured character limit.

---

## Non-goals

Do not implement:

- Long-term memory saves.
- Explicit remember requests.
- Topic close summaries.
- Medium memory episodes.
- Associative retrieval.
- Prompt compiler.
- DeepInfra integration.
- Interruption behavior.
- Dashboard/API endpoints.

---

## Verification commands

Run:

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

Run tests:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

If tests are filtered:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter CurrentConversationMemory
```

---

## Final response requirements for the agent

Report:

- Files changed.
- Services/models added.
- How topic continuation is detected.
- How active concepts are updated.
- Build result.
- Test result.
- Known limitations.
- Whether it is safe to proceed to Phase 2.
