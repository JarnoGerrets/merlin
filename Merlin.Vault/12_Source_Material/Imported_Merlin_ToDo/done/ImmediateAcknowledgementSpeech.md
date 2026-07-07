---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/ImmediateAcknowledgementSpeech.md
classification: implementation-plan
related_features:
  - Voice Interruption System
  - Responsive Feedback
status: implemented
imported_to_vault: true
---

# Merlin Immediate Acknowledgement and Progress Speech System

## Purpose

Merlin currently sometimes stays silent while a slow operation runs. This feels broken or awkward, especially when the delay comes from:

* DeepInfra response latency
* tool execution
* memory retrieval / prompt compilation
* research-style requests
* long-running diagnostics
* TTS preparation
* external services

The goal of this feature is to make Merlin feel alive, attentive, and premium while work is happening.

This system should allow Merlin to speak quickly after receiving a request, then continue working on the actual answer in parallel.

Core principle:

```text
Acknowledge immediately.
Work silently while useful.
Give progress only when silence becomes uncomfortable.
Never fake progress.
Never block the real task.
Never replace the final answer.
```

---

# 1. Problem being solved

Example current behavior:

```text
User: What does beam do in Whisper?
Merlin: [silent for 28 seconds while DeepInfra responds]
Merlin: [finally starts speaking]
```

This feels like Merlin did not hear the user, froze, or crashed.

Desired behavior:

```text
User: What does beam do in Whisper?
Merlin: "Good question, sir. Let me gather the relevant context."
[DeepInfra request continues]
Merlin: "I have the context. I am waiting on the reasoning response."
[DeepInfra still pending]
Merlin: "Still working on it, sir. This one is taking a little longer than usual."
[final answer arrives]
Merlin: "In Whisper, beam search keeps multiple decoding candidates..."
```

Important: the second and third messages should only happen if the wait is actually long enough.

---

# 2. Important terminology

## Immediate acknowledgement

A short phrase spoken quickly after request classification.

Example:

```text
"Of course, sir. Let me check that."
```

This should happen only when Merlin predicts the request may take noticeable time.

## Progress speech

A short follow-up spoken if the main task is still not ready after a configured threshold.

Example:

```text
"I have the context now. Waiting on the model response."
```

Progress speech should be useful, state-aware, and rare.

## Main response

The real answer from the tool, DeepInfra, or local command.

The acknowledgement and progress speech must never replace the main response.

---

# 3. Goals

The system must:

* Speak a short acknowledgement quickly when a request is likely to take time.
* Continue executing the real request in parallel.
* Avoid silence during long waits.
* Avoid annoying the user during fast requests.
* Avoid fake progress claims.
* Avoid repeating the same phrase too often.
* Prefer cached audio for common acknowledgement phrases.
* Keep the premium Merlin feel.
* Work primarily in voice mode.
* Avoid changing WebSocket/frontend contracts unless absolutely necessary.
* Not break existing tool, DeepInfra, memory, TTS, or playback behavior.
* Be compatible with future interruption/barge-in behavior.

---

# 4. Non-goals for MVP

Do not implement these yet:

* Full response length tuning.
* Full interruption/barge-in behavior.
* Complex personality engine.
* LLM-generated acknowledgements for every request.
* Frontend dashboard.
* Streaming DeepInfra token speech.
* Real-time partial answer generation.
* Fake progress bars.
* Long filler monologues.

This system is about small, tasteful, useful speech while the actual work continues.

---

# 5. Core behavior

## 5.1 Fast request behavior

For fast requests, Merlin should not speak an acknowledgement.

Example:

```text
User: What time is it?
Merlin: "It is 21:34, sir."
```

Bad behavior:

```text
User: What time is it?
Merlin: "I am checking the clock, sir."
Merlin: "It is 21:34, sir."
```

That is annoying if the answer is instant.

Rule:

```text
If expected response time is below the acknowledgement threshold, do not acknowledge separately.
```

Suggested threshold:

```text
Immediate local tool expected under 500ms:
- no acknowledgement

Potentially slow tool or DeepInfra:
- acknowledgement allowed after classification
```

---

## 5.2 Slow request behavior

For slow requests, Merlin should speak quickly.

Example:

```text
User: Why do people fear change?
Merlin: "That is a thoughtful question, sir. Let me gather my thoughts."
[main request continues]
Merlin: [final answer]
```

The acknowledgement should not wait for DeepInfra.

---

## 5.3 Very slow request behavior

If the main request is still not done after a few seconds, Merlin may speak progress updates.

Example timeline:

```text
0.0s   User finishes speaking
0.6s   STT complete
0.8s   Intent classified as DeepInfra/general reasoning
1.0s   Merlin says acknowledgement
1.1s   DeepInfra request already running or starts in parallel
6.0s   No final answer yet → optional progress update
14.0s  Still no answer → second progress update
25.0s  Still no answer → long-wait update
28.0s  Final answer arrives
29.0s  Merlin speaks final answer
```

Do not keep talking constantly. The goal is to reduce dead silence, not fill every second.

---

# 6. Request categories

The acknowledgement policy should choose phrases based on request type.

## 6.1 Deep conversational / reasoning request

Examples:

```text
Why do people fear change?
What is the meaning of life?
How should Merlin's memory architecture work?
Explain the tradeoffs between SQLite and Postgres.
```

Initial acknowledgement examples:

```text
"That is a good question, sir. Let me gather my thoughts."
"Interesting question, sir. I will think that through properly."
"Understood. Let me reason through that carefully."
"Give me a moment, sir. I want to answer that properly."
```

Progress examples:

```text
"I have the relevant context. I am still working through the answer."
"I am still reasoning through it, sir."
"This is taking a little longer than usual, but I am still on it."
```

---

## 6.2 Research / recommendation request

Examples:

```text
What family car should I buy?
Which laptop is best?
Find me a good GPU for local AI.
What are the best options for cloud GPUs?
```

Initial acknowledgement examples:

```text
"Of course, sir. Let me look into that properly."
"I will check that carefully, sir."
"Understood. I will compare the options properly."
"Let me gather the relevant details first."
```

Progress examples:

```text
"I am still checking the relevant details."
"I have part of the picture. I am narrowing it down now."
"This is taking a little longer, sir. I am still comparing the options."
```

Important:

Do not say “looking online” unless Merlin is actually browsing or using an external search/tool.

---

## 6.3 System / local tool request

Examples:

```text
How much RAM is free?
What is the current volume?
Open Discord.
Check GPU usage.
What time is it?
```

Initial acknowledgement examples:

```text
"I am checking that now, sir."
"One moment, sir. I am checking the system."
"Checking now."
"Right away, sir."
```

Progress examples:

```text
"The system check is still running."
"I am still waiting for the system result."
"That is taking longer than expected, sir."
```

For very fast tools, skip acknowledgement.

---

## 6.4 Memory request

Examples:

```text
What do you remember about SQLite?
What did we decide about Merlin memory?
Please save into long-term memory that Merlin should prefer SQLite.
```

Initial acknowledgement examples for search/recall:

```text
"Of course, sir. Let me check memory."
"I will look through memory for that."
"Let me retrieve the relevant memory."
```

Initial acknowledgement for explicit save:

Usually no acknowledgement needed. The final response can simply be:

```text
"Saved."
```

If save unexpectedly takes long:

```text
"I am saving that now, sir."
```

Progress examples for memory search:

```text
"I found a few relevant memories. I am putting them together."
"I am narrowing down the memory results."
```

---

## 6.5 Ambiguous or unclear request

Examples:

```text
What does bean do in Whisper?
```

Possible issue: STT probably heard “bean” instead of “beam”.

Acknowledgement should avoid committing to the wrong interpretation.

Initial acknowledgement examples:

```text
"I may need to clarify that, sir. Let me check the context first."
"Understood. I will check what that refers to."
```

Final answer may include correction:

```text
"I think you may have meant beam, not bean. In Whisper, beam search..."
```

---

# 7. Timing policy

The acknowledgement system needs a timing policy.

## 7.1 Suggested thresholds

```text
0-500ms:
- no acknowledgement
- just answer

500ms-1500ms:
- acknowledgement optional depending on request type
- if cached phrase is available, okay to speak

1500ms-6000ms:
- speak initial acknowledgement for slow predicted tasks

6000ms-12000ms:
- if main response not ready, optional first progress update

12000ms-25000ms:
- if still not ready, optional second progress update

25000ms+:
- long-wait update allowed
```

## 7.2 Example for 28-second DeepInfra wait

For a 28-second wait:

```text
0.8s:
"Good question, sir. Let me gather my thoughts."

7s:
"I have the context. I am waiting on the reasoning response."

16s:
"Still working on it, sir. This one is taking a little longer than usual."

28s:
Final answer starts.
```

## 7.3 Maximum progress speech

For one user request:

```text
Initial acknowledgement: max 1
Progress updates: max 2 or 3
Final answer: always spoken when ready
```

Do not speak every few seconds.

---

# 8. Phrase quality rules

Acknowledgements should feel premium, not robotic.

## 8.1 Good phrase properties

Good phrases are:

* short
* calm
* confident
* context-aware
* not too apologetic
* not too enthusiastic
* not fake
* not repetitive

Good:

```text
"Good question, sir. Let me gather my thoughts."
"I have the context. I am waiting on the reasoning response."
"I am still on it, sir."
```

Bad:

```text
"Please wait while I process your request."
"Loading..."
"Your request is very important to me."
"I am doing advanced AI reasoning now."
"Wow, that is such an amazing question!"
```

## 8.2 Avoid overusing “sir”

The user likes the premium assistant feel, but “sir” should not be in every phrase.

Use variation:

```text
"Good question, sir. Let me gather my thoughts."
"Understood. I am checking that now."
"I am still on it, sir."
"Almost there."
```

## 8.3 Do not claim unavailable actions

Do not say:

```text
"I am searching the web"
```

unless web/search is actually active.

Do not say:

```text
"I found the answer"
```

unless the result is actually ready.

Do not say:

```text
"I am comparing prices"
```

unless price/product lookup is actually happening.

---

# 9. Phrase library

Use a phrase library, not LLM-generated acknowledgements for MVP.

This ensures:

* fast selection
* predictable tone
* cacheable TTS
* no DeepInfra cost
* no hallucinated progress

## 9.1 Initial acknowledgement phrases

### General reasoning

```text
"Good question, sir. Let me gather my thoughts."
"Interesting question. Let me think that through."
"Understood. I will reason through that carefully."
"Give me a moment, sir. I want to answer that properly."
```

### Deep technical / architecture

```text
"Understood. Let me reason through the architecture."
"Good point, sir. I will think through the tradeoffs."
"Let me map that out carefully."
"That needs a proper answer. Give me a moment."
```

### Research / recommendation

```text
"Of course, sir. Let me look into that properly."
"I will check the relevant details."
"Understood. I will compare the options carefully."
"Let me gather the important details first."
```

### Local system / tool

```text
"I am checking that now."
"Checking now, sir."
"One moment. I am checking the system."
"Right away."
```

### Memory search

```text
"Of course. Let me check memory."
"I will look through memory for that."
"Let me retrieve the relevant context."
```

### Memory save

Usually skip separate acknowledgement and directly return:

```text
"Saved."
"Saved to long-term memory."
"Stored."
```

### Correction / interruption regeneration later

For future interruption behavior:

```text
"You are right. Let me rearrange my thoughts."
"Correct, sir. I will adjust that."
"Understood. I will reframe the answer."
```

This should be reused later for barge-in.

---

## 9.2 Progress phrases

### Generic still working

```text
"I am still on it."
"Still working on it, sir."
"This is taking a little longer than usual."
"Almost there."
```

### DeepInfra/model pending

```text
"I have the context. I am waiting on the reasoning response."
"The model response is taking a little longer than usual."
"I am still waiting for the reasoning step to complete."
```

### Memory retrieval

```text
"I found some relevant memory. I am putting it together."
"I am narrowing down the memory context."
"Memory retrieval is taking a little longer than usual."
```

### Tool pending

```text
"The system check is still running."
"I am still waiting for the tool result."
"That command is taking a little longer than expected."
```

### Research pending

```text
"I am still checking the relevant details."
"I have part of the picture. I am narrowing it down."
"This is taking a little longer, but I am still comparing it properly."
```

---

# 10. Repetition avoidance

The system must not repeat the same phrase too often.

Track phrase usage:

```text
phrase_id
last_used_at
usage_count_recent
category
```

Rules:

```text
Do not use the same exact phrase twice within 10 minutes.
Do not use the same initial acknowledgement phrase twice in a row.
Prefer category-specific phrases.
If all phrases are recently used, use the least recently used phrase.
```

For the POC, this can be in memory only. Persistent phrase history is not required.

---

# 11. Cached audio strategy

Acknowledgement speech must be fast.

Do not use a slow uncached Chatterbox generation path for common phrases if possible.

## 11.1 Pre-cache common phrases

At startup or first use, cache audio for common phrases:

```text
"Good question, sir. Let me gather my thoughts."
"Of course, sir. Let me look into that properly."
"I am checking that now."
"Of course. Let me check memory."
"I am still on it."
"This is taking a little longer than usual."
"Saved."
```

## 11.2 If phrase is not cached

If selected acknowledgement phrase is not cached:

Option A:

```text
use a cached generic phrase instead
```

Option B:

```text
generate it, but do not block main task
```

For MVP, prefer Option A.

## 11.3 Cache key

Use the same phrase audio cache mechanism already used by Chatterbox.

Cache key should be based on:

```text
text
voice/model
speaker settings if relevant
```

---

# 12. Concurrency model

The acknowledgement system must not block the main operation.

## 12.1 Desired execution flow

```text
User speech complete
↓
STT complete
↓
Intent routing/classification
↓
AcknowledgementPolicy decides whether acknowledgement is needed
↓
Start main operation task
↓
Start acknowledgement speech task
↓
Progress monitor watches main operation
↓
If main operation finishes quickly:
    skip/cancel progress updates
↓
If main operation takes long:
    speak progress updates
↓
Main result ready
↓
speak final answer
```

Important:

```text
The main operation must not wait for acknowledgement speech to finish unless playback architecture requires strict sequencing.
```

If final answer becomes ready while acknowledgement is still playing:

```text
finish acknowledgement if it is short
then speak final answer
```

If progress update is queued but final answer becomes ready:

```text
cancel the progress update
speak final answer
```

---

# 13. Audio playback priority

Add priority levels to speech playback if not already present.

Suggested priorities:

```text
Priority 100: emergency/stop/error
Priority 80: user interruption acknowledgement
Priority 60: final answer
Priority 40: immediate acknowledgement
Priority 30: progress update
Priority 10: optional ambience/earcon
```

For MVP, if full priority queue is too much, implement simple rules:

```text
Final answer can cancel pending progress update.
Progress update cannot interrupt final answer.
Acknowledgement should not interrupt an already speaking final answer.
```

---

# 14. State machine

Add a request progress state.

```text
RequestReceived
IntentClassified
AcknowledgementEligible
AcknowledgementSpeaking
MainWorkRunning
MainWorkWaitingOnDeepInfra
MainWorkWaitingOnTool
MainWorkWaitingOnMemory
MainResponseReady
FinalAnswerSpeaking
Completed
Cancelled
Failed
```

This helps phrase selection.

Example:

```text
MainWorkWaitingOnDeepInfra
→ "I have the context. I am waiting on the reasoning response."
```

```text
MainWorkWaitingOnTool
→ "The system check is still running."
```

---

# 15. Acknowledgement policy

Create a service:

```csharp
IAcknowledgementPolicy
```

Suggested method:

```csharp
AcknowledgementDecision Decide(AcknowledgementContext context);
```

## 15.1 Context input

```csharp
public sealed record AcknowledgementContext
{
    public required string UserText { get; init; }
    public required string NormalizedText { get; init; }
    public required string IntentDomain { get; init; }
    public required string Capability { get; init; }
    public required bool IsVoiceMode { get; init; }
    public required bool WillUseDeepInfra { get; init; }
    public required bool WillUseExternalTool { get; init; }
    public required bool IsExpectedFastLocalTool { get; init; }
    public required int RecentAcknowledgementCount { get; init; }
    public required DateTimeOffset Now { get; init; }
}
```

## 15.2 Decision output

```csharp
public sealed record AcknowledgementDecision
{
    public required bool ShouldSpeakInitialAcknowledgement { get; init; }
    public string? PhraseId { get; init; }
    public string? PhraseText { get; init; }
    public required TimeSpan FirstProgressAfter { get; init; }
    public required TimeSpan SecondProgressAfter { get; init; }
    public required int MaxProgressUpdates { get; init; }
    public required string Reason { get; init; }
}
```

---

# 16. Progress monitor

Create a service:

```csharp
IRequestProgressSpeechService
```

Responsibilities:

* Schedule progress updates.
* Cancel progress updates when final answer is ready.
* Select progress phrases based on current request state.
* Avoid repetition.
* Log progress speech events.

Suggested flow:

```csharp
var progressHandle = progressSpeech.Start(requestId, acknowledgementDecision);

try
{
    var result = await mainWork;
    progressHandle.MarkMainResponseReady();
    return result;
}
finally
{
    await progressHandle.StopAsync();
}
```

---

# 17. Integration points in Merlin

Likely integration points:

```text
CommandRouter
MerlinIntentRouter
LocalAIChatService
AssistantSpeechPlaybackService
ChatterboxTtsProvider
MemoryOrchestrator
Tool execution pipeline
```

## 17.1 After intent classification

Best place to decide acknowledgement:

```text
STT complete
Command normalized
Intent classified
Route decision known
```

At this point Merlin knows:

```text
local tool?
DeepInfra?
memory?
general chat?
likely slow?
```

## 17.2 Before DeepInfra call

Start acknowledgement/progress before or at the same time as DeepInfra.

Important:

```text
Do not wait for DeepInfra to start acknowledgement.
```

## 17.3 For local tools

Only acknowledge tools that may take noticeable time.

Examples:

No acknowledgement:

```text
time/date
simple volume read
simple local state
```

Acknowledgement allowed:

```text
GPU diagnostics
file search
long-running command
log analysis
system scan
```

---

# 18. Main answer collision handling

Potential issue:

```text
Acknowledgement is still playing when final answer is ready.
```

Rule:

If acknowledgement has less than about 1 second remaining:

```text
let it finish
then final answer
```

If acknowledgement just started and final answer is ready:

```text
optionally stop acknowledgement and speak answer
```

For MVP:

```text
let short acknowledgement finish
cancel only pending progress updates
```

Do not overcomplicate early.

---

# 19. Failure handling

If main operation fails after acknowledgement:

```text
Acknowledgement: "I am checking that now."
Main operation fails.
Final response: "I could not complete that check, sir."
```

Do not leave user with only acknowledgement.

If acknowledgement TTS fails:

```text
log it
continue main operation
do not fail request
```

If progress speech fails:

```text
log it
continue main operation
```

---

# 20. Logging requirements

Add logs for:

```text
Acknowledgement decision
Acknowledgement phrase selected
Acknowledgement skipped reason
Acknowledgement playback started
Acknowledgement playback completed
Progress update scheduled
Progress update cancelled because main response ready
Progress update spoken
Main response ready
Time from user speech end to acknowledgement start
Time from acknowledgement start to main response ready
Total silence before first speech
```

Example log:

```text
Acknowledgement selected.
RequestId: ...
Category: DeepReasoning
PhraseId: deep_reasoning_01
Reason: WillUseDeepInfra
FirstProgressAfterMs: 6000
```

Example log:

```text
Progress speech spoken.
RequestId: ...
State: WaitingOnDeepInfra
ElapsedMs: 7000
PhraseId: deepinfra_wait_01
```

Example log:

```text
Progress speech cancelled.
RequestId: ...
Reason: MainResponseReady
ElapsedMs: 10420
```

---

# 21. Metrics to track

Add aggregate metrics later if useful:

```text
Average silence before first speech
Acknowledgement rate by request type
Progress update rate
Average DeepInfra wait time
Average tool wait time
Average TTS wait time
User interruptions during acknowledgement
User interruptions during progress speech
```

For POC, logging is enough.

---

# 22. Acceptance criteria

## 22.1 Slow DeepInfra request

Given:

```text
User asks a general/deep question that routes to DeepInfra.
DeepInfra takes more than 6 seconds.
```

Expected:

```text
Merlin speaks initial acknowledgement quickly.
Merlin continues DeepInfra call in parallel.
If DeepInfra is still pending after threshold, Merlin speaks a progress update.
Final answer still speaks normally.
```

## 22.2 Fast local request

Given:

```text
User asks for the time.
Tool returns immediately.
```

Expected:

```text
Merlin does not speak separate acknowledgement.
Merlin just answers.
```

## 22.3 Memory save request

Given:

```text
User says: "Please save into long-term memory that Merlin should prefer SQLite."
```

Expected:

```text
Merlin saves locally.
Merlin says "Saved."
No separate acknowledgement needed.
No DeepInfra call.
```

## 22.4 Memory search request

Given:

```text
User asks: "What do you remember about SQLite?"
```

Expected:

```text
If retrieval/DeepInfra may take noticeable time, Merlin may say:
"Of course. Let me check memory."
Then final answer arrives normally.
```

## 22.5 Long 28-second DeepInfra wait

Given:

```text
DeepInfra takes about 28 seconds.
```

Expected approximate behavior:

```text
~1s: initial acknowledgement
~7s: first useful progress update
~16s: second useful progress update
~28s+: final answer
```

No more than 2 or 3 progress updates.

## 22.6 No fake progress

Given:

```text
DeepInfra is pending and no tool/search is active.
```

Merlin must not say:

```text
"I found the answer"
"I found sources"
"I checked the web"
```

unless true.

---

# 23. Implementation phases

## Phase 1: Acknowledgement phrase library and policy

Implement:

```text
AcknowledgementPhrase
AcknowledgementCategory
AcknowledgementPolicy
Phrase repetition avoidance
```

No playback integration yet.

Tests:

```text
DeepInfra request → acknowledgement selected
fast local request → acknowledgement skipped
memory save → skipped or direct Saved
memory search → memory acknowledgement selected
```

---

## Phase 2: Playback integration for initial acknowledgement

Implement:

```text
Send selected acknowledgement to speech playback
Use cached phrase audio where possible
Do not block main request
```

Tests:

```text
Acknowledgement starts before DeepInfra completes
Main request still completes normally
Final answer still plays
```

---

## Phase 3: Progress speech monitor

Implement:

```text
Progress scheduling
Progress cancellation
Progress phrase selection
Max progress update count
```

Tests:

```text
DeepInfra 8s delay → one progress update
DeepInfra 2s delay → no progress update
DeepInfra 28s delay → multiple bounded progress updates
Final answer ready cancels pending progress
```

---

## Phase 4: Request state integration

Implement state-aware progress:

```text
WaitingOnDeepInfra
WaitingOnTool
WaitingOnMemory
PreparingTts
```

Progress phrase should match actual state.

Tests:

```text
DeepInfra wait uses DeepInfra-safe phrase
Tool wait uses tool phrase
Memory wait uses memory phrase
```

---

## Phase 5: Logging and diagnostics

Implement logs and diagnostics.

Tests or manual verification:

```text
Logs show acknowledgement selected/skipped
Logs show playback start
Logs show progress update spoken/cancelled
Logs show main response ready timing
```

---

## Phase 6: Tuning and polish

Implement:

```text
phrase cooldowns
category variation
shorter progress text
configuration values
voice-mode-only gating
```

Do not overdo this before MVP is proven.

---

# 24. Configuration

Add config section:

```json
{
  "AcknowledgementSpeech": {
    "Enabled": true,
    "VoiceModeOnly": true,
    "InitialAcknowledgementDelayMs": 0,
    "MinimumExpectedLatencyMs": 1500,
    "FirstProgressAfterMs": 6000,
    "SecondProgressAfterMs": 14000,
    "LongWaitProgressAfterMs": 25000,
    "MaxProgressUpdates": 3,
    "PhraseCooldownSeconds": 600,
    "UseCachedAudioOnlyForAcknowledgements": true
  }
}
```

Meaning:

```text
Enabled:
Turn system on/off.

VoiceModeOnly:
Only use in voice mode first.

MinimumExpectedLatencyMs:
Do not acknowledge if request is expected to be faster than this.

FirstProgressAfterMs:
When to speak first progress update.

SecondProgressAfterMs:
When to speak second progress update.

LongWaitProgressAfterMs:
When to speak long-wait update.

MaxProgressUpdates:
Hard cap per request.

PhraseCooldownSeconds:
Avoid repeating phrases too often.

UseCachedAudioOnlyForAcknowledgements:
Avoid slow Chatterbox generation for filler speech.
```

---

# 25. Special care for Chatterbox

Chatterbox can be slow if phrase audio is not cached.

Therefore:

```text
Acknowledgement phrases should be short.
Common acknowledgement phrases should be cached.
Progress phrases should be cached.
If no cached phrase is available, use a cached generic phrase.
Do not generate long dynamic filler text.
```

Do not create 20-second acknowledgement audio.

Acknowledgement target duration:

```text
1.0-3.0 seconds
```

Progress update target duration:

```text
1.0-3.5 seconds
```

---

# 26. Premium feel rules

Merlin should sound calm and capable.

Do:

```text
"Good question, sir. Let me gather my thoughts."
"I am still on it."
"I have the context. I am waiting on the reasoning response."
```

Do not:

```text
"Processing request."
"Please wait."
"Still loading."
"Sorry sorry, this is taking long."
"Uh, I am not sure, let me think."
```

Avoid sounding like a web app loading spinner.

---

# 27. Relationship to future interruption behavior

This system should be designed so interruption can later cancel:

```text
acknowledgement speech
progress speech
main response
pending DeepInfra request
pending TTS chunks
```

For now, at least ensure each acknowledgement/progress speech is associated with:

```text
RequestId
TurnId if available
CorrelationId
SpeechType = Acknowledgement | Progress | FinalAnswer
```

This will make future barge-in easier.

---

# 28. Final expected behavior

Example 1: Deep question

```text
User: Why do people fear change?
Merlin: "That is a good question, sir. Let me gather my thoughts."
[DeepInfra pending]
Merlin: "I am still reasoning through it."
[Answer ready]
Merlin: "People often fear change because..."
```

Example 2: Slow memory-assisted question

```text
User: What did we decide about SQLite for Merlin memory?
Merlin: "Of course. Let me check memory."
[retrieval + DeepInfra]
Merlin: "We decided SQLite should be the local memory backend..."
```

Example 3: Fast tool

```text
User: What time is it?
Merlin: "It is 21:42, sir."
```

Example 4: Explicit memory save

```text
User: Please save into long-term memory that Merlin should prefer SQLite.
Merlin: "Saved."
```

Example 5: Very slow DeepInfra

```text
User: Explain the tradeoffs of local-first memory architecture.
Merlin: "Good question, sir. Let me gather my thoughts."
[6 seconds]
Merlin: "I have the context. I am waiting on the reasoning response."
[14 seconds]
Merlin: "Still working on it, sir. This one is taking a little longer than usual."
[final]
Merlin: "The main tradeoff is..."
```

---

# 29. Agent implementation instruction

Implement this incrementally.

Do not attempt to solve response tuning, interruption, or full duplex voice in this task.

Build the acknowledgement/progress speech system as a small subsystem:

```text
AcknowledgementPolicy
AcknowledgementPhraseLibrary
AcknowledgementSpeechService
RequestProgressSpeechService
AcknowledgementSpeechOptions
```

Integrate it after intent classification and before/alongside main command execution.

The first successful milestone is:

```text
A DeepInfra-bound request causes Merlin to quickly speak a cached acknowledgement while the DeepInfra request continues.
If DeepInfra takes longer than the progress threshold, Merlin speaks one useful progress update.
When the final answer is ready, Merlin speaks it normally.
Fast local requests do not get unnecessary acknowledgements.
```

Do not break the existing voice pipeline.

Do not introduce fake progress.

Do not add unnecessary packages.

Do not change database location.

Do not change memory persistence.

Do not implement interruption behavior yet.
