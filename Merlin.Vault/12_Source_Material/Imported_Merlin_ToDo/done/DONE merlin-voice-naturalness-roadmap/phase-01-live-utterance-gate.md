---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/DONE merlin-voice-naturalness-roadmap/phase-01-live-utterance-gate.md
classification: implementation-plan
related_features:
  - Voice Interruption System
status: implemented
imported_to_vault: true
---

# Phase 01 — Live Utterance Gate

## Purpose

Merlin already has strong low-level speech plumbing: microphone capture, VAD, ring buffers, barge-in capture, self-echo suppression, STT, active turn state, and live utterance routing. The missing layer is a higher-level intelligence gate that decides whether a transcript is actually useful before it reaches command routing, DeepInfra, playback control, or tool execution.

This phase adds a **Live Utterance Gate** between raw STT output and the rest of the assistant pipeline.

The core principle:

```text
Every transcript is evaluated.
Not every transcript deserves action.
Unknown or malformed text must not default to GeneralConversation.
Short but meaningful phrases must not be ignored.
```

## Why this matters

Real voice input is messy. Merlin will often capture partial or malformed transcripts such as:

```text
uh
yeah but
hey google
hey its infant
open the uh
sorry I meant
no no wait
```

Today, a rough failure mode is:

```text
messy capture
→ STT transcript
→ unknown/general conversation
→ DeepInfra call
→ weird response or wasted latency
```

That is too eager. Merlin needs a judgement layer that can decide:

```text
Is this meant for Merlin?
Is this complete enough to act on?
Is this a command, correction, cancellation, playback control, answer steering, or noise?
Should Merlin wait, ask clarification, ignore, or execute?
```

## Explicit non-goals

Do not implement full Live Answer Steering in this phase.

Do not implement browser rollback in this phase.

Do not implement a large LLM-based classifier as the first version.

Do not send ambiguous garbage to DeepInfra just to “see what it means.”

Do not ignore short phrases just because they are short.

## Existing repo areas to inspect first

Inspect these files and folders before coding:

```text
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/BargeIn/InterruptionClassifier.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend/Models/LiveAssistantTurn.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend.Tests/
```

The implementation should reuse existing `UserUtterance`, `UtteranceRouteDecision`, `UtteranceRouteKind`, and active turn state where reasonable. Add new models only where they clarify responsibility.

## Suggested location

Create a dedicated folder, unless an existing equivalent already exists:

```text
Merlin.Backend/Services/LiveUtterance/
  ILiveUtteranceGate.cs
  LiveUtteranceGate.cs
  LiveUtteranceGateModels.cs
  LiveUtteranceGateOptions.cs
```

Register it in dependency injection in the same style as other backend services.

## Conceptual pipeline

Target flow:

```text
Mic/VAD/STT
→ UserUtterance
→ LiveUtteranceGate
→ BargeIn route / playback control / command router / DeepInfra / clarification / ignore
```

The gate should be called before unknown text is allowed to become general conversation.

## Gate inputs

The gate should receive enough context to make state-aware decisions.

Suggested input model:

```csharp
public sealed class LiveUtteranceGateInput
{
    public required UserUtterance Utterance { get; init; }
    public LiveAssistantTurn? ActiveTurn { get; init; }
    public required string CurrentSystemState { get; init; }
    public required bool AssistantWasSpeaking { get; init; }
    public required bool IsIdleListening { get; init; }
    public string? PendingCommandDescription { get; init; }
    public string? RecentToolName { get; init; }
    public string? RecentToolTarget { get; init; }
    public IReadOnlyList<string> RecentTranscripts { get; init; } = Array.Empty<string>();
    public double? SttConfidence { get; init; }
    public double? AudioSpeechConfidence { get; init; }
}
```

Use the repo’s actual types and style; the above is a guide, not a requirement.

## Gate outputs

Suggested decisions:

```csharp
public enum LiveUtteranceGateDecisionKind
{
    AcceptPlaybackControl,
    AcceptCancellation,
    AcceptReplacement,
    AcceptCorrection,
    AcceptContinuation,
    AcceptNewRequest,
    AcceptStatusQuestion,
    HoldForMoreSpeech,
    AskClarification,
    IgnoreAsNoise,
    IgnoreAsEcho,
    IgnoreAsWakewordLeak,
    IgnoreAsGarbageTranscript,
    Unknown
}
```

Suggested result:

```csharp
public sealed class LiveUtteranceGateResult
{
    public required LiveUtteranceGateDecisionKind Decision { get; init; }
    public required double Confidence { get; init; }
    public required string Reason { get; init; }
    public string? NormalizedText { get; init; }
    public TimeSpan? HoldWindow { get; init; }
    public string? ClarificationPrompt { get; init; }
    public bool ShouldCallDeepInfra { get; init; }
    public bool ShouldRouteToCommandRouter { get; init; }
    public bool ShouldAffectPlayback { get; init; }
}
```

## Required policy rules

### 1. Control phrases always survive

These should be accepted even if short:

```text
stop
pause
wait
hold on
continue
go on
resume
cancel
cancel that
never mind
forget it
no
yes
```

State matters. For example, `no` is meaningful during confirmation or active playback, but may be ignored when idle with no active context.

### 2. Correction phrases survive during active flows

Examples:

```text
actually
I mean
I meant
sorry I meant
not Facebook
Google instead
open Google instead
no, Google
```

During an active OpenUrl flow, a single word like `Google` may be enough to accept as replacement if Facebook or browser opening is the current context.

### 3. Incomplete phrases should hold the floor

Examples:

```text
uh
um
well
yeah but
no no wait
sorry I meant
can you open
but that means
```

Do not immediately ask clarification. Hold briefly for more speech, especially during `Speaking`, `PausedByUser`, `FloorYieldPause`, or tool correction states.

### 4. Unknown text must not become GeneralConversation by default

This is the most important behavioral change.

Bad:

```text
"hey its infant"
→ GeneralConversation
→ DeepInfra
```

Better:

```text
"hey its infant"
→ HoldForMoreSpeech or IgnoreAsGarbageTranscript or AskClarification
→ DeepInfraCalled = false
```

### 5. Clarification is contextual and not constant

Merlin should not ask clarification every time a random fragment appears. Ask only when user intent is likely but unclear.

Good clarification moments:

```text
User clearly addressed Merlin but transcript is malformed.
Merlin is paused and waiting for the user.
A command was likely attempted but target/action is unclear.
Ambiguity repeats multiple times.
```

Avoid clarification for likely TV/background noise.

### 6. Clarification phrases should be short

Suggested prompts by context:

```text
General unclear: "Sorry, I didn't catch that."
Paused correction: "What should I change?"
Unclear command: "What would you like me to open?"
Unclear confirmation: "Was that yes or no?"
Unclear replacement: "What should I use instead?"
```

A personality phrase like `Sorry sir, I didn't understand. Can you clarify?` can be used, but avoid overusing long responses.

### 7. Wakeword leakage must be handled

Phrases like `hey google` or malformed variants should not automatically become Merlin conversations unless Merlin is configured to treat them as activation or the surrounding context makes them meaningful.

## State-specific behavior

### IdleListening

Be conservative. Unknown low-confidence fragments should usually be ignored or held, not sent to DeepInfra.

Accept clear commands/questions:

```text
open google
what time is it
tell me about X
```

Reject or hold malformed fragments:

```text
hey its infant
uh google maybe
```

### Speaking

The gate should cooperate with playback control.

```text
stop → AcceptPlaybackControl / hard stop
wait / hold on / no no → AcceptPlaybackControl / floor yield
yeah but... → HoldForMoreSpeech or future answer steering candidate
random garbage → likely pause only if audio was confidently user speech, but do not call DeepInfra
```

### PausedByUser / FloorYieldPause

Expect useful follow-up, but still filter garbage.

```text
"the rollback system, not interruption" → AcceptCorrection or future answer steering
"continue" → AcceptContinuation
"cancel that" → AcceptCancellation
"hey its infant" → HoldForMoreSpeech, then AskClarification after timeout
```

### AwaitingToolCommit / PlanningTool

Focus on replacement/cancel/confirm.

```text
stop → pause/clarify
cancel that → cancel
Google instead → replacement
continue → commit/resume if pending confirmation exists
random conversation → hold/ignore unless clear
```

### ExecutingTool

Accept cancellation/status/new request when clear. Do not let malformed text become unrelated DeepInfra chatter.

## Transcript assembly

Implement a small transcript assembly/hold mechanism if not already present.

Need behavior:

```text
"sorry I meant" → hold
next: "Google" → combined intent: "sorry I meant Google"
```

Do not overcomplicate this. A short in-memory recent transcript buffer is enough.

Suggested hold windows:

```text
Incomplete phrase during active flow: 1000–2500 ms
FloorYieldPause: 5000–10000 ms before clarification prompt
Idle malformed phrase: short hold or silent ignore
```

Make values configurable.

## Logging requirements

Add structured logs for every gate decision:

```text
LiveUtteranceGateEvaluated
- text
- normalizedText
- activeTurnId
- correlationId
- stateWhenCaptured
- assistantWasSpeaking
- decision
- confidence
- reason
- shouldCallDeepInfra
- shouldRouteToCommandRouter
- holdWindowMs
- clarificationPrompt
```

This is critical for debugging. You want to know whether Merlin ignored something, held it, clarified, or routed it.

## Tests to add

Add unit tests for `LiveUtteranceGate`.

Test cases:

1. `stop` during `Speaking` is accepted as playback control.
2. `stop` during `AwaitingToolCommit` is accepted as pause/clarify or cancellation route depending current policy.
3. `cancel that` during active flow is accepted as cancellation.
4. `sorry I meant Google` during OpenUrl active flow is accepted as replacement.
5. `Google` during active OpenUrl Facebook flow is accepted as replacement.
6. `Google` while idle with no context is not blindly accepted as command unless configured.
7. `hey its infant` while idle is ignored/held and does not call DeepInfra.
8. `hey its infant` during `FloorYieldPause` holds, then clarifies after timeout.
9. `yeah but` during speaking holds for more speech.
10. `continue` during paused state is accepted as continuation.
11. Unknown malformed text does not default to GeneralConversation.
12. Short valid phrases are not rejected because of length.

Integration tests if feasible:

1. Barge-in STT result goes through LiveUtteranceGate before DeepInfra.
2. Unknown garbage transcript does not trigger DeepInfra.
3. Clarification prompt is emitted only when state and confidence justify it.

## Acceptance criteria

This phase is done when:

```text
- A dedicated Live Utterance Gate exists and is DI-registered.
- BargeInCoordinator or the live utterance routing path calls it before routing unknown text.
- Unknown/malformed transcripts no longer default directly to GeneralConversation.
- Short important control/correction phrases still work.
- HoldForMoreSpeech exists for incomplete phrases.
- Contextual AskClarification exists.
- Logs clearly show gate decisions.
- Tests cover the major route decisions.
- Existing tests still pass.
```

