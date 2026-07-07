---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin-memory-layer-phases/06_phase_6_memory_orchestrator_deepinfra_integration.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 6 — Memory Orchestrator and DeepInfra Integration

## Goal

Connect the memory layer to Merlin's real conversation flow.

By the end of this phase, when Merlin decides a request must go to DeepInfra, it should:

```text
User message
↓
Update/analyze current topic
↓
Retrieve relevant memories
↓
Compile compact prompt
↓
Send compiled prompt to DeepInfra
↓
Receive response
↓
Update current conversation memory
↓
Optionally write explicit memories
```

This phase is sensitive because it affects actual model input and token usage.

Do not implement interruption behavior here.

---

## Why this phase matters

Previous phases built memory pieces in isolation.

This phase makes Merlin actually use them.

The desired behavior is:

```text
DeepInfra should receive a compact memory-aware prompt, not raw full chat history.
```

---

## Components to add

Add components similar to:

```text
MemoryOrchestrator
MemoryPreparationResult
MemoryPostResponseProcessor
DeepInfraPromptAdapter
MemoryPipelineOptions
```

Use existing:

```text
CurrentConversationMemoryService
MemoryWriter
TopicClosingService
AssociativeRetriever
PromptCompiler
Prompt compilation store
Existing DeepInfra client/call path
Existing router/escalation logic
```

---

## MemoryOrchestrator responsibilities

The orchestrator coordinates memory before and after model calls.

Suggested methods:

```csharp
Task<MemoryPreparationResult> PrepareForModelCallAsync(
    string userMessage,
    string escalationReason,
    CancellationToken cancellationToken = default);

Task ProcessModelResponseAsync(
    string userMessage,
    string assistantResponse,
    MemoryPreparationResult preparation,
    CancellationToken cancellationToken = default);
```

Suggested result:

```csharp
public sealed record MemoryPreparationResult
{
    public required string ConversationId { get; init; }
    public string? TopicId { get; init; }
    public string? TurnId { get; init; }
    public required string CompiledPrompt { get; init; }
    public required int EstimatedInputTokens { get; init; }
    public IReadOnlyList<string> IncludedMemoryIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> IncludedConceptIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RetrievedMemory> RetrievedMemories { get; init; } = Array.Empty<RetrievedMemory>();
}
```

---

## Before DeepInfra flow

Implement:

```text
1. Receive current user message.
2. Get or create current conversation state.
3. Apply/analyze user message.
4. Detect explicit remember request.
5. If explicit remember request can be handled locally, save it and optionally do not call DeepInfra unless a response is still needed.
6. Retrieve relevant memories with AssociativeRetriever.
7. Compile prompt with PromptCompiler.
8. Create/update assistant turn state if existing turn system requires it.
9. Return compiled prompt to DeepInfra call path.
```

Important: Whether explicit remember requests still call DeepInfra depends on the existing conversation UX. For MVP, it is acceptable to save memory locally and return a simple local response like `Saved.` without DeepInfra.

---

## After DeepInfra response flow

Implement:

```text
1. Update current conversation memory with assistant response.
2. Update generated text / spoken text if those hooks exist.
3. Process explicit memory save result if not already completed.
4. Do not automatically promote assistant claims to long-term memory.
5. Avoid creating medium memories on every response.
6. Let topic closing happen only on topic switch, explicit save, or session end.
```

Be conservative.

---

## Integration point discovery

Before changing code, inspect the existing Merlin backend flow:

- Where user messages enter.
- Where local intent routing happens.
- Where DeepInfra is called.
- Where current conversation history is currently constructed.
- Where assistant turns are tracked.
- Where TTS receives assistant text.

Replace or wrap the prompt construction step carefully.

Do not break local tools.

If a request can be answered locally, memory retrieval and DeepInfra prompt compilation may not be needed.

---

## Required behavior with local tools

If local router/tool can answer:

```text
User message
↓
Local tool handles it
↓
Current conversation memory may update lightly
↓
No DeepInfra call
```

Examples:

```text
what time is it
what date is it
set volume
```

Do not force DeepInfra just because memory exists.

---

## Required behavior with DeepInfra

If DeepInfra is needed:

```text
User message exact text must appear in compiled prompt.
Only relevant memories may appear.
Full raw conversation history must not be sent by default.
Prompt compilation must be logged.
```

---

## Safety fallback

If memory preparation fails:

- Do not crash Merlin.
- Log error.
- Fall back to minimal prompt with exact current user message.
- Do not send full history as fallback.

Fallback prompt:

```text
CURRENT USER MESSAGE:
"{exact user message}"
```

Maybe include a minimal system instruction.

---

## Token logging requirement

For every DeepInfra call after this phase, logs should include:

```text
estimated input tokens
prompt type
included memory count
included memory ids
conversation id
topic id
turn id if available
escalation reason
```

This is critical for verifying token reduction.

---

## Tests to add

### Test: model call uses compiled prompt

Setup:

- Store relevant memories.
- User asks related question.

Expected:

- DeepInfra client receives compiled prompt, not raw history.
- Prompt includes exact user message.
- Prompt includes relevant memory.
- Prompt compilation log exists.

Use a fake DeepInfra client if possible.

### Test: local tool path does not call DeepInfra

Input:

```text
what time is it
```

Expected:

- Local tool path remains local.
- No DeepInfra call.
- Memory may update lightly or not at all.

### Test: explicit remember can be handled locally

Input:

```text
Remember that Merlin should keep memory local-first.
```

Expected:

- Memory saved.
- DeepInfra not required unless existing UX demands a model response.

### Test: memory failure fallback

Force memory service failure.

Expected:

- No crash.
- DeepInfra receives minimal prompt.
- Full raw history is not sent.

### Test: prompt compilation log created for DeepInfra call

Expected:

- Log row exists.
- Estimated tokens > 0.
- Included memory IDs recorded.

---

## Non-goals

Do not implement:

- Interruption behavior.
- Correction regeneration prompts beyond compiling support already built.
- Embeddings.
- Dashboard UI.
- Full memory dashboard.
- Automatic aggressive long-term promotion.
- Perfect summaries.

---

## Verification commands

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter MemoryOrchestrator
```

Run all tests if practical:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

---

## Manual verification scenario

1. Start Merlin backend.
2. Save explicit memory:

```text
Remember that Merlin should never send full chat history to DeepInfra by default.
```

3. Ask:

```text
how should Merlin reduce DeepInfra costs again?
```

4. Inspect prompt compilation log.
5. Confirm:

- Prompt includes relevant memory.
- Prompt includes exact current message.
- Prompt does not include full raw chat history.
- Estimated tokens are logged.

---

## Final response requirements for the agent

Report:

- Files changed.
- Integration point used.
- How local tool path is preserved.
- How DeepInfra prompt construction changed.
- Fallback behavior.
- Build result.
- Test result.
- Known limitations.
- Whether it is safe to proceed to Phase 7.
