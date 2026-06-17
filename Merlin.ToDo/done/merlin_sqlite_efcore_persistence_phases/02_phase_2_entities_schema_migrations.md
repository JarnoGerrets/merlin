# Phase 2 - Entities, Schema, Relationships, and EF Migrations

## Objective

Create the first real database schema for Merlin's memory and conversation-state foundation using EF Core entities, entity configurations, indexes, relationships, and an initial migration.

This phase prepares the schema needed by the future brain-like memory system and interruption system.

Do not build the full memory behavior yet. Build the durable data model only.

## Required outcome

At the end of this phase, the AppData SQLite database should contain tables for:

- memories
- concepts
- memory-concept links
- concept edges
- conversations
- conversation topics
- assistant turns
- prompt compilations

These tables are the shared foundation for:

- current conversation memory
- medium-term memory
- long-term memory
- associative filing-cabinet retrieval
- turn interruption
- correction prompt regeneration
- DeepInfra token logging

## Naming convention

Use explicit table names.

Recommended table names:

```text
memories
concepts
memory_concepts
concept_edges
conversations
conversation_topics
assistant_turns
prompt_compilations
```

Entity class names should be singular:

```text
MemoryEntity
ConceptEntity
MemoryConceptEntity
ConceptEdgeEntity
ConversationEntity
ConversationTopicEntity
AssistantTurnEntity
PromptCompilationEntity
```

## Folder structure

Create:

```text
Merlin.Backend/Infrastructure/Persistence/Entities/
Merlin.Backend/Infrastructure/Persistence/Configurations/
```

Recommended files:

```text
Entities/
├── MemoryEntity.cs
├── ConceptEntity.cs
├── MemoryConceptEntity.cs
├── ConceptEdgeEntity.cs
├── ConversationEntity.cs
├── ConversationTopicEntity.cs
├── AssistantTurnEntity.cs
└── PromptCompilationEntity.cs

Configurations/
├── MemoryEntityConfiguration.cs
├── ConceptEntityConfiguration.cs
├── MemoryConceptEntityConfiguration.cs
├── ConceptEdgeEntityConfiguration.cs
├── ConversationEntityConfiguration.cs
├── ConversationTopicEntityConfiguration.cs
├── AssistantTurnEntityConfiguration.cs
└── PromptCompilationEntityConfiguration.cs
```

Keep entity persistence classes in Infrastructure. Do not put EF attributes all over domain/core models.

## Entity: MemoryEntity

Purpose: store medium-term episodes, long-term memories, project decisions, user preferences, tool preferences, implementation notes, debug results, and other durable memory records.

Suggested shape:

```csharp
public sealed class MemoryEntity
{
    public string Id { get; set; } = default!;
    public string MemoryType { get; set; } = default!;

    public string? Title { get; set; }
    public string Content { get; set; } = default!;
    public string? Summary { get; set; }

    public string? Project { get; set; }
    public string? Topic { get; set; }

    public double Importance { get; set; } = 0.5;
    public double Confidence { get; set; } = 0.8;

    public bool UserConfirmed { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public string? Source { get; set; }
    public string? SourceConversationId { get; set; }
    public string? SourceTurnId { get; set; }

    public List<MemoryConceptEntity> MemoryConcepts { get; set; } = [];
}
```

Recommended memory types for later use:

```text
current_topic_summary
episode
long_term_fact
project_decision
user_preference
tool_preference
implementation_note
debug_result
interruption_event
correction_event
```

Do not enforce all of these as an enum at database level yet unless the project already uses enums cleanly. A string is easier to evolve in early development.

## Entity: ConceptEntity

Purpose: represent filing-cabinet drawers and associative concepts.

Examples:

```text
Merlin
memory
current conversation
medium memory
long-term memory
SQLite
DeepInfra
interruption
voice
prompt compiler
```

Suggested shape:

```csharp
public sealed class ConceptEntity
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ConceptType { get; set; }
    public string? ParentConceptId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ConceptEntity? ParentConcept { get; set; }
    public List<ConceptEntity> ChildConcepts { get; set; } = [];
    public List<MemoryConceptEntity> MemoryConcepts { get; set; } = [];
    public List<ConceptEdgeEntity> OutgoingEdges { get; set; } = [];
    public List<ConceptEdgeEntity> IncomingEdges { get; set; } = [];
}
```

Concept type examples:

```text
project
architecture
technology
tool
preference
voice
memory_layer
system_component
```

## Entity: MemoryConceptEntity

Purpose: many-to-many relation between memories and concepts with a relevance weight.

Suggested shape:

```csharp
public sealed class MemoryConceptEntity
{
    public string MemoryId { get; set; } = default!;
    public string ConceptId { get; set; } = default!;
    public double Weight { get; set; } = 1.0;

    public MemoryEntity Memory { get; set; } = default!;
    public ConceptEntity Concept { get; set; } = default!;
}
```

This allows a memory to be strongly connected to one concept and weakly connected to another.

Example:

```text
Memory: "User wants Merlin memory like a human filing cabinet."
Concept weights:
- Merlin: 1.0
- memory: 1.0
- associative retrieval: 0.95
- DeepInfra: 0.7
- token reduction: 0.8
```

## Entity: ConceptEdgeEntity

Purpose: graph relation between concepts.

Suggested shape:

```csharp
public sealed class ConceptEdgeEntity
{
    public string FromConceptId { get; set; } = default!;
    public string ToConceptId { get; set; } = default!;
    public string RelationType { get; set; } = default!;
    public double Weight { get; set; } = 1.0;

    public ConceptEntity FromConcept { get; set; } = default!;
    public ConceptEntity ToConcept { get; set; } = default!;
}
```

Relation type examples:

```text
is_a
part_of
related_to
used_for
example_of
contrasts_with
preference_for
belongs_to_project
```

Examples:

```text
SQLite --used_for--> memory
DeepInfra --used_for--> reasoning
prompt compiler --used_for--> token reduction
assistant turn --part_of--> conversation
interruption --related_to--> assistant turn
```

## Entity: ConversationEntity

Purpose: track a conversation/session at a high level.

Suggested shape:

```csharp
public sealed class ConversationEntity
{
    public string Id { get; set; } = default!;
    public string? Title { get; set; }
    public string? ActiveTopic { get; set; }
    public string Status { get; set; } = "active";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public List<ConversationTopicEntity> Topics { get; set; } = [];
    public List<AssistantTurnEntity> AssistantTurns { get; set; } = [];
    public List<PromptCompilationEntity> PromptCompilations { get; set; } = [];
}
```

Status examples:

```text
active
paused
ended
abandoned
```

## Entity: ConversationTopicEntity

Purpose: represent a topic inside a conversation. This is the bridge between current conversation memory and medium memory.

Suggested shape:

```csharp
public sealed class ConversationTopicEntity
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public string Status { get; set; } = "active";

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public ConversationEntity Conversation { get; set; } = default!;
    public List<AssistantTurnEntity> AssistantTurns { get; set; } = [];
}
```

Status examples:

```text
active
completed
interrupted
abandoned
paused
```

## Entity: AssistantTurnEntity

Purpose: represent one assistant response turn. This is critical for future interruption behavior.

Suggested shape:

```csharp
public sealed class AssistantTurnEntity
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;
    public string? TopicId { get; set; }

    public string OriginalUserMessage { get; set; } = default!;

    public string? GeneratedTextSoFar { get; set; }
    public string? SpokenTextSoFar { get; set; }

    public string State { get; set; } = default!;
    public string? InterruptionReason { get; set; }
    public string? InterruptedByUserMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public ConversationEntity Conversation { get; set; } = default!;
    public ConversationTopicEntity? Topic { get; set; }
    public List<PromptCompilationEntity> PromptCompilations { get; set; } = [];
}
```

State examples:

```text
created
thinking
streaming_response
generating_tts
speaking
paused
interrupted
cancelled
completed
failed
```

Do not store every audio chunk in this table. Store text-level state only.

## Entity: PromptCompilationEntity

Purpose: log prompts created for DeepInfra and track token usage / included memories.

Suggested shape:

```csharp
public sealed class PromptCompilationEntity
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;
    public string? TurnId { get; set; }

    public string PromptType { get; set; } = default!;
    public string CompiledPrompt { get; set; } = default!;

    public int? EstimatedInputTokens { get; set; }
    public string? IncludedMemoryIdsJson { get; set; }
    public string? IncludedConceptIdsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ConversationEntity Conversation { get; set; } = default!;
    public AssistantTurnEntity? Turn { get; set; }
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

## DbContext DbSets

Add DbSets to `MerlinDbContext`:

```csharp
public DbSet<MemoryEntity> Memories => Set<MemoryEntity>();
public DbSet<ConceptEntity> Concepts => Set<ConceptEntity>();
public DbSet<MemoryConceptEntity> MemoryConcepts => Set<MemoryConceptEntity>();
public DbSet<ConceptEdgeEntity> ConceptEdges => Set<ConceptEdgeEntity>();
public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
public DbSet<ConversationTopicEntity> ConversationTopics => Set<ConversationTopicEntity>();
public DbSet<AssistantTurnEntity> AssistantTurns => Set<AssistantTurnEntity>();
public DbSet<PromptCompilationEntity> PromptCompilations => Set<PromptCompilationEntity>();
```

## Entity configurations

Use `IEntityTypeConfiguration<T>` classes instead of stuffing everything into `OnModelCreating`.

In `OnModelCreating`:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(MerlinDbContext).Assembly);
```

## Required indexes

Create indexes for common memory queries.

Memory indexes:

- `MemoryType`
- `Project`
- `Topic`
- `Importance`
- `CreatedAt`
- `ExpiresAt`
- `SourceConversationId`
- `SourceTurnId`

Concept indexes:

- unique `Name`
- `ConceptType`
- `ParentConceptId`

Conversation indexes:

- `Status`
- `CreatedAt`
- `UpdatedAt`

Topic indexes:

- `ConversationId`
- `Status`
- `StartedAt`

Assistant turn indexes:

- `ConversationId`
- `TopicId`
- `State`
- `CreatedAt`

Prompt compilation indexes:

- `ConversationId`
- `TurnId`
- `PromptType`
- `CreatedAt`

## Delete behavior

Use explicit delete behavior.

Recommended:

- Deleting a memory deletes memory-concept links.
- Deleting a concept deletes memory-concept links and concept-edge links.
- Deleting a conversation deletes topics, assistant turns, and prompt compilation logs.
- Deleting a topic should not delete the conversation.
- If a topic is deleted, assistant turns can have nullable `TopicId` set to null.

Be careful with concept self-references and concept edges. EF Core may complain about cascade cycles. If needed, use `DeleteBehavior.Restrict` or `DeleteBehavior.NoAction` for some concept relationships and handle deletes explicitly.

## Date/time storage

Use `DateTimeOffset` in C# models.

SQLite does not have a native datetime type; EF Core will store values as text or appropriate converted values depending on provider behavior.

Store timestamps in UTC conceptually. When setting timestamps in code, prefer:

```csharp
DateTimeOffset.UtcNow
```

## Migration creation

After entities/configurations are added, create the first migration.

Example command:

```bash
cd Merlin.Backend
dotnet ef migrations add InitialMerlinMemoryPersistence
```

If the solution layout requires specifying startup project, use the correct project flags.

Examples:

```bash
dotnet ef migrations add InitialMerlinMemoryPersistence --project Merlin.Backend --startup-project Merlin.Backend
```

Do not manually edit generated migration unless necessary.

## Apply migration

Start the backend and verify the hosted service applies the migration.

Alternatively:

```bash
dotnet ef database update
```

The database should be created under:

```text
%APPDATA%/Merlin/db/merlin_memory.db
```

## FTS note

Do not implement SQLite FTS5 in this phase unless it is trivial and safe.

EF Core does not manage FTS5 as elegantly as normal tables. FTS can be added later using a raw SQL migration if needed.

Phase 2 should focus on stable relational tables and indexes.

Search can start with normal `LIKE`/contains queries and concept links. FTS can be introduced in Phase 4.

## What not to do in Phase 2

Do not:

- implement full memory retrieval logic
- generate embeddings
- add DeepInfra calls
- build a UI
- build interruption behavior
- create voice barge-in logic
- add every possible memory table imaginable
- create a separate database per feature
- store raw audio in SQLite

## Phase 2 acceptance criteria

Phase 2 is complete when:

- All entity classes exist.
- All entity configuration classes exist.
- `MerlinDbContext` exposes all required DbSets.
- EF migration exists.
- Startup applies migration successfully.
- Database file exists in AppData.
- Tables exist in SQLite.
- Indexes exist.
- Foreign key relationships are configured.
- The code builds.
- No full memory behavior has been implemented yet.

## Suggested final agent message after Phase 2

```text
Phase 2 complete. The EF Core schema for memories, concepts, concept edges, conversations, topics, assistant turns, and prompt compilations has been created and migrated to the AppData SQLite database. The schema is ready for repository interfaces and implementations.
```
