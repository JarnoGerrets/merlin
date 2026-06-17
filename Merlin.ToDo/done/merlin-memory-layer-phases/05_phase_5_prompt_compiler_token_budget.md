# Phase 5 — Prompt Compiler and Token Budget

## Goal

Implement Merlin's prompt compiler.

This is the component that turns local memory into a compact DeepInfra context packet.

By the end of this phase, Merlin should be able to compile a prompt context containing:

- A concise system/task instruction.
- Current topic summary.
- Relevant long-term memories.
- Relevant medium memories.
- User preferences when relevant.
- Exact current user message.
- Token estimate.
- Included memory IDs.
- Included concept IDs.
- Prompt compilation log entry.

This phase must not yet integrate the compiler into the live DeepInfra call path unless that is explicitly part of Phase 6.

---

## Non-negotiable rule

The current user message is sacred.

Always include the current user message exactly as the user said it.

Do not summarize, rewrite, clean up, or reinterpret the current user message in the prompt compiler.

Previous context may be summarized, compressed, omitted, or trimmed.

---

## Why this phase matters

This is where Merlin starts reducing DeepInfra token usage.

Bad behavior:

```text
Send full conversation history to DeepInfra every time.
```

Correct behavior:

```text
Retrieve only relevant local memories.
Compile a tiny context packet.
Send current user message exactly.
Log what was sent and why.
```

---

## Components to add

Add components similar to:

```text
PromptCompiler
TokenBudgetService
CompiledMemoryContext
PromptCompileRequest
PromptCompileResult
PromptSection
PromptMemoryItem
PromptBudgetOptions
```

Use existing:

```text
CurrentConversationMemoryService
AssociativeRetriever
PromptCompilation store/logging
Token estimator if already created
```

---

## Prompt compile request model

Suggested shape:

```csharp
public sealed record PromptCompileRequest
{
    public required string CurrentUserMessage { get; init; }
    public required string PromptType { get; init; }
    public string? ConversationId { get; init; }
    public string? TurnId { get; init; }
    public string? EscalationReason { get; init; }
    public int MaxInputTokens { get; init; } = 2500;
    public int MaxMemoryTokens { get; init; } = 1000;
    public IReadOnlyList<RetrievedMemory> RetrievedMemories { get; init; } = Array.Empty<RetrievedMemory>();
}
```

Prompt types can include:

```text
normal_conversation
project_design
debugging
correction_regeneration
clarification
summary
```

Correction regeneration can be fully used later by interruption behavior, but the type can exist now.

---

## Prompt compile result model

Suggested shape:

```csharp
public sealed record PromptCompileResult
{
    public required string CompiledPrompt { get; init; }
    public required int EstimatedInputTokens { get; init; }
    public IReadOnlyList<string> IncludedMemoryIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> IncludedConceptIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OmittedMemoryIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TrimReasons { get; init; } = Array.Empty<string>();
    public string? PromptCompilationId { get; init; }
}
```

---

## Token estimation

Use simple token estimation for MVP:

```text
estimated_tokens = character_count / 4
```

Round up.

Create a `TokenBudgetService` so this can be replaced later.

Suggested methods:

```csharp
int EstimateTokens(string text);
bool IsWithinBudget(string text, int maxTokens);
```

---

## Prompt structure

Use stable sections.

Suggested compiled prompt format:

```text
SYSTEM:
You are Merlin's reasoning model. Use the compact local memory context. Do not assume missing project details. Prefer practical, local-first, cost-conscious guidance. Do not mention memory internals unless relevant.

CURRENT TOPIC:
{current topic summary if available}

RELEVANT LONG-TERM MEMORY:
- {memory title/content}

RELEVANT MEDIUM MEMORY:
- {episode summary}

USER PREFERENCES:
- {preference memory}

RETRIEVAL NOTES:
- {optional brief reasons, only if useful}

CURRENT USER MESSAGE:
"{exact current user message}"
```

Do not include huge JSON unless needed.

Keep the format readable for the model.

---

## Memory selection rules

Given retrieved memories:

1. Sort by score descending.
2. Prefer long-term architecture decisions and project goals when relevant.
3. Include user preferences when relevant.
4. Include medium episode summaries when they add context not already covered by long-term memories.
5. Deduplicate overlapping content.
6. Stop when memory token budget is reached.
7. Omit low-scoring memories.
8. Log omitted memories and reasons.

---

## Budget defaults

Implement configurable defaults.

Suggested:

### Tiny/simple request

```text
MaxInputTokens: 1000
MaxMemoryTokens: 300
```

### Normal project discussion

```text
MaxInputTokens: 2500
MaxMemoryTokens: 1000
```

### Deep design/debug session

```text
MaxInputTokens: 6000
MaxMemoryTokens: 2500
```

For MVP, default to normal project discussion.

---

## Hard budget behavior

If prompt exceeds budget:

1. Remove retrieval notes first.
2. Use memory summaries instead of full content when available.
3. Remove lowest-scoring medium memories.
4. Remove low-scoring long-term memories only if necessary.
5. Keep user preferences if relevant and short.
6. Never remove the current user message.
7. If still too large, include a warning metadata/log entry but send minimal prompt.

Do not silently exceed budget.

---

## Prompt compilation logging

Every compile should write a prompt compilation record using existing store.

Log:

```text
conversation id
turn id
prompt type
compiled prompt
estimated input tokens
included memory ids
included concept ids
created timestamp
```

If storing full compiled prompt is too sensitive later, this can be changed. For MVP, it is useful for debugging.

---

## Tests to add

### Test: current user message is exact

Input:

```text
i think we should use sql lite? maybe appdata? don't change my words
```

Expected:

- Compiled prompt contains exact string.

### Test: includes top relevant memories

Given retrieved memories with scores:

```text
0.95 architecture decision
0.80 user preference
0.20 unrelated episode
```

Expected:

- High-score memories included.
- Low-score memory omitted if budget tight.

### Test: token budget trimming

Given many long memories and low MaxInputTokens.

Expected:

- Prompt stays within budget or as close as possible.
- Omitted memory IDs recorded.
- Current user message remains exact.

### Test: prompt compilation is logged

Compile prompt.

Expected:

- Prompt compilation record saved.
- Estimated tokens saved.
- Included memory IDs saved.

### Test: summaries preferred over full content under budget pressure

Memory has summary and long content.

Expected:

- Summary used when budget is tight.

---

## Non-goals

Do not implement:

- Live DeepInfra pipeline integration.
- Automatic retrieval inside compiler if orchestration is not ready.
- Embeddings.
- Interruption behavior.
- Dashboard.
- Perfect token counting.

---

## Verification commands

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter PromptCompiler
```

Run all tests if practical:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

---

## Manual verification idea

Create several memories manually/test setup:

- Three-layer memory architecture.
- Reduce DeepInfra token usage.
- User prefers detailed implementation docs.

Compile prompt for:

```text
how were we going to build that brain memory thing again?
```

Expected prompt:

- Includes only relevant memories.
- Does not include unrelated memories.
- Exact user message appears at bottom.
- Prompt compilation log exists.

---

## Final response requirements for the agent

Report:

- Files changed.
- Prompt format implemented.
- Budget defaults.
- Trimming behavior.
- Logging behavior.
- Build result.
- Test result.
- Known limitations.
- Whether it is safe to proceed to Phase 6.
