---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# MemoryOrchestrator

## File

`Merlin.Backend/Core/Memory/Services/MemoryOrchestrator.cs`

Verified present in current repo.

## Purpose

Coordinates memory retrieval, prompt compilation, current conversation updates, topic closing, response processing, and profile fact extraction.

## Important Members

- `PrepareForModelCallAsync`: records current user turn, retrieves associative memories, compiles prompt, and returns memory preparation result.
- `ProcessModelResponseAsync`: stores assistant response, updates current conversation, writes memories, closes topics, and detects profile facts.
- `PreferredTypes`: biases retrieval type by message intent.

## Created / Consumed

Command/LLM paths call the orchestrator before and after model generation. It calls AssociativeRetriever, CurrentConversationMemoryService, MemoryWriter, PromptCompiler, TopicClosingService, and optional profile fact services.

## Related Features

- [[Memory System]]
- [[Correction Layer]]
- [[Voice Interruption System]]

## Tests

- `BrainLikeMemoryLayerTests.cs`
- memory store/service tests

## Change Notes for Agents

This note is shorter because it is not part of the P0/P1 browser-motion hardening set, but it is source-driven and should be updated when the file changes.
