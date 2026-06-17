# Phase 2 — Explicit Memory Writer

## Goal

Implement the first visible memory behavior: explicit user memory saving.

By the end of this phase, when the user says something like:

```text
Remember that Merlin should never send full chat history to DeepInfra by default.
```

Merlin should store a confirmed long-term memory locally, attach concepts to it, and avoid creating obvious duplicates.

This phase must not implement medium-memory topic closing, associative retrieval scoring, prompt compilation, DeepInfra integration, or interruption behavior.

---

## Why this phase matters

Explicit memory saves are the safest type of long-term memory because the user directly asked Merlin to remember something.

This phase gives Merlin a trustworthy long-term memory path before automatic memory writing is introduced.

---

## Components to add

Add components similar to:

```text
MemoryWriter
ExplicitMemoryRequestDetector
MemoryTypeClassifier
MemoryDuplicateDetector
MemoryConceptLinker
MemorySaveResult
ExplicitMemoryRequest
```

Suggested folders:

```text
Merlin.Backend/Core/Memory/Services/
Merlin.Backend/Core/Memory/Models/
```

Use existing memory and concept stores.

---

## Explicit memory detection

Implement local rule-based detection.

Detect phrases such as:

```text
remember that
remember this
save this
store this
note that
make a note that
from now on
in the future
always do this
never do this
keep in mind
```

The detector should produce a structured result.

Suggested model:

```csharp
public sealed record ExplicitMemoryRequest
{
    public required bool IsExplicitMemoryRequest { get; init; }
    public string? ContentToRemember { get; init; }
    public string? TriggerPhrase { get; init; }
    public double Confidence { get; init; }
    public string? Reason { get; init; }
}
```

Examples:

Input:

```text
Remember that Merlin should use SQLite for local memory.
```

Output:

```text
IsExplicitMemoryRequest = true
ContentToRemember = "Merlin should use SQLite for local memory."
TriggerPhrase = "Remember that"
Confidence = 0.95
```

Input:

```text
Can you remind me tomorrow to check this?
```

Output:

```text
IsExplicitMemoryRequest = false
```

Do not confuse reminders with memory saves.

---

## Memory type classification MVP

Implement a simple local classifier for explicit memories.

Suggested types:

```text
architecture_decision
project_goal
user_preference
tool_preference
implementation_note
fact
```

Heuristics:

### architecture_decision

Use when content contains project architecture decisions:

```text
Merlin should use...
The memory system should...
DeepInfra should...
Use SQLite...
Never send full chat history...
```

### project_goal

Use when content describes broad project goals:

```text
reduce token usage
keep costs low
local-first
fast response
inspectable memory
```

### user_preference

Use when the user expresses a stable preference:

```text
I prefer...
I like...
I don't like...
Always give me...
```

### tool_preference

Use when the memory affects a tool's behavior:

```text
when speaking dates...
when using the time tool...
when formatting...
```

### implementation_note

Use for practical implementation decisions that are not global architecture decisions.

### fact

Fallback.

Do not overbuild with a model call.

---

## Memory save behavior

When explicit memory is detected:

1. Extract content to remember.
2. Classify memory type.
3. Extract concepts.
4. Check for duplicates.
5. Save memory to long-term memory.
6. Mark `UserConfirmed = true`.
7. Set importance based on type and wording.
8. Link concepts.
9. Return a short result that the caller can use to respond.

Suggested save result:

```csharp
public sealed record MemorySaveResult
{
    public required bool Saved { get; init; }
    public string? MemoryId { get; init; }
    public string? MemoryType { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<string> Concepts { get; init; } = Array.Empty<string>();
    public bool WasDuplicate { get; init; }
    public string? ExistingMemoryId { get; init; }
    public string? Message { get; init; }
}
```

---

## Memory title generation MVP

Do not call DeepInfra.

Generate a short title locally.

Examples:

Content:

```text
Merlin should never send full chat history to DeepInfra by default.
```

Title:

```text
Do not send full chat history to DeepInfra
```

Content:

```text
The user prefers detailed implementation-ready Markdown files for coding agents.
```

Title:

```text
User prefers detailed implementation Markdown
```

Keep title under 80 characters if possible.

---

## Concept linking

Use existing concept extraction and concept stores.

For saved memory:

1. Extract concepts.
2. Create missing concepts only when appropriate.
3. Link concepts to memory with weights.
4. Give known important concepts higher weights.

Suggested weights:

```text
Direct extracted concept: 1.0
Project concept such as Merlin: 1.0
Provider/tool concept such as DeepInfra/SQLite: 0.9
Generic concept: 0.5-0.7
```

Do not create concepts for useless words.

---

## Duplicate detection MVP

Avoid saving obvious duplicates.

Implement simple duplicate checks:

1. Normalize content:
   - lowercase
   - trim
   - remove repeated spaces
   - remove punctuation that does not matter
2. Search existing long-term memories with same or similar content.
3. If exact normalized content exists, return existing memory.
4. If very similar title/content exists, optionally update existing memory instead of creating a new one.

No embeddings needed.

Expected behavior:

Input twice:

```text
Remember that Merlin should never send full chat history to DeepInfra by default.
```

Expected:

- First call creates memory.
- Second call returns duplicate/existing memory and does not create another identical row.

---

## User response guidance

This phase only creates backend behavior, but define expected response text for the future caller.

If memory saved:

```text
Saved.
```

Or:

```text
Saved — I’ll remember that Merlin should never send full chat history to DeepInfra by default.
```

If duplicate:

```text
I already had that saved.
```

Keep it short.

---

## Tests to add

### Test: explicit remember creates long-term memory

Input:

```text
Remember that Merlin should never send full chat history to DeepInfra by default.
```

Expected:

- Memory saved.
- Memory type is `architecture_decision` or equivalent.
- `UserConfirmed = true`.
- Importance high.
- Concepts include Merlin and DeepInfra if extractor supports them.

### Test: user preference memory

Input:

```text
Remember that I prefer detailed implementation-ready markdown files for coding agents.
```

Expected:

- Memory saved.
- Type is `user_preference`.
- User confirmed true.

### Test: duplicate explicit memory not duplicated

Save same memory twice.

Expected:

- Only one durable memory exists or second save reports duplicate.

### Test: reminder is not explicit memory

Input:

```text
Remind me tomorrow to check the database.
```

Expected:

- Not treated as memory save.

---

## Non-goals

Do not implement:

- Automatic long-term memory promotion.
- Medium memories.
- Topic close summaries.
- Prompt compiler.
- Associative retrieval.
- DeepInfra integration.
- Embeddings.
- Memory dashboard.
- Interruption behavior.

---

## Verification commands

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter MemoryWriter
```

Run all tests if practical:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

---

## Final response requirements for the agent

Report:

- Files changed.
- Detection phrases supported.
- Memory types supported.
- Duplicate behavior.
- Build result.
- Test result.
- Known limitations.
- Whether it is safe to proceed to Phase 3.
