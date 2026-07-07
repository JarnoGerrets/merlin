# Phase 04 — Active Answer Playback Context

## Purpose

This phase adds tracking for what Merlin has already spoken, what it is currently speaking, and what remains unspoken in the current answer.

This is required before Live Answer Steering can work correctly.

Core principle:

```text
Before Merlin can revise a spoken answer, it must know what has already been said aloud.
```

This phase does **not** generate revised continuations yet. It only tracks playback context reliably.

## Why this matters

If Merlin is explaining something and the user interrupts with:

```text
well yeah, but that does not mean X causes Y
```

A later system should respond naturally without repeating the already-spoken introduction.

To do that, Merlin needs:

```text
original user request
full planned answer or generated chunks
spoken text so far
current chunk
remaining unspoken text
interruption point
```

Without this, Live Answer Steering will either restart the answer or repeat old information.

## Explicit non-goals

Do not implement DeepInfra revised continuation in this phase.

Do not change semantic answer content in this phase.

Do not build a complex memory system.

Do not implement rollback.

Do not require perfect word-level timing in the first version.

## Existing repo areas to inspect first

```text
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/ChatterboxTtsProvider.cs
Merlin.Backend/Services/ChatterboxChunkPlanner.cs
Merlin.Backend/Services/SpeechPolicyService.cs
Merlin.Backend/Models/AssistantResponse.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend.Tests/
```

The repo already has TTS chunk planning. Attach answer context tracking where chunks are created/enqueued/spoken.

## Suggested new service

```text
Merlin.Backend/Services/AnswerPlayback/
  ActiveAnswerPlaybackContext.cs
  AnswerPlaybackChunk.cs
  IActiveAnswerPlaybackContextService.cs
  ActiveAnswerPlaybackContextService.cs
  AnswerPlaybackContextOptions.cs
```

Use existing folder conventions if there is a better place.

## Context model

Suggested shape:

```csharp
public sealed class ActiveAnswerPlaybackContext
{
    public required string AnswerId { get; init; }
    public required string TurnId { get; init; }
    public required string CorrelationId { get; init; }
    public required string OriginalUserRequest { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public List<AnswerPlaybackChunk> Chunks { get; } = new();

    public string GetSpokenText() => ...;
    public string GetCurrentChunkText() => ...;
    public string GetRemainingText() => ...;
}
```

Suggested chunk:

```csharp
public sealed class AnswerPlaybackChunk
{
    public required string ChunkId { get; init; }
    public required int Index { get; init; }
    public required string Text { get; init; }
    public AnswerChunkStatus Status { get; set; }
    public DateTimeOffset? EnqueuedAt { get; set; }
    public DateTimeOffset? PlaybackStartedAt { get; set; }
    public DateTimeOffset? PlaybackCompletedAt { get; set; }
    public DateTimeOffset? InterruptedAt { get; set; }
}
```

Suggested statuses:

```text
Planned
Enqueued
Synthesizing
Ready
Playing
Spoken
Interrupted
Skipped
Cancelled
Replaced
```

## Chunk granularity

Use the existing TTS chunk planning if possible.

Chunking should be good enough to avoid repeating entire answers. It does not need perfect word-level tracking in v1.

Recommended granularity:

```text
sentence-level or phrase-level chunks
not whole answer only
not individual words unless already available
```

## Context lifecycle

### Creation

Create context when Merlin begins preparing a spoken answer.

Required fields:

```text
answer id
turn id
correlation id
original user request
answer text or chunk text
```

### Enqueue

When a chunk is queued for TTS:

```text
status = Enqueued
```

### Playback start

When playback starts for a chunk:

```text
status = Playing
playbackStartedAt = now
```

### Playback complete

When a chunk finishes:

```text
status = Spoken
playbackCompletedAt = now
```

### Interruption

When user interrupts mid-chunk:

```text
current chunk = Interrupted
interruptedAt = now
remaining chunks stay queued/ready/planned
```

### Completion

When all chunks are done or cancelled:

```text
context archived or completed
```

Keep recent contexts briefly for debugging/correction.

## Partial chunk handling

If exact partial spoken text is unavailable, mark the whole current chunk as partially spoken/interrupted.

Suggested helper output:

```text
spokenText = all completed chunks + maybe current chunk prefix if available
currentChunkText = current chunk
remainingText = interrupted current chunk remainder if available + queued chunks
```

If no prefix timing exists, use conservative behavior:

```text
spokenText = completed chunks only
currentChunkText = interrupted chunk
remainingText = interrupted chunk + queued chunks
```

Later Live Answer Steering can use this to avoid repeating completed chunks even if current chunk handling is imperfect.

## Integration with playback control

Phase 02 should notify this context service when playback pauses, resumes, stops, or is interrupted.

Needed events or method calls:

```text
OnAnswerCreated
OnChunkEnqueued
OnChunkPlaybackStarted
OnChunkPlaybackCompleted
OnChunkInterrupted
OnAnswerCancelled
OnAnswerCompleted
```

## Logging requirements

Add logs:

```text
AnswerPlaybackContextCreated
AnswerPlaybackChunkAdded
AnswerPlaybackChunkEnqueued
AnswerPlaybackChunkStarted
AnswerPlaybackChunkSpoken
AnswerPlaybackChunkInterrupted
AnswerPlaybackContextCompleted
AnswerPlaybackContextCancelled
AnswerPlaybackContextSnapshotRequested
```

Include:

```text
answer id
turn id
correlation id
chunk id
chunk index
status
text length
```

Do not log huge full answer bodies at high log level. Use debug for larger text if needed.

## Snapshot API

Add a method that later systems can call:

```csharp
ActiveAnswerPlaybackSnapshot? GetCurrentSnapshot(string turnId);
```

Suggested snapshot:

```csharp
public sealed class ActiveAnswerPlaybackSnapshot
{
    public required string AnswerId { get; init; }
    public required string TurnId { get; init; }
    public required string OriginalUserRequest { get; init; }
    public required string SpokenText { get; init; }
    public required string CurrentChunkText { get; init; }
    public required string RemainingText { get; init; }
    public required int SpokenChunkCount { get; init; }
    public required int RemainingChunkCount { get; init; }
}
```

## Tests to add

1. Context is created for a spoken answer.
2. Chunks are added in order.
3. Chunk status moves from planned/enqueued to playing to spoken.
4. Spoken text contains completed chunks.
5. Remaining text contains unspoken chunks.
6. Interrupted current chunk is marked interrupted.
7. Snapshot returns original request, spoken text, current chunk, remaining text.
8. Cancelled answer marks context cancelled.
9. Completed answer marks context completed.
10. Long answer logging does not spam full text at inappropriate levels.

Integration tests if feasible:

1. A queued TTS response creates an active answer context.
2. Playback events update chunk status.
3. Barge-in interruption marks current chunk interrupted.

## Acceptance criteria

This phase is done when:

```text
- Merlin tracks active spoken answer context.
- Answer chunks have statuses.
- Spoken/unspoken/current text can be queried.
- Interruption marks current chunk/context appropriately.
- Logs and tests prove the context updates through playback lifecycle.
- No semantic answer steering is implemented yet.
```

