# Merlin Brain-Like Local Memory System

## Purpose of this document

This document describes the intended design and implementation plan for Merlin's local memory system.
It is written for the coding agent that will implement the feature inside the Merlin project.

The goal is not to build a generic chat-history store. The goal is to build a **brain-like local memory system** that lets Merlin remember broadly, retrieve associatively, and send only the smallest useful prompt to DeepInfra.

The user explicitly wants this system because Merlin is moving toward a hybrid architecture:

- Local tools and routing handle fast/simple commands.
- DeepInfra is used for deeper reasoning or longer conversations.
- DeepInfra token usage must stay low.
- Merlin should not send giant long conversations to DeepInfra every time.
- Merlin should feel like it has memory, but the memory should be local, inspectable, editable, and cost-efficient.

The central idea is:

```text
Store a rich local brain.
Activate only the relevant drawers.
Compile a tiny cloud prompt.
```

Or, in implementation terms:

```text
User message
↓
Local concept extraction
↓
Current topic tracking
↓
Associative memory retrieval
↓
Prompt compiler with strict token budget
↓
DeepInfra receives only a compact relevant context packet
↓
Response
↓
Memory writer updates local memory
```

---

# 1. Core concept

Merlin's memory should be inspired by how human memory feels.

A human does not remember by replaying an entire transcript. When someone says "car," the brain activates related concepts such as vehicles, driving, fuel, repairs, road trips, first car, car insurance, traffic, etc. When someone says "blue," the brain may activate color, sky, ocean, blue car, blue UI, calm feeling, and a holiday memory of blue water.

Merlin should work similarly.

When the user mentions a topic, Merlin should not blindly load all previous conversations. Instead, it should detect concepts and activate related memory drawers.

Example:

```text
User: how would the filing cabinet thing work for Merlin?
```

Merlin should infer concepts like:

```text
Merlin
memory
filing cabinet
human brain analogy
associative retrieval
current conversation memory
medium memory
long-term memory
DeepInfra token reduction
```

Then it should retrieve only the strongest matching memories from local storage, such as:

```text
- User proposed a three-layer memory system: current conversation, medium memory, long-term memory.
- User wants completed topics summarized into medium memory.
- User wants important facts and explicit remember requests promoted to long-term memory.
- User wants associative retrieval where concepts activate related drawers.
- Project goal: reduce DeepInfra token usage by sending only specific context.
```

Then the DeepInfra prompt should contain only that compact context, not the full chat history.

---

# 2. The three memory layers

Merlin should use three main memory layers:

1. Current conversation memory
2. Medium-term memory
3. Long-term memory

These layers must have different lifetimes, different storage rules, and different retrieval behavior.

---

## 2.1 Current conversation memory

Current conversation memory is Merlin's short-term working memory.

It tracks the active topic right now.

This layer should answer questions like:

- What are we currently talking about?
- What is the user's current goal?
- What entities, tools, files, or concepts are active?
- What has the user just clarified?
- Are there unresolved questions?
- Did the topic just change?

Current conversation memory should be lightweight and updated frequently.

It should not be treated as permanent memory.

Example structure:

```json
{
  "activeTopicId": "topic_2026_06_17_merlin_memory",
  "activeTopicTitle": "Merlin brain-like memory system",
  "currentGoal": "Design a local brain-like memory system that reduces DeepInfra token usage",
  "recentSummary": "The user clarified that Merlin memory should work like a human brain with current conversation, medium memory, and long-term memory. The user compared retrieval to a filing cabinet where concepts activate related memories.",
  "activeConcepts": [
    "Merlin",
    "memory",
    "human brain",
    "filing cabinet",
    "DeepInfra",
    "token reduction",
    "associative retrieval"
  ],
  "activeEntities": [
    "Merlin",
    "DeepInfra"
  ],
  "unresolvedQuestions": [
    "How should the database schema look?",
    "How should topic boundaries be detected?",
    "How should retrieved memories be compiled into prompts?"
  ],
  "lastUpdatedUtc": "2026-06-17T00:00:00Z"
}
```

### Responsibilities of current conversation memory

The current memory layer should:

- Keep the active topic state.
- Store a rolling compact summary of the current topic.
- Track the last few raw user/assistant turns if needed.
- Track important concepts and entities.
- Track user corrections inside the current topic.
- Detect when the user switches topic.
- Trigger medium-memory summarization when a topic appears finished.

### What should not go into current conversation memory

Do not use current conversation memory for:

- Permanent user preferences.
- Long-term project decisions.
- Huge raw logs.
- Full conversation transcripts.
- Old unrelated topics.

Current conversation memory should remain small.

Suggested token-equivalent target if included in a prompt:

```text
100-500 tokens for normal use.
500-1000 tokens for complex active design/debug sessions.
```

---

## 2.2 Medium-term memory

Medium-term memory is Merlin's episodic memory.

It stores completed topics, sessions, experiments, conclusions, and useful conversations.

When a current topic appears finished, Merlin should summarize it into medium memory.

Example medium memory:

```json
{
  "id": "mem_episode_001",
  "memoryType": "episode",
  "title": "Brain-like memory system for Merlin",
  "summary": "The user proposed a brain-like memory system for Merlin with three layers: current conversation memory, medium-term memory, and long-term memory. Current conversation tracks the active topic. When a topic is done, it is summarized into medium memory. Important conclusions and explicit remember requests are promoted to long-term memory. The user compared memory retrieval to a filing cabinet where concepts like 'car' activate the vehicle drawer and concepts like 'blue' activate related color memories such as blue water, a blue car, or Merlin's blue UI glow.",
  "concepts": [
    "Merlin",
    "memory",
    "brain-like memory",
    "current conversation memory",
    "medium memory",
    "long-term memory",
    "filing cabinet",
    "associative retrieval",
    "DeepInfra",
    "token reduction"
  ],
  "importance": 0.9,
  "confidence": 0.95,
  "createdAtUtc": "2026-06-17T00:00:00Z",
  "source": "conversation_summary"
}
```

Medium memory should be more detailed than long-term memory, but still summarized.

It should not store every raw message unless raw transcripts are stored separately for audit/debug.

### Responsibilities of medium memory

Medium memory should:

- Store topic summaries.
- Store past debugging investigations.
- Store project discussions.
- Store conclusions from conversations.
- Store experiments and their outcomes.
- Allow retrieval by topic, concept, keywords, recency, and importance.
- Serve as the source for later promotion into long-term memory.

### Medium memory examples for Merlin

Possible medium memories:

```text
- Chatterbox Turbo latency investigation
- DeepInfra context/token management discussion
- Hierarchical intent router design
- Smart interim feedback design
- Customizable date formatting and tool learning discussion
- Slack bridge between Merlin and Codex CLI idea
- Brain-like memory system design
```

### Medium memory lifetime

Medium memory does not have to live forever.

Some memories can expire or be archived.

Suggested expiry behavior:

```text
Project architecture discussions: long-lived or no expiry
Debugging logs/results: 7-30 days unless pinned
Temporary experiments: 30-90 days
General conversation summaries: 30-180 days depending importance
```

---

## 2.3 Long-term memory

Long-term memory is Merlin's stable durable memory.

It stores things that should affect future behavior or future project reasoning.

Long-term memory should be curated and relatively small compared to medium memory.

Things should enter long-term memory when:

- The user explicitly says to remember something.
- The user confirms a preference.
- A stable user preference is clearly established.
- A project architecture decision is made.
- A recurring behavior rule is established.
- A fact is repeatedly useful across conversations.
- A medium memory contains a highly important conclusion.

Example long-term memory records:

```json
{
  "id": "mem_ltm_001",
  "memoryType": "architecture_decision",
  "project": "Merlin",
  "title": "Three-layer Merlin memory model",
  "content": "Merlin's local memory should be designed with three layers: current conversation memory, medium-term episodic memory, and long-term memory.",
  "concepts": [
    "Merlin",
    "memory",
    "current conversation memory",
    "medium memory",
    "long-term memory"
  ],
  "importance": 1.0,
  "confidence": 0.95,
  "userConfirmed": true,
  "createdAtUtc": "2026-06-17T00:00:00Z"
}
```

```json
{
  "id": "mem_ltm_002",
  "memoryType": "architecture_decision",
  "project": "Merlin",
  "title": "Associative retrieval for Merlin memory",
  "content": "Merlin should retrieve memories associatively, like a filing cabinet or human brain: concepts mentioned by the user should activate related concepts and memories, not just exact keyword matches.",
  "concepts": [
    "Merlin",
    "associative retrieval",
    "concept graph",
    "filing cabinet",
    "human brain analogy"
  ],
  "importance": 1.0,
  "confidence": 0.95,
  "userConfirmed": true,
  "createdAtUtc": "2026-06-17T00:00:00Z"
}
```

```json
{
  "id": "mem_ltm_003",
  "memoryType": "project_goal",
  "project": "Merlin",
  "title": "Reduce DeepInfra token usage",
  "content": "Merlin should reduce DeepInfra token usage by storing rich local memory and sending only very specific retrieved context to cloud models.",
  "concepts": [
    "Merlin",
    "DeepInfra",
    "token reduction",
    "prompt compiler",
    "local memory"
  ],
  "importance": 1.0,
  "confidence": 0.95,
  "userConfirmed": true,
  "createdAtUtc": "2026-06-17T00:00:00Z"
}
```

### Long-term memory must be inspectable

Long-term memory should not be invisible magic.

Merlin needs a memory dashboard eventually where the user can:

- View long-term memories.
- Edit a memory.
- Delete a memory.
- Pin/unpin a memory.
- See why a memory was saved.
- See which concepts a memory is connected to.
- See when it was last used.
- See whether it was explicitly user-confirmed.

---

# 3. Associative retrieval: the filing cabinet model

The filing cabinet model is the heart of the system.

Merlin should not retrieve memory only by exact text match.

Merlin should retrieve by concept activation.

## 3.1 Concepts as drawers

A concept is a mental drawer.

Examples:

```text
memory
Merlin
DeepInfra
voice
TTS
Chatterbox
routing
local tools
date formatting
human brain
filing cabinet
blue
car
vehicle
UI glow
vacation
beach
```

Each memory should be connected to one or more concepts.

Example:

```text
Memory: "User compared memory to a filing cabinet where 'car' opens the vehicle drawer."
Connected concepts:
- memory
- filing cabinet
- car
- vehicle
- associative retrieval
- human brain analogy
```

When the user says "car," Merlin should find the concept `car`, then also activate nearby concepts like `vehicle`, `driving`, `road trip`, and memories connected to those.

When the user says "blue," Merlin should activate `blue`, `color`, `ocean`, `sky`, `Merlin UI glow`, `blue car`, and related memories.

## 3.2 Concept graph

Implement a concept graph.

The graph should contain:

- Concept nodes.
- Memory nodes.
- Edges between concepts.
- Edges between memories and concepts.
- Weights on edges.
- Relation types on edges.

Example graph:

```text
blue --is_a--> color
blue --related_to--> ocean
blue --related_to--> sky
blue --related_to--> Merlin UI glow
blue --related_to--> blue car
car --is_a--> vehicle
vehicle --related_to--> driving
memory --related_to--> human brain
memory --related_to--> filing cabinet
associative retrieval --used_for--> prompt reduction
DeepInfra --used_for--> deeper reasoning
prompt compiler --used_for--> token reduction
```

## 3.3 Spreading activation

When a message activates a concept, that activation should spread to related concepts with decreasing strength.

Example input:

```text
blue
```

Activation:

```text
blue = 1.0
color = 0.8
ocean = 0.6
sky = 0.6
Merlin UI glow = 0.6
blue car = 0.5
vehicle = 0.25
vacation = 0.25
beach = 0.25
```

Example input:

```text
Merlin memory filing cabinet
```

Activation:

```text
Merlin = 1.0
memory = 1.0
filing cabinet = 1.0
human brain analogy = 0.8
associative retrieval = 0.8
current conversation memory = 0.6
medium memory = 0.6
long-term memory = 0.6
DeepInfra token reduction = 0.5
prompt compiler = 0.5
```

Implementation note:

Do not over-engineer spreading activation in the first version.

Start with:

- Direct concept matches.
- One-hop related concepts.
- Weighted scoring.

Later add:

- Two-hop spreading.
- Edge-type-specific weights.
- Decay based on relation type.
- Reinforcement when memories are used successfully.

---

# 4. Retrieval strategy

Use hybrid retrieval.

Do not rely only on vector embeddings.

The system should combine:

1. Keyword search
2. Full-text search
3. Concept/tag search
4. Graph activation search
5. Optional semantic embedding search
6. Recency scoring
7. Importance scoring
8. Memory type filtering

## 4.1 Why not only embeddings?

Embeddings are useful for semantic similarity, but they are not enough.

Merlin also needs exact matches for technical terms such as:

```text
DeepInfra
ChatterboxTurboTTS
Codex CLI
Slack
CUDA
beam=5
Whisper
Merlin.Backend
Program.cs
```

Keyword and FTS search handle exact technical terms better.

Embeddings help when the wording changes:

```text
"memory like the human brain"
```

should match:

```text
"associative retrieval using concept graph"
```

So the retrieval system should be hybrid.

## 4.2 Suggested scoring formula

Initial scoring formula:

```text
final_score =
  keyword_score        * 0.20
+ semantic_score       * 0.25
+ concept_score        * 0.25
+ graph_activation     * 0.15
+ importance_score     * 0.10
+ recency_score        * 0.05
```

If embeddings are not implemented yet, use:

```text
final_score =
  keyword_score        * 0.30
+ concept_score        * 0.35
+ graph_activation     * 0.20
+ importance_score     * 0.10
+ recency_score        * 0.05
```

These weights should be configurable.

## 4.3 Memory type filtering

Retrieval should behave differently depending on intent.

Examples:

### User asks about project architecture

Prefer:

```text
architecture_decision
project_goal
implementation_note
episode
```

### User asks about their preference

Prefer:

```text
user_preference
tool_preference
confirmed_memory
```

### User asks about a recent debugging issue

Prefer:

```text
debug_result
episode
tool_result
recent medium memory
```

### User says "remember this"

Bypass normal retrieval and write to long-term memory.

### User asks "what did we discuss before?"

Prefer medium memory and long-term memory, and include source/topic summaries.

---

# 5. Prompt compiler

The prompt compiler is the component that turns retrieved local memory into a small DeepInfra prompt.

This component is critical.

It should enforce a strict token budget and never send huge histories accidentally.

## 5.1 Prompt compiler responsibilities

The prompt compiler should:

- Always include the current user message exactly.
- Include minimal system/developer behavior instructions needed for the current call.
- Include current conversation summary if relevant.
- Include only the top relevant memories.
- Deduplicate overlapping memory content.
- Prefer long-term project decisions over verbose episode summaries.
- Include source hints if helpful.
- Exclude irrelevant memories even if they are recent.
- Enforce hard token budgets.
- Log approximate input token count before calling DeepInfra.
- Log why each memory was included.

## 5.2 Current user message is sacred

Never summarize the current user message before sending it to DeepInfra.

The current user message should be included exactly as the user said it.

Everything else may be summarized, compressed, or omitted.

## 5.3 Suggested prompt sections

A compiled prompt should have sections like:

```text
SYSTEM:
You are Merlin's reasoning model. Use the compact local memory context. Do not assume missing project details. Prefer local-first, cost-conscious architecture.

CURRENT TOPIC:
The user is designing Merlin's local brain-like memory system.

RELEVANT LONG-TERM MEMORY:
- Merlin should use local memory to reduce DeepInfra token usage.
- Merlin memory should use current conversation, medium memory, and long-term memory.
- Merlin should retrieve memories associatively through concept activation.

RELEVANT MEDIUM MEMORY:
- In a prior discussion, the user compared memory to a human filing cabinet where concepts like car and blue activate related memories.

USER PREFERENCES:
- The user prefers practical implementation guidance and detailed prompts for the coding agent.
- The user prefers separated concerns in code.

CURRENT USER MESSAGE:
"okay awesome! create a very very detailed .md about this concept what i can save in my todo..."
```

## 5.4 Token budgets

Suggested default budgets:

```text
Tiny/simple request:
- Current user message: exact
- Recent context: 0-200 tokens
- Retrieved memory: 0-300 tokens
- Total prompt target: 500-1000 tokens

Normal project discussion:
- Current user message: exact
- Current topic summary: 100-300 tokens
- Long-term memory: 100-500 tokens
- Medium memory: 100-700 tokens
- Total prompt target: 1000-2500 tokens

Deep design/debug session:
- Current user message: exact
- Current topic summary: 300-800 tokens
- Relevant memories: 500-1500 tokens
- Tool/log summaries: 500-1500 tokens
- Total prompt target: 3000-6000 tokens

Large log/code analysis:
- Never send raw huge logs by default.
- Summarize/chunk locally first.
- Send only relevant excerpts.
- Total prompt target: task-dependent, but should require explicit escalation.
```

## 5.5 Hard safety rule

Merlin should have a maximum prompt token threshold.

If the compiled context exceeds the threshold:

1. Compress medium memories.
2. Remove low-scoring memories.
3. Remove duplicate memories.
4. Prefer long-term distilled facts over episode summaries.
5. Ask a local summarizer to compress logs if needed.
6. If still too large, send a minimal prompt and state that context was limited.

Do not silently send huge context.

---

# 6. Memory writing

Memory writing is the process that updates Merlin's local brain after messages, topic changes, and explicit remember requests.

## 6.1 Memory writer responsibilities

The memory writer should decide:

- Should this message update current conversation memory?
- Did the topic change?
- Should the previous topic be summarized into medium memory?
- Did the user explicitly ask Merlin to remember something?
- Did the user express a stable preference?
- Did the conversation produce a durable architecture decision?
- Should something be promoted to long-term memory?
- Should an old memory be updated instead of creating a duplicate?
- Should stale debug/tool memories expire?

## 6.2 Avoid DeepInfra for every memory decision

Do not call DeepInfra after every message just to update memory.

That defeats the purpose.

Preferred approach:

```text
Every message:
- Local topic tracking
- Local concept extraction
- Lightweight metadata update

Sometimes:
- Local or cheap summarization
- Medium memory creation
- Long-term promotion
- Embedding generation
```

DeepInfra should only be used for memory writing when:

- The summary is complex and important.
- The user explicitly asks for a high-quality saved memory.
- A long topic ended and the local summarizer is not sufficient.
- A complicated architecture decision must be distilled accurately.

## 6.3 Explicit remember requests

If the user says something like:

```text
remember this
save this
store this
from now on
in the future
always do this
note that
```

Merlin should detect it.

For explicit remember requests:

- Save to long-term memory immediately.
- Mark `userConfirmed = true`.
- Record source message.
- Extract concepts.
- Avoid asking for confirmation unless the memory content itself is ambiguous.

Example:

```text
User: remember that Merlin should never send the full conversation to DeepInfra by default.
```

Save:

```json
{
  "memoryType": "architecture_decision",
  "project": "Merlin",
  "content": "Merlin should never send the full conversation history to DeepInfra by default.",
  "userConfirmed": true,
  "importance": 1.0
}
```

## 6.4 Ambiguous tool/preference changes

If the user wants Merlin to change behavior but the desired behavior is ambiguous, Merlin should ask for confirmation with a concrete preview.

Example:

```text
User: please say dates with month names and no leading zeros
```

Merlin should respond:

```text
I understood this as: when speaking dates, use a format like "17 June 2026" instead of "2026-06-17" or "05-05-2026". Should I save that as your date pronunciation preference?
```

Only after confirmation should Merlin save it as long-term tool preference.

Memory example:

```json
{
  "memoryType": "tool_preference",
  "tool": "system.date",
  "content": "When speaking dates, use month names and avoid leading zeroes, e.g. '17 June 2026'.",
  "userConfirmed": true,
  "importance": 0.9
}
```

---

# 7. Topic boundary detection

Merlin must detect when the active topic changes.

This is necessary because completed topics should be summarized into medium memory.

## 7.1 Topic boundary signals

Signals that the topic is continuing:

- User uses words like "this", "that", "it", "the system", "the thing".
- User asks follow-up questions.
- Same concepts/entities are active.
- User asks for implementation details of the current idea.
- User asks for a `.md`, prompt, todo, or code based on the current discussion.

Signals that a new topic started:

- User asks about a clearly different domain.
- Active concepts have low overlap with previous topic.
- User says "new topic" or "different question".
- User switches from Merlin memory architecture to Whisper VRAM, weather, finances, etc.

Signals that a topic is done:

- User says "great", "awesome", "thanks", then asks unrelated question.
- User asks for final artifact/export/todo and then stops.
- Long pause/session end.
- Explicit instruction to summarize/save.

## 7.2 Topic boundary algorithm MVP

Start simple.

For each user message:

1. Extract concepts from the message.
2. Compare with current active topic concepts.
3. Calculate overlap score.
4. Check if the message is a follow-up by language cues.
5. If overlap is high or follow-up cue is present, continue current topic.
6. If overlap is low and no follow-up cue, close old topic and start new topic.

Example pseudo-code:

```csharp
public TopicDecision DecideTopic(UserMessage message, CurrentConversationMemory current)
{
    var messageConcepts = conceptExtractor.Extract(message.Text);
    var overlap = conceptComparer.CalculateOverlap(messageConcepts, current.ActiveConcepts);
    var hasFollowUpCue = followUpDetector.HasFollowUpCue(message.Text);

    if (hasFollowUpCue || overlap >= 0.35)
    {
        return TopicDecision.ContinueCurrent(messageConcepts);
    }

    return TopicDecision.StartNewTopic(messageConcepts);
}
```

This can become more sophisticated later.

---

# 8. Database design

Use SQLite first.

SQLite is enough for the first version and introduces very low overhead.

Expected overhead:

```text
Keyword/tag lookup: usually below 1-5 ms
FTS search: usually below 1-10 ms
Small insert/update: usually below 1-10 ms
Loading top memories: usually below 1-10 ms
```

The DB is not the expensive part. DeepInfra calls, embeddings, summarization, and bad prompt construction are the expensive parts.

## 8.1 Tables

### memories

```sql
CREATE TABLE memories (
    id TEXT PRIMARY KEY,
    memory_type TEXT NOT NULL,

    title TEXT,
    content TEXT NOT NULL,
    summary TEXT,

    project TEXT,
    topic TEXT,
    tool TEXT,

    importance REAL NOT NULL DEFAULT 0.5,
    confidence REAL NOT NULL DEFAULT 0.8,

    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    last_accessed_at_utc TEXT,
    expires_at_utc TEXT,

    source_type TEXT,
    source_id TEXT,

    user_confirmed INTEGER NOT NULL DEFAULT 0,
    pinned INTEGER NOT NULL DEFAULT 0,
    archived INTEGER NOT NULL DEFAULT 0
);
```

Suggested memory_type values:

```text
current_topic
episode
project_goal
architecture_decision
implementation_note
user_preference
tool_preference
debug_result
tool_result
fact
correction
```

### concepts

```sql
CREATE TABLE concepts (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    normalized_name TEXT NOT NULL UNIQUE,
    concept_type TEXT,
    parent_concept_id TEXT,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);
```

Suggested concept_type values:

```text
project
tool
technology
person
preference
architecture
domain
color
object
abstract
```

### memory_concepts

```sql
CREATE TABLE memory_concepts (
    memory_id TEXT NOT NULL,
    concept_id TEXT NOT NULL,
    weight REAL NOT NULL DEFAULT 1.0,
    source TEXT,
    PRIMARY KEY (memory_id, concept_id),
    FOREIGN KEY (memory_id) REFERENCES memories(id),
    FOREIGN KEY (concept_id) REFERENCES concepts(id)
);
```

### concept_edges

```sql
CREATE TABLE concept_edges (
    from_concept_id TEXT NOT NULL,
    to_concept_id TEXT NOT NULL,
    relation_type TEXT NOT NULL,
    weight REAL NOT NULL DEFAULT 1.0,
    created_at_utc TEXT NOT NULL,
    PRIMARY KEY (from_concept_id, to_concept_id, relation_type),
    FOREIGN KEY (from_concept_id) REFERENCES concepts(id),
    FOREIGN KEY (to_concept_id) REFERENCES concepts(id)
);
```

Suggested relation_type values:

```text
is_a
part_of
related_to
example_of
used_for
causes
caused_by
preference_for
belongs_to_project
implemented_by
```

### topics

```sql
CREATE TABLE topics (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    summary TEXT,
    status TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    closed_at_utc TEXT,
    importance REAL NOT NULL DEFAULT 0.5
);
```

Suggested status values:

```text
active
closed
archived
```

### topic_messages

This table is optional, but useful if Merlin stores raw or semi-raw conversation turns locally.

```sql
CREATE TABLE topic_messages (
    id TEXT PRIMARY KEY,
    topic_id TEXT NOT NULL,
    role TEXT NOT NULL,
    content TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (topic_id) REFERENCES topics(id)
);
```

### memory_access_log

Use this for observability and debugging.

```sql
CREATE TABLE memory_access_log (
    id TEXT PRIMARY KEY,
    memory_id TEXT NOT NULL,
    request_id TEXT NOT NULL,
    access_reason TEXT,
    score REAL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (memory_id) REFERENCES memories(id)
);
```

### compiled_prompt_log

Use this to track token savings and audit what was sent to DeepInfra.

```sql
CREATE TABLE compiled_prompt_log (
    id TEXT PRIMARY KEY,
    request_id TEXT NOT NULL,
    model_provider TEXT,
    model_name TEXT,
    estimated_input_tokens INTEGER,
    estimated_output_tokens INTEGER,
    memory_count INTEGER,
    included_memory_ids TEXT,
    escalation_reason TEXT,
    created_at_utc TEXT NOT NULL
);
```

## 8.2 Full-text search

Use SQLite FTS5 for searching memories.

```sql
CREATE VIRTUAL TABLE memories_fts USING fts5(
    title,
    content,
    summary,
    project,
    topic,
    tool,
    content='memories',
    content_rowid='rowid'
);
```

Depending on implementation constraints, keeping FTS in sync may require triggers or manual updates.

## 8.3 Embeddings later

Embeddings are optional for MVP.

Do not block the first version on embeddings.

When added, store embeddings only for:

- Medium memory summaries.
- Long-term memories.
- Important architecture decisions.
- User preferences.
- Tool preferences.

Do not embed every raw message by default.

Possible table:

```sql
CREATE TABLE memory_embeddings (
    memory_id TEXT PRIMARY KEY,
    embedding_model TEXT NOT NULL,
    vector BLOB NOT NULL,
    dimensions INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (memory_id) REFERENCES memories(id)
);
```

---

# 9. Main components to implement

Use separated concerns. Do not put the whole system in one giant class.

The user prefers clean separation.

Suggested components:

```text
MemoryOrchestrator
CurrentConversationMemoryService
TopicBoundaryDetector
ConceptExtractor
ConceptGraphService
AssociativeRetriever
MediumMemoryStore
LongTermMemoryStore
MemoryWriter
MemoryPromoter
PromptCompiler
MemoryDashboardService
MemoryAuditLogger
TokenBudgetService
```

## 9.1 MemoryOrchestrator

Coordinates the memory flow.

Responsibilities:

- Entry point for memory before/after user messages.
- Calls topic detection.
- Calls retrieval.
- Calls prompt compiler.
- Calls memory writer after response.
- Keeps orchestration logic separate from storage details.

Suggested methods:

```csharp
Task<CompiledMemoryContext> PrepareContextAsync(UserMessage message, RequestContext requestContext);
Task ProcessResponseAsync(UserMessage message, AssistantResponse response, RequestContext requestContext);
Task SaveExplicitMemoryAsync(string content, MemorySaveOptions options);
```

## 9.2 CurrentConversationMemoryService

Responsibilities:

- Maintain active topic.
- Update current topic summary.
- Track active concepts/entities.
- Store recent turns if needed.
- Expose current state to prompt compiler.

## 9.3 TopicBoundaryDetector

Responsibilities:

- Decide whether a new user message continues the active topic.
- Detect topic switching.
- Trigger topic closing.
- Provide confidence score.

Output example:

```csharp
public sealed class TopicBoundaryResult
{
    public bool IsNewTopic { get; init; }
    public bool ShouldClosePreviousTopic { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<string> DetectedConcepts { get; init; }
    public string? SuggestedTopicTitle { get; init; }
}
```

## 9.4 ConceptExtractor

Responsibilities:

- Extract concepts from user messages, assistant responses, summaries, tool outputs, and saved memories.
- Normalize concept names.
- Detect project-specific terms.
- Detect technical terms.
- Avoid over-extracting useless common words.

MVP approach:

- Use a curated list of Merlin/project concepts.
- Use simple phrase extraction.
- Use keyword matching.
- Use casing/technical token heuristics.
- Later optionally use a local model or embeddings.

Important: technical terms should be preserved exactly where useful.

Examples:

```text
DeepInfra
ChatterboxTurboTTS
Whisper
beam=5
CUDA
Codex CLI
Slack
Merlin.Backend
Program.cs
```

## 9.5 ConceptGraphService

Responsibilities:

- Create/update concepts.
- Create/update edges between concepts.
- Retrieve related concepts.
- Run simple spreading activation.
- Return activated concept scores.

MVP behavior:

```text
Input concepts
↓
Direct concepts get score 1.0
One-hop related concepts get score = input score * edge weight * 0.6
Return top N activated concepts
```

## 9.6 AssociativeRetriever

Responsibilities:

- Retrieve relevant memory records using hybrid search.
- Combine FTS, concepts, graph activation, importance, recency, and optional embeddings.
- Deduplicate results.
- Return scored memory candidates with reasons.

Output example:

```csharp
public sealed class RetrievedMemory
{
    public string MemoryId { get; init; }
    public string Title { get; init; }
    public string Content { get; init; }
    public string? Summary { get; init; }
    public string MemoryType { get; init; }
    public double Score { get; init; }
    public IReadOnlyList<string> MatchReasons { get; init; }
}
```

Match reasons should be human-readable for logs/dashboard:

```text
Matched concept: Merlin
Matched concept: DeepInfra
Graph activation from: filing cabinet → associative retrieval
High importance: 0.95
Recent medium memory
```

## 9.7 MemoryWriter

Responsibilities:

- Update current memory after each turn.
- Save explicit remember requests.
- Create medium memories when topics close.
- Detect durable decisions/preferences.
- Avoid duplicate memories.
- Mark confirmation state.

## 9.8 MemoryPromoter

Responsibilities:

- Promote important medium memories into long-term memory.
- Extract durable project decisions.
- Extract user preferences.
- Extract tool preferences.
- Avoid promoting temporary/debug-only data.

Promotion should be conservative.

Long-term memory quality matters more than quantity.

## 9.9 PromptCompiler

Responsibilities:

- Build compact context for DeepInfra.
- Enforce token budgets.
- Include exact current user message.
- Include only relevant memories.
- Compress/omit content when budget is exceeded.
- Log prompt composition.

## 9.10 TokenBudgetService

Responsibilities:

- Estimate tokens.
- Track prompt sizes.
- Track per-provider/model usage.
- Prevent accidental huge prompts.
- Provide cost-related diagnostics.

For MVP, a rough token estimate is acceptable:

```text
estimated_tokens = character_count / 4
```

Improve later with model-specific tokenizers if needed.

---

# 10. Request flow

## 10.1 Before DeepInfra call

```text
User message arrives
↓
Intent router checks if local tool can answer
↓
If local tool can answer:
    Execute locally
    Update current memory if useful
    Do not call DeepInfra
↓
If DeepInfra is needed:
    Extract concepts
    Update/inspect current topic
    Retrieve relevant memories associatively
    Compile tiny prompt
    Log estimated tokens and included memories
    Call DeepInfra
```

## 10.2 After response

```text
Assistant response generated
↓
Update current conversation memory
↓
Detect explicit memory changes
↓
If topic ended:
    Summarize topic into medium memory
    Promote important conclusions to long-term memory
↓
Update access logs
```

## 10.3 Topic switch flow

```text
New user message
↓
TopicBoundaryDetector says new topic
↓
Close previous active topic
↓
Summarize previous topic into medium memory
↓
Extract long-term candidates
↓
Start new active topic
```

---

# 11. Memory dashboard requirements

A dashboard is not required for the first backend-only MVP, but the system must be designed so a dashboard can be added.

The dashboard should eventually show:

```text
Merlin Memory
├── Current Conversation
│   ├── Active topic
│   ├── Current goal
│   ├── Active concepts
│   └── Recent summary
├── Medium Memory
│   ├── Topic summaries
│   ├── Debug episodes
│   ├── Project discussions
│   └── Experiments
├── Long-Term Memory
│   ├── User preferences
│   ├── Tool preferences
│   ├── Project goals
│   ├── Architecture decisions
│   └── Important facts
└── Concept Cabinet
    ├── Concepts
    ├── Related concepts
    ├── Memories connected to concepts
    └── Edge weights
```

For each memory, the dashboard should support:

- View full content.
- View summary.
- View concepts.
- View source.
- View created/updated/accessed timestamps.
- View importance/confidence.
- View whether user confirmed it.
- Edit memory.
- Delete memory.
- Pin memory.
- Archive memory.
- Set expiry.
- Show why it was retrieved.

This matters because personal assistant memory must be transparent and controllable.

---

# 12. Cost and performance guidance

## 12.1 Database overhead

A local SQLite database introduces very little overhead.

Expected per-request overhead:

```text
Simple concept lookup: 1-5 ms
FTS memory search: 1-10 ms
Loading top memories: 1-10 ms
Memory insert/update: 1-10 ms
Total common DB overhead: 5-30 ms
```

This is worth it because it can save thousands of DeepInfra input tokens.

## 12.2 Avoid expensive memory operations on every turn

Do not do all of this every message:

```text
DeepInfra summarization
Embedding generation
Long-term promotion
Full graph rebuild
Large raw transcript analysis
```

Instead:

```text
Every message:
- Extract concepts locally
- Update current memory lightly
- Retrieve relevant memories if needed

Sometimes:
- Summarize topic
- Create medium memory
- Promote to long-term
- Generate embeddings
```

## 12.3 Embedding cost control

When embeddings are added:

- Do not embed every raw message.
- Embed only summaries and important memories.
- Batch embedding generation if possible.
- Cache embeddings.
- Consider local embeddings if practical.

---

# 13. MVP implementation plan

## Phase 1: Basic local memory database

Implement:

- SQLite database.
- `memories` table.
- `concepts` table.
- `memory_concepts` table.
- `topics` table.
- Basic repository classes.
- Simple CRUD operations.

Acceptance criteria:

- Merlin can create a memory.
- Merlin can list memories.
- Merlin can update a memory.
- Merlin can delete/archive a memory.
- Merlin can attach concepts to a memory.

## Phase 2: Current conversation memory

Implement:

- Active topic state.
- Topic creation/update.
- Recent summary field.
- Active concepts.
- Simple topic boundary detection.

Acceptance criteria:

- Merlin can track the current topic.
- Follow-up messages continue the same topic.
- Unrelated messages start a new topic.
- Closing a topic creates a medium memory summary.

## Phase 3: Medium memory summaries

Implement:

- Topic closing.
- Episode memory creation.
- Concept extraction for episode summaries.
- Importance scoring.

Acceptance criteria:

- When a topic ends, Merlin saves a summarized episode.
- Episode memory has title, summary, concepts, importance, timestamps.
- Episode can be retrieved later.

## Phase 4: Long-term memory and explicit remember requests

Implement:

- Explicit remember detection.
- Long-term memory save.
- User-confirmed flag.
- Memory type classification.

Acceptance criteria:

- "Remember that X" saves X to long-term memory.
- Saved memory is marked `userConfirmed = true`.
- Memory has extracted concepts.
- Duplicate obvious memories are not repeatedly created.

## Phase 5: Associative retrieval MVP

Implement:

- Concept-based retrieval.
- FTS keyword retrieval.
- One-hop graph activation.
- Scoring and ranking.
- Retrieval reasons.

Acceptance criteria:

- Querying "DeepInfra memory cost" retrieves memory about reducing DeepInfra token usage.
- Querying "filing cabinet" retrieves the brain-like memory design.
- Querying "blue" can retrieve memories connected to blue/color if such memories exist.
- Retrieval results include score and match reasons.

## Phase 6: Prompt compiler

Implement:

- Compact context object.
- Prompt sections.
- Token budget estimation.
- Included memory limits.
- Prompt composition logging.

Acceptance criteria:

- Current user message is always exact.
- Only top relevant memories are included.
- Prompt token estimate is logged.
- If context exceeds budget, low-scoring memories are removed.
- DeepInfra is never given the full raw conversation by default.

## Phase 7: Memory audit logs

Implement:

- Memory access logs.
- Compiled prompt logs.
- Escalation reason logging.
- Token estimate logging.

Acceptance criteria:

- For every DeepInfra call, Merlin records approximate input tokens.
- Merlin records which memories were included.
- Merlin records why the memories were included.

## Phase 8: Dashboard/API preparation

Implement backend endpoints or services that can later power a UI.

Acceptance criteria:

- Can list memories by type.
- Can list concepts.
- Can show memories connected to a concept.
- Can edit/delete/archive/pin memory.
- Can view recent prompt compilation logs.

---

# 14. Example end-to-end scenario

## Scenario: User asks about memory system later

User says:

```text
How were we going to build that brain memory thing again?
```

Expected Merlin behavior:

1. Extract concepts:

```text
brain memory
Merlin
memory system
```

2. Activate related concepts:

```text
human brain analogy
filing cabinet
current conversation memory
medium memory
long-term memory
associative retrieval
DeepInfra token reduction
```

3. Retrieve memories:

```text
- Long-term architecture decision: Merlin uses three memory layers.
- Long-term architecture decision: Merlin uses associative concept retrieval.
- Medium episode: Prior discussion about filing cabinet analogy.
- Project goal: reduce DeepInfra token usage.
```

4. Compile DeepInfra prompt:

```text
Relevant context:
- Merlin's memory should be brain-like with current, medium, and long-term layers.
- Completed topics are summarized into medium memory.
- Important/user-confirmed memories go to long-term memory.
- Retrieval should work through concept activation like a filing cabinet.
- Goal is to send tiny specific prompts to DeepInfra.

Current user message:
"How were we going to build that brain memory thing again?"
```

5. DeepInfra receives the small prompt, not the full prior chat.

---

# 15. Testing requirements

## 15.1 Unit tests

Add tests for:

- Concept extraction.
- Topic boundary detection.
- Memory creation.
- Memory concept linking.
- Concept graph one-hop activation.
- Retrieval scoring.
- Prompt budget trimming.
- Explicit remember detection.

## 15.2 Integration tests

Add tests for:

- Full flow: user message → concepts → retrieval → prompt compile.
- Topic switch → medium memory creation.
- Explicit remember → long-term memory.
- Multiple memories with overlapping concepts → deduped prompt context.
- Prompt compiler refuses/avoids huge raw history.

## 15.3 Example test cases

### Test: explicit remember

Input:

```text
Remember that Merlin should never send full chat history to DeepInfra by default.
```

Expected:

- Long-term memory created.
- `userConfirmed = true`.
- Concepts include Merlin, DeepInfra, chat history, token reduction.

### Test: topic continuation

Previous topic:

```text
Merlin memory architecture
```

Input:

```text
okay but how would the database schema look?
```

Expected:

- Continue current topic.
- Active concepts add database/schema.

### Test: topic switch

Previous topic:

```text
Merlin memory architecture
```

Input:

```text
what does beam do in Whisper?
```

Expected:

- Close previous topic.
- Save medium memory summary.
- Start new topic about Whisper beam decoding.

### Test: associative retrieval

Stored memory:

```text
Merlin memory should work like a filing cabinet, where car opens vehicle-related memories.
```

Input:

```text
how did that drawer idea work again?
```

Expected:

- Retrieve filing cabinet memory through concepts drawer/filing cabinet/associative retrieval.

---

# 16. Non-goals for the first implementation

Do not try to build everything at once.

The following are not required for MVP:

- Perfect human-like memory.
- Complex neural memory models.
- Embeddings for every message.
- A full graph UI.
- Automatic perfect long-term promotion.
- Cloud-based memory services.
- Sending every message to DeepInfra for memory classification.

The MVP should prove the architecture works locally.

---

# 17. Important implementation principles

## 17.1 Local-first

Memory should live locally by default.

DeepInfra should receive only compiled context.

## 17.2 Small prompts by default

Never send full conversation history by default.

## 17.3 Current message exactness

Always send the current user message exactly.

## 17.4 Retrieval transparency

Every retrieved memory should have a reason.

## 17.5 User control

The user must be able to inspect, edit, and delete memory.

## 17.6 Conservative long-term memory

Long-term memory should be high-quality and not cluttered.

## 17.7 Medium memory can be broader

Medium memory can store more episode summaries, because it is not always sent to DeepInfra.

## 17.8 Avoid hidden expensive calls

Do not call DeepInfra for memory management unless necessary.

## 17.9 Separate concerns

Keep storage, retrieval, topic tracking, graph logic, and prompt compilation separate.

## 17.10 Log token usage

Every DeepInfra request should log estimated token usage and the included memory context.

---

# 18. Suggested file/module layout

Adjust to the existing Merlin backend structure, but keep concerns separated.

Possible layout:

```text
Merlin.Backend/
  Memory/
    Abstractions/
      IMemoryRepository.cs
      IConceptRepository.cs
      ITopicRepository.cs
      IConceptExtractor.cs
      IAssociativeRetriever.cs
      IPromptCompiler.cs
      IMemoryWriter.cs
    Models/
      MemoryRecord.cs
      ConceptRecord.cs
      ConceptEdge.cs
      TopicRecord.cs
      RetrievedMemory.cs
      CompiledMemoryContext.cs
      TopicBoundaryResult.cs
      MemoryType.cs
    Services/
      MemoryOrchestrator.cs
      CurrentConversationMemoryService.cs
      TopicBoundaryDetector.cs
      ConceptExtractor.cs
      ConceptGraphService.cs
      AssociativeRetriever.cs
      MemoryWriter.cs
      MemoryPromoter.cs
      PromptCompiler.cs
      TokenBudgetService.cs
      MemoryAuditLogger.cs
    Storage/
      SqliteMemoryRepository.cs
      SqliteConceptRepository.cs
      SqliteTopicRepository.cs
      MemoryDbInitializer.cs
      Migrations/
    Tests/
      ConceptExtractorTests.cs
      TopicBoundaryDetectorTests.cs
      AssociativeRetrieverTests.cs
      PromptCompilerTests.cs
      MemoryIntegrationTests.cs
```

---

# 19. Initial seed memories

When implementing, seed these memories for Merlin if appropriate:

```text
Project goal:
Merlin should minimize DeepInfra token usage by storing rich local memory and sending only specific retrieved context.

Architecture decision:
Merlin memory should use three layers: current conversation memory, medium-term episodic memory, and long-term memory.

Architecture decision:
Merlin should retrieve memories associatively using concepts, similar to a human filing cabinet where one concept activates related drawers.

Architecture decision:
Completed topics should be summarized into medium memory.

Architecture decision:
Important conclusions and explicit remember requests should be saved into long-term memory.

Architecture decision:
The current user message should always be sent exactly to DeepInfra; only previous context should be compressed or omitted.

Architecture principle:
Merlin should never send full raw chat history to DeepInfra by default.

User preference:
The user prefers detailed implementation-ready documents/prompts for coding agents when asking for todos or implementation plans.

User preference:
The user prefers separated concerns and does not like many responsibilities mixed into one file.
```

---

# 20. Final target behavior

When the system is complete, Merlin should feel like this:

```text
User: remember that I want date responses spoken with month names.
Merlin: Saved.

Later:
User: what date is it?
Merlin uses the local date tool and formats speech according to the saved tool preference.
No DeepInfra call needed.
```

```text
User: how were we going to reduce DeepInfra costs again?
Merlin retrieves relevant long-term and medium memories about memory compiler, local-first routing, and prompt reduction.
DeepInfra receives only a tiny summary.
```

```text
User: what was that filing cabinet idea?
Merlin activates concepts: filing cabinet, memory, associative retrieval, brain-like memory.
It retrieves the correct prior memory even if the user does not use the exact same wording.
```

```text
User: what does beam do in Whisper?
Merlin detects a new topic, closes the prior memory-system topic into medium memory, and starts a new topic for Whisper decoding.
```

The end result should be:

```text
Merlin remembers broadly locally.
Merlin retrieves associatively.
Merlin sends narrowly to DeepInfra.
Merlin keeps token usage low.
Merlin remains inspectable and controllable by the user.
```

---

# 21. Agent implementation instruction

Implement this incrementally.

Do not attempt the full final system in one giant commit.

Start with the database, models, and simple retrieval path. Then add topic tracking. Then add medium memory. Then add long-term explicit remember behavior. Then add prompt compilation. Then add graph/associative retrieval improvements.

Before changing code, inspect the existing Merlin backend structure and align the implementation with the project's current patterns.

Prefer simple, working, testable components over clever abstractions.

The first milestone is successful when Merlin can:

1. Store local memories in SQLite.
2. Attach concepts to memories.
3. Track the current topic.
4. Save completed topics as medium memory summaries.
5. Save explicit remember requests as long-term memory.
6. Retrieve relevant memories by concepts/keywords.
7. Compile a small context packet for DeepInfra.
8. Log estimated tokens and included memory IDs.

Do not send full conversation history to DeepInfra by default.

That rule is central to the feature.
