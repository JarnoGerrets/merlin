---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/DONE streaming_responses/merlin_streaming_response_tts_todo.md
classification: implementation-plan
related_features:
  - Voice Interruption System
  - Streaming Responses and TTS
status: implemented
imported_to_vault: true
---

# Merlin Streaming Response + Speakable TTS Pipeline Todo

## Goal

Implement streaming conversational responses for Merlin so DeepInfra model output can begin speaking much earlier, without sending unstable token chunks or trailing words into TTS.

The goal is **not** raw token-to-speech.

The goal is:

```text
DeepInfra streaming response
→ backend-owned text buffer
→ speakable segment detection
→ segment cleanup / formatting removal
→ ordered TTS segment queue
→ playback starts after the first safe segment
→ model continues streaming while TTS is already speaking
```

This should reduce perceived latency significantly while preserving natural speech quality and interruption safety.

---

## Core Principle

DeepInfra/network stream chunks are not speech chunks.

The backend must treat streamed deltas as unstable input and only commit text to TTS when the local segmenter decides the text has reached a safe boundary.

Bad:

```text
DeepInfra chunk: "The best way is to"
→ send to TTS
```

Good:

```text
DeepInfra chunk: "The best way is to"
→ append to buffer
→ no safe boundary
→ keep waiting

Later buffer: "The best way is to split only at complete sentence boundaries."
→ safe sentence boundary found
→ send complete sentence to TTS
```

Always maintain:

```text
committed speech segment | uncommitted tail
```

The uncommitted tail is the protection against random trailing words.

---

## Non-Goals

1. Do not route commands/tools through the model.
2. Do not stream local command acknowledgements.
3. Do not feed raw tokens directly into TTS.
4. Do not cut speech at arbitrary character limits.
5. Do not remove the non-streaming fallback until streaming is stable.
6. Do not let markdown/code/list formatting leak into spoken output.
7. Do not allow pending streamed text to keep playing after a user interruption.

---

## Current Problem

Current conversational path likely behaves like this:

```text
User asks question
→ prompt built
→ await full DeepInfra response
→ clean full response
→ send full response to TTS
→ playback starts
```

This creates high perceived latency because Merlin waits for the entire answer before speaking.

Target behavior:

```text
User asks question
→ prompt built
→ DeepInfra stream starts
→ first safe sentence/clause detected
→ TTS starts for first segment
→ DeepInfra continues streaming
→ later segments are queued while earlier ones play
```

---

## Desired End-State Architecture

```text
ConversationOrchestrator
  ↓
IAssistantTextGenerationService.StreamAsync(...)
  ↓
DeepInfraStreamingChatClient
  ↓
StreamingResponseAssembler
  ↓
SpeakableTextSanitizer
  ↓
SpeechSegmentQueue
  ↓
TtsSynthesisWorker
  ↓
AssistantSpeechPlaybackService
```

Interruption path:

```text
User interrupts
→ cancel current model stream
→ cancel current segment assembly
→ cancel queued TTS jobs
→ optionally pause/stop current playback
→ preserve spoken-so-far transcript
→ reprompt with original request + spoken answer + interruption text
```

---

## Important Design Decision

All conversational model responses should use the streaming path.

Do not add logic like:

```text
if answer seems long → stream
else → full response
```

That is unnecessary complexity.

Instead:

```text
Command/tool route → local path, no model
Conversational route → streaming model path
Fallback model without streaming → fake stream with one final delta
```

---

# PR Breakdown

## PR 1 — Introduce Streaming Text Generation Abstraction

### Goal

Create a unified model-generation interface where the orchestrator always consumes a stream, even if the underlying provider only supports full-response generation.

### Tasks

- [ ] Add `IAssistantTextGenerationService`.
- [ ] Add `ModelTextDelta` DTO.
- [ ] Add `ModelStreamCompleted` / final marker semantics.
- [ ] Wrap existing DeepInfra non-streaming client as fallback.
- [ ] Ensure existing conversational behavior still works with fake streaming.
- [ ] Do not change TTS behavior yet.

### Suggested interface

```csharp
public interface IAssistantTextGenerationService
{
    IAsyncEnumerable<ModelTextDelta> StreamAsync(
        CompiledPrompt prompt,
        ModelGenerationOptions options,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record ModelTextDelta(
    string Text,
    bool IsFinal = false,
    string? Provider = null,
    string? Model = null,
    int? SequenceNumber = null);
```

### Fallback adapter

```csharp
public sealed class NonStreamingGenerationAdapter : IAssistantTextGenerationService
{
    private readonly IFullResponseModelClient _client;

    public async IAsyncEnumerable<ModelTextDelta> StreamAsync(
        CompiledPrompt prompt,
        ModelGenerationOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await _client.GenerateAsync(prompt, options, cancellationToken);
        yield return new ModelTextDelta(response.Text, IsFinal: true, Provider: response.Provider, Model: response.Model);
    }
}
```

### Acceptance Criteria

- [ ] Existing conversational answers still work.
- [ ] Orchestrator can consume `IAsyncEnumerable<ModelTextDelta>`.
- [ ] Non-streaming fallback returns one final delta.
- [ ] Cancellation token is wired through.
- [ ] No behavior change yet for commands/tools.

### Tests

- [ ] Fake stream returns expected deltas.
- [ ] Cancellation stops generation.
- [ ] Full-response fallback produces exactly one final delta.
- [ ] Existing conversational unit tests still pass.

---

## PR 2 — Add DeepInfra Streaming Client

### Goal

Implement real streaming support for DeepInfra using the existing DeepInfra model configuration.

### Tasks

- [ ] Add `DeepInfraStreamingChatClient`.
- [ ] Use the same model, auth, base URL, timeout, and prompt formatting as the current DeepInfra path.
- [ ] Send request with streaming enabled.
- [ ] Parse server-sent events / OpenAI-compatible streaming chunks.
- [ ] Emit `ModelTextDelta` for each text delta.
- [ ] Emit final marker when stream completes.
- [ ] Handle provider errors cleanly.
- [ ] Preserve retry/fallback behavior where appropriate.
- [ ] Add logging around first-token latency and stream completion.

### Suggested metrics/logging

Capture:

```text
ModelStreamStarted
FirstDeltaReceivedMs
TotalDeltasReceived
TotalCharactersReceived
StreamCompletedMs
StreamCancelled
StreamFailed
FallbackUsed
```

### Important Behavior

The client should only expose text deltas. It should not decide speech segmentation.

Bad:

```csharp
// Do not segment here.
if (delta.EndsWith(".")) enqueueTts(delta);
```

Good:

```csharp
// Provider client only emits text.
yield return new ModelTextDelta(deltaText);
```

### Acceptance Criteria

- [ ] DeepInfra can stream normal conversational text.
- [ ] Stream cancellation works quickly.
- [ ] Errors fall back to existing model path.
- [ ] No raw streaming provider JSON leaks upward.
- [ ] No TTS changes yet.

### Tests

- [ ] Parse mocked streaming chunks.
- [ ] Ignore empty deltas.
- [ ] Correctly detect final chunk.
- [ ] Handle malformed SSE line without crashing entire app if recoverable.
- [ ] Cancel mid-stream.
- [ ] Provider failure triggers fallback.

---

## PR 3 — Add Streaming Response Assembler

### Goal

Create the local buffering component that turns unstable model deltas into stable, speakable text segments.

This is the key component that prevents trailing words.

### Concept

Maintain an internal buffer:

```text
_buffer = all text received but not yet committed to speech
```

Only release text up to the last safe boundary.

Example:

```text
Buffer:
"Merlin should stream responses, but it should not speak every raw token because"

Safe release:
"Merlin should stream responses,"

Uncommitted tail:
"but it should not speak every raw token because"
```

### Tasks

- [ ] Add `StreamingResponseAssembler`.
- [ ] Add `Append(ModelTextDelta delta)`.
- [ ] Add `DrainReadySegments(bool isFinal)`.
- [ ] Add local safe-boundary detection.
- [ ] Add dangling-ending guard.
- [ ] Add minimum word count rules.
- [ ] Add first-segment stricter rules.
- [ ] Add forced flush on final stream completion.
- [ ] Add optional merge of tiny final tail into previous segment.

### Suggested DTO

```csharp
public sealed record SpeakableTextSegment(
    string Text,
    int SequenceNumber,
    bool IsFinalSegment = false,
    bool WasForcedFlush = false,
    SpeakableBoundaryKind BoundaryKind = SpeakableBoundaryKind.Unknown);
```

```csharp
public enum SpeakableBoundaryKind
{
    Unknown,
    Sentence,
    Clause,
    Paragraph,
    FinalFlush,
    ForcedLongBufferFlush
}
```

### Safe Boundary Rules

Preferred release boundaries:

1. Sentence punctuation:
   - `.`
   - `?`
   - `!`
2. Paragraph or clean newline boundary.
3. Clause punctuation if the segment is long enough:
   - `,`
   - `;`
   - `:`
4. Final stream end.

Never release text only because the buffer reached a character limit.

For long buffers:

```text
if buffer > MaxBufferChars:
    find last safe boundary inside buffer
    release up to that boundary
    keep the tail
```

### Suggested defaults

```csharp
public sealed class StreamingSegmentationOptions
{
    public int FirstSegmentMinWords { get; init; } = 8;
    public int LaterSegmentMinWords { get; init; } = 5;
    public int PreferredSentenceMaxChars { get; init; } = 220;
    public int HardBufferMaxChars { get; init; } = 320;
    public int TinyFinalTailMaxWords { get; init; } = 4;
    public bool AllowClauseBoundaries { get; init; } = true;
    public bool MergeTinyFinalTail { get; init; } = true;
}
```

### Dangling Ending Guard

Reject non-final segments ending in words such as:

```text
and
or
but
because
that
to
of
for
with
without
as
if
when
while
where
which
who
into
from
by
so
then
than
about
```

Example rejected segments:

```text
"The best way is to"
"This means that"
"You should start with"
"The reason is because"
```

### Suggested helper

```csharp
private static readonly HashSet<string> BadEndingWords = new(StringComparer.OrdinalIgnoreCase)
{
    "and", "or", "but", "because", "that", "to", "of", "for",
    "with", "without", "as", "if", "when", "while", "where",
    "which", "who", "into", "from", "by", "so", "then", "than", "about"
};

private static bool EndsWithDanglingWord(string text)
{
    var words = Regex.Matches(text, @"\b[\w']+\b")
        .Select(m => m.Value)
        .ToList();

    if (words.Count == 0)
        return true;

    return BadEndingWords.Contains(words[^1]);
}
```

### Pseudocode

```csharp
public IReadOnlyList<SpeakableTextSegment> DrainReadySegments(bool isFinal)
{
    var ready = new List<SpeakableTextSegment>();

    while (true)
    {
        var boundary = FindLastSafeBoundary(_buffer, isFinal);

        if (boundary < 0)
            break;

        var candidate = _buffer[..boundary.EndIndex].Trim();
        var tail = _buffer[boundary.EndIndex..];

        if (!IsSpeakable(candidate, isFinal, boundary.Kind))
            break;

        ready.Add(new SpeakableTextSegment(
            Text: candidate,
            SequenceNumber: _nextSequenceNumber++,
            IsFinalSegment: isFinal && string.IsNullOrWhiteSpace(tail),
            WasForcedFlush: boundary.Kind == SpeakableBoundaryKind.ForcedLongBufferFlush,
            BoundaryKind: boundary.Kind));

        _buffer = tail.TrimStart();
    }

    return ready;
}
```

### Acceptance Criteria

- [ ] Random provider chunks never become speech chunks directly.
- [ ] Partial trailing words remain in buffer.
- [ ] First spoken segment is stable and natural.
- [ ] Later segments can be shorter but not dangling.
- [ ] Long buffers flush only at a safe boundary.
- [ ] Final stream flushes remaining text.
- [ ] Tiny final tails are handled gracefully.

### Tests

- [ ] `"The best way is to"` does not release.
- [ ] `"The best way is to split safely."` releases one sentence.
- [ ] `"This means that"` does not release.
- [ ] `"This means that Merlin needs a commit buffer."` releases.
- [ ] Long buffer releases at last comma/sentence boundary, not at exact char limit.
- [ ] Final flush releases remaining complete text.
- [ ] Tiny final tail merge works.
- [ ] Multiple sentences in one delta produce multiple segments.
- [ ] Newline/paragraph boundary works.
- [ ] Markdown heading does not become awkward speech.

---

## PR 4 — Add Speakable Text Sanitizer

### Goal

Clean each committed segment before it reaches TTS.

This is separate from segmentation. Segmentation decides **when** text is safe to speak. Sanitization decides **how** it should be spoken.

### Tasks

- [ ] Add `ISpeakableTextSanitizer`.
- [ ] Strip or convert markdown.
- [ ] Remove raw list markers.
- [ ] Convert headings to plain speech.
- [ ] Handle code blocks safely.
- [ ] Normalize whitespace.
- [ ] Remove citations/metadata if present.
- [ ] Avoid reading JSON unless explicitly required.
- [ ] Add tests for Markdown-heavy responses.

### Suggested interface

```csharp
public interface ISpeakableTextSanitizer
{
    string Sanitize(string text, SpeakableTextSanitizationContext context);
}
```

```csharp
public sealed record SpeakableTextSanitizationContext(
    bool IsCodeExplanationExpected = false,
    bool PreserveTechnicalSymbols = false,
    bool IsFirstSegment = false);
```

### Cleanup Rules

#### Markdown list markers

Input:

```text
- Add streaming
- Add segment queue
```

Spoken output:

```text
Add streaming. Add segment queue.
```

#### Numbered lists

Input:

```text
1. Add streaming.
2. Add cancellation.
```

Spoken output:

```text
First, add streaming. Second, add cancellation.
```

Optional: keep simpler initially:

```text
Add streaming. Add cancellation.
```

#### Headings

Input:

```text
## Why this matters
```

Spoken output:

```text
Why this matters.
```

#### Code fences

Input:

```text
```csharp
var x = 1;
```
```

Default spoken output:

```text
There is a code example here.
```

Only read code if the user explicitly asks for code to be spoken.

#### JSON

Input:

```json
{"intent":"open_url","confidence":0.92}
```

Default spoken output:

```text
I found structured intent data.
```

Or skip entirely depending on context.

### Acceptance Criteria

- [ ] TTS does not speak markdown syntax in normal answers.
- [ ] TTS does not speak raw code fences.
- [ ] TTS does not speak raw JSON accidentally.
- [ ] List answers sound natural.
- [ ] Sanitizer does not destroy normal technical explanation text.

### Tests

- [ ] Bullet list cleanup.
- [ ] Numbered list cleanup.
- [ ] Heading cleanup.
- [ ] Inline code cleanup.
- [ ] Code fence handling.
- [ ] JSON handling.
- [ ] Excess whitespace normalization.

---

## PR 5 — Add Speech Segment Queue

### Goal

Allow TTS to synthesize and play multiple committed speech segments in order while the model continues streaming.

### Required Behavior

```text
Segment 1 committed
→ TTS synthesis starts
→ playback starts

Segment 2 committed while segment 1 is playing
→ TTS synthesis can start or queue
→ playback waits until segment 1 finishes

Segment 3 committed
→ queue behind segment 2
```

### Tasks

- [ ] Add `SpeechSegmentQueue`.
- [ ] Add ordered segment lifecycle.
- [ ] Add cancellation support.
- [ ] Add max queue size/backpressure.
- [ ] Track spoken-so-far transcript.
- [ ] Expose queue state for interruption logic.
- [ ] Integrate with existing `AssistantSpeechPlaybackService`.

### Segment Lifecycle

Each segment should have state:

```text
Generated
QueuedForTts
Synthesizing
ReadyToPlay
Playing
Spoken
Cancelled
Failed
```

Suggested enum:

```csharp
public enum SpeechSegmentState
{
    Generated,
    QueuedForTts,
    Synthesizing,
    ReadyToPlay,
    Playing,
    Spoken,
    Cancelled,
    Failed
}
```

Suggested runtime model:

```csharp
public sealed class SpeechSegmentJob
{
    public required Guid Id { get; init; }
    public required int SequenceNumber { get; init; }
    public required string Text { get; init; }
    public SpeechSegmentState State { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PlaybackStartedAt { get; set; }
    public DateTimeOffset? PlaybackCompletedAt { get; set; }
}
```

### Queue Ordering Rule

TTS synthesis may be parallel later, but playback must be ordered.

Phase 1 should keep it simple:

```text
single synthesis worker
single playback order
no parallel synthesis yet
```

Optional later optimization:

```text
parallel synthesize segment N+1 while segment N is playing
but never play out of order
```

### Backpressure

Avoid runaway queue growth if model streams faster than TTS can speak.

Suggested simple limit:

```text
MaxPendingSegments = 5
```

If exceeded:

```text
pause draining new segments temporarily
or keep buffering model text but do not enqueue more TTS until queue shrinks
```

Do not cancel model just because TTS queue is full unless memory/latency becomes a problem.

### Acceptance Criteria

- [ ] Segment order is preserved.
- [ ] TTS starts after first committed segment.
- [ ] Later segments queue correctly.
- [ ] Current speech can be cancelled.
- [ ] Pending segments can be cleared.
- [ ] Spoken transcript is accurate.
- [ ] Queue does not grow unbounded.

### Tests

- [ ] Enqueue three segments and verify playback order.
- [ ] Cancel while segment 1 is playing.
- [ ] Cancel while segment 2 is synthesizing.
- [ ] Clear pending queue.
- [ ] Spoken transcript only includes actually spoken segments.
- [ ] Failed TTS segment does not deadlock queue.

---

## PR 6 — Integrate Streaming Pipeline Into Conversational Orchestrator

### Goal

Replace the current full-response wait path with streaming generation + assembler + sanitizer + speech queue.

### Target Flow

```csharp
await foreach (var delta in generationService.StreamAsync(prompt, options, cancellationToken))
{
    assembler.Append(delta);

    var rawSegments = assembler.DrainReadySegments(delta.IsFinal);

    foreach (var rawSegment in rawSegments)
    {
        var cleanText = sanitizer.Sanitize(rawSegment.Text, context);

        if (!string.IsNullOrWhiteSpace(cleanText))
        {
            await speechSegmentQueue.EnqueueAsync(cleanText, rawSegment, cancellationToken);
        }
    }
}
```

### Tasks

- [ ] Locate current DeepInfra full-response wait point.
- [ ] Replace with streaming generation loop.
- [ ] Wire assembler.
- [ ] Wire sanitizer.
- [ ] Wire speech queue.
- [ ] Preserve text response for UI/chat history.
- [ ] Preserve final assistant response object.
- [ ] Preserve fallback behavior.
- [ ] Add feature flag.

### Suggested feature flag

```json
{
  "StreamingResponses": {
    "Enabled": true,
    "UseDeepInfraStreaming": true,
    "UseSegmentedTts": true,
    "FallbackToFullResponse": true
  }
}
```

### Important

The UI/chat transcript may want the full final answer, while TTS speaks segments.

Track separately:

```text
Full generated text buffer → for transcript/UI/memory
Committed spoken segments → for TTS/interruption continuation
```

Do not rely on TTS queue text as the only copy of generated response.

### Acceptance Criteria

- [ ] Conversational answers start speaking after first safe segment.
- [ ] Full final text is still available for transcript/memory.
- [ ] Commands/tools remain unaffected.
- [ ] Existing fallback path works.
- [ ] Feature flag can disable streaming.

### Tests

- [ ] Conversational stream produces TTS segments.
- [ ] Full final answer is preserved.
- [ ] Empty deltas do not create empty speech.
- [ ] Fallback fake stream still speaks.
- [ ] Feature flag returns to old behavior.

---

## PR 7 — Add Interruption Cancellation + Continuation Safety

### Goal

Make streaming compatible with Merlin's interruption system.

When the user interrupts, Merlin must stop all in-flight work related to the previous answer.

### Required Cancellation Scope

On interruption/correction/stop:

```text
cancel current DeepInfra stream
cancel assembler
clear uncommitted text buffer
cancel queued TTS segments
stop/pause current playback depending on interruption type
preserve spoken-so-far transcript
preserve original user request
preserve interruption text
```

### Tasks

- [ ] Add `StreamingConversationSession` object.
- [ ] Add per-response cancellation token source.
- [ ] Connect interruption event to cancellation.
- [ ] Clear speech queue on cancellation.
- [ ] Expose spoken-so-far transcript.
- [ ] Expose generated-but-not-spoken text only for debugging, not continuation.
- [ ] Reprompt continuation from spoken-so-far, not from queued/unspoken text.

### Suggested session model

```csharp
public sealed class StreamingConversationSession
{
    public required Guid ResponseId { get; init; }
    public required string OriginalUserMessage { get; init; }
    public required CancellationTokenSource Cancellation { get; init; }

    public StringBuilder FullGeneratedText { get; } = new();
    public List<SpeakableTextSegment> CommittedSegments { get; } = new();
    public List<SpeechSegmentJob> SpokenSegments { get; } = new();

    public string SpokenTextSoFar => string.Join(" ", SpokenSegments.Select(s => s.Text));
}
```

### Continuation Prompt Data

When the user says:

```text
"No, I meant the backend part."
```

New prompt should include:

```text
Original user request:
<original question>

Assistant answer spoken so far:
<only actually spoken text>

User interruption/correction:
No, I meant the backend part.

Instruction:
Answer the correction naturally and continue from the corrected intent. Do not repeat already-spoken content unless needed for clarity.
```

Do not include queued-but-unspoken text as if the user heard it.

### Acceptance Criteria

- [ ] User interruption cancels model stream.
- [ ] User interruption cancels pending TTS.
- [ ] Spoken-so-far is accurate.
- [ ] Reprompt does not assume the user heard unspoken queued text.
- [ ] Stop command stops everything immediately.
- [ ] Correction command restarts answer cleanly.

### Tests

- [ ] Interrupt during model stream before first TTS segment.
- [ ] Interrupt while first TTS segment is playing.
- [ ] Interrupt while second segment is queued.
- [ ] Interrupt after model finished but TTS still speaking.
- [ ] Verify only spoken text enters continuation prompt.
- [ ] Verify pending queue is empty after interruption.

---

## PR 8 — Add Latency Metrics + Diagnostics

### Goal

Measure whether streaming actually improves perceived latency and does not introduce bad speech artifacts.

### Metrics

Capture per response:

```text
UserSpeechEndToModelRequestMs
ModelRequestToFirstDeltaMs
FirstDeltaToFirstCommittedSegmentMs
FirstCommittedSegmentToTtsStartMs
TtsStartToPlaybackStartMs
UserSpeechEndToFirstAudioMs
TotalModelStreamDurationMs
TotalTtsPlaybackDurationMs
SegmentsGeneratedCount
SegmentsSpokenCount
SegmentsCancelledCount
ForcedFlushCount
DanglingCandidateRejectedCount
FallbackUsed
```

### Tasks

- [ ] Add structured logging for streaming response sessions.
- [ ] Add timing stopwatch around model stream.
- [ ] Add timing stopwatch around first TTS segment.
- [ ] Add segment-level diagnostics.
- [ ] Add debug mode to show committed segment boundaries.
- [ ] Add optional dev overlay/log line for first-audio latency.

### Example debug log

```text
[StreamingResponse] responseId=... firstDeltaMs=384 firstSegmentMs=912 firstAudioMs=1480 segments=6 forcedFlush=0 rejectedDangling=2
```

### Acceptance Criteria

- [ ] First-audio latency is measurable.
- [ ] Segment count is measurable.
- [ ] Forced flushes are visible.
- [ ] Cancellations are visible.
- [ ] Fallback path is visible.

### Tests

- [ ] Metrics emitted for successful stream.
- [ ] Metrics emitted for cancelled stream.
- [ ] Metrics emitted for fallback.
- [ ] No null/invalid timing values on early cancellation.

---

## PR 9 — Tune Segmentation For Natural Speech

### Goal

Adjust segment boundaries based on real TTS behavior and user experience.

### What To Tune

- First segment minimum words.
- Clause boundary allowance.
- Max preferred sentence length.
- Hard buffer length.
- Dangling ending words.
- Tiny final tail handling.
- Markdown/list handling.

### Test Prompts

Use these voice prompts to test perceived latency and naturalness.

#### Long explanation

```text
Explain how Merlin's interruption pipeline should work when I correct you halfway through an answer.
```

Expected:

```text
Starts speaking after first sentence.
No weird partial phrases.
No trailing words.
Continues smoothly.
```

#### Technical answer with lists

```text
Give me the implementation steps for streaming DeepInfra responses into Chatterbox TTS.
```

Expected:

```text
Does not read bullet symbols.
Does not read markdown headings awkwardly.
List sounds natural.
```

#### Sentence with dangerous connectors

```text
Explain why we should not send raw model chunks directly to TTS.
```

Expected:

```text
Does not speak phrases ending in "because", "that", "to", "with".
```

#### Interruption mid-stream

```text
Explain streaming TTS architecture in detail.
```

Interrupt after first segment:

```text
No, focus only on the backend queue part.
```

Expected:

```text
Previous stream stops.
Queued old speech does not continue.
New response starts from corrected intent.
```

#### Stop command

```text
Explain the whole architecture.
```

Interrupt:

```text
Stop.
```

Expected:

```text
Immediate stop.
No trailing queued segment plays afterward.
```

### Acceptance Criteria

- [ ] Speech starts noticeably earlier than old full-response path.
- [ ] No random trailing words are heard.
- [ ] No markdown syntax is spoken in normal answers.
- [ ] No queued stale answer continues after correction.
- [ ] First segment feels natural, not rushed.
- [ ] Long answers continue smoothly.

---

# Detailed Component Specs

## 1. `IAssistantTextGenerationService`

### Responsibility

Expose model output as a stream of text deltas.

### Does

- Calls DeepInfra or fallback model.
- Yields text deltas.
- Supports cancellation.
- Reports provider/model info.

### Does Not

- Segment text for speech.
- Clean markdown.
- Call TTS.
- Handle playback.

---

## 2. `StreamingResponseAssembler`

### Responsibility

Convert random model deltas into backend-committed speakable segments.

### Does

- Maintains uncommitted buffer.
- Finds safe boundaries.
- Rejects dangling endings.
- Applies first/later segment thresholds.
- Flushes final tail safely.

### Does Not

- Call DeepInfra.
- Call TTS.
- Remove all markdown.
- Decide interruption behavior.

---

## 3. `SpeakableTextSanitizer`

### Responsibility

Make committed text sound good when spoken.

### Does

- Removes markdown syntax.
- Normalizes whitespace.
- Converts headings/lists.
- Handles code fences.
- Handles raw JSON defensively.

### Does Not

- Decide when text is safe to speak.
- Change semantic content heavily.
- Rewrite the answer creatively.

---

## 4. `SpeechSegmentQueue`

### Responsibility

Manage TTS jobs and ordered playback for committed speech segments.

### Does

- Enqueues segments.
- Synthesizes TTS.
- Plays in order.
- Tracks lifecycle.
- Cancels pending work.
- Tracks spoken-so-far.

### Does Not

- Generate model text.
- Segment model text.
- Decide final answer content.

---

## 5. `StreamingConversationSession`

### Responsibility

Own all state for one active streamed assistant answer.

### Does

- Holds response ID.
- Holds cancellation token.
- Tracks generated text.
- Tracks committed segments.
- Tracks spoken segments.
- Exposes spoken-so-far for interruption continuation.

### Does Not

- Persist memory by itself.
- Decide user intent.
- Execute tools.

---

# Edge Cases To Handle

## Edge Case 1 — Provider sends tiny chunks

Input deltas:

```text
"The"
" best"
" way"
" is"
" to"
```

Expected:

```text
No speech yet.
```

Later:

```text
"The best way is to use a commit buffer."
```

Expected:

```text
Speak full sentence.
```

---

## Edge Case 2 — Provider sends multiple sentences at once

Input:

```text
"Streaming helps. But raw chunks are unsafe. Use a segmenter."
```

Expected:

```text
Segment 1: "Streaming helps."
Segment 2: "But raw chunks are unsafe."
Segment 3: "Use a segmenter."
```

Potential tuning:

Very short segments may be merged to avoid choppy speech.

---

## Edge Case 3 — Dangling connector

Input:

```text
"This matters because"
```

Expected:

```text
Do not speak.
```

Later:

```text
"This matters because it prevents awkward trailing words."
```

Expected:

```text
Speak full sentence.
```

---

## Edge Case 4 — Long sentence with commas

Input:

```text
"The safest approach is to buffer the model stream locally, release only up to a safe punctuation boundary, and keep the rest uncommitted until more text arrives."
```

Expected:

Possible segmentation:

```text
"The safest approach is to buffer the model stream locally,"
"release only up to a safe punctuation boundary,"
"and keep the rest uncommitted until more text arrives."
```

But avoid segment ending in dangling connector:

```text
"and"
```

should never be spoken alone or at the end of a segment.

---

## Edge Case 5 — Markdown list

Input:

```text
"1. Add streaming.\n2. Add segmenting.\n3. Add cancellation."
```

Expected spoken output:

```text
"Add streaming. Add segmenting. Add cancellation."
```

Or:

```text
"First, add streaming. Second, add segmenting. Third, add cancellation."
```

Pick one style and keep it consistent.

---

## Edge Case 6 — Code block begins mid-stream

Input:

```text
"Use this interface:\n```csharp\npublic interface"
```

Expected:

```text
Do not read half code block awkwardly.
```

Options:

1. Skip code block speech entirely.
2. Say: "There is a code example here."
3. Only read code if user explicitly requested code details.

Phase 1 recommendation:

```text
Do not speak raw code blocks.
Say a short placeholder if necessary.
```

---

## Edge Case 7 — Final tiny tail

Segments:

```text
"Streaming makes Merlin faster."
```

Final tail:

```text
"Much faster."
```

Options:

1. Speak as its own final segment.
2. Merge into previous if previous has not started playback yet.
3. Keep it as final if previous already played.

Recommended:

```text
If previous segment is not yet played, merge tiny final tail.
If previous segment already played, speak final tail only if it sounds complete.
```

---

## Edge Case 8 — User interrupts before first segment

State:

```text
Model stream active
Assembler buffer has text
No TTS yet
```

User says:

```text
"Wait, I meant something else."
```

Expected:

```text
Cancel model stream.
Clear assembler buffer.
No stale text should ever be spoken.
Start new flow from correction.
```

---

## Edge Case 9 — User interrupts while TTS is speaking

State:

```text
Segment 1 playing
Segment 2 queued
Model still streaming
```

Expected:

```text
Pause/stop segment 1 depending on interruption type.
Cancel segment 2.
Cancel model stream.
Clear assembler buffer.
Reprompt using only actually spoken portion of segment 1 if available.
```

If partial spoken word tracking is unavailable, use segment-level approximation first:

```text
Include completed spoken segments only.
```

Later improvement:

```text
Track playback position inside current segment.
```

---

# Rollout Plan

## Phase 1 — Safe Infrastructure

- Add streaming abstraction.
- Add fake stream fallback.
- Add DeepInfra streaming client.
- Keep old TTS behavior.

Risk: low.

## Phase 2 — Segmenter In Isolation

- Add assembler.
- Add sanitizer.
- Unit test heavily.
- No production playback change yet.

Risk: low-to-medium.

## Phase 3 — Segmented TTS Behind Feature Flag

- Add speech segment queue.
- Enable for dev/test only.
- Keep instant rollback to full response.

Risk: medium.

## Phase 4 — Interruption Integration

- Cancel stream and queue on interruption.
- Track spoken-so-far.
- Reprompt from spoken content only.

Risk: medium-to-high because it touches UX-critical interruption flow.

## Phase 5 — Default On

- Enable streaming for all conversational model responses.
- Keep fallback flag.
- Tune segment thresholds with real voice tests.

Risk: medium.

---

# Configuration Proposal

```json
{
  "StreamingResponses": {
    "Enabled": true,
    "UseDeepInfraStreaming": true,
    "UseSegmentedTts": true,
    "FallbackToFullResponse": true,
    "FirstSegmentMinWords": 8,
    "LaterSegmentMinWords": 5,
    "PreferredSentenceMaxChars": 220,
    "HardBufferMaxChars": 320,
    "AllowClauseBoundaries": true,
    "MergeTinyFinalTail": true,
    "TinyFinalTailMaxWords": 4,
    "MaxPendingTtsSegments": 5,
    "SkipCodeBlocksInSpeech": true,
    "DebugLogSegmentBoundaries": true
  }
}
```

---

# Manual Test Checklist

## Basic Streaming

- [ ] Ask a long explanatory question.
- [ ] Confirm speech starts before full answer is complete.
- [ ] Confirm answer continues smoothly.
- [ ] Confirm final answer still appears complete in UI/logs.

## Trailing Word Prevention

- [ ] Test answers that contain phrases like `because`, `that`, `to`, `with`.
- [ ] Confirm Merlin never speaks a segment ending awkwardly on those words.

## Markdown Cleanup

- [ ] Ask for implementation steps.
- [ ] Confirm Merlin does not say "dash", "hash hash", or raw numbering awkwardly.

## Interruption

- [ ] Interrupt before first spoken segment.
- [ ] Interrupt during first spoken segment.
- [ ] Interrupt while later segments are queued.
- [ ] Confirm stale queued speech never plays.

## Stop

- [ ] Say `stop` during streamed answer.
- [ ] Confirm model stream and TTS queue stop immediately.

## Correction

- [ ] Say `no, I meant...` during streamed answer.
- [ ] Confirm new answer uses correction.
- [ ] Confirm old answer does not resume.

## Fallback

- [ ] Disable DeepInfra streaming.
- [ ] Confirm fake stream fallback still works.
- [ ] Disable segmented TTS.
- [ ] Confirm old full-response behavior still works.

---

# Agent Implementation Notes

## Where To Be Careful

1. Do not confuse provider chunks with speech chunks.
2. Do not let the TTS queue outlive the response session.
3. Do not include unspoken queued text in continuation prompts.
4. Do not break command/tool local routing.
5. Do not remove fallback until streaming has been tested thoroughly.
6. Keep the segmenter heavily unit-tested; this is the main safety layer.

## Preferred Implementation Style

- Small services.
- Clear interfaces.
- Dependency injection friendly.
- Feature flags for rollout.
- Unit tests around segmentation and cancellation.
- Structured logs for timing.
- Keep current behavior as fallback.

## Suggested File Areas

Adjust names to actual repo structure.

```text
Merlin.Backend/Services/AI/Streaming/
  IAssistantTextGenerationService.cs
  ModelTextDelta.cs
  DeepInfraStreamingChatClient.cs
  NonStreamingGenerationAdapter.cs

Merlin.Backend/Services/Speech/Streaming/
  StreamingResponseAssembler.cs
  StreamingSegmentationOptions.cs
  SpeakableTextSegment.cs
  ISpeakableTextSanitizer.cs
  SpeakableTextSanitizer.cs
  SpeechSegmentQueue.cs
  SpeechSegmentJob.cs
  StreamingConversationSession.cs

Merlin.Backend.Tests/Services/AI/Streaming/
  DeepInfraStreamingChatClientTests.cs
  NonStreamingGenerationAdapterTests.cs

Merlin.Backend.Tests/Services/Speech/Streaming/
  StreamingResponseAssemblerTests.cs
  SpeakableTextSanitizerTests.cs
  SpeechSegmentQueueTests.cs
  StreamingConversationCancellationTests.cs
```

---

# Definition Of Done

This work is done when:

- [ ] Conversational DeepInfra answers stream by default.
- [ ] Merlin starts speaking after the first safe segment instead of waiting for the full response.
- [ ] Raw model/network chunks are never sent directly to TTS.
- [ ] The segmenter prevents dangling trailing words.
- [ ] Markdown/code/list formatting is cleaned before speech.
- [ ] Interruption cancels model streaming and pending TTS.
- [ ] Continuation prompts only use actually spoken text.
- [ ] Full generated response is still available for transcript/memory.
- [ ] Feature flag can safely return to old full-response behavior.
- [ ] Tests cover segmentation, sanitization, queue order, cancellation, and fallback.
- [ ] Logs show first-token latency, first-segment latency, and first-audio latency.

---

# Recommended First Agent Prompt

```text
Implement PR 1 for Merlin's streaming conversational response pipeline.

Goal:
Introduce a streaming text generation abstraction without changing current speech behavior yet.

Requirements:
- Add IAssistantTextGenerationService with StreamAsync returning IAsyncEnumerable<ModelTextDelta>.
- Add ModelTextDelta DTO.
- Add a NonStreamingGenerationAdapter that wraps the existing full-response model client and yields one final delta.
- Wire the conversational model path to consume the streaming abstraction while preserving current full-response behavior.
- Commands/tools must remain untouched and must not route through the model.
- Add unit tests for fake streaming, cancellation, and fallback behavior.
- Do not implement TTS segmentation yet.
- Keep the old behavior available as fallback.

Acceptance criteria:
- Existing conversational responses still work.
- Existing command/tool routing still works.
- The orchestrator can consume a model stream.
- The fake stream fallback yields exactly one final delta.
- Cancellation is wired through.
```

---

# Recommended Second Agent Prompt

```text
Implement PR 2 and PR 3 for Merlin's streaming conversational response pipeline.

Goal:
Add DeepInfra streaming support and a local StreamingResponseAssembler that prevents unsafe trailing-word TTS chunks.

Requirements:
- Implement DeepInfraStreamingChatClient using the existing DeepInfra config/model/auth.
- Send streaming-enabled chat completion requests.
- Parse streaming response chunks and emit ModelTextDelta values.
- Add StreamingResponseAssembler.
- The assembler must maintain an uncommitted buffer.
- It must only release text up to safe sentence/clause/paragraph boundaries.
- It must never release text just because a provider chunk arrived.
- It must reject non-final segments ending in dangling words like and/or/but/because/that/to/of/for/with.
- It must support stricter first-segment rules.
- It must flush remaining text on final stream completion.
- Add unit tests for random tiny chunks, dangling endings, long buffers, final flush, and multiple sentences in one chunk.

Do not integrate segmented TTS yet unless all tests pass.
```

---

# Recommended Third Agent Prompt

```text
Implement PR 4, PR 5, and PR 6 for Merlin's streaming conversational response pipeline.

Goal:
Clean committed text for speech, queue TTS segments in order, and integrate the streaming pipeline into the conversational response path behind a feature flag.

Requirements:
- Add SpeakableTextSanitizer for markdown/list/code/json cleanup.
- Add SpeechSegmentQueue with ordered segment lifecycle.
- Track Generated, QueuedForTts, Synthesizing, ReadyToPlay, Playing, Spoken, Cancelled, Failed states.
- Integrate model stream → assembler → sanitizer → speech queue.
- Preserve full generated text separately for UI/transcript/memory.
- Add feature flags for StreamingResponses.Enabled and UseSegmentedTts.
- Keep old full-response behavior as fallback.
- Add tests for sanitization, queue order, cancellation, and fallback.

Important:
Do not allow raw provider chunks into TTS.
Do not let markdown syntax leak into spoken output.
```

---

# Recommended Fourth Agent Prompt

```text
Implement PR 7 and PR 8 for Merlin's streaming conversational response pipeline.

Goal:
Make streamed speech interruption-safe and measurable.

Requirements:
- Add StreamingConversationSession per active assistant response.
- On interruption, cancel the active model stream, clear assembler buffer, cancel queued TTS, and stop/pause current playback according to the existing interruption policy.
- Track spoken-so-far using only actually completed spoken segments.
- Continuation/correction prompts must use original user request + spoken-so-far + user interruption text.
- Do not include queued-but-unspoken text as if the user heard it.
- Add structured metrics for first delta, first committed segment, first audio, stream duration, segment counts, cancellations, forced flushes, and fallback use.
- Add tests for interruption before first segment, during playback, with queued segments, and after model completion while TTS is still playing.

Acceptance criteria:
Old queued speech never continues after the user corrects or stops Merlin.
```
