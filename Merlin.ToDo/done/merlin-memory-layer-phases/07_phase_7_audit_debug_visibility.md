# Phase 7 — Audit and Debug Visibility

## Goal

Add enough backend visibility to inspect and debug Merlin's memory system.

This phase does not require a full dashboard UI.

The goal is to make memory behavior observable so the user can answer:

- What does Merlin currently think the topic is?
- What memories exist?
- What concepts exist?
- Which concepts are linked to a memory?
- What memories are retrieved for this query?
- What prompt was compiled for DeepInfra?
- Which memories were included in the prompt?
- How many tokens were estimated?

This phase should add dev-only endpoints or commands, depending on the existing backend style.

---

## Why this phase matters

Memory systems become dangerous when they are invisible.

Merlin must not silently store and use memories without a way to inspect what happened.

The user explicitly wants memory to be local, inspectable, editable, and controllable.

This phase creates the backend foundation for that transparency.

---

## Scope

Add backend-only debug visibility.

This may be:

- Dev-only HTTP endpoints.
- Dev menu commands.
- CLI/debug commands.
- Internal diagnostic service methods used by tests.

Choose the approach that best matches the existing Merlin project.

If adding HTTP endpoints, protect them so they are only enabled in development.

---

## Components to add

Add components similar to:

```text
MemoryDebugService
MemoryDebugController
MemoryDebugOptions
MemoryInspectionModels
```

Or, if the project avoids controllers for this area:

```text
MemoryDiagnosticCommands
MemoryDiagnosticService
```

---

## Required debug capabilities

### 1. Show current conversation state

Should return:

```text
conversation id
topic id
topic title
recent summary
current goal
active concepts
last updated timestamp
```

Example endpoint/command:

```text
GET /dev/memory/current
```

### 2. List memories

Filters:

```text
memory type
concept
project
topic
text query
include archived
limit
```

Example:

```text
GET /dev/memory/memories?type=architecture_decision&limit=20
```

Return:

```text
id
type
title
summary/content preview
importance
confidence
user confirmed
created/updated/accessed timestamps
linked concepts
```

### 3. Get memory detail

Example:

```text
GET /dev/memory/memories/{id}
```

Return full memory:

```text
content
summary
concepts
source ids
importance
confidence
timestamps
archived/pinned/expiry if available
```

### 4. List concepts

Example:

```text
GET /dev/memory/concepts
```

Return:

```text
id
name
normalized name
concept type
parent concept
edge counts
memory counts
```

### 5. Show concept detail

Example:

```text
GET /dev/memory/concepts/{id}
```

Return:

```text
concept
related concepts
edges
memories linked to concept
```

### 6. Test retrieval

Example:

```text
POST /dev/memory/retrieve
```

Input:

```json
{
  "query": "what was the filing cabinet idea?",
  "maxResults": 8
}
```

Return:

```text
query
detected concepts
activated concepts
retrieved memories
scores
reasons
```

This is extremely important for tuning retrieval.

### 7. Test prompt compile

Example:

```text
POST /dev/memory/compile-prompt
```

Input:

```json
{
  "message": "how were we going to reduce DeepInfra costs again?",
  "maxInputTokens": 2500
}
```

Return:

```text
compiled prompt
estimated tokens
included memory ids
included concept ids
omitted memory ids
trim reasons
prompt compilation id
```

This makes token behavior visible before the prompt is sent to DeepInfra.

### 8. List prompt compilation logs

Example:

```text
GET /dev/memory/prompt-compilations?limit=20
```

Return:

```text
id
prompt type
estimated tokens
included memory ids
included concept ids
conversation id
turn id
created timestamp
compiled prompt preview
```

### 9. Delete/archive test memories

At minimum, provide a way to delete/archive debug/test memories.

Example:

```text
DELETE /dev/memory/memories/{id}
```

or:

```text
POST /dev/memory/memories/{id}/archive
```

If delete is too risky, archive is enough for now.

---

## Development-only safety

If endpoints are used, they must only be active in development.

Example checks:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapMemoryDebugEndpoints();
}
```

Do not expose debug memory endpoints in production by default.

---

## Response shape guidance

Use simple DTOs.

Do not return EF entities directly.

Example:

```csharp
public sealed record MemoryDebugDto
{
    public required string Id { get; init; }
    public required string MemoryType { get; init; }
    public string? Title { get; init; }
    public string? Preview { get; init; }
    public double Importance { get; init; }
    public double Confidence { get; init; }
    public bool UserConfirmed { get; init; }
    public IReadOnlyList<string> Concepts { get; init; } = Array.Empty<string>();
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
```

---

## Tests to add

### Test: current state debug returns active topic

Expected:

- Debug service returns current conversation state.

### Test: list memories returns saved memory

Setup:

- Save memory.

Expected:

- Memory appears in debug list.

### Test: retrieval debug includes reasons

Setup:

- Save concept-linked memory.

Action:

- Run retrieval debug query.

Expected:

- Result includes score and reasons.

### Test: prompt compile debug returns compiled prompt

Action:

- Compile prompt through debug service.

Expected:

- Prompt returned.
- Estimated tokens returned.
- Included memory IDs returned.

### Test: endpoints are dev-only if endpoints are added

If possible, verify debug endpoints are not mapped outside development.

---

## Non-goals

Do not implement:

- Full frontend memory dashboard.
- Authentication/authorization UI.
- Complex editing experience.
- Embeddings UI.
- Interruption UI.
- Production public API.

This is debug/dev visibility only.

---

## Manual verification scenario

1. Start backend in development.
2. Save explicit memory:

```text
Remember that Merlin should use local memory to reduce DeepInfra token costs.
```

3. Call retrieval debug with:

```text
DeepInfra token costs
```

4. Confirm the saved memory appears with reasons.
5. Call prompt compile debug with:

```text
how do we reduce DeepInfra costs again?
```

6. Confirm compact prompt includes the memory and exact user message.
7. Inspect prompt compilation logs.

---

## Verification commands

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter MemoryDebug
```

Run all tests if practical:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

---

## Final response requirements for the agent

Report:

- Files changed.
- Debug capabilities added.
- Endpoint/command list.
- Whether they are development-only.
- Build result.
- Test result.
- Known limitations.
- Suggested next workstream.

Suggested next workstreams after this phase:

```text
1. Tune retrieval quality.
2. Add FTS if simple LIKE search is not enough.
3. Add memory dashboard UI.
4. Add interruption/correction prompt regeneration using memory context.
5. Add embeddings later if needed.
```
