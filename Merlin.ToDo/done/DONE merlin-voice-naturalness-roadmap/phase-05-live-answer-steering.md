# Phase 05 — Live Answer Steering

## Purpose

This phase lets the user steer Merlin mid-answer without forcing Merlin to restart or repeat itself.

Example target behavior:

```text
Merlin:
"The x-axis represents the input variable, and the y-axis shows..."

User:
"Well yeah, but that does not mean X causes Y."

Merlin:
"Right, that only shows a relationship, not causation. So the safer interpretation is..."
```

Core principle:

```text
Do not restart the answer.
Do not repeat what was already spoken.
Address the user's interruption and continue from the current point.
```

## Dependencies

Do not implement this phase before these are stable:

```text
Phase 01 — Live Utterance Gate
Phase 02 — Conversational Playback Control And Floor Handoff
Phase 04 — Active Answer Playback Context
```

Live Answer Steering depends on:

```text
- reliable user interruption capture
- natural playback pause/yield
- spoken/unspoken answer tracking
- gate decisions that distinguish steering from garbage/cancel/replace
```

## Explicit non-goals

Do not use this for tool rollback.

Do not steer answers when the user clearly said `stop` or `cancel that`.

Do not send garbage/malformed transcripts to DeepInfra as steering.

Do not repeat already spoken chunks.

Do not keep talking over the user.

## Existing repo areas to inspect first

```text
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/LiveUtterance/
Merlin.Backend/Services/AnswerPlayback/
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/LocalAIChatService.cs
Merlin.Backend/Services/DeepInfra*
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend.Tests/
```

Use actual service names in the repo.

## New route kind

Add or formalize a route kind:

```text
SteerCurrentAnswer
```

This is different from:

```text
StopPlaybackOnly
CancelActiveTurn
ReplaceActiveTurn
QueueAfterActiveTurn
AcceptNewRequest
```

Steering means the user is not cancelling the answer, but shaping it.

## Steering candidates

Examples that should often steer an active explanation:

```text
yeah but...
well yeah, but that does not mean...
that doesn't imply...
but what about...
isn't it more like...
not exactly, because...
so basically...
wait, wouldn't that mean...
```

Examples that should not steer:

```text
stop → stop playback only
cancel that → cancel active turn
never mind → cancel active turn
open Google instead → replacement/new tool request
continue → resume
random malformed garbage → hold/clarify/ignore
```

## Flow

Target flow:

```text
Merlin is speaking a long answer
↓
User starts speaking
↓
Playback Control yields/pause main answer
↓
User finishes
↓
Live Utterance Gate accepts as SteerCurrentAnswer
↓
Active Answer Playback Context snapshot is retrieved
↓
Optional micro-response plays: "Right, let me adjust that."
↓
DeepInfra/LLM receives steering prompt
↓
LLM returns revised continuation only
↓
Old remaining playback is discarded/replaced
↓
Merlin speaks revised continuation
```

## Prompt design

The steering prompt should ask for a continuation, not a full answer restart.

Suggested prompt:

```text
You are Merlin, a voice assistant currently speaking an answer.

Original user request:
{originalRequest}

Text already spoken aloud:
{spokenText}

Current interrupted chunk:
{currentChunkText}

Unspoken planned continuation:
{remainingText}

The user interrupted with:
{interruptionText}

Task:
Respond naturally to the user's interruption and continue the answer from this point.
Do not repeat the already spoken text.
Do not restart the explanation.
Address the user's point directly.
If the user corrected an assumption, adapt to it.
Keep the continuation concise and conversational.
Return only the continuation Merlin should speak next.
```

Keep prompt size controlled. If spoken/remaining text is huge, trim intelligently.

## Context trimming

Suggested trimming:

```text
spokenText: last 1000–2000 chars or last N chunks
currentChunkText: full current chunk
remainingText: next 1000–2000 chars or next N chunks
original request: full if short, summarized if long
```

Do not overwhelm the LLM with the entire conversation unless needed.

## Micro-response behavior

Micro-response is optional and should happen after user finishes speaking, not during.

Safe examples:

```text
Right, let me adjust that.
Got it.
Good distinction.
Okay, narrowing that.
```

Avoid saying `good catch` before verifying whether the user is right.

If LLM response is expected quickly, micro-response may be skipped.

## Replacement behavior

When revised continuation is ready:

```text
- cancel/discard old remaining TTS chunks
- mark old chunks as Replaced or Skipped
- create new answer/playback context or branch under same answer id
- enqueue revised continuation
```

The user should not hear old uncorrected remaining content after steering.

## Branch tracking

Consider tracking answer branches:

```text
answer-123 original branch
answer-123 branch-2 after steering interruption
```

This helps logs and debugging.

Minimal version can simply mark remaining old chunks as replaced and create a new context for the revised continuation.

## Failure behavior

If LLM revision fails:

```text
- do not resume old content blindly if the user clearly objected
- say a short clarification or fallback
```

Examples:

```text
Can you clarify what you want me to change?
I paused there. What should I adjust?
```

If the interruption was low confidence, use the gate’s hold/clarify policy instead of steering.

## Logging requirements

Add logs:

```text
LiveAnswerSteeringRequested
LiveAnswerSteeringSnapshotCreated
LiveAnswerSteeringPromptBuilt
LiveAnswerSteeringMicroResponseQueued
LiveAnswerSteeringLlmStarted
LiveAnswerSteeringLlmCompleted
LiveAnswerSteeringLlmCancelled
LiveAnswerSteeringContinuationQueued
LiveAnswerSteeringOldChunksReplaced
LiveAnswerSteeringFallbackClarification
```

Include:

```text
turn id
answer id
interruption text
spoken char count
remaining char count
current chunk id
new continuation length
```

Avoid logging full large prompts at high levels.

## Tests to add

Unit tests:

1. Steering candidate during speaking routes as `SteerCurrentAnswer`.
2. `stop` during speaking does not steer.
3. `cancel that` does not steer.
4. Malformed garbage does not steer.
5. Steering prompt includes original request, spoken text, current chunk, remaining text, and interruption text.
6. Steering prompt instructs not to repeat already spoken text.
7. Old remaining chunks are marked replaced/skipped when revised continuation is queued.
8. LLM cancellation is treated as cancellation, not provider failure.
9. Steering failure produces clarification/fallback, not old-content resume.
10. Micro-response is suppressed while user is still speaking.

Integration tests if feasible:

1. User interrupts explanation with objection; revised continuation is requested.
2. Revised continuation replaces remaining queued playback.
3. Already spoken chunks are not enqueued again.

## Acceptance criteria

This phase is done when:

```text
- Mid-answer user input can route as SteerCurrentAnswer.
- Merlin uses active answer playback context to build revised continuation prompts.
- Revised continuation does not repeat already-spoken chunks.
- Old remaining playback is replaced/skipped.
- Stop/cancel/replacement still bypass steering correctly.
- Garbage transcripts do not trigger steering.
- Logs and tests cover the full steering flow.
```

## Agent prompt

```text
You are working in the Merlin codebase.

Read and implement this phase:
Merlin.ToDo/phase-05-live-answer-steering.md

Goal:
Implement Live Answer Steering. When Merlin is speaking an explanatory answer and the user interrupts with a meaningful objection, correction, or question, Merlin should pause, capture the interruption, retrieve spoken/unspoken answer context, ask the LLM for a revised continuation, replace old remaining playback, and continue without repeating already-spoken content.

Important dependencies:
- Phase 01 Live Utterance Gate should exist.
- Phase 02 Conversational Playback Control should exist.
- Phase 04 Active Answer Playback Context should exist.

Important constraints:
- Do not use steering for "stop", "cancel that", or clear tool replacements.
- Do not send malformed garbage to DeepInfra as steering.
- Do not repeat already spoken chunks.
- Do not speak over the user.
- Keep prompts bounded; trim long spoken/remaining context.

Files to inspect first:
- Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
- Merlin.Backend/Services/LiveUtterance/
- Merlin.Backend/Services/AnswerPlayback/
- Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
- Merlin.Backend/Services/CommandRouter.cs
- Merlin.Backend/Services/LocalAIChatService.cs
- Merlin.Backend/Services/DeepInfra*
- Merlin.Backend/WebSocket/WebSocketHandler.cs
- Merlin.Backend.Tests/

Implementation guidance:
- Add/extend route kind SteerCurrentAnswer.
- Use Live Utterance Gate to distinguish steering from stop/cancel/replacement/garbage.
- Retrieve ActiveAnswerPlaybackSnapshot for the active turn.
- Build a continuation-only LLM prompt with original request, spoken text, current chunk, remaining text, and interruption text.
- Mark old remaining chunks as replaced/skipped.
- Queue only the revised continuation.
- Add logs and tests.

Acceptance tests:
- Objection during explanation triggers steering.
- Stop/cancel do not trigger steering.
- Garbage transcript does not trigger steering.
- Prompt includes required context and says not to repeat spoken text.
- Old remaining chunks are replaced/skipped.
- Revised continuation is queued.

After coding:
- Run the backend test suite.
- Summarize what changed.
- Note any limitations around chunk precision, prompt trimming, or fallback behavior.
```
