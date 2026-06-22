# Merlin Conversational Interruption Redesign

## Document Purpose

This document describes a full redesign of Merlin's interruption behavior.

The goal is to move Merlin from simple interruption handling:

```text
Assistant is speaking
↓
User says something
↓
Either ignore, suppress, or cancel
```

toward actual conversational interruption understanding:

```text
Assistant is speaking
↓
User says something
↓
Merlin pauses/listens when needed
↓
Merlin classifies the role of the interruption
↓
Merlin decides whether to continue, cancel, clarify, queue, or recompose
↓
Merlin preserves the red wire of the conversation
```

This document is meant to be agent-ready. It should guide implementation after the `ResponsiveFeedback` migration.

---

# Core Principle

Merlin should not merely detect that the user interrupted.

Merlin should understand what the interruption means in the conversation.

The key rule:

```text
Merlin should preserve the main conversational thread unless the user clearly redirects, corrects, cancels, or asks to branch.
```

The second key rule:

```text
After a meaningful interruption, Merlin should not resume raw old speech mid-sentence.
Merlin should recompose the continuation from a clean checkpoint.
```

---

# Why This Redesign Is Needed

Current interruption handling is mostly technical:

```text
Did the user speak while the assistant was speaking?
Was it speech or echo?
Should playback stop?
Should we treat this as a new request?
```

That is necessary, but not enough.

A conversational partner needs to distinguish between:

```text
"yeah"
```

and:

```text
"No, I meant what is the meaning of a wife?"
```

and:

```text
"But sometimes the water color too, right?"
```

These three interruptions require completely different behavior.

---

# Desired User Experience

## Case 1: Passive backchannel

Assistant is speaking.

User says:

```text
"yeah"
```

Expected behavior:

```text
Merlin continues speaking.
No DeepInfra call.
No new turn.
No interruption response.
Maybe a tiny visual acknowledgement at most.
```

## Case 2: Hard correction / redirect

Assistant is speaking.

User says:

```text
"Nooooo, I meant what is the meaning of a wife?"
```

Expected behavior:

```text
Merlin stops current playback.
Cancels current answer.
Acknowledges the correction.
Sends the corrected request to DeepInfra.
Uses responsive feedback while processing.
Speaks the new answer.
```

Example bridge:

```text
"Oh, you meant what is the meaning of a wife. Let me re-organise that."
```

## Case 3: Side comment that does not require a new answer

Assistant says:

```text
"Due to the color of the pool liner, the swimming pool water looks more blue..."
```

User interrupts:

```text
"Well yeah, but sometimes water color too."
```

Expected behavior:

```text
Merlin pauses.
Recognizes this as a related side comment.
Briefly acknowledges.
Does not blindly resume mid-sentence.
Recomposes from a clean checkpoint if needed.
Continues the original answer with the added context.
```

Example bridge:

```text
"True, that can matter too."
```

Then continuation should be smooth:

```text
"So besides the liner, the water itself can also affect the color..."
```

## Case 4: Clarification/follow-up during the answer

Assistant is explaining pool water color.

User says:

```text
"But sometimes the water color too, right?"
```

Expected behavior:

```text
Merlin pauses.
Classifies it as a related clarification/follow-up.
Calls DeepInfra for a short clarification answer.
Speaks that clarification quickly.
In parallel or afterward, asks DeepInfra to recompose the continuation.
Continues the original answer from a clean checkpoint, including the clarification context.
```

Example clarification:

```text
"Yes, exactly. The water itself can also affect the color, especially in deeper pools."
```

Then recomposed continuation:

```text
"So there are really two effects: the liner can influence the perceived color, and the water itself can look bluer as depth increases..."
```

## Case 5: User asks to stop

User says:

```text
"stop"
"shut up"
"cancel"
"never mind"
```

Expected behavior:

```text
Merlin stops speaking.
Cancels or pauses current turn depending on command.
Does not call DeepInfra.
May give no verbal response, or a tiny acknowledgement only if appropriate.
```

---

# Important Design Change

The old mental model is:

```text
Pause original answer
Answer clarification
Resume original answer
```

The improved model is:

```text
Pause original answer
Discard unsafe partial sentence/chunk
Answer or acknowledge the interruption
Recompose the original answer from a clean checkpoint
Continue naturally with interruption context included
```

This matters because the user may interrupt mid-sentence.

Bad UX:

```text
Assistant: "Due to the color of the pool li—"
User: "But sometimes water color too, right?"
Assistant: "Yes, exactly..."
Assistant resumes: "—ner, the water looks..."
```

Good UX:

```text
Assistant: "Due to the color of the pool li—"
User: "But sometimes water color too, right?"
Assistant: "Yes, exactly. The water itself can also affect the color."
Assistant continues: "So besides the liner, there are two things going on..."
```

Merlin should preserve the conversation thread, not the exact old audio stream.

---

# Relationship To ResponsiveFeedback

This redesign should happen after or alongside the `ResponsiveFeedback` migration.

`ResponsiveFeedback` provides:

```text
- short bridge phrases
- acknowledgement phrases
- progress phrases while processing
- cooldowns
- anti-repeat logic
- cheap context-aware feedback selection
```

The interruption system decides:

```text
- whether the interruption matters
- whether to pause, continue, cancel, branch, queue, or recompose
- whether DeepInfra is needed
- what context to send to DeepInfra
```

Together:

```text
InterruptionDecision
        ↓
ResponsiveFeedback
        ↓
natural bridge speech
        ↓
DeepInfra clarification/continuation if needed
```

---

# Target Architecture Overview

```text
AssistantSpeechPlaybackService
        ↓
Playback is active

SpeechPresence / BargeIn
        ↓
User speech detected during playback

InterruptionCaptureService
        ↓
captures candidate user interruption audio/transcript

InterruptionClassifier
        ↓
classifies interruption type and urgency

ConversationFocusManager
        ↓
decides how this affects the current conversational thread

InterruptionOrchestrator
        ↓
executes decision:
  - continue
  - pause
  - stop
  - cancel
  - clarify
  - recompose
  - queue follow-up

AnswerRecomposer
        ↓
builds DeepInfra prompts for clarification and continuation

ResponsiveFeedbackOrchestrator
        ↓
speaks bridge/progress feedback while processing

AssistantSpeechPlaybackService
        ↓
speaks clarification and recomposed continuation
```

---

# Logical Channels

The system should use logical channels, not necessarily multiple physical audio outputs.

## Main Answer Channel

The original answer currently being spoken.

Example:

```text
The long answer to: "Why does pool water look blue?"
```

## Interruption Channel

The user speech that occurred while Merlin was speaking.

Example:

```text
"But sometimes the water color too, right?"
```

## Clarification Channel

A short answer to the interruption.

Example:

```text
"Yes, exactly. The water itself can also affect the color..."
```

## Recomposition Channel

The revised continuation of the original answer.

Example:

```text
"So besides the liner, the water itself also matters..."
```

## Feedback Channel

Short bridge/progress phrases.

Example:

```text
"Good point, let me include that."
"Let me fold that into the answer."
```

## Follow-Up Queue

Questions/comments to answer after the current response.

Example:

```text
"Also explain how sunlight affects it."
```

Important:

```text
These are logical channels.
Physical speech output should still be serialized through one controlled speech queue.
Do not allow overlapping Merlin voices unless explicitly designed later.
```

---

# Main Components To Add

## 1. InterruptionCaptureService

Purpose:

```text
Capture and package user speech that occurs while Merlin is speaking.
```

Responsibilities:

- Listen to speech presence/barge-in events.
- Detect candidate user interruption during assistant playback.
- Decide whether to pause playback immediately or wait for STT confidence.
- Produce an `InterruptionCandidate`.
- Include timing data.
- Include current playback/answer state snapshot.
- Avoid treating assistant self-echo as user speech.

Suggested location:

```text
Merlin.Backend/Services/Interruption/
```

Suggested files:

```text
Merlin.Backend/Services/Interruption/InterruptionCaptureService.cs
Merlin.Backend/Services/Interruption/IInterruptionCaptureService.cs
Merlin.Backend/Services/Interruption/InterruptionCandidate.cs
```

## 2. InterruptionClassifier

Purpose:

```text
Determine what kind of interruption the user made.
```

Responsibilities:

- Fast local classification for obvious cases.
- Optional model classification for ambiguous cases.
- Decide if the interruption requires reasoning.
- Decide if DeepInfra is needed.
- Produce an `InterruptionDecision`.

Suggested files:

```text
Merlin.Backend/Services/Interruption/InterruptionClassifier.cs
Merlin.Backend/Services/Interruption/IInterruptionClassifier.cs
Merlin.Backend/Services/Interruption/InterruptionType.cs
Merlin.Backend/Services/Interruption/InterruptionDecision.cs
Merlin.Backend/Services/Interruption/InterruptionHandlingStrategy.cs
```

## 3. ConversationFocusManager

Purpose:

```text
Maintain the red wire of the conversation.
```

Responsibilities:

- Know the active main turn.
- Know whether playback is active.
- Know whether current answer can be recomposed.
- Decide whether the current interruption modifies, replaces, queues behind, or ignores the current main answer.
- Store active answer state/checkpoints.
- Manage follow-up queue.
- Manage branch context.

Suggested files:

```text
Merlin.Backend/Services/Conversation/ConversationFocusManager.cs
Merlin.Backend/Services/Conversation/IConversationFocusManager.cs
Merlin.Backend/Services/Conversation/ConversationThreadState.cs
Merlin.Backend/Services/Conversation/ConversationBranch.cs
Merlin.Backend/Services/Conversation/QueuedFollowUp.cs
```

This could also live under:

```text
Merlin.Backend/Services/Interruption/
```

but `Conversation` is better long-term if Merlin will support richer dialogue state.

## 4. SpokenAnswerTracker

Purpose:

```text
Track what Merlin has spoken so far and where safe continuation checkpoints are.
```

Responsibilities:

- Track original question.
- Track full generated draft if available.
- Track spoken text so far.
- Track current sentence/chunk.
- Track last completed sentence.
- Track unspoken remainder.
- Track playback position.
- Provide clean recomposition checkpoint.
- Discard unsafe partial sentence on meaningful interruption.

Suggested files:

```text
Merlin.Backend/Services/Speech/SpokenAnswerTracker.cs
Merlin.Backend/Services/Speech/ISpokenAnswerTracker.cs
Merlin.Backend/Services/Speech/SpokenAnswerState.cs
Merlin.Backend/Services/Speech/SpokenAnswerCheckpoint.cs
```

If the project prefers grouping by feature:

```text
Merlin.Backend/Services/Interruption/SpokenAnswerTracker.cs
```

## 5. AnswerRecomposer

Purpose:

```text
Build and execute DeepInfra prompts for clarification and recomposed continuation.
```

Responsibilities:

- Create prompt for short clarification.
- Create prompt for continuation recomposition.
- Choose between split calls and combined calls.
- Ensure continuation does not repeat spoken content.
- Ensure continuation starts from a clean checkpoint.
- Use the user's interruption and clarification answer as context.
- Return structured outputs.

Suggested files:

```text
Merlin.Backend/Services/Interruption/AnswerRecomposer.cs
Merlin.Backend/Services/Interruption/IAnswerRecomposer.cs
Merlin.Backend/Services/Interruption/ClarificationRequest.cs
Merlin.Backend/Services/Interruption/ClarificationResult.cs
Merlin.Backend/Services/Interruption/ContinuationRecompositionRequest.cs
Merlin.Backend/Services/Interruption/ContinuationRecompositionResult.cs
```

## 6. InterruptionOrchestrator

Purpose:

```text
Central coordinator that executes interruption decisions.
```

Responsibilities:

- Receive interruption candidate.
- Ask classifier for decision.
- Pause/continue/cancel playback.
- Ask focus manager for active answer checkpoint.
- Route to DeepInfra if needed.
- Speak bridge/clarification/continuation in the right order.
- Use ResponsiveFeedback for intermediate comments.
- Keep audio queue controlled.
- Ensure final answer path remains stable.

Suggested files:

```text
Merlin.Backend/Services/Interruption/InterruptionOrchestrator.cs
Merlin.Backend/Services/Interruption/IInterruptionOrchestrator.cs
```

---

# Data Model Design

## InterruptionCandidate

```csharp
public sealed class InterruptionCandidate
{
    public string CorrelationId { get; init; } = "";
    public string ActiveTurnId { get; init; } = "";

    public string Transcript { get; init; } = "";
    public double TranscriptConfidence { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }

    public TimeSpan AssistantPlaybackPosition { get; init; }
    public bool AssistantWasSpeaking { get; init; }
    public bool PlaybackWasPausedForCapture { get; init; }

    public string? CurrentAssistantSentence { get; init; }
    public string? LastCompletedAssistantSentence { get; init; }
    public string? OriginalUserQuestion { get; init; }

    public bool IsLikelySelfEcho { get; init; }
    public bool IsLikelyUserSpeech { get; init; }
}
```

## InterruptionType

```csharp
public enum InterruptionType
{
    Unknown = 0,

    Backchannel,
    PassiveAgreement,
    StopRequest,
    CancelRequest,
    RepeatRequest,

    Correction,
    Redirect,
    ClarificationQuestion,
    RelatedFollowUpQuestion,
    SideComment,
    Disagreement,
    AdditionalContext,

    PlaybackControl,
    NoiseOrFalsePositive
}
```

## InterruptionHandlingStrategy

```csharp
public enum InterruptionHandlingStrategy
{
    Unknown = 0,

    IgnoreAndContinue,
    ContinueWithoutResponse,
    LocalBridgeAndRecomposeFromCheckpoint,
    ClarifyThenRecomposeFromCheckpoint,
    CancelAndRedirect,
    QueueFollowUpAfterCurrent,
    StopPlayback,
    AskUserToClarifyInterruption
}
```

Important:

```text
Avoid a broad "ClarifyThenResumeRaw" strategy.
Raw resume is only acceptable for true backchannels.
Meaningful interruptions should recompose from a checkpoint.
```

## InterruptionDecision

```csharp
public sealed class InterruptionDecision
{
    public InterruptionType Type { get; init; } = InterruptionType.Unknown;
    public InterruptionHandlingStrategy Strategy { get; init; } = InterruptionHandlingStrategy.Unknown;

    public double Confidence { get; init; }

    public bool PausePlayback { get; init; }
    public bool CancelOriginalTurn { get; init; }
    public bool ResumeRawPlayback { get; init; }
    public bool DiscardCurrentPartialSentence { get; init; } = true;

    public bool RequiresDeepInfraClarification { get; init; }
    public bool RequiresContinuationRecomposition { get; init; }
    public bool CanRunContinuationInParallel { get; init; }

    public bool QueueAfterCurrentTurn { get; init; }
    public bool NeedsUserConfirmation { get; init; }

    public string? LocalBridgePhrase { get; init; }
    public string? RewrittenUserRequest { get; init; }

    public int ClarificationMaxTokens { get; init; } = 90;
    public int ContinuationMaxTokens { get; init; } = 500;

    public string Reason { get; init; } = "";
}
```

## SpokenAnswerState

```csharp
public sealed class SpokenAnswerState
{
    public string TurnId { get; init; } = "";
    public string CorrelationId { get; init; } = "";

    public string OriginalUserQuestion { get; init; } = "";
    public string? OriginalAssistantDraft { get; init; }

    public string SpokenSoFar { get; init; } = "";
    public string LastCompletedSentence { get; init; } = "";
    public string CurrentPartialSentence { get; init; } = "";
    public string UnspokenRemainder { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }
    public TimeSpan PlaybackPosition { get; init; }

    public bool CanRecompose { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
```

## SpokenAnswerCheckpoint

```csharp
public sealed class SpokenAnswerCheckpoint
{
    public string TurnId { get; init; } = "";
    public string CorrelationId { get; init; } = "";

    public string OriginalUserQuestion { get; init; } = "";

    public string SafeSpokenPrefix { get; init; } = "";
    public string LastCompletedSentence { get; init; } = "";
    public string DiscardedPartialSentence { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }
    public TimeSpan PlaybackPosition { get; init; }

    public string? OriginalPlanOrIntent { get; init; }
}
```

Meaning:

```text
SafeSpokenPrefix:
  What the user has already heard and should not be repeated much.

LastCompletedSentence:
  The best safe point to continue from.

DiscardedPartialSentence:
  The cut-off sentence/chunk that should not be resumed raw.
```

## ClarificationRequest

```csharp
public sealed class ClarificationRequest
{
    public string OriginalUserQuestion { get; init; } = "";
    public string SpokenAnswerSoFar { get; init; } = "";
    public string LastCompletedSentence { get; init; } = "";
    public string DiscardedPartialSentence { get; init; } = "";
    public string UserInterruption { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }

    public int MaxTokens { get; init; } = 90;
    public string Tone { get; init; } = "brief, natural, conversational";
}
```

## ClarificationResult

```csharp
public sealed class ClarificationResult
{
    public string ReplyText { get; init; } = "";
    public string ExtractedClarificationContext { get; init; } = "";

    public bool ShouldRecomposeContinuation { get; init; } = true;
    public bool UserQuestionAnswered { get; init; } = true;
}
```

## ContinuationRecompositionRequest

```csharp
public sealed class ContinuationRecompositionRequest
{
    public string OriginalUserQuestion { get; init; } = "";
    public string SpokenAnswerSoFar { get; init; } = "";
    public string LastCompletedSentence { get; init; } = "";
    public string DiscardedPartialSentence { get; init; } = "";
    public string UserInterruption { get; init; } = "";
    public string ClarificationReply { get; init; } = "";
    public string ClarificationContext { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }
    public string? OriginalPlanOrIntent { get; init; }

    public int MaxTokens { get; init; } = 500;
}
```

## ContinuationRecompositionResult

```csharp
public sealed class ContinuationRecompositionResult
{
    public string ContinuationText { get; init; } = "";
    public bool AvoidedRepeatingSpokenContent { get; init; }
    public bool IncludedClarificationContext { get; init; }
}
```

---

# Interruption Classification

## Local Classification First

Do not call DeepInfra for every interruption.

Local classification can identify obvious cases:

```text
"yeah"                       -> Backchannel
"mhm"                        -> Backchannel
"right"                      -> PassiveAgreement
"okay"                       -> PassiveAgreement
"stop"                       -> StopRequest
"shut up"                    -> StopRequest
"cancel"                     -> CancelRequest
"never mind"                 -> CancelRequest
"repeat that"                -> RepeatRequest
"no I meant..."              -> Correction
"actually I meant..."        -> Correction
"wait what do you mean by"   -> ClarificationQuestion
"what is ..."                -> ClarificationQuestion
"but ... right?"             -> RelatedFollowUpQuestion
```

Important:

```text
Local classification decides the role of the interruption.
It does not answer factual content.
```

Example:

```text
"But sometimes the water color too, right?"
```

Local classifier can decide:

```text
This is probably a related clarification/follow-up.
It needs reasoning.
It should not cancel the original answer.
It should recompose the continuation.
```

Local classifier cannot know:

```text
Whether water absorbs red wavelengths more than blue.
```

That requires DeepInfra or another knowledge source.

## Ambiguous Classification

If local rules are uncertain, use a small classification call.

This is not the same as answering the question.

The model classification prompt should return only a decision JSON.

Example output:

```json
{
  "type": "RelatedFollowUpQuestion",
  "strategy": "ClarifyThenRecomposeFromCheckpoint",
  "confidence": 0.82,
  "requiresDeepInfraClarification": true,
  "requiresContinuationRecomposition": true,
  "reason": "User asks a related question that adds context to the current explanation."
}
```

## Classification should be cheap

Use:

```text
- low max tokens
- JSON-only response
- no long reasoning
- no factual answer
```

Do not let classification become expensive.

---

# Interruption Decision Rules

## Rule 1: Backchannels do not interrupt

Examples:

```text
"yeah"
"mhm"
"right"
"okay"
```

Decision:

```json
{
  "strategy": "ContinueWithoutResponse",
  "pausePlayback": false,
  "resumeRawPlayback": true,
  "requiresDeepInfraClarification": false,
  "requiresContinuationRecomposition": false
}
```

## Rule 2: Stop/cancel commands do not call DeepInfra

Examples:

```text
"stop"
"cancel"
"never mind"
"shut up"
```

Decision:

```json
{
  "strategy": "StopPlayback",
  "pausePlayback": true,
  "cancelOriginalTurn": true,
  "requiresDeepInfraClarification": false,
  "requiresContinuationRecomposition": false
}
```

## Rule 3: Corrections replace the current main turn

Examples:

```text
"No, I meant X."
"Actually, explain X instead."
"That is not what I asked."
```

Decision:

```json
{
  "strategy": "CancelAndRedirect",
  "pausePlayback": true,
  "cancelOriginalTurn": true,
  "requiresDeepInfraClarification": false,
  "requiresContinuationRecomposition": false,
  "rewrittenUserRequest": "X"
}
```

Then send `rewrittenUserRequest` as a new main request.

## Rule 4: Meaningful side comments should not resume raw speech

Examples:

```text
"Well yeah, but sometimes water color too."
"Sunlight probably matters too."
"That is only true indoors though."
```

Decision:

```json
{
  "strategy": "LocalBridgeAndRecomposeFromCheckpoint",
  "pausePlayback": true,
  "cancelOriginalTurn": false,
  "discardCurrentPartialSentence": true,
  "requiresDeepInfraClarification": false,
  "requiresContinuationRecomposition": true
}
```

This uses a quick local bridge or responsive feedback phrase, then recomposes the continuation.

## Rule 5: Clarification questions need DeepInfra for content

Examples:

```text
"What do you mean by liner?"
"But the water itself too, right?"
"How does depth make it blue?"
```

Decision:

```json
{
  "strategy": "ClarifyThenRecomposeFromCheckpoint",
  "pausePlayback": true,
  "cancelOriginalTurn": false,
  "discardCurrentPartialSentence": true,
  "requiresDeepInfraClarification": true,
  "requiresContinuationRecomposition": true,
  "canRunContinuationInParallel": true
}
```

## Rule 6: Follow-ups may be queued

Examples:

```text
"Can you explain sunlight too after this?"
"After this, tell me how to fix it."
```

Decision:

```json
{
  "strategy": "QueueFollowUpAfterCurrent",
  "pausePlayback": true,
  "cancelOriginalTurn": false,
  "queueAfterCurrentTurn": true,
  "requiresDeepInfraClarification": false,
  "requiresContinuationRecomposition": false
}
```

Merlin can say:

```text
"Sure, I’ll come back to that after this."
```

Then continue/recompose current answer.

---

# DeepInfra Usage Strategy

DeepInfra is used for content, not for every interruption.

## No DeepInfra

Use no DeepInfra for:

```text
- backchannels
- obvious stop/cancel
- simple playback controls
- local bridge phrases
```

## DeepInfra classification only

Use a small classification call when:

```text
- local classifier is uncertain
- interruption could be side comment, correction, or follow-up
- wrong action would be annoying
```

## DeepInfra clarification call

Use this when:

```text
- user asks a real clarification question
- user asks "right?"
- user asks "what do you mean by X?"
- user asks a related factual question
```

This call should be short and latency-sensitive.

## DeepInfra continuation recomposition call

Use this when:

```text
- meaningful interruption changed the context
- original answer should continue
- raw resume would sound bad
- current sentence was interrupted
- the explanation should now include the user's added point
```

This call can be longer and quality-sensitive.

---

# Split Calls vs Combined Calls

## Combined Call

A combined call asks DeepInfra for:

```json
{
  "clarificationReply": "...",
  "continuation": "..."
}
```

Use only when:

```text
- original answer is short
- continuation is expected to be short
- low latency matters more than maximum quality
- interruption is simple
```

## Split Calls

Split calls use:

```text
Call 1: short clarification
Call 2: recomposed continuation
```

Use when:

```text
- original answer is long
- spoken answer is 50-70 seconds or more
- continuation may need significant reasoning
- user needs quick acknowledgement first
- recomposition needs better quality
```

This is the recommended default for long spoken answers.

## Why Split Calls Are Better For Long Answers

A clarification should feel immediate:

```text
"Yes, exactly. The water itself can also affect the color."
```

The continuation may be longer:

```text
"So besides the liner, water depth, clarity, lighting, and dissolved materials all matter..."
```

If both are generated in one call, the user waits longer before hearing the short clarification.

Better:

```text
Pause
↓
short clarification call
↓
speak clarification
↓
long recomposition call in parallel or afterward
↓
speak continuation
```

---

# Parallel Execution Strategy

Best UX for long answers:

```text
1. Pause original playback.
2. Start DeepInfra clarification call.
3. When clarification text arrives, start speaking it.
4. While clarification speech is playing, start continuation recomposition call.
5. If continuation is ready when clarification ends:
      speak continuation immediately.
   Else:
      use ResponsiveFeedback:
      "Let me fold that into the answer."
      then speak continuation when ready.
```

Important:

```text
Do not overlap clarification speech and continuation speech.
Run model jobs in parallel, not audio playback.
```

---

# Prompt Templates

## 1. Ambiguous Interruption Classification Prompt

Purpose:

```text
Classify what role the user's interruption plays.
Do not answer the user's question.
Return only JSON.
```

Template:

```text
You are classifying a user's interruption during assistant speech.

The assistant was answering this original question:
"{originalUserQuestion}"

The assistant had already spoken:
"{spokenAnswerSoFar}"

The assistant was interrupted during or after:
"{lastCompletedSentence}"

The user interrupted with:
"{userInterruption}"

Classify the interruption. Do not answer it. Decide how the assistant should handle it.

Valid interruptionType values:
- Backchannel
- PassiveAgreement
- StopRequest
- CancelRequest
- Correction
- Redirect
- ClarificationQuestion
- RelatedFollowUpQuestion
- SideComment
- Disagreement
- AdditionalContext
- RepeatRequest
- NoiseOrFalsePositive
- Unknown

Valid strategy values:
- ContinueWithoutResponse
- StopPlayback
- CancelAndRedirect
- LocalBridgeAndRecomposeFromCheckpoint
- ClarifyThenRecomposeFromCheckpoint
- QueueFollowUpAfterCurrent
- AskUserToClarifyInterruption

Return strict JSON:
{
  "interruptionType": "...",
  "strategy": "...",
  "confidence": 0.0,
  "requiresDeepInfraClarification": true/false,
  "requiresContinuationRecomposition": true/false,
  "canRunContinuationInParallel": true/false,
  "rewrittenUserRequest": null or "...",
  "reason": "short reason"
}
```

## 2. Short Clarification Prompt

Purpose:

```text
Answer the interruption briefly in the context of the current answer.
```

Template:

```text
The assistant was answering this original user question:

"{originalUserQuestion}"

The assistant had already spoken this much:

"{spokenAnswerSoFar}"

The assistant was interrupted after this clean checkpoint:

"{lastCompletedSentence}"

The assistant was cut off during this partial sentence. Do not continue this partial sentence directly:

"{discardedPartialSentence}"

The user interrupted with this clarification/follow-up:

"{userInterruption}"

Task:
Answer the user's interruption briefly and naturally in context.
Keep it to 1-2 short sentences.
Do not restart the original answer.
Do not continue the full original answer yet.
Do not mention internal concepts like checkpoints, prompts, or recomposition.
If the user's interruption is asking for confirmation, directly confirm or correct it.
If the user's interruption is unclear, ask a very short clarifying question.

Return strict JSON:
{
  "replyText": "...",
  "clarificationContext": "one sentence summary of the factual/contextual point to include in the continuation",
  "shouldRecomposeContinuation": true/false
}
```

Example output:

```json
{
  "replyText": "Yes, exactly. The water itself can also affect the color, especially in deeper pools.",
  "clarificationContext": "The user pointed out that the optical properties and depth of the water itself can affect perceived pool color.",
  "shouldRecomposeContinuation": true
}
```

## 3. Continuation Recomposition Prompt

Purpose:

```text
Continue the original answer from a clean checkpoint, including the interruption context.
```

Template:

```text
You are continuing an assistant answer after the user interrupted.

Original user question:
"{originalUserQuestion}"

The assistant had already spoken:
"{spokenAnswerSoFar}"

The last safe completed sentence/checkpoint was:
"{lastCompletedSentence}"

The assistant was cut off during this partial sentence:
"{discardedPartialSentence}"

Do not continue the cut-off partial sentence directly.
Treat it as discarded.

The user interruption was:
"{userInterruption}"

The assistant replied to the interruption with:
"{clarificationReply}"

Important clarification/context to include:
"{clarificationContext}"

Task:
Continue the original answer naturally from the last safe checkpoint.
Preserve the original answer's red wire.
Incorporate the user's clarification/context.
Avoid repeating what the user already heard.
Do not restart the answer from the beginning.
Do not refer to this as an interruption unless it sounds natural.
Do not mention prompts, checkpoints, or internal state.
Start with a smooth transition.
Keep the tone conversational and spoken-friendly.

Return strict JSON:
{
  "continuationText": "...",
  "includedClarificationContext": true/false,
  "avoidedRepeatingSpokenContent": true/false
}
```

## 4. Correction / Redirect Prompt

Purpose:

```text
When the user says "No, I meant X", cancel original and answer X.
```

Template:

```text
The assistant was previously answering:

"{originalUserQuestion}"

The user interrupted and corrected the request:

"{userInterruption}"

The corrected request is:

"{rewrittenUserRequest}"

Task:
Answer the corrected request.
Do not continue the previous answer.
Acknowledge the correction briefly if useful, then answer the corrected request.

Return the answer normally.
```

This can go through the normal main request path rather than the recomposer.

---

# Speech Checkpointing

## Why checkpointing is required

Merlin cannot recompose well unless it knows what the user has already heard.

It needs:

```text
- original question
- spoken text so far
- last completed sentence
- current partial sentence
- unspoken remainder
```

Without this, continuation will either:

```text
- repeat too much
- resume awkwardly
- miss the user's clarification
- contradict earlier speech
```

## Where to track checkpoints

Best location depends on current speech architecture.

Likely relevant files:

```text
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/TtsRouter.cs
Merlin.Backend/Services/ChatterboxTtsProvider.cs
Merlin.Backend/Models/LiveAssistantTurn.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
```

Preferred approach:

```text
Track text chunks at the speech queue level.
Do not try to infer from raw audio position if avoidable.
```

## Chunk-based speech tracking

Instead of treating the answer as one giant string, split into speech chunks.

Example:

```text
Chunk 1:
"Pool water can look blue for a few reasons."

Chunk 2:
"The pool liner can strongly influence the perceived color."

Chunk 3:
"The water itself can also contribute, especially in deeper pools."
```

Track:

```text
current chunk id
current sentence index
last completed chunk
last completed sentence
currently playing sentence
```

Then interruption checkpointing becomes easier.

## Sentence boundary tracking

Use simple sentence splitting first:

```text
.
?
!
```

Do not overcomplicate at first.

Later, improve for abbreviations and Dutch/English mixed speech.

## On meaningful interruption

When interruption is meaningful:

```text
1. Pause playback.
2. Capture current chunk/sentence.
3. Mark current partial sentence/chunk as discarded.
4. Use last completed sentence as checkpoint.
5. Ask recomposer for continuation from that checkpoint.
```

## On backchannel

When interruption is a true backchannel:

```text
1. Do not discard partial sentence.
2. Continue raw playback.
3. Do not recompose.
```

---

# Audio Playback Behavior

## Physical audio rule

Only one Merlin voice should speak at a time.

Even if there are logical channels:

```text
Clarification channel
Continuation channel
Feedback channel
```

the actual audio output should be serialized:

```text
bridge/clarification
↓
continuation
↓
queued follow-up, if any
```

## Pausing vs stopping

Need two playback operations:

```text
Pause current playback:
  Used while deciding interruption.
  May resume only if interruption is backchannel/noise.

Stop/cancel current playback:
  Used when meaningful interruption requires recomposition or redirect.
  Current partial speech is discarded.
```

For meaningful interruption, use stop/cancel after classification.

## Recommended behavior

When user speech is detected during playback:

```text
1. Temporarily pause or duck playback quickly.
2. Capture user speech.
3. Classify.
4. If backchannel/noise:
      resume original playback.
   Else:
      cancel current playback from current partial sentence onward.
      process interruption.
```

This avoids the assistant talking over the user.

## Risk

Pausing too aggressively may make Merlin feel jumpy if it pauses on every tiny "yeah".

Solution:

```text
Use fast local backchannel detection if possible.
Very short backchannels can continue without full pause.
Meaningful speech gets pause.
```

---

# Conversation Focus Manager

## Purpose

The `ConversationFocusManager` maintains:

```text
What is the main conversation currently about?
What answer is being spoken?
What did the user just interrupt with?
Does the interruption replace, branch, or modify the main answer?
What continuation should happen next?
```

## Suggested state

```csharp
public sealed class ConversationThreadState
{
    public string ThreadId { get; init; } = "";
    public string ActiveTurnId { get; init; } = "";

    public string OriginalUserQuestion { get; init; } = "";
    public string? CurrentAnswerPurpose { get; init; }

    public SpokenAnswerState? ActiveSpokenAnswer { get; init; }

    public List<QueuedFollowUp> FollowUpQueue { get; init; } = new();

    public bool IsAssistantSpeaking { get; init; }
    public bool IsInterrupted { get; init; }
    public bool IsRecomposing { get; init; }
}
```

## QueuedFollowUp

```csharp
public sealed class QueuedFollowUp
{
    public string Id { get; init; } = "";
    public string UserText { get; init; } = "";
    public string RelatedTurnId { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }

    public bool RequiresDeepInfra { get; init; } = true;
}
```

## Focus actions

The manager should support:

```csharp
ConversationFocusAction ContinueMainAnswer();
ConversationFocusAction CancelAndReplaceMainTurn(string rewrittenRequest);
ConversationFocusAction RecomposeMainAnswer(SpokenAnswerCheckpoint checkpoint);
ConversationFocusAction QueueFollowUp(string userText);
ConversationFocusAction StopCurrentTurn();
```

---

# Interruption Orchestration Flows

## Flow A: Backchannel

```text
Assistant speaking
↓
User says "yeah"
↓
InterruptionCandidate created
↓
Local classifier: Backchannel
↓
Decision: ContinueWithoutResponse
↓
Playback continues
↓
No DeepInfra
↓
No recomposition
```

## Flow B: Stop

```text
Assistant speaking
↓
User says "stop"
↓
Classifier: StopRequest
↓
Decision: StopPlayback
↓
Playback stops
↓
Current turn marked cancelled/stopped
↓
No DeepInfra
```

## Flow C: Correction / Redirect

```text
Assistant speaking
↓
User says "No, I meant what is the meaning of a wife?"
↓
Classifier: Correction
↓
Decision: CancelAndRedirect
↓
Playback stops
↓
Current turn cancelled
↓
Responsive feedback:
  "Oh, you meant what is the meaning of a wife. Let me re-organise that."
↓
New main request created:
  "What is the meaning of a wife?"
↓
Normal CommandRouter/DeepInfra flow
↓
New answer spoken
```

## Flow D: Clarification + Recompose

```text
Assistant speaking
↓
User says "But sometimes the water itself too, right?"
↓
Playback paused
↓
Classifier: RelatedFollowUpQuestion / ClarificationQuestion
↓
Decision: ClarifyThenRecomposeFromCheckpoint
↓
SpokenAnswerTracker creates checkpoint
↓
Cancel old playback from partial sentence onward
↓
DeepInfra call 1: short clarification
↓
Speak clarification
↓
DeepInfra call 2: recomposed continuation
↓
Speak recomposed continuation
↓
Update active spoken answer state
```

## Flow E: Side comment + Recompose

```text
Assistant speaking
↓
User says "Well yeah, but sunlight too."
↓
Classifier: SideComment / AdditionalContext
↓
Decision: LocalBridgeAndRecomposeFromCheckpoint
↓
Responsive feedback:
  "True, that can matter too."
↓
DeepInfra continuation recomposition
↓
Speak updated continuation
```

## Flow F: Queue Follow-up

```text
Assistant speaking
↓
User says "Can you explain sunlight after this?"
↓
Classifier: RelatedFollowUpQuestion / QueueAfterCurrent
↓
Decision: QueueFollowUpAfterCurrent
↓
Responsive feedback:
  "Sure, I’ll come back to that after this."
↓
Current answer recomposes or continues
↓
After current answer completes:
  DeepInfra handles queued follow-up
```

---

# Configuration

Add options:

```text
Merlin.Backend/Configuration/InterruptionHandlingOptions.cs
```

Suggested shape:

```csharp
public sealed class InterruptionHandlingOptions
{
    public bool Enabled { get; set; } = true;

    public bool EnableLocalClassification { get; set; } = true;
    public bool EnableModelClassificationForAmbiguousCases { get; set; } = true;

    public bool EnableClarificationCalls { get; set; } = true;
    public bool EnableContinuationRecomposition { get; set; } = true;
    public bool EnableParallelContinuationRecomposition { get; set; } = true;

    public bool RecomposeAfterMeaningfulInterruption { get; set; } = true;
    public bool ResumeRawOnlyForBackchannels { get; set; } = true;

    public int ClarificationMaxTokens { get; set; } = 90;
    public int ContinuationMaxTokens { get; set; } = 500;
    public int ClassificationMaxTokens { get; set; } = 120;

    public int MinimumInterruptionTranscriptChars { get; set; } = 2;
    public double MinimumInterruptionTranscriptConfidence { get; set; } = 0.55;

    public int MaxQueuedFollowUps { get; set; } = 3;

    public int MeaningfulInterruptionPauseTimeoutMs { get; set; } = 1200;
    public int ClarificationTimeoutMs { get; set; } = 5000;
    public int ContinuationTimeoutMs { get; set; } = 15000;

    public bool EnableDiagnosticsLogging { get; set; } = true;
}
```

Add to `appsettings.json`:

```json
"InterruptionHandling": {
  "Enabled": true,
  "EnableLocalClassification": true,
  "EnableModelClassificationForAmbiguousCases": true,
  "EnableClarificationCalls": true,
  "EnableContinuationRecomposition": true,
  "EnableParallelContinuationRecomposition": true,
  "RecomposeAfterMeaningfulInterruption": true,
  "ResumeRawOnlyForBackchannels": true,
  "ClarificationMaxTokens": 90,
  "ContinuationMaxTokens": 500,
  "ClassificationMaxTokens": 120,
  "MinimumInterruptionTranscriptChars": 2,
  "MinimumInterruptionTranscriptConfidence": 0.55,
  "MaxQueuedFollowUps": 3,
  "MeaningfulInterruptionPauseTimeoutMs": 1200,
  "ClarificationTimeoutMs": 5000,
  "ContinuationTimeoutMs": 15000,
  "EnableDiagnosticsLogging": true
}
```

---

# Suggested File Additions

## Interruption services

```text
Merlin.Backend/Services/Interruption/IInterruptionOrchestrator.cs
Merlin.Backend/Services/Interruption/InterruptionOrchestrator.cs

Merlin.Backend/Services/Interruption/IInterruptionClassifier.cs
Merlin.Backend/Services/Interruption/InterruptionClassifier.cs

Merlin.Backend/Services/Interruption/IInterruptionCaptureService.cs
Merlin.Backend/Services/Interruption/InterruptionCaptureService.cs

Merlin.Backend/Services/Interruption/IAnswerRecomposer.cs
Merlin.Backend/Services/Interruption/AnswerRecomposer.cs

Merlin.Backend/Services/Interruption/InterruptionCandidate.cs
Merlin.Backend/Services/Interruption/InterruptionDecision.cs
Merlin.Backend/Services/Interruption/InterruptionType.cs
Merlin.Backend/Services/Interruption/InterruptionHandlingStrategy.cs

Merlin.Backend/Services/Interruption/ClarificationRequest.cs
Merlin.Backend/Services/Interruption/ClarificationResult.cs
Merlin.Backend/Services/Interruption/ContinuationRecompositionRequest.cs
Merlin.Backend/Services/Interruption/ContinuationRecompositionResult.cs
```

## Conversation/focus services

```text
Merlin.Backend/Services/Conversation/IConversationFocusManager.cs
Merlin.Backend/Services/Conversation/ConversationFocusManager.cs
Merlin.Backend/Services/Conversation/ConversationThreadState.cs
Merlin.Backend/Services/Conversation/ConversationBranch.cs
Merlin.Backend/Services/Conversation/QueuedFollowUp.cs
```

## Spoken answer tracking

```text
Merlin.Backend/Services/Speech/ISpokenAnswerTracker.cs
Merlin.Backend/Services/Speech/SpokenAnswerTracker.cs
Merlin.Backend/Services/Speech/SpokenAnswerState.cs
Merlin.Backend/Services/Speech/SpokenAnswerCheckpoint.cs
```

Alternative grouping if preferred:

```text
Merlin.Backend/Services/Interruption/SpokenAnswerTracker.cs
```

## Configuration

```text
Merlin.Backend/Configuration/InterruptionHandlingOptions.cs
```

## Tests

```text
Merlin.Backend.Tests/InterruptionClassifierTests.cs
Merlin.Backend.Tests/InterruptionOrchestratorTests.cs
Merlin.Backend.Tests/AnswerRecomposerTests.cs
Merlin.Backend.Tests/ConversationFocusManagerTests.cs
Merlin.Backend.Tests/SpokenAnswerTrackerTests.cs
Merlin.Backend.Tests/InterruptionIntegrationTests.cs
```

---

# Integration Points

## AssistantSpeechPlaybackService

Likely modifications:

```text
- expose current playback state
- support pause/resume/cancel by turn id
- notify SpokenAnswerTracker when chunks/sentences start and complete
- expose current spoken checkpoint
```

Careful:

```text
Do not break existing TTS queue behavior.
Do not allow multiple Merlin speech streams to overlap.
```

## LiveAssistantTurnService

Likely modifications:

```text
- update state to Interrupted
- update state to Recomputing/Recomposing if such state exists or is added
- preserve correlation/turn id links
```

Potential new state:

```text
RecomposingAnswer
HandlingInterruption
ClarifyingInterruption
```

Only add states if needed. Avoid state explosion.

## SpeechPresence / BargeIn

Likely modifications:

```text
- route confirmed user speech during playback into InterruptionCaptureService
- distinguish backchannel candidates from hard interruption candidates if possible
- avoid self-echo false positives
```

Do not weaken existing self-echo gates.

## CommandRouter

For correction/redirect:

```text
InterruptionOrchestrator may create a new AssistantRequest
and route it through CommandRouter or the same core request path.
```

Avoid recursive mess.

Preferred:

```text
InterruptionOrchestrator calls an IRequestTurnRunner abstraction
```

if available or added later.

First version can call existing router carefully, but avoid infinite loops.

## ResponsiveFeedbackOrchestrator

Use it for:

```text
- "Good point, let me include that."
- "Let me re-organise that."
- "I’ll come back to that after this."
- "Let me fold that into the answer."
```

Do not hard-code all bridge phrases in interruption classes.

---

# Implementation Phases

## Phase 0: Preserve Current Behavior

Before adding new behavior:

- Run all tests.
- Save current interruption behavior observations.
- Inspect logs around:
  - barge-in
  - speech presence
  - playback pause/cancel
  - self-echo suppression
- Identify exact path from detected speech to current interruption handling.

Success criteria:

```text
Current baseline known.
No code changed yet.
```

## Phase 1: Add Data Models And Local Classifier

Add:

```text
InterruptionType
InterruptionHandlingStrategy
InterruptionCandidate
InterruptionDecision
InterruptionClassifier
```

Implement local rules only.

Test:

```text
"yeah" -> ContinueWithoutResponse
"stop" -> StopPlayback
"no I meant X" -> CancelAndRedirect
"what do you mean by X" -> ClarifyThenRecomposeFromCheckpoint
"but X too right" -> ClarifyThenRecomposeFromCheckpoint
```

No runtime integration yet.

## Phase 2: Add SpokenAnswerTracker

Track:

```text
Original question
Spoken so far
Last completed sentence
Current partial sentence
Safe checkpoint
```

Start simple with chunk/sentence tracking.

Test:

```text
If interrupted mid-sentence, checkpoint uses last completed sentence.
Partial sentence is marked discarded.
```

## Phase 3: Add AnswerRecomposer

Add prompt builders and fake model tests.

Implement:

```text
BuildClarificationPrompt
BuildContinuationRecompositionPrompt
ParseClarificationJson
ParseContinuationJson
```

Use fake DeepInfra client in tests.

Do not integrate live playback yet.

## Phase 4: Add InterruptionOrchestrator

Implement flows:

```text
Backchannel -> continue
Stop -> stop
Correction -> cancel and route new request
Clarification -> clarify + recompose
Side comment -> bridge + recompose
Queue follow-up -> queue + continue/recompose
```

Use fakes for:

```text
playback service
DeepInfra client
responsive feedback
focus manager
spoken answer tracker
```

## Phase 5: Integrate With Playback/BargeIn

Route actual speech interruptions into orchestrator.

Start behind config:

```json
"InterruptionHandling": {
  "Enabled": false
}
```

Then enable in development.

## Phase 6: Enable Recomposition For Meaningful Interruptions

Turn on:

```json
"RecomposeAfterMeaningfulInterruption": true
```

Validate:

```text
No raw mid-sentence resume after meaningful interruption.
Backchannels still continue.
```

## Phase 7: Parallel Clarification/Continuation

After sequential flow works, optimize:

```text
Clarification call first
Start continuation call while clarification is being spoken
Serialize audio output
Use feedback if continuation is not ready
```

## Phase 8: Queued Follow-ups

Add follow-up queue behavior.

Example:

```text
"Can you explain sunlight after this?"
```

Expected:

```text
acknowledge queue
finish/recompose current answer
then answer follow-up
```

---

# Testing Plan

## InterruptionClassifierTests

Test cases:

```text
"yeah" -> Backchannel / ContinueWithoutResponse
"mhm" -> Backchannel / ContinueWithoutResponse
"right" -> PassiveAgreement / ContinueWithoutResponse
"stop" -> StopRequest / StopPlayback
"cancel" -> CancelRequest / StopPlayback or CancelAndRedirect
"no I meant what is X" -> Correction / CancelAndRedirect
"actually explain Y" -> Correction / CancelAndRedirect
"what do you mean by liner" -> ClarificationQuestion / ClarifyThenRecomposeFromCheckpoint
"but the water itself too right" -> RelatedFollowUpQuestion / ClarifyThenRecomposeFromCheckpoint
"well yeah but sunlight too" -> SideComment / LocalBridgeAndRecomposeFromCheckpoint
```

## SpokenAnswerTrackerTests

Test:

```text
Given answer:
"Pool water can look blue for several reasons. Due to the color of the pool liner, it can look more blue."

If interrupted during:
"Due to the color of the pool li..."

Then:
LastCompletedSentence = "Pool water can look blue for several reasons."
DiscardedPartialSentence starts with "Due to the color..."
```

## AnswerRecomposerTests

Use fake model output.

Test:

```text
Clarification prompt contains:
- original question
- spoken answer so far
- last completed sentence
- discarded partial sentence
- user interruption

Continuation prompt contains:
- clarification reply
- clarification context
- instruction not to repeat spoken content
- instruction not to continue partial sentence directly
```

## InterruptionOrchestratorTests

### Backchannel

Expect:

```text
playback not cancelled
DeepInfra not called
responsive feedback not spoken
```

### Stop

Expect:

```text
playback cancelled
turn marked cancelled
DeepInfra not called
```

### Correction

Expect:

```text
playback cancelled
old turn cancelled
new request routed
bridge feedback spoken
```

### Clarification

Expect:

```text
playback paused/cancelled from checkpoint
clarification model call made
clarification spoken
continuation recomposition call made
continuation spoken
```

### Side comment

Expect:

```text
bridge feedback spoken
continuation recomposition call made
no clarification call if not needed
```

## Integration Tests

Simulate:

```text
Assistant is speaking long answer
User interrupts mid-sentence
System captures interruption
Classifier returns clarification decision
Old playback does not resume mid-sentence
Continuation starts naturally
```

## Regression Tests

Ensure:

```text
"yeah" does not cancel playback
self-echo does not trigger interruption
assistant feedback speech does not trigger another interruption
fast final responses still work
responsive feedback does not overlap with continuation speech
```

---

# Logging And Diagnostics

Log these events:

```text
interruption_candidate_detected
interruption_candidate_rejected
interruption_classified
interruption_decision_selected
playback_paused_for_interruption
playback_resumed_after_backchannel
playback_cancelled_for_recomposition
clarification_call_started
clarification_call_completed
continuation_recomposition_started
continuation_recomposition_completed
continuation_recomposition_failed
follow_up_queued
follow_up_started
```

Include:

```text
correlationId
turnId
interruptionType
strategy
confidence
requiresClarification
requiresRecomposition
checkpoint length
discarded partial length
```

Do not log full user text by default unless current diagnostics already do that.

---

# Failure Handling

## Clarification call fails

Fallback:

```text
"Good point. Let me fold that into the answer."
```

Then either:

```text
- try continuation recomposition anyway, or
- ask user to repeat if context is insufficient
```

## Continuation recomposition fails

Fallback options:

1. If old unspoken remainder exists and interruption was minor:

```text
continue from next clean sentence, not mid-sentence
```

2. If old remainder is unsafe:

```text
"Sorry, I lost the thread there. Let me restart that part."
```

Then call normal answer generation with original question + interruption context.

## Classification uncertain

Fallback:

```text
pause
ask very short clarification:
"Did you want me to change direction, or should I keep going?"
```

Do not overuse this. It can become annoying.

## User interrupts while clarification is being spoken

Treat as a new interruption on the clarification channel.

Possible rules:

```text
Stop/cancel -> stop
Correction -> redirect
Backchannel -> continue clarification
Another clarification -> merge into recomposition context
```

First version can keep this simple:

```text
If already handling interruption, only stop/cancel/correction are handled.
Other speech is queued or ignored with diagnostics.
```

---

# UX Rules

## Rule: never resume a cut-off partial sentence after meaningful interruption

Bad:

```text
"...pool li—"
"Yes, water matters."
"—ner makes it blue..."
```

Good:

```text
"...pool li—"
"Yes, water matters."
"So there are two effects..."
```

## Rule: backchannels should not make Merlin jumpy

```text
"yeah"
"mhm"
"right"
```

should not trigger full pause/recompose.

## Rule: keep clarification short

Clarification should be:

```text
1-2 sentences
spoken-friendly
not a full answer
```

## Rule: preserve the red wire

Continuation should:

```text
continue original answer
include user's point
avoid repeating too much
not restart from zero
not ignore the interruption
```

## Rule: do not overtalk

If continuation is not ready:

```text
use one short feedback phrase
then wait
```

Do not keep filling silence endlessly.

---

# Acceptance Criteria

The redesign is successful when:

- `"yeah"` does not stop Merlin.
- `"stop"` stops Merlin without DeepInfra.
- `"no, I meant X"` cancels old answer and starts X.
- meaningful interruptions do not resume mid-sentence.
- clarification questions get a quick clarification answer.
- long answers use split clarification + continuation recomposition.
- continuation includes the user's clarification/context.
- raw old speech is resumed only for true backchannels/noise.
- Merlin preserves the main conversation thread.
- DeepInfra is not called for every tiny interruption.
- audio output does not overlap chaotically.
- self-echo does not trigger fake interruptions.
- existing responsive feedback/progress behavior still works.
- the feature can be disabled by config.

---

# Recommended First Implementation Scope

Do this first:

```text
- Add local InterruptionClassifier.
- Add InterruptionDecision model.
- Add SpokenAnswerTracker checkpoint model.
- Add AnswerRecomposer prompt builder.
- Add tests.
```

Do not integrate with live playback yet.

## First live integration

Then:

```text
- handle "yeah" as continue
- handle "stop" as stop
- handle "no I meant X" as cancel/redirect
```

## Then recomposition

Then:

```text
- handle clarification question
- generate short clarification
- generate recomposed continuation
- prevent raw mid-sentence resume
```

---

# Recommended Final Architecture

```text
SpeechPresence / BargeIn
  ↓
InterruptionCaptureService
  ↓
InterruptionClassifier
  ├── Local rules
  └── Optional DeepInfra classification for ambiguous cases
  ↓
InterruptionDecision
  ↓
ConversationFocusManager
  ↓
InterruptionOrchestrator
  ├── AssistantSpeechPlaybackService
  ├── SpokenAnswerTracker
  ├── ResponsiveFeedbackOrchestrator
  ├── AnswerRecomposer
  └── CommandRouter for redirects/follow-ups
  ↓
Clarification speech
  ↓
Recomposed continuation speech
```

---

# Final Notes

This redesign is not just about interruption.

It is about conversational control.

The goal is:

```text
Merlin listens while speaking,
understands why the user interrupted,
answers the interruption when needed,
and continues the original thought naturally.
```

The most important behavior change is:

```text
Meaningful interruption means recomposition from a clean checkpoint,
not raw resume.
```

That is what will make Merlin feel like a real conversation partner instead of a playback machine.
