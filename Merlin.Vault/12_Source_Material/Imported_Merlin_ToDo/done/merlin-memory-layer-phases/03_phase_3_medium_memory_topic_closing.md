---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin-memory-layer-phases/03_phase_3_medium_memory_topic_closing.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 3 — Medium Memory and Topic Closing

## Goal

Implement the flow that turns a completed current topic into a medium-term episode memory.

This phase implements the user's three-layer memory idea:

```text
Current conversation memory
↓ when topic is done
Medium-term episodic memory
```

By the end of this phase, Merlin should be able to close an active topic, create a summarized episode memory, attach concepts, and mark the topic as closed.

This phase must not implement full associative retrieval, prompt compilation, DeepInfra integration, or interruption behavior.

---

## Why this phase matters

Medium memory is Merlin's episodic memory.

It stores summaries of conversations, debugging sessions, design sessions, implementation decisions, and experiments.

It should be broader than long-term memory, but still summarized.

Medium memory is where Merlin can later retrieve:

```text
We talked about this before.
Here is the summary of that topic.
These were the conclusions.
```

---

## Components to add

Add or extend components similar to:

```text
TopicClosingService
MediumMemoryWriter
TopicSummaryBuilder
TopicImportanceScorer
TopicCloseResult
```

Use existing:

```text
CurrentConversationMemoryService
MemoryWriter or memory store
ConceptExtractor
Concept stores
Conversation/topic stores
```

---

## Topic close behavior

Implement a method similar to:

```csharp
Task<TopicCloseResult> CloseCurrentTopicAsync(
    TopicCloseReason reason,
    CancellationToken cancellationToken = default);
```

Suggested close reasons:

```text
topic_switch
user_requested_summary
session_end
manual_close
interrupted_or_abandoned
implementation_completed
```

Suggested result:

```csharp
public sealed record TopicCloseResult
{
    public required bool Closed { get; init; }
    public string? TopicId { get; init; }
    public string? MediumMemoryId { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<string> Concepts { get; init; } = Array.Empty<string>();
    public string? Reason { get; init; }
}
```

---

## Episode memory shape

When a topic closes, create a memory record with type:

```text
episode
```

Suggested fields:

```text
MemoryType: episode
Title: topic title
Content: summarized episode content
Summary: shorter version if separate summary field exists
Project: Merlin when relevant
Topic: topic title or topic id depending existing schema
Importance: calculated by importance scorer
Confidence: 0.8-0.95 depending data quality
UserConfirmed: false unless user explicitly requested save
SourceConversationId: active conversation id
SourceTurnId: optional last turn id
```

Example content:

```text
The user discussed Merlin's brain-like memory layer after completing the SQLite/EF persistence foundation. The topic focused on splitting the memory implementation into safe phases. The agreed direction was to implement current conversation memory first, then explicit memory writing, medium memory topic closing, associative retrieval, prompt compilation, orchestration with DeepInfra, and debug visibility.
```

---

## TopicSummaryBuilder MVP

Do not call DeepInfra by default.

Use available local state:

- Topic title.
- Recent summary.
- Active concepts.
- Known current goal.
- Last few assistant/user turn snippets if available in stores.

Build a readable summary with a template.

Suggested template:

```text
Topic: {title}

Summary:
{recentSummary}

Key concepts:
{concepts}

Outcome:
{derived outcome or close reason}
```

But store as natural text in the memory record.

Keep summary bounded, for example:

```text
Target: 300-800 words maximum for complex topics.
MVP: 100-300 words is acceptable.
```

Do not store full raw transcripts as medium memory.

---

## Importance scoring MVP

Create a simple scorer.

Inputs:

- Close reason.
- Number of turns in topic if available.
- Active concepts.
- Whether topic relates to Merlin architecture.
- Whether user asked for a todo/spec/prompt.
- Whether explicit long-term memories were created during topic.

Suggested scoring:

```text
Base importance: 0.5
+0.2 if project includes Merlin
+0.2 if concepts include memory, DeepInfra, routing, voice, interruption, architecture, prompt compiler
+0.1 if topic had multiple turns
+0.1 if user asked for implementation artifact
+0.1 if user explicitly asked to save/summarize
Clamp between 0.1 and 1.0
```

Do not overbuild.

---

## Topic switch integration

Extend Phase 1 behavior carefully.

When `TopicBoundaryDetector` says a new topic has started:

1. Close previous topic.
2. Create medium memory episode.
3. Start new topic.

But avoid closing if:

- There is no meaningful topic content.
- Topic was just created and has no useful summary.
- User message is ambiguous.

If uncertain, keep current topic open.

---

## User-requested topic save

Support user phrases such as:

```text
save this topic
summarize this topic
store this discussion
create a memory of this
remember this whole discussion
```

Behavior:

- Close or snapshot current topic into medium memory.
- If user explicitly asked to remember the whole discussion, mark the resulting episode `UserConfirmed = true` or add metadata indicating user-requested save.
- Do not necessarily close the topic if the user wants to continue; support snapshot behavior if possible.

MVP can close the topic if snapshot is too much.

---

## Tests to add

### Test: close current topic creates episode memory

Setup:

- Active topic exists.
- Recent summary exists.
- Active concepts exist.

Action:

```text
CloseCurrentTopicAsync(topic_switch)
```

Expected:

- Topic status becomes closed.
- Episode memory is created.
- Memory type is episode.
- Concepts are linked.

### Test: topic switch closes previous topic

Existing topic:

```text
Merlin memory architecture
```

Input:

```text
what does beam do in Whisper?
```

Expected:

- Previous topic closed.
- Episode memory created for previous topic.
- New topic created for Whisper.

### Test: trivial topic does not create useless memory

Setup:

- Topic with no meaningful content.

Action:

```text
CloseCurrentTopicAsync(session_end)
```

Expected:

- No episode memory, or memory marked very low importance depending implementation.

### Test: user requested save marks source correctly

Input:

```text
save this discussion as memory
```

Expected:

- Episode memory created.
- Source/reason indicates user-requested summary.

---

## Non-goals

Do not implement:

- Full associative retrieval.
- Prompt compiler.
- DeepInfra integration.
- Automatic long-term promotion.
- Embeddings.
- Dashboard.
- Interruption behavior.

Automatic promotion can come later. For now, medium memory is enough.

---

## Verification commands

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter MediumMemory
```

Run all tests if practical:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

---

## Manual verification idea

If dev/debug commands exist, manually:

1. Start topic about Merlin memory.
2. Send a few follow-ups.
3. Send unrelated topic.
4. Inspect DB.
5. Confirm previous topic became an `episode` memory.

---

## Final response requirements for the agent

Report:

- Files changed.
- Topic close behavior implemented.
- Episode memory structure.
- Importance scoring rules.
- Build result.
- Test result.
- Known limitations.
- Whether it is safe to proceed to Phase 4.
