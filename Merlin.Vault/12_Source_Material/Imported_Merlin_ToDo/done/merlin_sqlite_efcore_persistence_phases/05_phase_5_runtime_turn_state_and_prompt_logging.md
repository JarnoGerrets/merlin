---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin_sqlite_efcore_persistence_phases/05_phase_5_runtime_turn_state_and_prompt_logging.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 5 - Runtime Conversation State, Turn State, and Prompt Logging

## Objective

Prepare the runtime state layer that future memory and interruption behavior will depend on.

This phase connects persistence to active conversation handling:

- current conversation
- current topic
- active assistant turn
- generated text so far
- spoken text so far
- interruption metadata
- prompt compilation logging

Do not build full voice barge-in yet. This phase only prepares the state that makes voice interruption possible later.

## Why this phase matters

The future interruption system must be able to handle this scenario:

```text
Merlin: "To build the memory system, I would start with PostgreSQL..."
User: "No, SQLite."
```

To regenerate correctly through DeepInfra, Merlin must know:

- the original user message
- the active topic
- what answer was being generated
- what text was already spoken
- what user correction interrupted it
- what memory/context was included in the original prompt

This phase makes that data available.

## Required services

Create services in Core/Application layer, not Infrastructure:

```text
Merlin.Backend/Core/Conversation/
├── IConversationRuntimeState.cs
├── ConversationRuntimeState.cs
├── IAssistantTurnTracker.cs
├── AssistantTurnTracker.cs
├── IPromptCompilationLogger.cs
└── PromptCompilationLogger.cs
```

Folder names may vary depending on existing architecture. Keep the separation clear:

```text
runtime state service -> uses store interfaces -> store implementations use EF Core
```

## ConversationRuntimeState

Purpose: provide a clean way to get or update the active conversation and active topic.

Suggested interface:

```csharp
public interface IConversationRuntimeState
{
    Task<ConversationRecord> GetCurrentConversationAsync(
        CancellationToken cancellationToken = default);

    Task<ConversationTopicRecord?> GetCurrentTopicAsync(
        CancellationToken cancellationToken = default);

    Task<ConversationTopicRecord> StartOrSwitchTopicAsync(
        string topicTitle,
        CancellationToken cancellationToken = default);

    Task CompleteCurrentTopicAsync(
        string? summary,
        CancellationToken cancellationToken = default);
}
```

This does not need advanced topic detection yet.

MVP topic behavior:

- create active conversation if missing
- create active topic if missing
- allow explicit topic switch
- allow topic completion with summary

## AssistantTurnTracker

Purpose: own active turn tracking and persistence of partial generated/spoken text.

Suggested interface:

```csharp
public interface IAssistantTurnTracker
{
    Task<AssistantTurnRecord> StartTurnAsync(
        string originalUserMessage,
        string? topicId,
        CancellationToken cancellationToken = default);

    Task AppendGeneratedTextAsync(
        string turnId,
        string textDelta,
        CancellationToken cancellationToken = default);

    Task AppendSpokenTextAsync(
        string turnId,
        string spokenTextDelta,
        CancellationToken cancellationToken = default);

    Task MarkTurnStateAsync(
        string turnId,
        string state,
        CancellationToken cancellationToken = default);

    Task MarkInterruptedAsync(
        string turnId,
        string reason,
        string interruptedByUserMessage,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        string turnId,
        CancellationToken cancellationToken = default);
}
```

Important: do not save after every single character/token. Batch updates.

Recommended update cadence:

- update generated text after sentence boundaries or chunks
- update spoken text after TTS chunk playback completes
- update immediately on interruption/completion/failure

## Turn IDs

Every assistant turn must have a unique ID.

Use a GUID or ULID-style ID.

Example:

```csharp
Guid.NewGuid().ToString("N")
```

The exact format is not critical, but it must be unique and stable.

Future interruption behavior will rely on this rule:

```text
Every generated token, TTS chunk, audio buffer, DeepInfra stream, and memory update belongs to a TurnId.
If the turn is cancelled, late output from that TurnId must be ignored.
```

This phase only prepares the DB/runtime side of that rule.

## PromptCompilationLogger

Purpose: persist what Merlin sends to DeepInfra.

Suggested interface:

```csharp
public interface IPromptCompilationLogger
{
    Task LogPromptAsync(
        string conversationId,
        string? turnId,
        string promptType,
        string compiledPrompt,
        int? estimatedInputTokens,
        IReadOnlyCollection<string> includedMemoryIds,
        IReadOnlyCollection<string> includedConceptIds,
        CancellationToken cancellationToken = default);
}
```

Prompt types:

```text
normal
correction
clarification
continuation
summary
memory_write
```

## Token estimate helper

Create a simple token estimate helper.

It does not need to be perfect.

MVP estimate:

```text
estimated tokens = characters / 4
```

Suggested service:

```csharp
public interface ITokenEstimator
{
    int EstimateTokens(string text);
}
```

This lets Merlin log approximate prompt usage before exact provider usage is available.

## Prompt logging privacy and size rules

Storing full compiled prompts can be useful for debugging, but it may also grow the database.

For now, store full prompt text because this is a development system and prompt auditing is important.

But implement with awareness:

- do not log prompts at info level
- database storage is okay
- future cleanup/retention can be added later
- include token estimate
- include memory IDs and concept IDs separately

## Prompt compilation record examples

Normal prompt:

```text
PromptType: normal
TurnId: current turn
IncludedMemoryIdsJson: ["mem-1", "mem-2"]
IncludedConceptIdsJson: ["concept-memory", "concept-sqlite"]
EstimatedInputTokens: 850
```

Correction prompt:

```text
PromptType: correction
TurnId: new correction turn or interrupted turn depending on final design
IncludedMemoryIdsJson: ["mem-memory-architecture"]
IncludedConceptIdsJson: ["concept-sqlite", "concept-correction"]
EstimatedInputTokens: 420
```

## Runtime state should not replace persistent memory

Conversation state is not the same as long-term memory.

Conversation/turn state answers:

```text
What is happening right now?
What is Merlin currently saying?
What was interrupted?
What prompt was sent?
```

Memory answers:

```text
What does Merlin know from past conversations?
What did the user ask Merlin to remember?
What project decisions exist?
What concepts are related?
```

Both are needed.

## DeepInfra-aware correction preparation

This phase should not implement the full interruption controller, but it should make the data available to build a correction prompt later.

Ensure stored turn state can answer:

```text
OriginalUserMessage
GeneratedTextSoFar
SpokenTextSoFar
InterruptionReason
InterruptedByUserMessage
```

For the example:

```text
OriginalUserMessage:
"How should we build Merlin's memory system?"

SpokenTextSoFar:
"To build the memory system, I would start with PostgreSQL..."

InterruptedByUserMessage:
"No, SQLite."
```

Later, the correction prompt builder can generate:

```text
The assistant was answering the original request and started in a PostgreSQL direction. The user interrupted with "No, SQLite." Discard PostgreSQL and continue with SQLite as the database choice.
```

## State update safety

If a turn is marked interrupted, do not later mark it completed unless there is a deliberate state transition rule.

Invalid sequence:

```text
speaking -> interrupted -> completed
```

Valid sequence:

```text
speaking -> interrupted
new correction turn -> completed
```

Or:

```text
speaking -> paused -> speaking -> completed
```

## Basic state machine constants

Create constants instead of scattering strings everywhere.

Example:

```csharp
public static class AssistantTurnStates
{
    public const string Created = "created";
    public const string Thinking = "thinking";
    public const string StreamingResponse = "streaming_response";
    public const string GeneratingTts = "generating_tts";
    public const string Speaking = "speaking";
    public const string Paused = "paused";
    public const string Interrupted = "interrupted";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
```

Similar constants can exist for topic statuses and prompt types.

## Tests or smoke checks

Add tests or dev commands that prove:

1. current conversation can be created
2. topic can be started
3. turn can be started
4. generated text can be appended
5. spoken text can be appended
6. turn can be marked interrupted
7. prompt compilation can be logged
8. prompt compilation can be retrieved by turn

## Example smoke flow

```text
Start conversation
Start topic: "SQLite EF persistence"
Start turn with original user message: "How should we build this?"
Append generated: "I would use PostgreSQL..."
Append spoken: "I would use PostgreSQL..."
Mark interrupted with: "No, SQLite."
Log correction prompt compilation
Verify DB contains the expected state
```

## What not to do in Phase 5

Do not:

- implement full interruption controller
- build voice barge-in
- keep microphone open while speaking
- cancel DeepInfra streams yet
- implement MemoryCompiler fully
- implement long-term memory promotion
- summarize topics automatically with DeepInfra

This phase prepares the state foundation only.

## Phase 5 acceptance criteria

Phase 5 is complete when:

- Runtime conversation state service exists.
- Assistant turn tracker exists.
- Prompt compilation logger exists.
- Token estimator exists.
- Turn states/constants exist.
- Current conversation/topic can be persisted.
- Assistant turn partial generated/spoken text can be persisted.
- Assistant turn can be marked interrupted.
- Prompt compilation can be logged with estimated tokens and included IDs.
- Smoke test/dev command validates the full state flow.
- The code builds.

## Suggested final agent message after Phase 5

```text
Phase 5 complete. Merlin now has persistent runtime conversation and assistant turn state, including partial generated/spoken text and interruption metadata. Prompt compilations can be logged with token estimates and included memory/concept IDs. This prepares the system for the MemoryCompiler and later DeepInfra-aware interruption behavior.
```
