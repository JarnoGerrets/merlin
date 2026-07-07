---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/FullDuplexBargeIn_Option4C.md
classification: implementation-plan
related_features:
  - Voice Interruption System
status: implemented
imported_to_vault: true
---

# Merlin Full-Duplex Barge-In Option 4C

## Target path in repository

Save this file in the Merlin repository as:

```text
Merlin.ToDo/FullDuplexBargeIn_Option4C.md
```

## Purpose

This document describes the implementation plan for Merlin's full-duplex barge-in system using the **Option 4C** approach:

```text
Acoustic Echo Cancellation
+ Voice Activity Detection
+ speaker ducking
+ gated short-window STT
+ interruption classification
+ turn cancellation / correction regeneration
```

The goal is to let the user naturally interrupt Merlin while Merlin is speaking, without Merlin mistaking its own speaker audio for user speech.

This is not just a playback stop button. It is a complete voice turn-taking system.

---

# 1. Core problem

When Merlin speaks through speakers, the microphone hears:

```text
Merlin's own TTS audio
+ room echo
+ user voice
+ keyboard/fan/background noise
```

If Merlin simply keeps the microphone open and runs normal VAD/STT, it may hear itself and accidentally interrupt itself.

Bad behavior:

```text
Merlin speaks: "The first thing is..."
Microphone hears Merlin's own TTS.
VAD detects speech.
STT transcribes Merlin.
Merlin thinks the user interrupted.
Merlin cancels itself.
```

Target behavior:

```text
Merlin speaks.
Microphone remains open.
AEC removes or reduces Merlin's own speaker audio from the mic signal.
VAD detects actual user speech over Merlin.
Merlin ducks its own volume.
A short gated STT pass checks whether the user is interrupting.
If interruption is confirmed, Merlin stops the current turn and handles the correction.
If it was noise/backchannel/echo, Merlin continues normally.
```

---

# 2. Why Option 4C

Option 4C means **do not run full Whisper continuously while Merlin speaks**.

Instead:

```text
AEC always runs while playback is active.
VAD runs on the echo-cancelled mic stream.
Only when VAD detects likely near-end user speech do we capture a short trigger window.
Only then do we run STT on the trigger audio.
Only if STT/classifier says this is a real interruption do we cancel the current turn.
```

This is resource-friendly because Whisper only runs when there is a possible interruption.

Do not implement:

```text
continuous medium.en transcription during every Merlin spoken response
```

That would waste GPU compute and may compete with Chatterbox TTS.

---

# 3. High-level architecture

```text
Assistant final/ack/progress speech playback
        ↓
Playback reference tap
        ↓
AEC reference signal

Microphone input
        ↓
AEC processor
        ↓
Echo-reduced near-end audio
        ↓
VAD
        ↓
Possible user speech detected
        ↓
Speaker ducking
        ↓
Short trigger buffer
        ↓
Gated STT
        ↓
Interruption classifier
        ↓
Decision:
    - ignore/noise/backchannel
    - hard stop
    - pause
    - correction
    - clarification
    - topic change
        ↓
TurnManager / cancellation / regeneration
```

---

# 4. Required components

Implement this as separated components. Do not place all logic inside `CommandRouter` or `AssistantSpeechPlaybackService`.

Suggested components:

```text
BargeInOptions
PlaybackReferenceTap
AcousticEchoCancellationService
BargeInAudioCaptureService
BargeInVadService
SpeakerDuckingService
BargeInTriggerBuffer
BargeInSttService
InterruptionClassifier
BargeInCoordinator
TurnCancellationService / TurnManager integration
CorrectionPromptBuilder integration later if already available
BargeInDiagnosticsLogger
```

Use existing Merlin naming conventions where possible.

---

# 5. Configuration

Add configuration under `appsettings.json`:

```json
{
  "BargeIn": {
    "Enabled": false,
    "Mode": "Option4C",
    "RequireVoiceMode": true,
    "EnableAec": true,
    "EnableVad": true,
    "EnableSpeakerDucking": true,
    "EnableGatedStt": true,
    "EnableTurnCancellation": true,

    "AecProvider": "WebRtcOrPlatform",
    "AecFrameMs": 10,
    "AecSampleRate": 16000,

    "VadMinSpeechMs": 250,
    "VadTriggerSpeechMs": 350,
    "VadEndSilenceMs": 500,
    "VadEnergyThreshold": 0.015,
    "VadUseAdaptiveNoiseFloor": true,

    "TriggerPreRollMs": 300,
    "TriggerCaptureMs": 1500,
    "TriggerMaxCaptureMs": 2500,

    "DuckingVolumePercent": 20,
    "DuckingFadeMs": 80,
    "DuckingRestoreMs": 150,

    "GatedSttModel": "medium.en",
    "GatedSttDevice": "cuda",
    "GatedSttBeamSize": 3,
    "GatedSttTemperature": 0,
    "GatedSttMaxAudioMs": 2500,

    "RequireWakeWordForFirstVersion": false,
    "WakeWords": ["merlin"],

    "HardStopPhrases": ["stop", "cancel", "shut up", "quiet", "enough"],
    "PausePhrases": ["wait", "pause", "hold on", "one second"],
    "CorrectionPhrases": ["no", "actually", "no i mean", "i mean", "not that", "that's wrong", "you misunderstood", "correct that"],
    "ClarificationQuestionPrefixes": ["what", "why", "how", "which", "where"],

    "MinClassifierConfidence": 0.75,
    "MinHardStopConfidence": 0.65,
    "MaxBargeInsPerAssistantTurn": 3,

    "LogAudioDiagnostics": true,
    "SaveDebugAudio": false,
    "DebugAudioPath": "%APPDATA%/Merlin/debug/barge-in"
  }
}
```

For initial development, keep `Enabled=false` by default until manual verification passes.

The agent may temporarily enable it in dev settings or tests, but production default should be safe.

---

# 6. Relationship to existing Merlin systems

This feature must integrate with existing systems:

## Existing likely systems

```text
CommandRouter
MerlinIntentRouter
AssistantSpeechPlaybackService
ChatterboxTtsProvider
PythonVoiceService / STT pipeline
LocalAIChatService / DeepInfra path
AcknowledgementSpeechService
RequestProgressSpeechService
MemoryOrchestrator
WebSocketHandler
```

## Must not break

```text
normal voice commands
current STT
current TTS
acknowledgement/progress speech
memory persistence
DeepInfra prompt compilation
local tools
frontend WebSocket contract
```

Do not change WebSocket contracts unless absolutely necessary.

---

# 7. Speech item types

All speech sent to playback should have a type or equivalent metadata where possible:

```text
Acknowledgement
Progress
FinalAnswer
ToolResult
Error
UserInterruptionAck
```

Barge-in should only run while Merlin is actively speaking user-facing audio.

Do not run barge-in capture during silence unless the normal voice listener already handles that.

---

# 8. Playback reference tap

AEC needs a reference of what Merlin is playing.

Implement a playback reference tap:

```text
AssistantSpeechPlaybackService
        ↓
currently played PCM/audio buffer
        ↓
PlaybackReferenceTap
        ↓
AEC reference stream
```

Requirements:

- Capture the exact audio being sent to speakers if feasible.
- Use PCM format compatible with AEC, ideally mono 16 kHz or a clean converted reference stream.
- Keep timestamps or frame ordering.
- Do not block playback.
- If reference stream is unavailable, AEC must report degraded mode and barge-in should either disable itself or require stricter trigger conditions.

Important:

The reference should be aligned with microphone audio as closely as possible. Latency mismatch hurts AEC.

---

# 9. Acoustic Echo Cancellation service

Create an abstraction:

```csharp
public interface IAcousticEchoCancellationService
{
    Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default);
    AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame);
    Task DisposeAsync();
}
```

The implementation may use:

- WebRTC Audio Processing Module if available/practical.
- A platform audio processing API if already available in the project.
- A clear placeholder implementation for development that passes through audio but marks AEC as unavailable.

Important:

Do not pretend echo cancellation is active if it is not. Logs must say clearly whether real AEC is running.

If implementing real AEC requires a native dependency and that is too risky for the first commit, do this:

1. Implement interfaces and pipeline.
2. Add a no-op AEC implementation with explicit degraded-mode logs.
3. Keep full barge-in disabled unless real AEC is configured OR require wake-word/hotkey fallback.
4. Document exactly what remains to add.

But the intended final target is real AEC.

---

# 10. VAD service

Create a VAD service that runs on echo-reduced audio, not raw mic audio.

MVP VAD can be energy-based with adaptive noise floor.

Suggested input:

```csharp
public sealed record VadFrameInput
{
    public required ReadOnlyMemory<float> Samples { get; init; }
    public required int SampleRate { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
```

Suggested output:

```csharp
public sealed record VadFrameResult
{
    public required bool IsSpeech { get; init; }
    public required double Energy { get; init; }
    public required double NoiseFloor { get; init; }
    public required double Confidence { get; init; }
}
```

VAD should trigger a possible barge-in only when speech is sustained:

```text
speech detected for at least VadTriggerSpeechMs
```

Do not trigger on a single loud click/pop.

---

# 11. Speaker ducking

When possible user speech is detected:

```text
Merlin volume fades to DuckingVolumePercent.
```

If interruption is confirmed:

```text
stop/cancel current assistant turn according to interruption type.
```

If interruption is rejected:

```text
restore speaker volume smoothly.
```

Ducking should be fast but not jarring.

Suggested:

```text
fade down: 80ms
fade up: 150ms
```

If playback service cannot currently control volume per item, create an abstraction and log TODO. Do not break playback.

---

# 12. Trigger buffer

When VAD detects possible user speech, capture a short buffer for gated STT.

Buffer should include:

```text
pre-roll before VAD trigger
current speech audio
up to max capture window
```

Suggested:

```text
TriggerPreRollMs: 300
TriggerCaptureMs: 1500
TriggerMaxCaptureMs: 2500
```

This gives Whisper enough context to hear:

```text
stop
wait
no, I mean beam
actually use SQLite
what do you mean by that
```

---

# 13. Gated STT service

The gated STT service should transcribe only the short trigger audio.

Do not run continuous full STT during playback.

MVP:

```text
Use the same Whisper stack as Merlin where practical.
Use medium.en if already loaded and practical.
Use beam size 3 or 5 depending performance.
Use temperature 0.
Limit audio to 2.5 seconds.
```

If medium.en short-window STT is too slow for barge-in, support a config option for a smaller trigger model later:

```text
tiny.en or base.en for interruption detection only
```

But do not change production normal STT model in this task.

---

# 14. Interruption classifier

Create an interruption classifier that takes the gated STT transcript and current assistant state.

Input:

```csharp
public sealed record InterruptionClassificationInput
{
    public required string RawTranscript { get; init; }
    public required string NormalizedTranscript { get; init; }
    public required string AssistantTurnId { get; init; }
    public required string CurrentSpeechType { get; init; }
    public required string SpokenTextSoFar { get; init; }
    public required double VadConfidence { get; init; }
    public required bool WasWakeWordPresent { get; init; }
}
```

Output:

```csharp
public sealed record InterruptionClassificationResult
{
    public required InterruptionType Type { get; init; }
    public required double Confidence { get; init; }
    public required string Reason { get; init; }
    public string? CorrectedUserMessage { get; init; }
}
```

Types:

```text
None
NoiseOrEcho
Backchannel
HardStop
Pause
Correction
ClarificationQuestion
TopicChange
```

## Classification rules MVP

Hard stop:

```text
stop
cancel
shut up
quiet
enough
```

Pause:

```text
wait
pause
hold on
one second
```

Correction:

```text
no
no, I mean ...
actually ...
I mean ...
not that ...
that's wrong ...
you misunderstood ...
correct that ...
```

Clarification:

```text
what do you mean
what is that
why
how
which
```

Backchannel:

```text
yes
yeah
mhm
okay
right
```

Backchannels should not cancel the assistant turn.

---

# 15. Turn cancellation integration

This feature depends on turn IDs and cancellation tokens.

If turn cancellation infrastructure already exists, use it.

If not, implement minimal safe plumbing:

```text
AssistantTurnId
CancellationTokenSource
GeneratedTextSoFar
SpokenTextSoFar
PendingTtsChunks
TurnState
WasInterrupted
InterruptionReason
```

Every TTS chunk/audio item should be associated with the active assistant turn where possible.

When a true interruption occurs:

```text
1. stop current playback
2. cancel pending TTS chunks for that turn
3. cancel active DeepInfra stream if possible
4. ignore late output from cancelled turn
5. mark turn interrupted
6. process user interruption transcript
```

For this task, do not overbuild the whole conversation state if already partly implemented. Integrate with existing turn/request/correlation IDs.

---

# 16. Correction regeneration behavior

If the user says:

```text
no, I mean beam
```

Merlin should not continue the old answer.

Flow:

```text
Current answer is cancelled.
Old partial assistant text is preserved as interrupted context.
New correction prompt is compiled.
DeepInfra receives a compact correction prompt.
Merlin speaks a local acknowledgement like:
"You are right. Let me reframe that."
Then final regenerated answer follows.
```

If correction regeneration is too large for this first implementation, implement cancellation + capture + logging first, and add a TODO for regeneration. However, the target behavior should be documented and the architecture should support it.

---

# 17. Local acknowledgement for interruptions

When user interruption is confirmed, Merlin should speak a short local phrase when appropriate:

Correction:

```text
"You are right. Let me reframe that."
"Understood. I will adjust that."
"Correct. Let me rearrange my thoughts."
```

Pause:

```text
"Paused."
```

Hard stop:

```text
Usually silent stop, or a tiny non-verbal UI indication.
```

Do not say implementation-breaking phrases such as:

```text
"The model is regenerating."
"DeepInfra is restarting."
"The LLM is updating."
```

---

# 18. Safety and write-action behavior

If Merlin is executing or about to execute a dangerous/irreversible action, interruption rules are stricter.

Examples of potentially dangerous actions:

```text
send email
delete file
modify calendar
shutdown PC
execute command with side effects
```

Rules:

```text
Before commit: interruption cancels action.
During commit: attempt cancellation if possible.
After commit: do not claim it was cancelled; explain current state.
```

For MVP, ensure hard stop can stop speech and pending reasoning. Tool-specific rollback can be later.

---

# 19. False positive prevention

False positives are the biggest risk.

Apply these safeguards:

```text
Only run barge-in while Merlin is speaking.
Only use echo-reduced audio for VAD.
Require sustained speech.
Require high confidence for correction/pause.
Require lower confidence for hard-stop only if phrase is very short and clear.
Treat backchannels as non-interruptions.
Limit max barge-ins per assistant turn.
Log all barge-in decisions.
Provide config kill switch.
```

If AEC is unavailable or degraded, use stricter mode:

```text
Require wake word: "Merlin, stop" / "Merlin, wait" / "Merlin, no"
```

---

# 20. Debug logging

Add detailed logs.

Required logs:

```text
Barge-in monitor started
Barge-in monitor stopped
AEC initialized / unavailable / degraded
Playback reference frame received
Mic frame processed
VAD possible speech detected
Speaker ducking started
Trigger buffer captured
Gated STT started
Gated STT result
Interruption classifier result
Barge-in ignored: reason
Barge-in accepted: type/confidence
Assistant turn cancelled
Late output ignored from cancelled turn
Speaker ducking restored
```

Log fields:

```text
CorrelationId
AssistantTurnId
SpeechType
PlaybackRequestId
VadConfidence
SttTranscript
ClassificationType
ClassificationConfidence
AecMode
ElapsedMs
```

Do not log raw audio bytes.

---

# 21. Debug audio option

Add optional debug audio saving only when config enables it:

```text
SaveDebugAudio = true
```

Save:

```text
raw mic trigger audio
AEC output trigger audio
playback reference snippet if useful
```

Path:

```text
%APPDATA%/Merlin/debug/barge-in/yyyyMMdd_HHmmss/
```

Never enable by default.

---

# 22. Manual test scenarios

## Scenario 1: hard stop

User asks a long answer.

While Merlin speaks, user says:

```text
stop
```

Expected:

```text
Merlin stops speaking.
No old audio resumes.
DeepInfra/TTS output from old turn is ignored if still arriving.
```

## Scenario 2: correction

Merlin says wrong thing:

```text
Merlin: "In Whisper, bean is not a standard concept..."
User: "No, I mean beam."
```

Expected:

```text
Merlin stops current response.
Merlin acknowledges correction.
Merlin regenerates around beam.
Old answer does not resume.
```

## Scenario 3: clarification

Merlin says:

```text
"Use acoustic echo cancellation."
```

User says:

```text
what does that mean?
```

Expected:

```text
Merlin pauses/cancels current flow depending MVP design.
Merlin answers clarification or logs it as clarification.
```

## Scenario 4: backchannel

Merlin speaks.

User says:

```text
yeah
```

Expected:

```text
Merlin does not stop.
```

## Scenario 5: echo false positive

Merlin speaks a sentence containing words like:

```text
no
wait
actually
```

User says nothing.

Expected:

```text
Merlin does not interrupt itself.
```

---

# 23. Tests

Add unit tests for:

```text
InterruptionClassifier
Vad trigger logic
Phrase matching
Backchannel handling
Hard stop handling
Correction handling
Wake-word fallback rules
```

Add integration-ish tests for:

```text
final answer cancellation path
late TTS chunk ignored after cancellation
progress/ack speech can be interrupted by real user interruption
false positive echo-like transcript ignored if AEC confidence/degraded mode says no near-end speech
```

Do not require real microphone or real speakers for automated tests.
Use fake audio frames and fake STT results.

---

# 24. Implementation phases

## Phase 1: Architecture and config

Implement:

```text
BargeInOptions
interfaces
DI registration
kill switch
basic diagnostics logs
```

No real audio behavior yet.

Acceptance:

```text
Build passes.
Options bind from appsettings.
Feature disabled by default.
```

## Phase 2: Playback reference and monitor lifecycle

Implement:

```text
PlaybackReferenceTap
BargeInCoordinator start/stop while assistant speech plays
speech item metadata hookup where possible
```

Acceptance:

```text
When Merlin starts speaking, barge-in monitor can start.
When Merlin stops speaking, monitor stops.
No behavior change when BargeIn.Enabled=false.
```

## Phase 3: AEC abstraction

Implement:

```text
IAcousticEchoCancellationService
real provider if practical
explicit no-op degraded provider if real AEC cannot be added safely
AEC status logging
```

Acceptance:

```text
Logs clearly show AEC active or degraded.
Barge-in does not pretend AEC works if it does not.
```

## Phase 4: VAD and trigger buffer

Implement:

```text
VAD over echo-reduced audio
trigger buffer with pre-roll
speaker ducking start/restore
```

Acceptance:

```text
Possible user speech causes ducking and trigger capture.
Noise/clicks do not trigger.
Ducking restores if no interruption is confirmed.
```

## Phase 5: Gated STT

Implement:

```text
gated STT service for short trigger audio
reuse existing STT where clean
strict max audio length
logs
```

Acceptance:

```text
STT only runs after VAD trigger.
No continuous Whisper while Merlin speaks.
```

## Phase 6: Interruption classifier

Implement:

```text
HardStop
Pause
Correction
Clarification
Backchannel
None
```

Acceptance:

```text
"stop" => HardStop
"no, I mean beam" => Correction
"yeah" => Backchannel
"what do you mean" => Clarification
```

## Phase 7: Turn cancellation integration

Implement:

```text
hard stop cancellation
ignore late audio/text from cancelled turn
pending TTS clear
active playback stop
```

Acceptance:

```text
User can stop Merlin mid-speech.
Old audio does not resume.
```

## Phase 8: Correction regeneration integration

Implement:

```text
capture correction text
build corrected prompt/context
local acknowledgement
start new answer
```

Acceptance:

```text
"No, I mean beam" cancels old answer and creates new corrected answer.
```

If this phase is too large, stop after Phase 7 and report what remains.

## Phase 9: Manual test + tuning

Tune:

```text
VAD thresholds
confidence thresholds
ducking timing
trigger capture length
wake-word fallback if needed
```

Acceptance:

```text
Manual hard stop and correction work reliably.
False positives are rare.
```

---

# 25. Explicit non-goals

Do not implement in this task:

```text
voice correction learning mappings
left-side chat log UI
full memory dashboard
response tuning
new STT model benchmarking
new TTS model
frontend redesign
continuous Whisper transcription during playback
```

---

# 26. Developer safety instructions

- Keep `BargeIn.Enabled=false` by default until manual verification.
- Use clear abstractions and small services.
- Do not deeply entangle barge-in with `CommandRouter`.
- Do not break normal voice input when Merlin is not speaking.
- Do not break acknowledgement/progress speech.
- Do not alter memory persistence.
- Do not alter the AppData DB path.
- Do not add large native dependencies without documenting why.
- If real AEC cannot be implemented safely in one run, implement the pipeline with explicit degraded mode and stop before pretending it works.

---

# 27. Final agent report requirements

When implementation finishes, report:

```text
Files changed
Which phases completed
Whether real AEC is active or degraded/no-op
Whether BargeIn.Enabled defaults to false
How to enable it for manual testing
How to run hard-stop test
How to run correction test
What logs to watch
Tests added
Verification commands run
Known limitations
Next recommended task
```

Run:

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter BargeIn
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

Also manually test with:

```text
Explain the tradeoffs of local-first memory architecture.
```

Then interrupt with:

```text
stop
```

Then test correction:

```text
No, I mean beam.
```

---

# 28. Final target behavior

Merlin should eventually feel like this:

```text
User: What does bean do in Whisper?
Merlin: "I think you may mean bean as in..."
User, speaking over Merlin: "No, I mean beam."
Merlin: [stops immediately]
Merlin: "You are right. Let me reframe that."
Merlin: "In Whisper, beam search keeps multiple candidate transcriptions..."
```

And:

```text
User: Explain local-first memory architecture.
Merlin: [starts answering]
User: "Stop."
Merlin: [stops, old audio never resumes]
```

This is the intended premium voice interaction: Merlin is no longer a monologue machine. It can be interrupted and corrected like a real assistant.
