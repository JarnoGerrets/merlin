---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/Merlin_Correction_Regeneration_Token_And_Short_Stop_Fix.md
classification: implementation-plan
related_features:
  - Correction Layer
status: current
imported_to_vault: true
---

# Merlin Correction Regeneration Token Ownership And Short Hard-Stop Capture Fix

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Correction_Regeneration_Token_And_Short_Stop_Fix.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Fix two log-proven interruption bugs in Merlin:

1. **Correction regeneration is created correctly, then cancelled immediately.**
2. **Short hard-stop phrases like "stop" may never reach STT/classification.**

These are separate from semantic correction rewriting.

Before improving "I meant" / "I mean" semantic quality, Merlin must first make sure:

```text
correctly detected corrections can actually complete
```

and:

```text
short emergency stop phrases can reach the hard-stop path
```

---

## Current observed behavior from logs

### Problem A - Correction regeneration dies immediately

Observed sequence:

```text
VAD detects user speech
Speaker ducking starts
Audio is captured
STT hears correction text
Classifier marks it as Correction
BargeInCoordinator selects Correction
Correction regeneration requested
WebSocketHandler creates a new correction correlation id
Corrected request is dispatched through normal pipeline
Command received: "i mean family car"
It routes to general_conversation / DeepInfra
DeepInfra fails: The operation was canceled
Live turn processing cancelled for the new correction correlation id
```

This means correction detection and dispatch are working, but the **new correction request is being cancelled immediately**.

Likely root cause:

```text
The new correction request is linked to the old barge-in/capture/interruption cancellation token.
That old token is cancelled as part of ending capture/stopping interruption handling.
The new correction turn is born with a dying or already-cancelled token.
```

Correct behavior:

```text
Old barge-in/capture token:
  may cancel old capture/interruption work
  must not own the regenerated correction request lifetime

New correction request:
  gets new correlation id
  gets fresh live turn token
  may link only to WebSocket/session lifetime, not the old barge-in capture token
```

---

### Problem B - "stop" and "please stop" may never reach classification

Observed sequence for a failed "stop" run:

```text
Barge-in monitor starts
Backend playback starts
Microphone capture starts
Chatterbox continues generating chunks
No VAD possible speech detected
No speaker ducking started
No gated STT started
No interruption classifier result
No hard-cancel action
No assistant turn cancelled
```

So in that run, "stop" did not fail because the classifier rejected it.

It likely failed earlier:

```text
User says "stop"
↓
Audio burst is too short for current VAD trigger settings
↓
No interruption capture starts
↓
STT never hears "stop"
↓
hard-stop classifier never runs
```

Likely explanation:

```text
Current VAD trigger settings are tuned for longer corrections.
A one-word "stop" may be shorter than minimum sustained-speech thresholds.
"Merlin stop" works better because it is longer audio.
```

Correct behavior:

```text
while assistant is speaking:
  short hard-stop bursts should have a faster candidate path
  "stop" should not need to be as long as a normal correction
  idle wake-word policy should remain stricter
```

---

## Important scope distinction

This task is **not** the same as:

```text
semantic correction rewrite
web search
capability routing
instant ducking volume application
```

This task specifically fixes:

```text
1. regenerated correction request cancellation-token ownership
2. short hard-stop speech detection/capture path
```

If the instant ducking/hard-stop TODO is also being worked on, coordinate carefully. The short-stop work in this file can overlap with hard-stop recognition, but this task is focused on making short stop utterances **reach** the classifier/path at all.

---

## Non-goals

Do not implement these here:

- semantic "I meant" vs "I mean" rewriting
- DeepInfra correction rewrite
- web search
- web research
- Codex integration
- file/email/calendar capabilities
- major routing redesign
- new TTS/STT provider
- full VAD/AEC replacement
- destructive action support

Do not bypass the normal request pipeline.

Do not make regenerated correction requests call DeepInfra directly.

---

## Existing functionality to preserve

Merlin reportedly already has:

```text
live turn cancellation
stale response suppression
correction regeneration
CorrectionRequestBuilder
new correction correlation id format:
  {oldCorrelationId}:correction:{guid}
normal correction dispatch through:
  WebSocketHandler -> CommandRouter -> HybridIntentParser -> ToolRegistry -> ITool
```

Preserve all of this.

Hard stop must still:

```text
clear playback
cancel active live turn
suppress old response
not dispatch correction regeneration
```

Correction must still:

```text
clear playback
cancel old live turn
dispatch corrected request through normal pipeline
use a new correlation id
```

Backchannel/clarification must still:

```text
not cancel unless classified as hard stop/correction
not dispatch correction regeneration
preserve current pause/resume behavior
```

---

# Part 1 - Fix correction regeneration cancellation-token ownership

## Goal

A regenerated correction request must be able to complete even if the old barge-in/capture/interruption token is cancelled.

---

## Required investigation

Inspect:

```text
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/BargeInInterfaces.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/CorrectionRequestBuilder.cs
Merlin.Backend/Services/Interfaces/ICorrectionRequestBuilder.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend/Services/Interfaces/ILiveAssistantTurnService.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/LocalAIChatService.cs
Merlin.Backend.Tests/CorrectionRegenerationTests.cs
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/WebSocketHandlerTests.cs
```

Search for:

```text
DispatchCorrectionRegenerationAsync
ProcessAndEmitLiveRequestAsync
ProcessLiveRequestAsync
CorrectionRegeneration
CorrectionRequested
CorrectionText
NewCorrelationId
CancellationToken
CreateLinkedTokenSource
CancelTurnAsync
BeginTurn
ShouldEmit
Live turn processing cancelled
Operation was canceled
```

Determine exactly:

1. Which token is passed from `BargeInCoordinator` to correction regeneration.
2. Which token is passed into `WebSocketHandler.ProcessAndEmitLiveRequestAsync`.
3. Whether the new correction request token is linked to the old barge-in/capture token.
4. Whether that token can already be cancelled by the time DeepInfra starts.
5. Which token should represent the WebSocket/session lifetime.
6. Which token should represent only old capture/interruption lifecycle.
7. Whether `LiveAssistantTurnService.BeginTurn` creates a fresh token for the new correlation id.
8. Whether `CommandRouter`/DeepInfra receive the new live turn token or the old cancellation token.

Mention these findings in the final report.

---

## Correct lifecycle

Expected correction regeneration lifecycle:

```text
old assistant output is speaking
↓
user interrupts with correction
↓
barge-in capture token controls only capture/classification lifecycle
↓
correction classified
↓
old playback cleared
↓
old live turn cancelled if still active
↓
corrected request built
↓
new correlation id generated
↓
new request dispatched
↓
new live turn created
↓
new live turn gets fresh CTS/token
↓
new token may link to WebSocket/session lifetime
↓
new token must not link to old barge-in/capture token
↓
DeepInfra/tool/routing receives new token
↓
new answer completes unless user interrupts again or socket closes
```

---

## Token ownership rule

Use separate cancellation lifetimes:

### Old barge-in/capture token

Allowed to cancel:

```text
capture
VAD session
gated STT
interruption classification
old interruption handling work
```

Not allowed to cancel:

```text
new regenerated correction request
```

### Old live turn token

Allowed to cancel:

```text
old backend model/tool work if still active
old response/polishing/speech enqueue
```

Not allowed to cancel:

```text
new regenerated correction request
```

### WebSocket/session token

Allowed to cancel:

```text
all request work when client disconnects/server shuts down
including regenerated correction requests
```

### New correction live turn token

Allowed to cancel:

```text
new corrected request work
new DeepInfra/local generation
new tool work
new polishing
new speech enqueue
```

---

## Implementation direction

Find where the regenerated correction is dispatched.

It likely resembles:

```csharp
await DispatchCorrectionRegenerationAsync(..., cancellationToken);
```

or:

```csharp
await ProcessAndEmitLiveRequestAsync(correctedRequest, ..., cancellationToken);
```

If that `cancellationToken` comes from barge-in/capture/interruption handling, do not pass it as the new request lifetime.

Instead, use one of these:

### Preferred approach

Pass a stable session/WebSocket lifetime token to correction dispatch.

```csharp
await ProcessAndEmitLiveRequestAsync(
    correctedRequest,
    context,
    websocketSessionCancellationToken);
```

Inside `ProcessAndEmitLiveRequestAsync`, let `LiveAssistantTurnService.BeginTurn` create/link the new live turn token.

### If no stable session token exists

Introduce one in the WebSocket/session context.

Suggested model:

```csharp
public sealed record AssistantRequestDispatchContext(
    string SessionId,
    WebSocket WebSocket,
    CancellationToken SessionCancellationToken,
    // existing fields...
);
```

or extend the existing context.

### If dispatch must be called from BargeInCoordinator

Do not give BargeInCoordinator ownership of the new request token.

Instead, have it raise a correction event/result that WebSocketHandler handles with the proper session token.

---

## Required guard

If the old barge-in token is cancelled immediately after correction classification, the new correction request must still complete.

Test this explicitly.

Pseudo-test:

```text
oldCaptureCts = new CancellationTokenSource()
correction is classified
dispatch correction regeneration
oldCaptureCts.Cancel()
new corrected request still reaches fake CommandRouter/DeepInfra
new response is sent
```

---

## Required tests

Add/update tests:

```text
Correction_regeneration_uses_fresh_live_turn_token
Correction_regeneration_does_not_link_to_old_capture_token
Cancelling_old_barge_in_token_does_not_cancel_new_correction_request
Cancelling_socket_session_token_does_cancel_new_correction_request
Old_cancelled_correlation_still_suppresses_old_response
New_correction_correlation_can_emit_response
New_correction_deepinfra_request_is_not_immediately_cancelled
Correction_family_card_to_family_car_completes
Correction_i_mean_voice_purpose_completes
Hard_stop_still_does_not_dispatch_new_request
Backchannel_still_does_not_dispatch_new_request
```

Use fake delayed services instead of real DeepInfra.

---

# Part 2 - Add short hard-stop capture path

## Goal

While Merlin is speaking, one-word hard stops like:

```text
stop
abort
cancel
wait
```

should be able to trigger interruption capture/STT/classification even if they are too short for the normal correction VAD threshold.

---

## Required investigation

Inspect:

```text
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/*
Merlin.Backend/Configuration/*
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/BargeInTests.cs
```

Search for:

```text
MinimumSpeechMs
MinSpeechMs
SustainedSpeechMs
Vad
VoiceActivity
PossibleSpeech
Trigger
TriggerBuffer
Trigger buffer captured
Gated STT
HardStop
RequireWakeWordForFirstVersion
WakeWords
AssistantSpeaking
Playback
```

Determine:

1. What VAD/speech thresholds are currently used for barge-in trigger.
2. Whether there is a minimum sustained speech duration around 250-350ms.
3. Whether "stop" can be shorter than that.
4. Whether the system has a separate "assistant is currently speaking" state.
5. Whether hard-stop detection can be attempted on shorter audio windows while speaking.
6. How to avoid false positives from random noise or Merlin's own TTS leakage.

Mention findings in final report.

---

## Desired policy

When assistant is speaking:

```text
short speech burst can start a hard-stop candidate capture
```

When assistant is idle:

```text
keep normal wake-word policy and normal thresholds
```

This is not global wake-word removal.

It is an emergency interruption path only while Merlin is actively speaking/outputting audio.

---

## Suggested approach

Add a fast hard-stop candidate path parallel to normal correction capture.

### Normal correction capture

Keep existing behavior for longer corrections:

```text
VAD possible speech for normal threshold
capture enough audio for sentence
run STT
classify correction/backchannel/clarification/etc.
```

### Fast hard-stop candidate path

While assistant is speaking:

```text
shorter VAD burst detected
↓
capture short audio window
↓
run gated STT/classifier optimized for hard-stop phrases
↓
if hard stop recognized:
    clear playback
    cancel live turn
    suppress old response
↓
if not hard stop:
    do not overreact
    optionally continue normal capture if speech continues
```

---

## Suggested config

Add or extend config:

```json
"BargeIn": {
  "FastHardStop": {
    "Enabled": true,
    "MinSpeechMs": 120,
    "CaptureWindowMs": 900,
    "PostSpeechPaddingMs": 150,
    "RequireAssistantSpeaking": true,
    "RequireWakeWord": false,
    "MinSttConfidence": 0.45
  }
}
```

Adapt to current config style.

The exact values should be tunable.

Suggested starting values:

```text
MinSpeechMs: 120-180
CaptureWindowMs: 700-1200
PostSpeechPaddingMs: 100-200
```

---

## Hard-stop phrase recognition

This task can reuse or coordinate with natural hard-stop recognition.

The fast path should look for phrases like:

```text
stop
please stop
stop talking
abort
abort that
cancel
cancel that
wait
hold on
pause
shut up
quiet
enough
never mind
```

Important:

```text
Short hard-stop capture gets the audio to STT.
Hard-stop phrase normalizer decides whether the transcript means hard stop.
```

If the hard-stop normalizer is implemented in another TODO, reuse it.

If not, add a minimal version here.

---

## Avoid false positives

Because this path is more sensitive, avoid accidental hard cancels.

Rules:

```text
only enabled while assistant is speaking
only acts after STT/classifier sees hard-stop-like text
does not cancel on noise-only bursts
does not cancel on unclear transcripts
does not cancel on general questions like "how do I stop a process"
does not disable wake-word globally
```

If STT returns empty/low confidence, do not hard cancel.

If speech continues beyond short window, normal correction capture can take over.

---

## Interaction with ducking

Fast hard-stop detection should not be delayed by ducking.

Ducking should happen immediately on VAD/speech energy, but hard-stop capture/classification still needs STT.

If the instant ducking TODO is not implemented yet, do not block this task on it.

---

## Required tests

Add/update tests:

```text
Short_stop_burst_while_speaking_starts_fast_hard_stop_path
Short_abort_burst_while_speaking_starts_fast_hard_stop_path
Short_stop_transcript_classifies_as_hard_cancel
Short_please_stop_transcript_classifies_as_hard_cancel
Short_noise_burst_does_not_cancel
Short_stop_while_idle_does_not_bypass_wake_word_policy
Fast_hard_stop_clears_playback
Fast_hard_stop_cancels_live_turn
Fast_hard_stop_does_not_dispatch_correction_regeneration
Fast_hard_stop_still_suppresses_old_response
Longer_correction_still_uses_normal_correction_capture
Backchannel_still_does_not_trigger_fast_hard_stop
```

If audio/VAD is hard to unit test, test the decision layer with simulated frame durations/VAD events.

---

# Part 3 - Logging and diagnostics

Add logs that prove what happened.

For correction token ownership:

```text
Correction regeneration dispatch requested.
OriginalCorrelationId=...
NewCorrelationId=...
OldCaptureTokenCancelled=...
SessionTokenCancelled=...
NewLiveTurnTokenCancelledAtStart=...
```

When new correction request starts:

```text
Correction live turn started.
NewCorrelationId=...
LinkedToSession=true
LinkedToOldCapture=false
```

If cancelled:

```text
Correction live turn cancelled.
Reason=...
TokenSource=...
```

For short hard stop:

```text
Fast hard-stop candidate started.
SpeechMs=...
AssistantSpeaking=true
Fast hard-stop STT result.
Transcript=..., Confidence=...
Fast hard-stop accepted/rejected.
Reason=...
```

Avoid logging full sensitive transcripts unless current logging policy allows it. Use snippets/lengths when appropriate.

---

# Part 4 - Acceptance criteria

This task is complete when:

## Correction token ownership

- [ ] Regenerated correction requests use a fresh live turn token.
- [ ] Regenerated correction requests do not link to old barge-in/capture/interruption token.
- [ ] Cancelling the old barge-in/capture token does not cancel the new correction request.
- [ ] New correction request can reach CommandRouter and DeepInfra/fake model.
- [ ] Old cancelled response remains suppressed.
- [ ] New correction response can be sent/spoken.
- [ ] Socket/session cancellation still cancels new correction request.
- [ ] Tests cover the cancellation-lifetime bug.

## Short hard-stop capture

- [ ] While assistant is speaking, short "stop" can trigger interruption capture/classification.
- [ ] While assistant is speaking, short "abort" can trigger interruption capture/classification.
- [ ] Fast hard-stop path does not require wake word while assistant is speaking.
- [ ] Idle wake-word policy remains stricter.
- [ ] Noise/empty transcripts do not trigger hard stop.
- [ ] Fast hard stop clears playback and cancels active live turn.
- [ ] Fast hard stop does not trigger correction regeneration.
- [ ] Tests cover the short-stop issue.

## Regression

- [ ] Existing hard stop still works.
- [ ] Existing correction regeneration still works.
- [ ] Existing backchannel/clarification behavior still works.
- [ ] Full backend test suite passes.

---

# Part 5 - Manual verification checklist

After implementation, manually test with Merlin running.

## Correction token bug

Scenario:

```text
Ask: "What is the best family card?"
Wait for Merlin to answer.
Interrupt: "I mean family car."
```

Expected:

```text
old answer stops
new correction request is dispatched
new response answers about family car
DeepInfra is not immediately cancelled
old family-card answer does not return
```

Scenario:

```text
Ask a brainstorming question.
Interrupt: "I mean, what is the purpose of a voice?"
```

Expected:

```text
old answer stops
new correction request completes
Merlin answers the new question
```

## Short stop bug

While Merlin is speaking, test:

```text
stop
please stop
abort
cancel
wait
```

Expected:

```text
short utterance reaches hard-stop path
speech stops
active live turn cancels
old answer does not return
```

While Merlin is idle, test:

```text
stop
abort
please stop
```

Expected:

```text
idle wake-word policy remains unchanged
no broad accidental command behavior unless explicitly configured
```

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. What caused correction regeneration to cancel immediately.
4. Which token was wrong.
5. Which token is now used for regenerated correction requests.
6. How old and new cancellation lifetimes are separated.
7. How session/socket cancellation is still respected.
8. How short hard-stop capture works.
9. What thresholds/config values were added or changed.
10. How false positives are avoided.
11. How hard stop/correction/backchannel behavior was preserved.
12. Tests added.
13. Tests run and results.
14. Known limitations.
15. Recommended tuning values for fast hard-stop thresholds.

Do not simply say "implemented." Explain the lifecycle with the actual classes and methods changed.

---

# Recommended first implementation cut

If the whole task is too large, do this first:

```text
1. Fix correction regeneration token ownership.
2. Add tests proving old capture token cancellation does not kill the new correction request.
3. Run full backend tests.
```

Then do short hard-stop capture as the second cut.

Correction regeneration being immediately cancelled is the highest-priority bug because logs show corrections are already being detected and dispatched but cannot complete.
