---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin-memory-layer-phases/04_phase_4_associative_retrieval_mvp.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 4 — Associative Retrieval MVP

## Goal

Implement the first version of Merlin's filing-cabinet style memory retrieval.

By the end of this phase, Merlin should be able to retrieve relevant memories using:

- Keyword search.
- Direct concept matches.
- One-hop related concepts from the concept graph.
- Importance scoring.
- Recency scoring.
- Human-readable retrieval reasons.

This phase must not implement embeddings, multi-hop graph traversal, full prompt compilation, DeepInfra integration, or interruption behavior.

---

## Why this phase matters

This is the heart of the user's brain-like memory idea.

When the user says:

```text
what was that filing cabinet idea again?
```

Merlin should retrieve memories connected to:

```text
filing cabinet
memory
human brain analogy
associative retrieval
current conversation memory
medium memory
long-term memory
DeepInfra token reduction
```

It should not require the user to use the exact same words as before.

---

## Components to add

Add components similar to:

```text
AssociativeRetriever
ConceptGraphActivationService
MemoryRetrievalScorer
RetrievalReasonBuilder
MemoryRetrievalRequest
RetrievedMemory
ActivatedConcept
```

Use existing:

```text
ConceptExtractor
Memory stores
Concept stores
Seed concept edges
```

---

## Retrieval request model

Suggested shape:

```csharp
public sealed record MemoryRetrievalRequest
{
    public required string Query { get; init; }
    public IReadOnlyList<string> PreferredMemoryTypes { get; init; } = Array.Empty<string>();
    public int MaxResults { get; init; } = 8;
    public bool IncludeArchived { get; init; } = false;
    public DateTimeOffset? NowUtc { get; init; }
}
```

---

## RetrievedMemory model

Suggested shape:

```csharp
public sealed record RetrievedMemory
{
    public required string MemoryId { get; init; }
    public required string MemoryType { get; init; }
    public string? Title { get; init; }
    public required string Content { get; init; }
    public string? Summary { get; init; }
    public double Score { get; init; }
    public IReadOnlyList<string> MatchedConcepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MatchReasons { get; init; } = Array.Empty<string>();
}
```

Retrieval reasons are mandatory for debugging.

Examples:

```text
Matched keyword: DeepInfra
Matched direct concept: memory
Activated related concept: prompt compiler from token reduction
High importance: 0.95
Recent episode memory
```

---

## Concept activation MVP

Implement one-hop graph activation.

Input:

```text
Query concepts: memory, DeepInfra, filing cabinet
```

Output:

```text
memory = 1.0
DeepInfra = 1.0
filing cabinet = 1.0
associative retrieval = 0.6
current conversation memory = 0.6
medium memory = 0.6
long-term memory = 0.6
prompt compiler = 0.5
token reduction = 0.5
```

### Activation algorithm

1. Extract direct concepts from the query.
2. Give each direct concept score `1.0`.
3. Load outgoing/incoming concept edges for direct concepts.
4. Add one-hop concepts with:

```text
activated_score = direct_score * edge_weight * relation_decay
```

Suggested relation decay:

```text
is_a: 0.8
part_of: 0.75
used_for: 0.7
related_to: 0.6
example_of: 0.6
belongs_to_project: 0.5
implemented_by: 0.5
unknown/default: 0.5
```

5. If a concept is activated multiple times, keep the highest score or combine with a cap at 1.0.
6. Return top concepts, for example top 20.

Do not implement two-hop activation yet.

---

## Retrieval pipeline

Implement retrieval in stages:

```text
User query
↓
Extract direct concepts
↓
Activate one-hop related concepts
↓
Search memories by keyword
↓
Search memories by direct/activated concepts
↓
Merge candidates
↓
Score candidates
↓
Deduplicate
↓
Return top results with reasons
```

---

## Candidate search

Use existing store methods if possible.

Search sources:

### Keyword search

Use simple LIKE/search implemented in persistence foundation.

Search:

- title
- content
- summary
- project
- topic

### Concept search

Find memories linked to:

- direct concepts
- activated concepts

### Memory type preference

If request has preferred memory types, boost those types.

Do not exclude other types unless the request explicitly says to filter.

---

## Scoring MVP

Use this scoring formula when embeddings are not available:

```text
final_score =
  keyword_score      * 0.30
+ concept_score      * 0.35
+ graph_score        * 0.20
+ importance_score   * 0.10
+ recency_score      * 0.05
```

### keyword_score

Suggested:

```text
0.0 no keyword match
0.3 weak match
0.6 match in content/summary
0.8 match in title
1.0 exact phrase/important technical term match
```

### concept_score

Suggested:

```text
number/weight of direct matched concepts normalized 0..1
```

### graph_score

Suggested:

```text
sum of activated concept scores linked to memory normalized 0..1
```

### importance_score

Use memory importance, already 0..1.

### recency_score

Use simple decay:

```text
created within 7 days: 1.0
within 30 days: 0.8
within 90 days: 0.5
older: 0.2
pinned/long-term architecture decision: do not penalize too much
```

For long-term memories, recency should matter less. Do not let an old architecture decision disappear only because it is old.

---

## Memory type behavior

Add optional intent-based preferences if already available, but keep MVP simple.

Useful preferences:

### Project architecture questions

Boost:

```text
architecture_decision
project_goal
implementation_note
episode
```

### User preference questions

Boost:

```text
user_preference
tool_preference
```

### Debugging/history questions

Boost:

```text
episode
debug_result
tool_result
```

### Explicit remember requests

Do not use retrieval path. That belongs to MemoryWriter.

---

## Deduplication

The retriever may find the same memory through keyword and concept search.

Deduplicate by memory ID.

When merging duplicate candidates:

- Keep highest score components.
- Combine reasons.
- Preserve direct concept matches.

---

## Required test memories

If test setup needs seed memories, use examples like:

```text
Architecture decision: Merlin should use three memory layers: current conversation memory, medium-term episodic memory, and long-term memory.
```

```text
Architecture decision: Merlin should retrieve memories associatively using concepts, similar to a human filing cabinet where one concept activates related drawers.
```

```text
Project goal: Merlin should reduce DeepInfra token usage by sending only compact retrieved context to cloud models.
```

---

## Tests to add

### Test: retrieve by direct keyword

Stored memory:

```text
Merlin should reduce DeepInfra token usage by sending compact context.
```

Query:

```text
DeepInfra costs
```

Expected:

- Memory retrieved.
- Reason includes keyword/concept match.

### Test: retrieve by direct concept

Stored memory linked to concept:

```text
prompt compiler
```

Query:

```text
how do we compile prompts?
```

Expected:

- Memory retrieved by concept.

### Test: retrieve by graph activation

Concept edge:

```text
filing cabinet -> associative retrieval
```

Memory linked to:

```text
associative retrieval
```

Query:

```text
what was the filing cabinet idea?
```

Expected:

- Memory retrieved even if `associative retrieval` not in query.
- Reason includes graph activation.

### Test: high-importance long-term memory beats weak recent memory

Setup:

- Old high-importance architecture memory.
- Recent low-importance unrelated memory.

Query architecture topic.

Expected:

- High-importance relevant memory ranks above recent unrelated memory.

### Test: max results respected

Query with many matches.

Expected:

- Result count <= MaxResults.

---

## Non-goals

Do not implement:

- Embeddings.
- Vector search.
- Two-hop or unlimited graph traversal.
- Prompt compiler.
- DeepInfra integration.
- Dashboard.
- Interruption behavior.

---

## Verification commands

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter AssociativeRetriever
```

Run all tests if practical:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

---

## Manual verification idea

Create or seed a memory:

```text
Merlin memory should work like a filing cabinet where concepts activate related drawers.
```

Search:

```text
drawer idea
```

Expected:

- Memory retrieved.
- Reasons explain the match.

---

## Final response requirements for the agent

Report:

- Files changed.
- Retrieval pipeline implemented.
- Scoring formula used.
- Graph activation behavior.
- Tests added and result.
- Known limitations.
- Whether it is safe to proceed to Phase 5.
