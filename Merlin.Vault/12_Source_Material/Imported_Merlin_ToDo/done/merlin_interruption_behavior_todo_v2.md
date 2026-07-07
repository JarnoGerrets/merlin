---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin_interruption_behavior_todo_v2.md
classification: implementation-plan
related_features:
  - Voice Interruption System
status: implemented
imported_to_vault: true
---

# Merlin Interruption / Barge-In Behaviour System — DeepInfra-Aware Rewrite

## Purpose of this document

This document describes the full implementation concept for Merlin's interruption and barge-in behaviour. It is intended to be read by an implementation agent working inside the Merlin codebase.

This version explicitly accounts for the fact that Merlin's deeper reasoning model is offsite through DeepInfra. Merlin does not locally hold a full reasoning model that can instantly continue an answer after a correction. Therefore, when the user interrupts a generated answer with a correction, Merlin must usually cancel the current offsite generation and create a new compact correction prompt for DeepInfra.

Because this creates a short delay before the regenerated answer begins, Merlin must provide an immediate local acknowledgement such as:

```text
"You are right, let me rearrange my thoughts."
```

or:

```text
"SQLite, yes — let me adjust that."
```

This local acknowledgement is not the deep answer. It is a conversation-control bridge that tells the user Merlin heard them, accepted the correction, and is regenerating the answer with the new direction.

The goal is to make Merlin feel like a real conversational assistant instead of a rigid request-response machine. In a natural spoken conversation, the user must be able to interrupt the assistant while it is thinking, generating text, generating speech, speaking, or preparing a tool action.

This must not be implemented as a small playback-only patch. Stopping audio is only the visible part. The real system needs cancellation, turn ownership, interruption classification, offsite regeneration, local acknowledgement phrases, tool safety, memory integration, and state management.

The desired result is:

```text
User speaks
↓
Merlin starts answering
↓
User talks over Merlin because they changed their mind, noticed a mistake, or want clarification
↓
Merlin immediately stops or pauses the current answer
↓
Merlin gives a short local acknowledgement when regeneration is needed
↓
Merlin cancels/abandons the old DeepInfra request
↓
Merlin compiles a compact corrected prompt
↓
Merlin sends the corrected prompt to DeepInfra
↓
Merlin speaks the regenerated answer
↓
Old output never leaks back in
↓
The conversation continues naturally
```

The core design rule:

```text
Every assistant turn must be interruptible, cancellable, attributable to a TurnId, and safe to abandon.
```

The second core design rule:

```text
When a user correction requires DeepInfra regeneration, Merlin must acknowledge the correction locally before the new offsite response is ready.
```

The third core design rule:

```text
The acknowledgement must be meaningful and state-specific, not random filler.
```

---

# 1. Problem statement

Currently Merlin behaves too much like this:

```text
User asks something
↓
Merlin processes the request
↓
Merlin generates the full or partial response
↓
Merlin speaks until finished
↓
Only after that can the user effectively speak again
```

This creates several problems:

1. The user cannot correct Merlin when it starts going in the wrong direction.
2. The user cannot change their mind naturally.
3. The user cannot ask a mid-answer clarification question.
4. The user cannot stop long answers quickly.
5. The user cannot safely cancel pending actions.
6. Merlin feels non-conversational and stubborn.
7. Long TTS responses become frustrating because the user is forced to wait.
8. The memory system may accidentally treat partial or wrong assistant output as useful context.
9. If cancellation is added only to playback, old DeepInfra or TTS chunks may still arrive later and resume the wrong answer.
10. Because DeepInfra is offsite, Merlin cannot instantly generate a corrected continuation locally.
11. If Merlin stops speaking and waits silently for DeepInfra, the user may think Merlin froze or failed.
12. If Merlin says generic filler every time, it will feel fake and annoying.

The interruption system must solve all of these, not just silence the speaker.

---

# 2. Important architectural reality: DeepInfra is stateless and offsite

Merlin's local application controls the conversation flow, but DeepInfra performs the deeper reasoning.

DeepInfra does not automatically know:

```text
- what Merlin already said
- what the user interrupted
- which part was wrong
- whether the old answer should be discarded
- what correction should replace it
- what memories should remain relevant
```

DeepInfra only knows what Merlin sends in the new request.

Therefore, after a correction interruption, Merlin must usually build a new prompt.

Example:

```text
Merlin: "To build the memory system, I would start with PostgreSQL..."
User: "No, SQLite."
```

Merlin must not simply continue the old answer by locally replacing the word PostgreSQL with SQLite. The correct architecture may change substantially:

```text
PostgreSQL implies:
- external database service
- possibly pgvector
- server-style deployment
- more operational complexity
- more multi-user assumptions

SQLite implies:
- local-first file database
- simple deployment
- FTS5
- low overhead
- excellent MVP fit
- possible vector extension later
```

So the answer must be regenerated or redirected by DeepInfra.

The correct flow is:

```text
Old Turn 123 starts
↓
DeepInfra generates PostgreSQL direction
↓
Merlin begins speaking it
↓
User interrupts: "No, SQLite."
↓
Merlin stops playback immediately
↓
Merlin locally says: "SQLite, yes — let me adjust that."
↓
Turn 123 is cancelled
↓
Correction Turn 124 is created
↓
Merlin builds a compact correction prompt
↓
DeepInfra generates the SQLite-based answer
↓
Merlin speaks Turn 124
↓
Any late output from Turn 123 is discarded forever
```

This is the foundational behaviour this document describes.

---

# 3. Desired user experience

Merlin should support natural interruptions like these.

## 3.1 Hard stop

```text
Merlin: "To build this system, I would start by..."
User: "Stop."
Merlin: [stops immediately]
```

No long apology. No full sentence acknowledgement required. A tiny UI state change or short audio cue is enough.

Expected local behaviour:

```text
- stop audio immediately
- clear queued TTS
- cancel/abandon active DeepInfra stream
- mark turn as cancelled_by_user
- do not regenerate
- do not ask a question unless needed
```

## 3.2 Correction requiring DeepInfra regeneration

```text
Merlin: "To build the memory system, I would start with PostgreSQL..."
User: "No, SQLite."
Merlin: [stops immediately]
Merlin local acknowledgement: "SQLite, yes — let me rearrange that."
Merlin regenerated answer: "For Merlin, SQLite is the better starting point because the memory is local-first..."
```

Important:

```text
The acknowledgement is local.
The corrected answer is generated by DeepInfra.
```

The acknowledgement exists because there may be a 1-2 second delay before the new DeepInfra response can be spoken.

## 3.3 Correction of scope

```text
Merlin: "For the frontend I would..."
User: "No, I mean the backend service."
Merlin: [stops current answer]
Merlin local acknowledgement: "Backend service, got it — I’ll reframe it."
Merlin regenerated answer: "For the backend service, I’d separate the orchestration layer from..."
```

The new user utterance should replace or modify the previous request.

## 3.4 Change of mind / topic switch

```text
Merlin: "The best approach is probably..."
User: "Actually forget that, let's talk about Whisper VRAM usage."
Merlin: [cancels current response]
Merlin local acknowledgement: "Okay, switching topics."
Merlin regenerated answer: "For Whisper VRAM usage, the key factors are model size, precision, and beam size..."
```

This should close, suspend, or mark the previous topic as interrupted.

## 3.5 Pause

```text
Merlin: "The first component is the InterruptionController..."
User: "Wait."
Merlin: [pauses]
User: "Continue."
Merlin: [continues]
```

Pause is not the same as cancel.

A pause should not necessarily trigger DeepInfra regeneration. If the user only says "wait" and later says "continue", Merlin can resume the existing audio/text if still valid.

## 3.6 Pause followed by correction

```text
Merlin: "The first component is the InterruptionController..."
User: "Wait."
Merlin: "Paused."
User: "Actually, before that, explain the DeepInfra regeneration part."
Merlin: "Right — I’ll focus on the regeneration flow first."
Merlin regenerated answer: "Because DeepInfra is offsite, an interruption creates a new turn..."
```

Pause should preserve state temporarily. A later correction may then cancel or replace the paused answer.

## 3.7 Clarification during an answer

```text
Merlin: "Each assistant response needs a TurnId..."
User: "Wait, what is a TurnId?"
Merlin: [pauses main answer]
Merlin local acknowledgement: "Good question — briefly:"
Merlin generated/local answer: "A TurnId is the unique ID for the current assistant response, so old chunks can be ignored after cancellation."
Merlin: "Continuing from there..."
```

Clarification is not the same as correction. Merlin should not always throw away the original answer.

There are two possible implementations:

1. Small clarification can be answered locally if it is a known concept in the app.
2. Larger clarification can create a child DeepInfra turn and then optionally resume the parent turn.

## 3.8 Backchannel / non-interruption

```text
Merlin: "The first part is the TurnManager..."
User: "yeah"
Merlin: [continues]
```

Short confirmations like "yeah", "mhm", "okay", or breathing/noise should not cancel the answer unless paired with interrupt intent.

---

# 4. The local acknowledgement / bridge phrase system

Because DeepInfra may take a second or two to produce the corrected answer, Merlin needs a local acknowledgement layer.

This is not optional. Without it, the interaction feels broken:

```text
User interrupts
↓
Merlin stops
↓
Silence for 1-2 seconds
↓
User wonders whether Merlin crashed or ignored them
```

The acknowledgement layer solves that:

```text
User interrupts
↓
Merlin stops
↓
Merlin immediately acknowledges the correction
↓
DeepInfra regenerates in the background
↓
Merlin continues with the corrected answer
```

## 4.1 Requirements for acknowledgement phrases

Acknowledgement phrases must be:

```text
- short
- local / prewritten / no DeepInfra dependency
- context-sensitive
- not overly apologetic
- not random filler
- not repeated with the exact same wording every time
- not used for hard stop when silence is better
- safe to say before the corrected answer is ready
```

They should communicate one of these meanings:

```text
- I heard the correction.
- You are right; I was going the wrong way.
- I am changing direction.
- I am rebuilding the answer around your correction.
- I am pausing rather than continuing incorrectly.
```

## 4.2 Phrase categories

### 4.2.1 Correction acknowledgement

Use when the user corrects an assumption, technology choice, entity, scope, or direction.

Examples:

```text
"You are right — let me rearrange my thoughts."
"Right, SQLite — let me adjust that."
"Correct, I was going the wrong direction. Let me reframe it."
"Got it — I’ll rebuild the answer around that."
"Yes, that changes the direction. Let me correct it."
"Good catch — I’ll switch to that."
"Understood. I’ll use SQLite instead."
```

Use variable insertion when safe:

```text
"Right, {correction} — let me adjust that."
"Got it — focusing on {scope} instead."
"Understood. I’ll use {replacement} instead."
```

Examples:

```text
User: "No, SQLite."
Merlin: "Right, SQLite — let me adjust that."

User: "No, the backend."
Merlin: "Got it — focusing on the backend instead."
```

### 4.2.2 Topic switch acknowledgement

Use when the user abandons the current topic and switches to another.

Examples:

```text
"Okay, switching topics."
"Sure — parking that for now."
"Got it, new direction."
"Alright, let’s move to that."
"Understood, I’ll drop the previous thread."
```

### 4.2.3 Pause acknowledgement

Use when user says wait/pause/hold on.

Examples:

```text
"Paused."
"Sure, I’ll pause."
"Holding there."
```

Keep these extremely short.

### 4.2.4 Clarification acknowledgement

Use when the user asks a mid-answer question about something Merlin just said.

Examples:

```text
"Good question — briefly:"
"Sure — that means this:"
"Right, let me unpack that first."
"Quick clarification:"
```

### 4.2.5 Tool cancellation acknowledgement

Use when the user interrupts a pending tool action.

Examples:

```text
"Stopped before running it."
"Cancelled before committing the action."
"I paused the action before making changes."
"That action had already completed, so I can’t cancel it now."
```

### 4.2.6 Hard stop acknowledgement

Usually say nothing. Silence is the acknowledgement.

Optional minimal acknowledgement only if UX requires it:

```text
"Stopped."
```

Do not say:

```text
"You are right, let me rearrange my thoughts."
```

for a hard stop. The user asked Merlin to stop, not regenerate.

## 4.3 Acknowledgement selection algorithm

The InterruptionController should select a phrase based on the classified interruption type.

Pseudo-flow:

```text
Interruption detected
↓
Classify interruption
↓
If hard_stop:
    stop audio
    say nothing or "Stopped."
    do not regenerate

If pause:
    pause audio
    say "Paused." or show UI state
    do not regenerate yet

If correction:
    stop audio
    cancel old turn
    select correction acknowledgement
    speak acknowledgement locally
    build correction prompt
    send DeepInfra request

If topic_change:
    stop audio
    cancel or suspend old turn
    select topic switch acknowledgement
    speak acknowledgement locally
    build new-topic prompt
    send DeepInfra request

If clarification_question:
    pause parent turn
    select clarification acknowledgement
    answer clarification locally or via DeepInfra child turn
    resume or abandon parent based on user intent

If backchannel/noise:
    continue current playback
```

## 4.4 Phrase repetition prevention

Do not use the same phrase repeatedly.

Maintain a short rolling history:

```text
LastAcknowledgementPhrases[10]
```

When selecting a phrase:

```text
- avoid exact repeats from the last 5 acknowledgements
- prefer shorter phrases during rapid interruptions
- prefer more explicit phrases after wrong-direction corrections
```

Example:

```text
First correction:
"Right, SQLite — let me adjust that."

Second correction soon after:
"Got it — I’ll reframe it."

Third correction:
"Good catch, changing direction."
```

## 4.5 Acknowledgement audio strategy

The acknowledgement must be fast.

Preferred options:

1. Pre-generate common acknowledgement phrases with the same Merlin voice.
2. Cache generated acknowledgement audio after first use.
3. For dynamic phrases, use short TTS chunks and keep them under one sentence.
4. If TTS latency is too high, use a very short nonverbal sound or UI state while generating dynamic acknowledgement.

Recommended MVP:

```text
- keep a small set of prewritten static phrases
- pre-generate or cache them
- do not use DeepInfra to generate acknowledgement wording
```

Dynamic phrase support can be added later.

## 4.6 Acknowledgement should not contaminate memory

The local acknowledgement is conversation control, not a project conclusion.

Do not save phrases like this to long-term memory:

```text
"You are right — let me rearrange my thoughts."
```

Instead, memory should store the actual correction event:

```json
{
  "event_type": "user_correction",
  "old_direction": "PostgreSQL",
  "new_direction": "SQLite",
  "effect": "old assistant turn cancelled and regenerated",
  "importance": 0.7
}
```

---

# 5. System components

Implement or adapt these components.

```text
InterruptionController
TurnManager
AssistantTurnRunner
InterruptionClassifier
AcknowledgementService
CorrectionPromptBuilder
DeepInfraRequestController
TtsPlaybackController
TtsQueueManager
ToolExecutionController
MemoryInterruptionRecorder
ConversationStateMachine
```

## 5.1 InterruptionController

The central orchestrator for interruption events.

Responsibilities:

```text
- receive potential interruption signals
- determine whether the current assistant turn should stop, pause, continue, or regenerate
- ask InterruptionClassifier for type/confidence
- stop playback immediately for likely high-confidence interruption
- invoke AcknowledgementService when needed
- cancel old TurnId
- create new TurnId for regenerated answer
- call CorrectionPromptBuilder for DeepInfra correction prompts
- coordinate with TurnManager and DeepInfraRequestController
```

The InterruptionController should not contain all implementation details. It orchestrates specialized services.

## 5.2 TurnManager

Owns assistant turns.

Responsibilities:

```text
- create AssistantTurn records
- assign TurnIds
- track current active turn
- track paused parent turns
- cancel turns
- mark turns as interrupted, completed, failed, paused, resumed, abandoned
- reject late output from inactive turns
```

## 5.3 AssistantTurnRunner

Runs one assistant turn from prompt to speech.

Responsibilities:

```text
- request DeepInfra response
- stream generated text when available
- chunk response text for TTS
- enqueue TTS chunks
- track generated/spoken text so far
- respect cancellation tokens
- emit events with TurnId
```

## 5.4 InterruptionClassifier

Classifies the user's utterance during an assistant turn.

Inputs:

```text
- partial or final transcript
- current assistant state
- current spoken text
- current generated text
- original user request
- active topic
- VAD confidence
- ASR confidence
- duration of user speech
- whether wake word was used
```

Outputs:

```json
{
  "type": "correction",
  "confidence": 0.92,
  "requires_deepinfra_regeneration": true,
  "local_acknowledgement_kind": "correction",
  "replacement_hint": "SQLite",
  "target_hint": "database choice",
  "should_cancel_current_turn": true,
  "should_pause_current_turn": false
}
```

Supported labels:

```text
hard_stop
pause
resume
correction
topic_change
clarification_question
backchannel
noise
unknown
```

## 5.5 AcknowledgementService

Provides immediate local bridge phrases.

Responsibilities:

```text
- select a short phrase based on interruption type
- avoid repetitive phrasing
- optionally insert safe dynamic terms like "SQLite"
- return text and/or cached audio
- never call DeepInfra for acknowledgement phrase generation
- emit phrase with TurnId and acknowledgement type
```

Example method shape:

```csharp
public interface IAcknowledgementService
{
    AcknowledgementResult CreateAcknowledgement(InterruptionClassification classification, AssistantTurn interruptedTurn);
}
```

Example result:

```csharp
public sealed class AcknowledgementResult
{
    public string Text { get; init; }
    public string Kind { get; init; }
    public bool IsLocalOnly { get; init; } = true;
    public string? CachedAudioPath { get; init; }
}
```

## 5.6 CorrectionPromptBuilder

Builds compact prompts for DeepInfra after interruption.

Responsibilities:

```text
- include original user request
- include short summary of assistant answer so far
- include exact partial spoken text if useful
- include exact user interruption
- include local interpretation of the correction
- include relevant memories from MemoryCompiler
- instruct DeepInfra to discard or revise the old direction
- keep token count small
```

This component is essential because DeepInfra is offsite and stateless.

## 5.7 DeepInfraRequestController

Owns active DeepInfra requests.

Responsibilities:

```text
- start requests with cancellation tokens
- stream responses when available
- cancel or abandon active streams
- attach every token/chunk to TurnId
- ignore late data from cancelled turns
- report latency and first-token timing
```

## 5.8 TtsPlaybackController

Owns audio playback.

Responsibilities:

```text
- play TTS chunks by TurnId
- stop current playback immediately on interruption
- support pause/resume if possible
- duck volume during potential interruption
- clear queued chunks for cancelled turns
- play local acknowledgement audio quickly
```

## 5.9 ToolExecutionController

Owns local and external tool actions.

Responsibilities:

```text
- classify actions by cancellability and commit boundary
- cancel safe running actions when interrupted
- prevent irreversible actions from committing without confirmation
- report whether cancellation happened before or after commit
```

## 5.10 MemoryInterruptionRecorder

Records interruption events without polluting long-term memory.

Responsibilities:

```text
- mark interrupted assistant output as unconfirmed
- save user corrections as high-priority context when useful
- prevent interrupted assistant claims from becoming project decisions
- update topic state as completed, interrupted, paused, abandoned, or corrected
```

---

# 6. AssistantTurn data model

Every assistant response must be represented as a turn object.

Suggested shape:

```csharp
public sealed class AssistantTurn
{
    public Guid TurnId { get; init; }
    public Guid? ParentTurnId { get; init; }
    public Guid ConversationId { get; init; }

    public AssistantTurnState State { get; set; }
    public AssistantTurnKind Kind { get; set; }

    public string OriginalUserMessage { get; set; } = string.Empty;
    public string CompiledPromptSentToDeepInfra { get; set; } = string.Empty;

    public string GeneratedTextSoFar { get; set; } = string.Empty;
    public string SpokenTextSoFar { get; set; } = string.Empty;
    public string UnspokenGeneratedText { get; set; } = string.Empty;

    public string? InterruptionTranscript { get; set; }
    public string? InterruptionReason { get; set; }
    public string? LocalAcknowledgementText { get; set; }

    public bool WasInterrupted { get; set; }
    public bool WasCancelled { get; set; }
    public bool RequiresRegeneration { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? FirstTokenAt { get; set; }
    public DateTimeOffset? FirstAudioAt { get; set; }
    public DateTimeOffset? InterruptedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public CancellationTokenSource Cancellation { get; init; } = new();
}
```

Suggested states:

```csharp
public enum AssistantTurnState
{
    Created,
    CompilingPrompt,
    WaitingForDeepInfra,
    StreamingText,
    GeneratingTts,
    Speaking,
    PausedByUser,
    Interrupted,
    Cancelled,
    RegeneratingReplacement,
    Completed,
    Failed
}
```

Suggested kinds:

```csharp
public enum AssistantTurnKind
{
    NormalAnswer,
    CorrectionRegeneration,
    ClarificationChildTurn,
    ResumeParentTurn,
    LocalAcknowledgementOnly,
    ToolExecutionAnswer
}
```

Critical rule:

```text
Every token, TTS chunk, audio buffer, tool result, and memory update must include TurnId.
```

If the TurnId is no longer active, discard the output.

---

# 7. Conversation state machine

Merlin should maintain a clear state machine.

Suggested states:

```text
Idle
Listening
Transcribing
ClassifyingIntent
CompilingPrompt
WaitingForDeepInfra
StreamingResponse
GeneratingTts
Speaking
PotentialInterruption
AcknowledgingInterruption
RegeneratingAfterCorrection
PausedByUser
ExecutingTool
AwaitingConfirmation
Interrupted
Cancelled
```

Example correction transition:

```text
Speaking
↓ user speech detected
PotentialInterruption
↓ classifier says correction
AcknowledgingInterruption
↓ old turn cancelled, local acknowledgement spoken
RegeneratingAfterCorrection
↓ new compact prompt sent to DeepInfra
StreamingResponse
↓ TTS chunks produced
Speaking
```

Example hard stop transition:

```text
Speaking
↓ user says "stop"
PotentialInterruption
↓ classifier says hard_stop
Cancelled
↓ audio stopped, queues cleared, no regeneration
Idle / Listening
```

Example pause transition:

```text
Speaking
↓ user says "wait"
PausedByUser
↓ user says "continue"
Speaking
```

Example clarification transition:

```text
Speaking
↓ user asks "what is a TurnId?"
PausedByUser
↓ child clarification turn created
WaitingForDeepInfra or LocalClarification
↓ clarification spoken
ResumeParentTurn or Listening
```

---

# 8. Interruption classification rules

Start rule-based. Do not require a local LLM for MVP.

## 8.1 Hard stop phrases

Examples:

```text
stop
cancel
shut up
be quiet
never mind
abort
halt
forget it
```

Classification:

```json
{
  "type": "hard_stop",
  "requires_deepinfra_regeneration": false,
  "should_cancel_current_turn": true,
  "acknowledgement_kind": "none_or_minimal"
}
```

## 8.2 Pause phrases

Examples:

```text
wait
pause
hold on
one sec
hang on
```

Classification:

```json
{
  "type": "pause",
  "requires_deepinfra_regeneration": false,
  "should_pause_current_turn": true,
  "acknowledgement_kind": "pause"
}
```

## 8.3 Resume phrases

Examples:

```text
continue
go on
resume
keep going
carry on
```

Classification:

```json
{
  "type": "resume",
  "requires_deepinfra_regeneration": false,
  "should_resume_paused_turn": true
}
```

## 8.4 Correction phrases

Examples:

```text
no, SQLite
no, not PostgreSQL
actually use SQLite
I meant the backend
not the frontend
you misunderstood
that's not what I mean
wrong direction
no, I said medium memory, not long-term memory
```

Heuristics:

```text
- starts with "no"
- starts with "actually"
- contains "not X, Y"
- contains "I mean"
- contains "I meant"
- contains "use X instead"
- contains "wrong direction"
- contains direct replacement phrase
```

Classification:

```json
{
  "type": "correction",
  "requires_deepinfra_regeneration": true,
  "should_cancel_current_turn": true,
  "acknowledgement_kind": "correction",
  "replacement_hint": "SQLite",
  "target_hint": "database choice"
}
```

## 8.5 Topic change phrases

Examples:

```text
forget that, let's talk about Whisper
actually never mind, different question
park this for later
switch topics
let's move to GPU usage
```

Classification:

```json
{
  "type": "topic_change",
  "requires_deepinfra_regeneration": true,
  "should_cancel_current_turn": true,
  "acknowledgement_kind": "topic_switch"
}
```

## 8.6 Clarification question phrases

Examples:

```text
what does that mean?
what is a TurnId?
why SQLite?
how does that work?
wait, explain that part
```

Classification:

```json
{
  "type": "clarification_question",
  "requires_deepinfra_regeneration": true,
  "should_pause_current_turn": true,
  "acknowledgement_kind": "clarification"
}
```

Note: Some clarification questions may be answered locally if they map to known concepts. Otherwise use DeepInfra.

## 8.7 Backchannel phrases

Examples:

```text
yeah
mhm
okay
right
sure
```

Classification:

```json
{
  "type": "backchannel",
  "requires_deepinfra_regeneration": false,
  "should_continue_current_turn": true
}
```

Be careful. "Okay stop" is not a backchannel. It is a hard stop.

## 8.8 Unknown interruption

If the user speaks but classification is uncertain:

Preferred behaviour:

```text
- duck Merlin's volume briefly
- wait for more transcript
- if still unclear, pause instead of cancelling when speech seems intentional
- ask a very short local prompt only if needed
```

Example:

```text
Merlin: [speaking]
User: [unclear speech]
Merlin: [ducks volume]
User: "no, SQLite"
Merlin: "Right, SQLite — let me adjust that."
```

---

# 9. DeepInfra correction prompt generation

When a correction requires regeneration, Merlin must build a compact prompt.

Do not resend the full conversation.

## 9.1 Prompt should include

```text
1. Original user request
2. Relevant compact memory context
3. Assistant partial answer before interruption
4. User interruption transcript
5. Local interpretation of interruption
6. Explicit instruction to discard/modify old direction
7. Response style instruction
```

## 9.2 Example correction prompt

```text
SYSTEM:
You are Merlin's offsite reasoning model. The local Merlin runtime is managing a voice conversation. The user interrupted the previous answer with a correction. Continue from the corrected direction only.

RELEVANT CONTEXT:
The user is designing Merlin, a local-first voice assistant. Merlin should use local memory and send small prompts to DeepInfra.

ORIGINAL USER REQUEST:
"How would we build the memory system?"

ASSISTANT PARTIAL ANSWER BEFORE INTERRUPTION:
"To build the memory system, I would start with PostgreSQL..."

USER INTERRUPTION:
"No, SQLite."

LOCAL INTERPRETATION:
The user rejected PostgreSQL and wants the design based on SQLite.

TASK:
Regenerate the answer around SQLite. Do not continue the PostgreSQL direction. Do not over-apologize. Briefly acknowledge the correction and continue practically. Avoid repeating the local acknowledgement phrase if one was already spoken.
```

## 9.3 Important prompt instruction about local acknowledgement

If Merlin already spoke a local acknowledgement like:

```text
"Right, SQLite — let me adjust that."
```

then the DeepInfra prompt should say:

```text
The local runtime has already acknowledged the correction. Do not repeat a long apology. Start directly with the corrected explanation.
```

This prevents awkward duplication:

```text
Merlin local: "Right, SQLite — let me adjust that."
Merlin generated: "You are right, SQLite is better. Let me adjust..."
```

Bad. Too repetitive.

Better:

```text
Merlin local: "Right, SQLite — let me adjust that."
Merlin generated: "For Merlin, SQLite is the better starting point because..."
```

## 9.4 Example prompt for scope correction

```text
SYSTEM:
You are Merlin's offsite reasoning model. The user interrupted the previous answer with a scope correction. Continue with the corrected scope.

ORIGINAL USER REQUEST:
"How should we implement interruption behaviour?"

ASSISTANT PARTIAL ANSWER BEFORE INTERRUPTION:
"For the frontend, I would add a visible interrupt button..."

USER INTERRUPTION:
"No, I mean the backend service."

LOCAL INTERPRETATION:
The user wants backend architecture, not frontend UI.

LOCAL ACKNOWLEDGEMENT ALREADY SPOKEN:
"Backend service, got it — I’ll reframe it."

TASK:
Continue with backend service design only. Do not restart the whole topic unless needed. Do not repeat the acknowledgement. Be practical and implementation-focused.
```

## 9.5 Example prompt for clarification

```text
SYSTEM:
You are Merlin's offsite reasoning model. The user interrupted to ask a clarification question about the current answer. Answer the clarification briefly and clearly.

PARENT TOPIC:
Merlin interruption architecture.

ASSISTANT PARTIAL ANSWER BEFORE INTERRUPTION:
"Every generated token, TTS chunk, and audio buffer should be tied to a TurnId..."

USER CLARIFICATION:
"Wait, what is a TurnId?"

TASK:
Explain TurnId in simple practical terms for Merlin. Keep it brief. After explaining, include one sentence that makes it easy to resume the parent topic.
```

## 9.6 Token budget for correction prompts

Target correction prompt size:

```text
Small correction:       200-500 input tokens
Medium correction:      500-900 input tokens
Complex correction:     900-1500 input tokens
```

Do not include:

```text
- full old conversation
- full old answer
- long memory dumps
- raw logs unless correction is about logs
- every previous interruption
```

Include only what DeepInfra needs to regenerate correctly.

---

# 10. Local acknowledgement timing

The acknowledgement should happen immediately after Merlin has enough confidence that the user is interrupting.

## 10.1 Target timing

```text
User starts interruption speech
↓
VAD detects speech: 50-150 ms
↓
Merlin ducks or stops audio: 100-250 ms
↓
ASR partial transcript gives intent: 300-800 ms
↓
Merlin local acknowledgement starts: ideally < 1 second after interruption begins
↓
DeepInfra request starts in parallel
↓
Regenerated answer starts when ready
```

## 10.2 Do not wait for DeepInfra before acknowledging

Bad:

```text
User: "No, SQLite."
Merlin: [silence while DeepInfra regenerates]
Merlin: "SQLite is better because..."
```

Good:

```text
User: "No, SQLite."
Merlin: "Right, SQLite — let me adjust that."
Merlin: [short natural pause]
Merlin: "For Merlin, SQLite is the better starting point because..."
```

## 10.3 Parallelization

After classification:

```text
1. Stop old audio immediately.
2. Cancel old turn.
3. Start building correction prompt.
4. Speak local acknowledgement.
5. Start DeepInfra request as soon as prompt is ready.
6. Prepare TTS pipeline for regenerated response.
```

Do not unnecessarily serialize everything.

Potential parallel flow:

```text
Interruption classified as correction
├── Audio: stop old playback
├── TTS: play cached acknowledgement
├── Prompt: build correction prompt
├── Network: start DeepInfra request
└── Memory: mark old turn interrupted
```

---

# 11. Audio and TTS handling

## 11.1 Stop old audio immediately

When interruption is confirmed or highly likely:

```text
- stop current audio buffer
- clear queued audio chunks for old TurnId
- prevent old chunks from resuming
```

This should happen before the acknowledgement.

## 11.2 Acknowledgement audio priority

Local acknowledgement audio should have higher priority than generated answer audio.

Audio queue priority order:

```text
1. Emergency stop / silence
2. Local acknowledgement phrase
3. Tool safety warning
4. Regenerated DeepInfra answer
5. Normal assistant answer
```

## 11.3 Cached acknowledgement audio

For fastest UX, cache common phrases.

Suggested static phrase cache:

```text
Paused.
Stopped.
Okay, switching topics.
Right — let me adjust that.
Got it — I’ll reframe it.
Good question — briefly.
You are right — let me rearrange my thoughts.
Stopped before running it.
Cancelled before committing the action.
```

Dynamic phrases like:

```text
"Right, SQLite — let me adjust that."
```

can be generated on demand or approximated with static phrase:

```text
"Right — let me adjust that."
```

MVP recommendation:

```text
Use static cached phrases first.
Add dynamic phrase generation later.
```

## 11.4 Barge-in while acknowledgement is playing

The acknowledgement itself must also be interruptible.

Example:

```text
User: "No, SQLite."
Merlin: "Right, SQLite — let me..."
User: "Actually never mind."
Merlin: [stops acknowledgement]
Merlin: "Okay, switching."
```

So even local acknowledgement playback needs TurnId or event ownership.

Recommended:

```text
AcknowledgementTurnId or ChildTurnId
```

If a new interruption occurs, cancel the acknowledgement audio too.

---

# 12. TurnId ownership and late output safety

This is mandatory.

Every output belongs to a TurnId:

```text
DeepInfra token
text chunk
sentence chunk
TTS generation job
TTS audio chunk
audio queue item
local acknowledgement phrase
tool result
memory update
```

If a turn is cancelled, all future output for that turn is invalid.

Pseudo-rule:

```csharp
if (!turnManager.IsTurnActive(chunk.TurnId))
{
    discard chunk;
    return;
}
```

This prevents the worst possible bug:

```text
User interrupts PostgreSQL answer
Merlin starts SQLite answer
Old PostgreSQL audio arrives late
Merlin suddenly speaks PostgreSQL again
```

That must never happen.

---

# 13. Cancellation levels

Stopping Merlin has multiple layers.

## 13.1 Level 1: stop playback

Immediate.

```text
Audio output stops now.
```

## 13.2 Level 2: clear queued TTS

Remove old queued chunks.

```text
TtsQueue.Clear(turnId)
```

## 13.3 Level 3: cancel active TTS generation

If possible, cancel active TTS generation.

If Chatterbox call is blocking and cannot be cancelled mid-call, do this:

```text
- mark TurnId cancelled
- let blocking call finish in background
- discard resulting audio when it returns
```

## 13.4 Level 4: cancel or abandon DeepInfra stream

If streaming HTTP supports cancellation:

```csharp
cancellationTokenSource.Cancel();
```

If actual network cancellation is unreliable:

```text
- stop reading
- mark TurnId cancelled
- discard late tokens
```

## 13.5 Level 5: cancel tool execution

Safe tool actions should support cancellation.

Unsafe actions need commit boundaries.

---

# 14. Tool execution safety

Interruption during tool actions must be handled carefully.

Classify tool actions as:

```text
read_only
reversible_write
irreversible_or_external_commit
long_running_cancellable
long_running_non_cancellable
```

## 14.1 Read-only tools

Examples:

```text
search files
read logs
inspect calendar
query memory
```

On interruption:

```text
cancel if possible
or discard result if completed late
```

## 14.2 Reversible writes

Examples:

```text
create draft
create temporary file
add local label that can be removed
```

On interruption:

```text
cancel before commit if possible
if already committed, explain state and offer reversal if safe
```

## 14.3 Irreversible/external commits

Examples:

```text
send email
modify calendar invite
delete file
shutdown PC
make purchase
```

Required flow:

```text
Prepare
↓
Review
↓
Explicit confirmation
↓
Commit
```

Interruption before commit:

```text
"Cancelled before committing the action."
```

Interruption after commit:

```text
"That had already completed, so I can’t cancel it now."
```

Do not pretend cancellation succeeded if it did not.

---

# 15. Memory integration

The interruption system must integrate with Merlin's local memory system.

## 15.1 Do not save interrupted assistant output as fact

If Merlin starts saying something wrong and the user interrupts, the old answer should not become trusted memory.

Example:

```text
Merlin: "Start with PostgreSQL..."
User: "No, SQLite."
```

Do not save:

```text
Merlin memory: "Memory system should start with PostgreSQL."
```

Save instead:

```json
{
  "type": "user_correction",
  "topic": "Merlin memory system",
  "old_assistant_direction": "PostgreSQL",
  "corrected_direction": "SQLite",
  "confidence": 0.95,
  "source": "user_interruption"
}
```

## 15.2 Topic status

Topics should have statuses:

```text
active
completed
paused
interrupted
abandoned
corrected
superseded
```

If user changes topic mid-answer:

```json
{
  "topic": "Merlin interruption architecture",
  "status": "abandoned",
  "reason": "user switched to Whisper VRAM usage"
}
```

If user corrects direction:

```json
{
  "topic": "Merlin memory database choice",
  "status": "corrected",
  "correction": "Use SQLite instead of PostgreSQL"
}
```

## 15.3 Explicit corrections should be high-priority

User corrections are valuable memory signals.

Examples:

```text
"No, SQLite."
"No, the backend."
"I don't want packages for that."
"Do not use Postgres for local Merlin memory."
```

These should often be saved as medium memory and sometimes promoted to long-term project memory if stable and important.

## 15.4 Local acknowledgement is not memory content

Do not store the acknowledgement phrase itself as meaningful project memory.

This is not useful:

```json
{
  "memory": "Merlin said: You are right, let me rearrange my thoughts."
}
```

This is useful:

```json
{
  "memory": "User corrected Merlin to use SQLite instead of PostgreSQL for the local memory implementation."
}
```

---

# 16. Logging and diagnostics

Log interruption behaviour thoroughly.

Each interruption event should log:

```text
conversation_id
old_turn_id
new_turn_id if regeneration happens
interruption_timestamp
assistant_state_when_interrupted
spoken_text_so_far
user_interruption_transcript
classification_type
classification_confidence
local_acknowledgement_text
requires_deepinfra_regeneration
old_deepinfra_cancel_requested
old_tts_queue_cleared
late_chunks_discarded_count
correction_prompt_token_estimate
new_deepinfra_first_token_latency_ms
new_tts_first_audio_latency_ms
```

Example log:

```json
{
  "event": "assistant_interrupted",
  "old_turn_id": "123",
  "new_turn_id": "124",
  "state": "Speaking",
  "spoken_text_so_far": "To build the memory system, I would start with PostgreSQL...",
  "user_interruption": "No, SQLite.",
  "classification": "correction",
  "confidence": 0.94,
  "acknowledgement": "Right, SQLite — let me adjust that.",
  "requires_deepinfra_regeneration": true,
  "old_turn_cancelled": true,
  "old_audio_queue_cleared": true,
  "correction_prompt_tokens": 412,
  "first_token_latency_ms": 860,
  "first_audio_latency_ms": 1420
}
```

These logs are essential for tuning.

---

# 17. Latency goals

Set realistic targets.

## 17.1 Hard stop latency

```text
Target: < 250 ms after classification or interrupt button
Ideal: 50-150 ms
```

## 17.2 Audio ducking latency

```text
Target: < 150 ms after VAD detects user speech
```

## 17.3 Local acknowledgement start latency

```text
Target: < 1000 ms after user begins clear interruption
Ideal: 300-700 ms
```

## 17.4 Regenerated DeepInfra answer first token

```text
Target: as fast as provider/model allows
Expected: often 500 ms - 2+ seconds
```

## 17.5 Regenerated answer first audio

```text
Target: as soon as first sentence/chunk is available
Avoid waiting for full answer
```

The local acknowledgement exists specifically to hide or soften the DeepInfra/TTS delay.

---

# 18. MVP implementation order

Do not start with full-duplex perfect voice. Build this in layers.

## Phase 1: TurnId ownership and manual cancellation

Implement:

```text
- AssistantTurn model
- TurnManager
- TurnId attached to text chunks, TTS chunks, and audio queue items
- cancellation token per turn
- manual interrupt button/hotkey
- stop playback
- clear TTS queue
- ignore late chunks from cancelled TurnIds
```

Acceptance criteria:

```text
While Merlin is speaking, manual interrupt stops speech immediately.
Old audio never resumes.
Late TTS chunks from old turn are discarded.
Late DeepInfra tokens from old turn are discarded.
Next user message starts a clean new turn.
```

## Phase 2: Local acknowledgement service

Implement:

```text
- AcknowledgementService
- phrase categories
- phrase repetition prevention
- cached/static acknowledgement audio support
- acknowledgement playback priority
```

Acceptance criteria:

```text
When a correction is detected manually or by text input, Merlin immediately says a short local phrase before DeepInfra answer begins.
Hard stop does not produce unnecessary acknowledgement.
Pause says only a very short phrase like "Paused."
```

## Phase 3: Correction prompt builder

Implement:

```text
- CorrectionPromptBuilder
- include original request
- include partial assistant output
- include user correction
- include local interpretation
- include relevant memory packet
- include instruction not to repeat local acknowledgement
```

Acceptance criteria:

```text
After "No, SQLite", a new DeepInfra call is made with a compact prompt that clearly discards PostgreSQL and uses SQLite.
The generated answer does not repeat the local acknowledgement awkwardly.
Prompt token estimate is logged.
```

## Phase 4: Rule-based interruption classifier

Implement:

```text
- hard_stop rules
- pause/resume rules
- correction rules
- topic_change rules
- clarification rules
- backchannel/noise rules
```

Acceptance criteria:

```text
"stop" cancels without regeneration.
"wait" pauses.
"continue" resumes.
"No, SQLite" cancels and regenerates.
"Actually forget that, talk about Whisper" cancels and starts new topic.
"What is a TurnId?" creates clarification behaviour.
"yeah" does not interrupt.
```

## Phase 5: Voice barge-in MVP

Implement:

```text
- keep microphone active while speaking
- VAD during playback
- audio ducking on suspected user speech
- ASR partial transcript for interruption classification
- high-confidence commands first
```

Acceptance criteria:

```text
User can say "stop" while Merlin speaks and Merlin stops.
User can say "wait" and Merlin pauses.
User can say "No, SQLite" and Merlin cancels, acknowledges locally, and regenerates.
Merlin does not stop on normal backchannel sounds.
```

## Phase 6: Echo cancellation / anti-self-triggering

Implement:

```text
- acoustic echo cancellation if feasible
- or wake-word/keyboard fallback until AEC is reliable
- prevent Merlin's own TTS from being transcribed as user interruption
```

Acceptance criteria:

```text
Merlin can speak while listening.
Merlin's own voice does not trigger interruption.
User speech over Merlin is detected reliably.
```

## Phase 7: Clarification child turns and resume

Implement:

```text
- parent turn pause
- child clarification turn
- optional resume parent
- user can abandon parent after clarification
```

Acceptance criteria:

```text
User can interrupt with "what does that mean?"
Merlin answers briefly.
Merlin can continue original answer if appropriate.
Old parent turn remains valid only if not cancelled.
```

## Phase 8: Interruption-aware memory

Implement:

```text
- interruption event memory records
- correction memory records
- topic status updates
- prevention of interrupted assistant output promotion
```

Acceptance criteria:

```text
Interrupted wrong assistant content is not saved as fact.
User corrections can become project memory.
Topic summaries mention interruptions/corrections when relevant.
```

---

# 19. Testing requirements

Add automated tests where possible.

## 19.1 TurnId cancellation tests

Test:

```text
- old turn emits text after cancellation
- old turn emits TTS chunk after cancellation
- old turn emits tool result after cancellation
```

Expected:

```text
All late outputs are discarded.
```

## 19.2 Correction flow test

Scenario:

```text
Original request: "How should Merlin store memory?"
Assistant partial: "I would start with PostgreSQL..."
User interruption: "No, SQLite."
```

Expected:

```text
- old turn cancelled
- audio stopped
- acknowledgement selected
- new correction prompt includes SQLite
- new prompt instructs DeepInfra not to continue PostgreSQL
- old chunks discarded
```

## 19.3 Local acknowledgement test

Test:

```text
classification = correction, replacement_hint = SQLite
```

Expected phrase examples:

```text
"Right, SQLite — let me adjust that."
```

or fallback:

```text
"Right — let me adjust that."
```

Also test repetition prevention.

## 19.4 Hard stop test

Scenario:

```text
User says "stop"
```

Expected:

```text
- no DeepInfra regeneration
- no long acknowledgement
- playback stops
- turn marked cancelled_by_user
```

## 19.5 Pause/resume test

Scenario:

```text
User says "wait"
then "continue"
```

Expected:

```text
- parent turn paused
- no DeepInfra regeneration yet
- playback resumes if still valid
```

## 19.6 Clarification test

Scenario:

```text
Assistant says "Use TurnIds"
User says "What is a TurnId?"
```

Expected:

```text
- parent turn paused
- clarification child turn created
- response explains TurnId
- parent can resume or be abandoned
```

## 19.7 Backchannel test

Scenario:

```text
User says "yeah" while Merlin speaks
```

Expected:

```text
- playback continues
- no cancellation
- no correction prompt
```

## 19.8 Memory safety test

Scenario:

```text
Assistant partial contains wrong claim
User interrupts and corrects it
```

Expected:

```text
- wrong assistant claim is not promoted to memory
- user correction is recorded
```

---

# 20. Failure modes to prevent

## 20.1 Old audio resumes after interruption

Cause:

```text
TTS queue not cleared or chunks not TurnId-bound.
```

Prevention:

```text
TurnId on every chunk and queue item.
```

## 20.2 Merlin goes silent after correction

Cause:

```text
Waiting for DeepInfra without local acknowledgement.
```

Prevention:

```text
AcknowledgementService speaks immediately.
```

## 20.3 Merlin repeats acknowledgement awkwardly

Cause:

```text
Local acknowledgement spoken, then DeepInfra also apologizes/re-acknowledges.
```

Prevention:

```text
Correction prompt tells DeepInfra local acknowledgement already happened.
```

## 20.4 Merlin cancels on every small sound

Cause:

```text
VAD treated as interruption intent.
```

Prevention:

```text
Use classifier with transcript and confidence. Treat backchannels separately.
```

## 20.5 Merlin saves wrong interrupted content

Cause:

```text
Memory writer treats partial assistant output as completed conclusion.
```

Prevention:

```text
MemoryInterruptionRecorder marks old output unconfirmed/interrupted.
```

## 20.6 Merlin locally patches complex answer incorrectly

Cause:

```text
Trying to replace terms in old answer instead of regenerating through DeepInfra.
```

Prevention:

```text
For correction types, build new DeepInfra correction prompt.
```

## 20.7 Tool action commits despite interruption

Cause:

```text
No commit boundary.
```

Prevention:

```text
Prepare/review/confirm/commit flow for irreversible actions.
```

---

# 21. Example end-to-end flows

## 21.1 Correction: PostgreSQL to SQLite

```text
User:
"How should we build Merlin's memory database?"

Merlin Turn 100:
"I would start with PostgreSQL because..."

User interruption:
"No, SQLite."

Local Merlin:
- detects user speech
- stops current audio
- classifies correction
- cancels Turn 100
- selects acknowledgement

Merlin local acknowledgement:
"Right, SQLite — let me adjust that."

Local Merlin:
- builds correction prompt
- sends DeepInfra Turn 101
- ignores late Turn 100 chunks

Merlin Turn 101:
"For Merlin, SQLite is the better starting point because the memory system is local-first..."
```

## 21.2 Hard stop

```text
Merlin:
"The next part is a detailed explanation of..."

User:
"Stop."

Local Merlin:
- stops playback
- cancels current turn
- clears queue
- no acknowledgement or only "Stopped."
- no DeepInfra regeneration
```

## 21.3 Pause then resume

```text
Merlin:
"The InterruptionController should own..."

User:
"Wait."

Merlin:
"Paused."

User:
"Continue."

Merlin:
[resumes if parent turn still valid]
```

## 21.4 Clarification child turn

```text
Merlin:
"Every TTS chunk must carry the TurnId."

User:
"What is a TurnId?"

Merlin local:
"Good question — briefly."

Merlin child answer:
"A TurnId is the ID of one assistant response. It lets Merlin discard old audio or tokens after interruption."

Merlin:
"Continuing from there..."
```

## 21.5 Topic switch

```text
Merlin:
"For memory retrieval, I would..."

User:
"Actually forget memory for now. How much VRAM does Whisper medium use?"

Merlin local:
"Okay, switching topics."

Merlin:
[starts new DeepInfra turn with Whisper VRAM prompt]
```

---

# 22. Configuration options

Add configurable settings for interruption behaviour.

Suggested config:

```json
{
  "Interruption": {
    "EnableManualInterrupt": true,
    "EnableVoiceBargeIn": true,
    "EnableAudioDucking": true,
    "EnableLocalAcknowledgements": true,
    "EnableDynamicAcknowledgements": false,
    "MinSpeechMsForInterruption": 250,
    "MinClassifierConfidenceToCancel": 0.75,
    "MinClassifierConfidenceToPause": 0.55,
    "HardStopPhrases": ["stop", "cancel", "shut up", "never mind"],
    "PausePhrases": ["wait", "pause", "hold on"],
    "CorrectionStarters": ["no", "actually", "I mean", "I meant", "not"],
    "AcknowledgementCacheEnabled": true,
    "MaxAcknowledgementWords": 10,
    "CorrectionPromptMaxTokens": 1200
  }
}
```

---

# 23. Integration with the memory compiler

The MemoryCompiler should support special prompt modes:

```text
normal_answer
correction_regeneration
clarification_child_turn
topic_switch
resume_parent_turn
tool_cancellation_response
```

For correction regeneration, MemoryCompiler should:

```text
- retrieve only memories relevant to corrected topic
- include user correction as highest priority
- include original user request
- include partial assistant text only if useful
- exclude irrelevant old context
- enforce small token budget
```

This connects directly to Merlin's brain-like memory system.

The interruption system creates the event:

```text
User corrected the active answer from PostgreSQL to SQLite.
```

The memory system retrieves the right drawers:

```text
Merlin
memory system
SQLite
local-first storage
DeepInfra token reduction
```

The prompt compiler sends only that compact packet to DeepInfra.

---

# 24. Non-goals for the first version

Do not attempt these in the first milestone:

```text
- perfect full-duplex human-level conversation
- advanced local LLM-based interruption classification
- complex emotional acknowledgement generation
- regenerating answers without DeepInfra
- editing already-spoken content retroactively
- storing every interruption permanently
- supporting all possible ambiguous speech patterns
```

The MVP should be reliable before it is clever.

---

# 25. First milestone definition

The first milestone is successful when all of the following work:

```text
1. Every assistant answer has a TurnId.
2. Every DeepInfra token/chunk is associated with that TurnId.
3. Every TTS chunk/audio buffer is associated with that TurnId.
4. Manual interruption stops current playback.
5. Manual interruption clears queued TTS for the old TurnId.
6. Late output from old TurnIds is discarded.
7. A correction such as "No, SQLite" cancels the old turn.
8. Merlin immediately speaks a local acknowledgement.
9. Merlin builds a compact correction prompt.
10. Merlin sends a new DeepInfra request for the corrected answer.
11. DeepInfra is told not to repeat the local acknowledgement.
12. Memory writer does not save interrupted assistant output as fact.
13. Logs show cancellation, acknowledgement, correction prompt tokens, and regeneration latency.
```

---

# 26. Final design principle

Merlin's interruption system should be designed around this principle:

```text
Stop locally. Acknowledge locally. Regenerate remotely. Continue naturally.
```

More complete:

```text
Local Merlin owns turn control.
DeepInfra owns deep reasoning.
MemoryCompiler owns compact context.
AcknowledgementService owns the human-feeling bridge during regeneration latency.
TurnId ownership prevents old output from leaking back in.
```

The goal is not just that Merlin can be stopped.

The goal is that Merlin can be conversationally corrected.

A corrected flow should feel like this:

```text
Merlin: "To build the memory system, I would start with PostgreSQL..."
User: "No, SQLite."
Merlin: [stops immediately]
Merlin: "Right, SQLite — let me adjust that."
Merlin: "For Merlin, SQLite is the better starting point because..."
```

That is the behaviour to implement.
