---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/DONE memory_refactor/merlin_memory_refactor_plan_schema_aware.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Merlin Memory Refactor Plan V2: Schema-Aware Unified Brain, User Profile Facts, and Structured Prompt Blocks

## Purpose

This document enriches the original Merlin memory refactor plan with the **actual current SQLite schema** exported from `test.sql`.

The previous plan described where Merlin should go. This version also documents where Merlin currently is at the database level, so the implementation agent can make schema-aware changes instead of creating duplicate structures.

The target remains:

- One active memory brain.
- No memoryless or legacy-memory fallback for normal conversation.
- Dedicated stateful User Profile Facts for Jarno's current preferences and requirements.
- Generic long-term memory used for contextual recall, not for rediscovering basic standing preferences.
- Structured prompt blocks instead of one untyped giant prompt string.
- Automatic memory hygiene focused on active-context quality, not database size.
- Keep raw memory evidence in SQLite where useful, but inject only clean active context.

---

# 1. Confirmed Current Database Snapshot

The uploaded `test.sql` file is not a raw SQL dump. It is a JSON-style database schema export for:

```text
merlin_memory_base
```

It contains table definitions, columns, constraints, indexes, and empty row arrays.

Important limitation:

```text
rows: 0
```

So this file confirms structure, but not actual stored memories.

## Confirmed Tables

The current database has **9 tables**:

```text
__EFMigrationsHistory
assistant_turns
concept_edges
concepts
conversation_topics
conversations
memories
memory_concepts
prompt_compilations
```

It has **28 indexes**.

High-level current model:

```text
conversations
→ conversation_topics
→ assistant_turns
→ prompt_compilations

memories
→ memory_concepts
→ concepts
→ concept_edges
```

This means the current DB already has:

- conversation tracking
- topic tracking
- assistant turn tracking
- interruption-aware assistant turn state
- long-term memories
- concept graph retrieval primitives
- prompt compilation logging

It does **not** currently have:

- a dedicated `user_profile_facts` table
- memory lifecycle/status fields on `memories`
- structured prompt block storage
- SQLite FTS tables
- embedding/vector tables

---

# 2. Current Schema Map

## 2.1 `memories`

Current columns:

```text
Id
MemoryType
Title
Content
Summary
Project
Topic
Importance
Confidence
UserConfirmed
CreatedAt
UpdatedAt
LastAccessedAt
ExpiresAt
Source
SourceConversationId
SourceTurnId
```

Current purpose:

- Stores generic long-term memory records.
- Supports memory type, title/content/summary, project/topic scope, importance, confidence, confirmation, timestamps, expiry, and source references.

Current strengths:

- Good base for long-term contextual memories.
- Already has project/topic fields.
- Already has source conversation/turn fields.
- Already has importance/confidence scoring.
- Already has expiry support.

Current limitations:

- No `Status` field.
- No `ProfileId` field.
- No stateful `Key` for preferences.
- No `Category` for profile fact grouping.
- No `DisplayText` optimized for prompt injection.
- No `CompactContent` for compressed active context.
- No `TagsJson` / `MemoryAnchorsJson`.
- No `MergedIntoMemoryId`.
- No `SupersedesMemoryId`.
- No `ArchivedAt` / `DeletedAt`.
- No enforced foreign keys from `SourceConversationId` / `SourceTurnId` to their source tables.

Interpretation:

```text
memories = good for contextual long-term memory, project memory, episodic memory, and summaries.
memories = not ideal as the primary storage for current user preferences.
```

Do not overload this table as the only place for profile/preferences unless there is a strong reason. User preferences need stateful update/supersede behavior.

---

## 2.2 `concepts`

Current columns:

```text
Id
Name
ConceptType
ParentConceptId
CreatedAt
```

Current constraints/indexes:

- `Name` is unique.
- `ParentConceptId` references `concepts.Id` with restricted delete behavior.

Current purpose:

- Stores concepts used for tagging and graph retrieval.
- Supports parent-child concept hierarchy.

Current strengths:

- Good base for a concept graph.
- Global unique concept names avoid exact duplicate concept rows.
- Parent hierarchy can support broad/narrow concept relationships.

Current limitations/risks:

- Because `Name` is globally unique, concept normalization matters a lot.
- Without normalization, concepts like these may all become separate concepts:

```text
short answer
short answers
concise response
concise responses
```

Needed improvement:

- Add/ensure concept normalization in code.
- Consider storing canonical names and aliases later.
- Use profile fact keys for stable preferences instead of relying only on fuzzy concept names.

---

## 2.3 `memory_concepts`

Current columns:

```text
MemoryId
ConceptId
Weight
```

Current constraints:

- Primary key: `MemoryId + ConceptId`
- `MemoryId` references `memories.Id` with cascade delete.
- `ConceptId` references `concepts.Id` with cascade delete.

Current purpose:

- Many-to-many join table between memories and concepts.
- `Weight` allows stronger/weaker concept associations.

Current strengths:

- Good for retrieval scoring.
- Good base for tag/concept activation.

Current limitations:

- Only connects concepts to generic memories, not to future user profile facts.

Possible future addition:

```text
user_profile_fact_concepts
```

or a generic concept link table that can support multiple source entity types.

Do not add this immediately unless the first implementation needs concept-tagged profile facts. Profile facts can start with `Category`, `Key`, `Value`, and `MetadataJson`.

---

## 2.4 `concept_edges`

Current columns:

```text
FromConceptId
ToConceptId
RelationType
Weight
```

Current constraints:

- Primary key: `FromConceptId + ToConceptId + RelationType`
- `FromConceptId` references `concepts.Id` with cascade delete.
- `ToConceptId` references `concepts.Id` with cascade delete.

Current purpose:

- Stores weighted graph relationships between concepts.

Examples of possible relations:

```text
response_length -> response_preferences
short_answers -> concise_responses
Merlin -> memory_system
memory_system -> user_profile_facts
```

Current strengths:

- Good for graph-based retrieval.
- Good for expanding related concepts.

Current limitations:

- No semantic vectors yet.
- Relationship quality depends heavily on extraction/normalization logic.

---

## 2.5 `conversations`

Current columns:

```text
Id
Title
ActiveTopic
Status
CreatedAt
UpdatedAt
EndedAt
```

Current purpose:

- Stores conversation/session lifecycle.

Current strengths:

- Has active topic field.
- Has status and timestamps.

Current limitations:

- No `ProfileId`.
- No provider/model fields.
- No explicit mode/source fields.

Because Merlin is single-user, missing `ProfileId` is not urgent. But a simple fixed `ProfileId = default` can still be useful for separating real/test/imported memories later.

---

## 2.6 `conversation_topics`

Current columns:

```text
Id
ConversationId
Title
Summary
Status
StartedAt
EndedAt
```

Current constraints:

- `ConversationId` references `conversations.Id` with cascade delete.

Current purpose:

- Stores topics within conversations.
- Can support episodic memory creation when a topic closes.

Current strengths:

- Good base for automatic episode summaries.
- Good bridge between session memory and long-term memory.

Current limitations:

- No `Project` field.
- No `Importance` field.
- No `ConceptsJson` / topic concept links.
- No explicit link to generated memory records.

Possible future improvements:

```text
Project
Importance
ConceptsJson
GeneratedMemoryId
```

Not required for the first profile-facts refactor.

---

## 2.7 `assistant_turns`

Current columns:

```text
Id
ConversationId
TopicId
OriginalUserMessage
GeneratedTextSoFar
SpokenTextSoFar
State
InterruptionReason
InterruptedByUserMessage
CreatedAt
UpdatedAt
CompletedAt
```

Current constraints:

- `ConversationId` references `conversations.Id` with cascade delete.
- `TopicId` references `conversation_topics.Id` with `ON DELETE SET NULL`.

Current purpose:

- Stores assistant turns, generated text, spoken text, and interruption state.

Current strengths:

- Very relevant for Merlin's voice/interruption architecture.
- Can support analysis of partially spoken responses and user interruptions.

Current limitations:

- No explicit assistant response final text field separate from generated/spoken progress.
- No model/provider field.
- No direct link to prompt compilation except from `prompt_compilations.TurnId`.

This table should stay. It is not a memory smell; it is useful runtime/conversation state.

---

## 2.8 `prompt_compilations`

Current columns:

```text
Id
ConversationId
TurnId
PromptType
CompiledPrompt
EstimatedInputTokens
IncludedMemoryIdsJson
IncludedConceptIdsJson
CreatedAt
```

Current constraints:

- `ConversationId` references `conversations.Id` with cascade delete.
- `TurnId` references `assistant_turns.Id` with `ON DELETE SET NULL`.

Current purpose:

- Stores compiled prompt debug records.
- Tracks included memory IDs and concept IDs.

Current strengths:

- Excellent for debugging prompt/context behavior.
- Already tracks estimated token count.
- Already tracks which memories/concepts were injected.

Current limitations:

- Stores one giant `CompiledPrompt` string.
- No structured prompt blocks.
- No block type/order/priority/token budget inspection.
- No direct distinction between profile facts, session memory, long-term memory, retrieval notes, and user message.

Needed improvement:

```text
Add CompiledBlocksJson
```

or create:

```text
prompt_compilation_blocks
```

The first implementation can use `CompiledBlocksJson` to avoid overengineering.

---

## 2.9 `__EFMigrationsHistory`

Current columns:

```text
MigrationId
ProductVersion
```

Current purpose:

- Standard EF Core migration history.

Observation from export:

- The exported rows are empty.

The agent should verify the actual running DB/migrations before implementing. This file may be a schema export without data.

---

# 3. Confirmed Current Indexes

Current index groups:

```text
assistant_turns:
- ConversationId
- CreatedAt
- State
- TopicId

concept_edges:
- RelationType
- ToConceptId

concepts:
- ConceptType
- Name UNIQUE
- ParentConceptId

conversation_topics:
- ConversationId
- StartedAt
- Status

conversations:
- CreatedAt
- Status
- UpdatedAt

memories:
- CreatedAt
- ExpiresAt
- Importance
- MemoryType
- Project
- SourceConversationId
- SourceTurnId
- Topic

memory_concepts:
- ConceptId

prompt_compilations:
- ConversationId
- CreatedAt
- PromptType
- TurnId
```

The current indexes are good for basic filtering by type, project, topic, importance, recency, conversation, and prompt type.

Missing indexes for the target refactor:

```text
user_profile_facts.ProfileId
user_profile_facts.Key
user_profile_facts.Category
user_profile_facts.Status
user_profile_facts.ProfileId + Key + Status
memories.Status
memories.MergedIntoMemoryId
memories.SupersedesMemoryId
```

Later, if FTS is added:

```text
memories_fts
```

---

# 4. Current Architecture Verdict

The current DB is already a real Core Memory base. It is not just random logs.

It supports:

- generic long-term memory
- concept graph retrieval
- conversation/topic tracking
- assistant turn tracking
- prompt compilation logging

But it currently lacks the exact layer needed for stable personal-assistant behavior:

```text
Dedicated stateful user profile facts/preferences.
```

So the correct target split is:

```text
user_profile_facts
= current truths/preferences about Jarno and Merlin behavior

memories
= contextual long-term memories, project decisions, episodes, summaries

conversation_topics / assistant_turns
= session/topic/turn tracking

concepts / memory_concepts / concept_edges
= retrieval graph

prompt_compilations
= debug/inspection of compiled prompts
```

This means the refactor should not replace the current memory DB. It should extend it cleanly.

---

# 5. Core Design Decision: Add User Profile Facts

## 5.1 Why `user_profile_facts` Is Needed

Merlin should not query all long-term memory every turn just to rediscover basic stable preferences.

Bad flow:

```text
Every prompt
→ search long-term memories
→ maybe find "Jarno likes concise responses"
→ maybe inject it
```

Better flow:

```text
Every prompt
→ load active User Profile Facts
→ inject relevant response/coding/Merlin behavior preferences directly
→ query long-term memories only for topic-specific context
```

This gives Merlin stable behavior.

## 5.2 Why Not Just Use `memories.MemoryType = user_profile`?

You technically could store profile facts in `memories`, but it is not ideal.

The `memories` table is append-oriented. It is good for:

- episodes
- project decisions
- discussion summaries
- contextual facts
- historical records

User profile facts are stateful. They need:

- one active value per key
- superseding old values
- category-based prompt injection
- direct high-priority inclusion
- clear current truth

Example:

```text
Old: response.length.default = short
New: response.length.default = medium_to_long
```

This should not become two active memories. It should become one active profile fact with the old value superseded.

## 5.3 Proposed Table: `user_profile_facts`

Add a new table:

```text
user_profile_facts
```

Proposed columns:

```text
Id TEXT NOT NULL PRIMARY KEY
ProfileId TEXT NOT NULL
Key TEXT NOT NULL
Category TEXT NOT NULL
Value TEXT NOT NULL
DisplayText TEXT NOT NULL
Priority REAL NOT NULL
Confidence REAL NOT NULL
Status TEXT NOT NULL
CreatedAt TEXT NOT NULL
UpdatedAt TEXT NOT NULL
LastConfirmedAt TEXT NULL
SourceType TEXT NOT NULL
SourceMemoryId TEXT NULL
SupersedesFactId TEXT NULL
MetadataJson TEXT NULL
```

Recommended indexes:

```text
IX_user_profile_facts_ProfileId
IX_user_profile_facts_Key
IX_user_profile_facts_Category
IX_user_profile_facts_Status
IX_user_profile_facts_ProfileId_Key_Status
```

Important rule:

```text
Only one active fact per ProfileId + Key.
```

This may be enforced in code first. A filtered unique index can be considered later if SQLite/EF setup supports it cleanly.

## 5.4 Suggested C# Entity

```csharp
public sealed class UserProfileFact
{
    public Guid Id { get; set; }
    public string ProfileId { get; set; } = "default";
    public string Key { get; set; } = "";
    public string Category { get; set; } = "";
    public string Value { get; set; } = "";
    public string DisplayText { get; set; } = "";
    public double Priority { get; set; } = 0.5;
    public double Confidence { get; set; } = 1.0;
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastConfirmedAt { get; set; }
    public string SourceType { get; set; } = "";
    public Guid? SourceMemoryId { get; set; }
    public Guid? SupersedesFactId { get; set; }
    public string? MetadataJson { get; set; }
}
```

## 5.5 Example Rows

### Response length preference

```text
ProfileId: default
Key: response.length.default
Category: response_preferences
Value: medium_to_long
DisplayText: Jarno prefers medium-to-long responses by default.
Priority: 0.85
Confidence: 1.0
Status: active
SourceType: explicit_user_instruction
```

### Merlin runtime preference

```text
ProfileId: default
Key: merlin.runtime.memory_required
Category: merlin_behavior_preferences
Value: fail_closed
DisplayText: Merlin should fail closed if the core memory system is unavailable.
Priority: 1.0
Confidence: 1.0
Status: active
SourceType: explicit_user_instruction
```

### Coding preference

```text
ProfileId: default
Key: coding.dependencies.object_mapping
Category: coding_preferences
Value: avoid_object_mapping_packages
DisplayText: Jarno prefers not to use object-mapping packages.
Priority: 0.8
Confidence: 1.0
Status: active
SourceType: explicit_user_instruction
```

---

# 6. Profile Fact Categories

Start with a small but extensible taxonomy.

## 6.1 `response_preferences`

Prompt block:

```text
<ResponsePreferences>
...
</ResponsePreferences>
```

Example keys:

```text
response.length.default
response.tone.default
response.detail_level.default
response.criticism_preference
response.formatting.preference
```

Example display text:

```text
Jarno prefers concise, direct responses by default.
Jarno wants critical feedback when discussing architecture or implementation choices.
Jarno wants detailed reports when asking for agent prompts or implementation plans.
```

## 6.2 `coding_preferences`

Prompt block:

```text
<CodingPreferences>
...
</CodingPreferences>
```

Example keys:

```text
coding.style.separate_concerns
coding.dependencies.object_mapping
coding.dotnet.program_cs
coding.tests.preference
```

Example display text:

```text
Jarno prefers code separated by concern.
Jarno prefers not to use object-mapping packages.
Jarno works with .NET 6 or later where Program.cs is used instead of Startup.cs.
```

## 6.3 `merlin_behavior_preferences`

Prompt block:

```text
<MerlinBehaviorPreferences>
...
</MerlinBehaviorPreferences>
```

Example keys:

```text
merlin.runtime.memory_required
merlin.runtime.legacy_fallback
merlin.memory.manual_intervention
merlin.memory.profile_facts
merlin.interaction.address_style
```

Example display text:

```text
Merlin should fail closed if core memory is unavailable.
Merlin should not continue in a memoryless or legacy-memory fallback mode.
Jarno wants memory hygiene to be automatic instead of requiring constant manual cleanup.
```

## 6.4 `workflow_preferences`

Prompt block:

```text
<WorkflowPreferences>
...
</WorkflowPreferences>
```

Example keys:

```text
workflow.agent_prompts.extensive_markdown
workflow.todo_docs.detail_level
workflow.refactor.safe_stages
```

Example display text:

```text
Jarno likes extensive .md implementation plans for agent tasks.
Jarno prefers implementation in safe, inspectable stages.
```

## 6.5 `personal_facts`

Prompt block:

```text
<PersonalFacts>
...
</PersonalFacts>
```

Use sparingly. Store only personal facts that help Merlin behave better.

Example display text:

```text
Jarno is the only intended user of Merlin.
Merlin is being built as Jarno's personal local assistant.
```

---

# 7. Preference Update / Supersede Behavior

The biggest behavioral improvement is stateful preference updates.

Example conversation:

```text
User: I want short responses.
```

System stores:

```text
Key: response.length.default
Value: short
Status: active
```

Later:

```text
User: I prefer medium to long responses.
```

System should detect:

```text
same key: response.length.default
old value: short
new value: medium_to_long
relationship: supersedes previous value
```

Then:

```text
old fact → status = superseded
new fact → status = active
new fact.SupersedesFactId = old fact Id
```

Assistant response:

```text
I will remember that you prefer medium-to-long responses instead of short responses from now on, sir.
```

This prevents contradictory active prompt context.

Important distinction:

```text
Profile facts = current state.
Episodic memories = history.
```

So profile facts should supersede. Episodic memories should usually remain as historical records.

---

# 8. Add Lifecycle Fields To `memories`

The existing `memories` table should remain, but it needs lifecycle fields so memory hygiene can work without destructive deletion.

## 8.1 Proposed New Columns

Minimum recommended additions:

```text
Status TEXT NOT NULL DEFAULT 'active'
CompactContent TEXT NULL
TagsJson TEXT NULL
MemoryAnchorsJson TEXT NULL
MergedIntoMemoryId TEXT NULL
SupersedesMemoryId TEXT NULL
ArchivedAt TEXT NULL
DeletedAt TEXT NULL
```

If the first migration needs to be smaller, add at least:

```text
Status
CompactContent
TagsJson
MemoryAnchorsJson
MergedIntoMemoryId
SupersedesMemoryId
```

## 8.2 Status Values

```text
active
merged
superseded
archived
deleted
```

Only `active` memories should normally be considered for prompt injection.

## 8.3 Why This Matters

The goal is not to reduce SQLite storage. Storage overhead is negligible for a single-user local assistant.

The real goal is active-context quality.

Bad:

```text
Inject every similar old memory.
```

Good:

```text
Keep raw/source memories in DB.
Inject only the clean active memory.
Keep merged/superseded/archived records as evidence.
```

---

# 9. Prompt Compilation Refactor

## 9.1 Current State

Current table:

```text
prompt_compilations
```

Current key field:

```text
CompiledPrompt
```

This stores one big final prompt string.

It also has:

```text
IncludedMemoryIdsJson
IncludedConceptIdsJson
EstimatedInputTokens
```

This is useful, but not enough for debugging structured context.

## 9.2 Target: Prompt Blocks

Internally, prompt compilation should produce typed blocks before rendering.

Example C# shape:

```csharp
public sealed class PromptBlock
{
    public string Type { get; init; } = "";
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public int Priority { get; init; }
    public bool Required { get; init; }
    public int EstimatedTokens { get; init; }
    public int SortOrder { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
```

Prompt block types:

```text
system_identity
runtime_rules
response_preferences
coding_preferences
merlin_behavior_preferences
workflow_preferences
personal_facts
project_context
session_memory
topic_memory
relevant_long_term_memory
retrieval_notes
tool_context
user_message
```

## 9.3 Storage Option A: Add `CompiledBlocksJson`

Simpler first step:

```text
ALTER TABLE prompt_compilations ADD COLUMN CompiledBlocksJson TEXT NULL;
```

This preserves current logging but adds structured inspection.

## 9.4 Storage Option B: Add `prompt_compilation_blocks`

Cleaner but more work:

```text
prompt_compilation_blocks
```

Columns:

```text
Id
PromptCompilationId
BlockType
Title
Content
Priority
Required
EstimatedTokens
SortOrder
MetadataJson
```

Recommendation:

```text
Start with CompiledBlocksJson.
Only add prompt_compilation_blocks if/when debugging needs direct SQL filtering per block.
```

## 9.5 Rendered Prompt Example

```text
<SystemIdentity>
You are Merlin, Jarno's local personal assistant.
</SystemIdentity>

<RuntimeRules>
- Use the provided memory/context as the current source of truth.
- Do not claim access to unavailable systems.
</RuntimeRules>

<ResponsePreferences>
- Jarno prefers concise, direct responses by default.
- Jarno wants critical feedback for technical architecture decisions.
</ResponsePreferences>

<MerlinBehaviorPreferences>
- Merlin should fail closed if core memory is unavailable.
- Merlin should not continue in memoryless or legacy fallback mode.
</MerlinBehaviorPreferences>

<CodingPreferences>
- Jarno prefers separated code concerns.
- Jarno prefers not to use object-mapping packages.
</CodingPreferences>

<SessionMemory>
- Current topic: Merlin memory refactor.
- Current decision: add user profile facts and structured prompt blocks.
</SessionMemory>

<RelevantLongTermMemory>
- Previous memory analysis found overlapping SQLite and JSON memory systems.
</RelevantLongTermMemory>

<UserMessage>
...
</UserMessage>
```

---

# 10. Retrieval Responsibilities After Refactor

## 10.1 User Profile Facts

Should be loaded directly by category/key.

Used for:

- response preferences
- coding preferences
- Merlin behavior preferences
- workflow preferences
- stable personal facts

Should not require long-term semantic search every turn.

## 10.2 Long-Term Memories

Current `memories` table should be used for:

- project decisions
- old conversations
- topic summaries
- episodic context
- historical context

Retrieved conditionally using:

- current user message
- active topic
- project/topic
- concept graph
- importance
- recency
- later: FTS
- later: embeddings

## 10.3 Session / Topic Memory

Current `conversations`, `conversation_topics`, and `assistant_turns` are already suitable for active session context.

Use them for:

- current topic
- current topic summary
- recent turns
- interrupted assistant turn state

---

# 11. FTS and Semantic Retrieval Roadmap

## 11.1 Do Not Start With Embeddings

Current DB has no vector/embedding tables. That is fine.

Do not add embeddings before:

- user profile facts exist
- memory statuses exist
- prompt blocks exist
- legacy fallback behavior is removed

## 11.2 Add SQLite FTS First

Future table:

```text
memories_fts
```

Index:

```text
Title
Content
Summary
CompactContent
TagsJson
MemoryAnchorsJson
Project
Topic
```

Use FTS together with existing fields:

```text
MemoryType
Project
Topic
Importance
CreatedAt
ExpiresAt
Status
concept links
```

## 11.3 Embeddings Later

Future table:

```text
memory_embeddings
```

Possible columns:

```text
Id
MemoryId
EmbeddingModel
VectorBlob
CreatedAt
```

Embeddings should be a signal, not the final authority.

Never merge/delete/supersede based only on embedding similarity.

---

# 12. Automatic Memory Hygiene

## 12.1 Main Goal

Memory hygiene is not about shrinking the database.

For a single-user local app, memory records are tiny.

The real issue is:

```text
retrieval quality
prompt quality
contradiction avoidance
not injecting stale or duplicate context
```

## 12.2 New Memory Pipeline

```text
New memory or profile fact candidate
→ normalize
→ classify
→ determine whether it belongs in user_profile_facts or memories
→ compare with nearby active records
→ create/update/merge/supersede/archive
→ update concepts/tags/anchors
→ log minimally if diagnostics enabled
```

## 12.3 Profile Facts Hygiene

Profile facts should aggressively avoid duplicate active keys.

Rules:

```text
same ProfileId + same Key + compatible meaning → update/confirm existing fact
same ProfileId + same Key + conflicting value → supersede old fact
same ProfileId + different Key → keep separate
```

Example:

```text
I want short responses.
I like concise answers.
```

Likely same key:

```text
response.length.default
```

Result:

```text
one active fact, maybe updated DisplayText/MetadataJson
```

Example:

```text
I want short responses.
I prefer medium to long responses.
```

Same key, conflicting value:

```text
old → superseded
new → active
```

## 12.4 Generic Memories Hygiene

Generic memories should be less aggressively merged.

Rules:

```text
exact duplicate → skip or merge
near duplicate same event/decision → merge
same topic but different time/state → keep separate or summarize later
old but useful source record → archive instead of delete
```

---

# 13. Fail-Closed Runtime Behavior

## 13.1 Desired Behavior

For normal conversation:

```text
Core Memory available → continue
Core Memory unavailable → fail clearly
```

Do not continue with:

- old JSON memory
- no memory
- partial fallback prompts
- local fallback that has different memory semantics

## 13.2 Allowed Exception

Simple local commands may still execute if they do not require assistant reasoning/memory.

Examples:

```text
stop speaking
open notepad
set volume
close overlay
```

## 13.3 Agent Task

The implementation agent must inspect current runtime paths and identify:

- where DeepInfra path uses Core Memory
- where local fallback path uses old memory/session logic
- where old JSON memory is injected
- where normal conversation can continue without Core Memory

Then change behavior so:

```text
normal conversation has one memory path only
provider changes do not change memory behavior
```

---

# 14. Legacy JSON Memory Quarantine / Removal

## 14.1 Goal

Old JSON memory should not be part of the active Merlin brain.

Likely legacy systems to inspect:

```text
LongTermMemoryStore
ConversationSummaryStore
ConversationSessionService
MemoryExtractionService
memory candidate approval flow
old JSON memory files
```

Exact names must be verified in code.

## 14.2 Quarantine Before Deletion

First:

- remove old JSON memory from active prompt paths
- remove old JSON memory from fallback conversation paths
- mark classes as legacy if still referenced
- update StatusTool so old JSON is not reported as active memory
- keep import/debug only if useful

Later:

- delete unused legacy services
- delete or rewrite old tests
- optionally add one migration/import command if any old data should be preserved

## 14.3 No Manual Candidate UI

Do not build a manual memory approval workflow.

Jarno does not want daily manual memory maintenance.

Manual forget is useful. Manual memory cleanup is not.

---

# 15. StatusTool / Diagnostics Update

The current diagnostics must reflect the real memory brain.

Suggested output:

```json
{
  "memory": {
    "mode": "sqlite-core",
    "healthy": true,
    "databaseAvailable": true,
    "memoryCount": 123,
    "activeMemoryCount": 100,
    "activeProfileFactCount": 18,
    "conceptCount": 88,
    "activeTopic": "Merlin memory architecture refactor",
    "lastPromptCompilationAt": "2026-06-22T...",
    "legacyJsonEnabled": false,
    "degradedFallbackEnabled": false
  }
}
```

If legacy JSON still exists temporarily, report it separately:

```json
{
  "legacyMemory": {
    "enabledInRuntime": false,
    "jsonFilesDetected": true,
    "legacyMemoryCount": 42
  }
}
```

Do not present legacy JSON memory count as Merlin's active memory count.

---

# 16. Logging Strategy

Because Merlin is local and single-user, privacy is not the main concern.

The concern is noise and unnecessary storage growth.

Desired state:

```text
development/debugging → detailed memory/prompt logs can be enabled
normal daily use → almost no logs
```

Suggested config:

```json
{
  "Diagnostics": {
    "PromptCompilationLogging": false,
    "MemoryRetrievalLogging": false,
    "MemoryWriteLogging": true,
    "VerboseMemoryDebug": false,
    "MaxPromptLogEntries": 100
  }
}
```

Keep logging code. Disable most logging by default once stable.

For prompt logs, either:

- keep last 100
- keep last 7 days
- keep only failed/abnormal prompt compilations
- enable full prompt logs only in debug mode

---

# 17. Schema-Aware Implementation Stages

## Stage 0: Code/Schema Alignment Report

Before editing, agent must inspect:

- actual `DbContext`
- entity classes
- EF migrations
- repository classes
- MemoryOrchestrator
- MemoryWriter
- MemoryRetriever
- PromptCompiler
- LocalAIChatService / DeepInfra path
- old JSON memory services
- StatusTool
- tests

Then produce a short alignment report:

```text
Current table/entity → keep/modify/add/remove
Current service → keep/refactor/remove
Current runtime path → unified/fail-closed/legacy
```

Do not create duplicate structures if equivalent code already exists.

## Stage 1: Fail-Closed Core Memory Runtime

Goal:

```text
Normal conversation must not continue without Core Memory.
```

Tasks:

- identify memoryless/legacy fallback paths
- remove old JSON memory from active conversation prompts
- make general conversation require Core Memory health
- keep simple command-only operations separate if safe
- add tests

Acceptance:

```text
If Core Memory DB is unavailable, normal chat fails clearly.
No old JSON memory is injected into normal chat.
Provider choice does not change memory semantics.
```

## Stage 2: Add `user_profile_facts`

Goal:

```text
Dedicated stateful storage for Jarno's current preferences/facts.
```

Tasks:

- add entity
- update DbContext
- add migration
- add indexes
- add service/repository
- add tests

Acceptance:

```text
Can create, read, update, supersede, archive, and delete/forget profile facts.
Only active profile facts are used for prompt injection.
```

## Stage 3: Profile Fact Classifier / Upsert Logic

Goal:

```text
Explicit preferences update state instead of becoming duplicate memories.
```

Tasks:

- detect explicit user preference/fact statements
- map to stable keys/categories
- detect existing active fact by key
- update/supersede as needed
- produce natural acknowledgement

Acceptance examples:

```text
"I want short responses" → response.length.default = short
"I prefer medium to long responses" → supersedes short
"I want you to always be concise" → updates/creates response length/detail preference
```

## Stage 4: Add Memory Lifecycle Fields

Goal:

```text
Generic memories can be active/merged/superseded/archived/deleted.
```

Tasks:

- add status/lifecycle fields to `memories`
- update retrieval to only use active memories by default
- update writer/hygiene logic
- add tests

Acceptance:

```text
Archived/merged/superseded/deleted memories are not injected by default.
Raw evidence can remain in DB without polluting prompts.
```

## Stage 5: PromptBlock Compiler

Goal:

```text
Prompt context is structured before rendering.
```

Tasks:

- add `PromptBlock` model
- compile profile facts into typed blocks
- compile session/topic memory into blocks
- compile long-term retrieval into separate blocks
- render XML-style prompt sections
- add `CompiledBlocksJson` to `prompt_compilations`
- add tests

Acceptance:

```text
ResponsePreferences block comes from user_profile_facts.
RelevantLongTermMemory block comes from memories retrieval.
UserMessage block is preserved separately.
CompiledBlocksJson is stored for debugging when enabled.
```

## Stage 6: StatusTool Update

Goal:

```text
Diagnostics show the real active memory brain.
```

Tasks:

- report Core Memory health
- report active profile fact count
- report active memory count
- report concept count
- report prompt compilation state
- report degraded fallback disabled
- report legacy JSON disabled/not runtime active

Acceptance:

```text
StatusTool no longer treats legacy JSON as Merlin's active memory.
```

## Stage 7: Memory Hygiene MVP

Goal:

```text
Prevent obvious duplicate/contradictory active memory.
```

Tasks:

- exact normalized duplicate check
- string/token similarity check
- same-key profile fact supersede
- same-category merge rules
- archive/merge old records instead of deleting
- add tests

Acceptance:

```text
Equivalent profile preferences do not create multiple active facts.
Conflicting same-key preferences supersede correctly.
Episodic memories are not over-merged.
```

## Stage 8: SQLite FTS

Goal:

```text
Improve generic long-term memory retrieval.
```

Tasks:

- add FTS table/index
- index memory text + compact/tags/anchors
- combine FTS with concept graph and existing scoring
- add tests

Acceptance:

```text
Long-term memory retrieval improves without adding semantic vectors yet.
```

## Stage 9: Optional Embeddings

Goal:

```text
Add semantic retrieval/dedupe as a supporting signal.
```

Tasks:

- choose local embedding model/provider
- add embedding table
- generate/store vectors
- use similarity as one score component
- never merge/delete based only on embeddings

Acceptance:

```text
Semantic retrieval helps recall but does not cause destructive false merges.
```

---

# 18. Tests Required

## Runtime Tests

```text
general conversation fails if Core Memory unavailable
general conversation does not use old JSON memory
provider choice does not change memory source
simple local commands can still run if designed to be memory-independent
```

## User Profile Fact Tests

```text
create active profile fact
retrieve active facts by category
retrieve active fact by key
supersede old fact
only new fact is active
deleted/archived fact is not injected
only one active fact per ProfileId + Key
```

## Preference Update Tests

```text
"I want short responses" creates response.length.default = short
"I prefer medium to long responses" supersedes short
paraphrased equivalent preference dedupes
context-specific preference does not incorrectly overwrite global preference
```

## Memory Lifecycle Tests

```text
active memory is retrievable
archived memory is not injected by default
merged memory points to target memory
superseded memory is excluded by default
deleted memory is excluded
```

## Prompt Block Tests

```text
ResponsePreferences block comes from user_profile_facts
CodingPreferences block comes from user_profile_facts
MerlinBehaviorPreferences block comes from user_profile_facts
RelevantLongTermMemory block comes from memories retrieval
UserMessage block is preserved exactly
CompiledBlocksJson is stored when prompt logging is enabled
```

## Diagnostics Tests

```text
StatusTool reports Core Memory health
StatusTool reports active profile fact count
StatusTool reports active memory count
StatusTool reports degradedFallbackEnabled = false
StatusTool does not report old JSON memory as active Merlin memory
```

---

# 19. Things Not To Do Yet

Do not build a full memory UI now.

Jarno is fine inspecting SQLite directly for now.

Do not build a manual memory candidate approval workflow.

Do not add embeddings before profile facts and prompt blocks are stable.

Do not delete old legacy code before proving it is not used.

Do not overload `memories` with stateful preferences if `user_profile_facts` is the cleaner fit.

Do not let local fallback use a different memory brain.

Do not make normal conversation work memoryless.

---

# 20. Final Target State

Final architecture:

```text
User message
→ CommandRouter
→ GeneralConversationTool
→ Core Memory required check
→ UserProfileService loads active profile facts
→ Session/Topic service loads current context
→ LongTermMemoryRetriever retrieves relevant active memories
→ PromptBlockCompiler builds typed blocks
→ PromptRenderer renders provider-specific prompt/messages
→ LLM provider responds
→ MemoryWriter / ProfileFactUpdater / TopicUpdater update state
→ MemoryHygieneService keeps active context clean
```

Final database split:

```text
user_profile_facts
= current stateful facts/preferences about Jarno and Merlin behavior

memories
= contextual long-term memory, project decisions, episodes, summaries

concepts + memory_concepts + concept_edges
= retrieval graph

conversations + conversation_topics + assistant_turns
= session/topic/turn state

prompt_compilations
= debug log of final prompt + structured blocks
```

Final principle:

```text
One assistant.
One active memory brain.
One profile truth layer.
One prompt compiler.
No braindead fallback state.
No manual daily memory cleanup.
```

---

# 21. Acceptance Criteria For This Refactor

The refactor is successful when:

```text
Merlin has one active memory system.
Normal conversation fails clearly if Core Memory is unavailable.
Old JSON memory is not used in active prompts.
user_profile_facts exists and stores stateful current preferences.
Contradictory preferences supersede old values instead of accumulating.
Generic memories have lifecycle/status handling.
Profile facts are injected into typed prompt blocks.
Long-term memory retrieval is only used for contextual recall.
prompt_compilations can store structured prompt blocks.
StatusTool reports the real active memory brain.
Logging can be disabled for normal use.
Tests cover fail-closed behavior, profile updates, prompt blocks, lifecycle, and diagnostics.
```

---

# 22. Agent Instructions

When implementing:

- Inspect code before editing.
- Verify exact current entity names and DbContext mappings.
- Do not create duplicate tables if equivalent structures already exist.
- Keep changes staged.
- Add tests per stage.
- Do not delete legacy memory until active runtime no longer uses it.
- Do not add embeddings yet.
- Do not build a memory UI now.
- Do not make normal conversation continue without Core Memory.
- Prefer explicit failure over inconsistent memory behavior.
- Preserve raw evidence where useful, but inject only clean active state.

