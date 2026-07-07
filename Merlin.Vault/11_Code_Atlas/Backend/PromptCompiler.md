---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# PromptCompiler

## File

`Merlin.Backend/Core/Memory/Services/PromptCompiler.cs`

Verified present in current repo.

## Purpose

Builds final model prompts from current user message, current conversation state, retrieved memories, topic context, profile facts, and token budget limits.

## Important Members

- `CompileAsync`: loads current conversation/profile facts, builds prompt blocks, renders prompt, resolves included concept ids, persists compilation record.
- `BuildPrompt`: assembles system/current-topic/current-conversation/memory/profile blocks.
- `AppendProfileFactBlocks`, `AddMemoryBlock`, `BuildRetrievalNotes`: select and format memory/profile content.
- `TokenBudgetService`: estimates and checks token usage.

## Created / Consumed

`MemoryOrchestrator` and `MemoryDebugService` call it. Prompt compilation records are stored via `IPromptCompilationStore`.

## Related Features

- [[Memory System]]
- [[Correction Layer]]
- [[Voice Interruption System]]

## Tests

- `PromptRendererTests.cs`
- `BrainLikeMemoryLayerTests.cs`

## Change Notes for Agents

This note is shorter because it is not part of the P0/P1 browser-motion hardening set, but it is source-driven and should be updated when the file changes.
