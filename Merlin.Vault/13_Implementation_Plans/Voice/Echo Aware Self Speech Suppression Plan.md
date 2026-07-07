---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/Merlin_Echo_Aware_Self_Speech_Suppression_Implementation.md
related_features:
  - Voice Interruption System
status: current
ready_for_agent: true
---

## Plan Status

Status: current
Ready for agent use: yes
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: [[Voice Interruption System]]
Related architecture: [[Voice Pipeline Architecture]]
Related code atlas: [[AssistantSpeechPlaybackService]]
Original source: `Merlin.ToDo/Merlin_Echo_Aware_Self_Speech_Suppression_Implementation.md`

# Echo Aware Self Speech Suppression Plan

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Echo_Aware_Self_Speech_Suppression_Implementation.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Fix the problem where Merlin interrupts itself when speaking through speakers.

The user reports:

```text
When Merlin speaks over speakers, it interrupts itself.
It constantly ducks in and out of conversation.
```

This means Merlin's microphone is likely hearing Merlin's own TTS playback through the speakers, and the barge-in/VAD path is treating that playback leakage as user speech.

The target behavior:

```text
Merlin speaks through speakers
↓
microphone hears some of Merlin's own playback
↓
Merlin recognizes this as self-echo / assistant playback leakage
↓
Merlin does NOT duck, capture, classify, or interrupt itself
```

But still:

```text
User speaks over Merlin
↓
microphone contains user speech beyond expected playback echo
↓
Merlin ducks quickly
↓
Merlin can detect hard stop / correction / clarification
```

The fix should be echo-aware/self-speech-aware, not a global sensitivity reduction.

---

## Why this is happening

After improving interruption sensitivity, short-stop detection, and ducking, Merlin is more responsive to speech-like audio.

That is good.

But with speakers, the mic hears Merlin's own output:

```text
Merlin TTS playback
↓
speaker output
↓
room / desk / mic pickup
↓
VAD sees speech-like audio
↓
barge-in thinks user is speaking
↓
ducking starts
↓
speaker volume drops
↓
mic hears less Merlin
↓
ducking restores
↓
mic hears Merlin again
↓
repeat
```

This creates the loop:

```text
duck
restore
duck
restore
capture
cancel
ignore
repeat
```

The root problem is that VAD currently answers only:

```text
Is there speech-like audio in the microphone?
```

But during assistant playback, Merlin needs to answer:

```text
Is this speech-like audio probably the user, or probably Merlin's own playback leaking into the mic?
```

---

## Important distinction

This is not the same as:

```text
hard-stop phrase recognition
correction semantic rewrite
web search
TTS latency
general VAD tuning
```

This task is about adding an echo-aware gate before ducking/barge-in activation.

Do not fix this by simply raising VAD thresholds globally. That would likely break the recently improved short hard-stop behavior.

The correct solution is conditional:

```text
assistant not speaking:
  normal VAD behavior

assistant speaking through headphones/low leakage:
  normal or lightly gated VAD behavior

assistant speaking through speakers / mic hears playback:
  echo-aware self-speech suppression
```

---

## Non-goals

Do not implement these here:

- semantic correction rewrite
- web search / web research
- capability routing changes
- Codex integration
- new TTS/STT provider
- destructive action support
- full voice identity embedding system
- full machine-learning speaker recognition
- major frontend redesign
- major rewrite of WebRTC APM

This task should add a practical first version of:

```text
self-speech suppression
playback-aware barge-in gating
echo-aware ducking gating
diagnostics and tests
```

---

## Mental model

The best first version is not "recognize Merlin's voice" by speaker embedding.

The best first version is:

```text
Merlin knows exactly what audio it is playing.
If the microphone hears audio that tracks the playback audio, treat it as self-echo.
If the microphone hears extra speech energy beyond expected echo, treat it as possible user speech.
```

So the system should not be:

```text
mic VAD says speech
↓
duck/barge-in immediately
```

It should become:

```text
mic VAD says speech
↓
assistant playback is active?
  no -> allow normal VAD path
  yes -> run self-speech/echo gate
↓
if likely self-echo only:
  suppress ducking/barge-in
if likely user speech over assistant:
  allow ducking/barge-in
```

---

## Required investigation before coding

Inspect these files/classes if they exist:

```text
Merlin.Backend/Services/BargeIn/
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/SpeakerDuckingService.cs
Merlin.Backend/Services/BargeIn/BargeInInterfaces.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/Interfaces/IAssistantSpeechPlaybackService.cs
Merlin.Backend/Services/Interfaces/ILiveAssistantTurnService.cs
Merlin.Backend/Services/SpeechPolicyService.cs
Merlin.Backend/Configuration/
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/WebSocketHandlerTests.cs
```

Search for:

```text
VAD
VoiceActivity
PossibleSpeech
BargeIn
Ducking
SpeakerDucking
CurrentVolumeMultiplier
waveOut.Volume
Playback
SpeechPlayback
AssistantSpeaking
VisualState
Speaking
PlaybackStarted
PlaybackCompleted
PlaybackEnergy
AudioLevel
Rms
Energy
Aec
WebRTC
APM
Echo
Capture
Microphone
```

Before editing, answer these questions:

1. Where does Merlin know assistant playback is active?
2. Does `AssistantSpeechPlaybackService` expose playback state?
3. Does the playback path expose current/estimated playback audio energy?
4. Does the microphone/VAD path expose frame energy or RMS?
5. Does WebRTC APM/AEC expose residual echo / near-end speech information?
6. Where does VAD currently trigger ducking?
7. Where does VAD currently trigger barge-in capture?
8. Is ducking triggered before or after any echo/AEC gate?
9. Does the system behave differently for headphones vs speakers?
10. Is there any existing self-speech suppression logic?

Mention findings in the final report.

---

# Part 1 - Add playback activity and playback energy visibility

## Goal

The barge-in system must know when Merlin is currently producing audio, and ideally how loud that output is.

At minimum, expose:

```text
assistant playback active: true/false
```

Preferred:

```text
assistant playback active: true/false
current playback energy/RMS
recent playback energy/RMS
playback started timestamp
playback device/output volume multiplier
```

---

## Suggested service/model

If no existing service provides this, add a small service:

```text
Merlin.Backend/Services/Interfaces/IAssistantPlaybackMonitor.cs
Merlin.Backend/Services/AssistantPlaybackMonitor.cs
```

Suggested interface:

```csharp
public interface IAssistantPlaybackMonitor
{
    bool IsPlaybackActive { get; }

    DateTimeOffset? PlaybackStartedAt { get; }

    double CurrentPlaybackEnergy { get; }

    double RecentPlaybackEnergy { get; }

    void NotifyPlaybackStarted(string correlationId);

    void NotifyPlaybackStopped(string correlationId);

    void ReportPlaybackFrameEnergy(double rms, DateTimeOffset timestamp);
}
```

If `AssistantSpeechPlaybackService` is the only owner of playback, it can implement this directly or publish to this monitor.

Keep it simple. Do not over-engineer.

---

## Playback energy

If easy, calculate a simple RMS/energy from audio buffers before sending to output.

Suggested:

```text
RMS = sqrt(mean(sample^2))
```

Keep a short rolling window:

```text
100ms - 500ms
```

This does not need to be perfect.

It only needs to help distinguish:

```text
mic energy roughly equals expected playback leakage
```

from:

```text
mic energy significantly exceeds playback leakage
```

If playback energy is hard to extract in the first pass, implement playback-active-only gating first, and leave energy comparison as the next step. But document the limitation.

---

# Part 2 - Add microphone frame energy visibility

## Goal

The self-speech suppression gate needs the microphone frame energy/RMS that VAD already sees or can compute.

If current VAD logic already calculates frame energy, reuse it.

If not, add a small utility:

```text
AudioEnergyCalculator
```

Suggested methods:

```csharp
public static double CalculateRms(ReadOnlySpan<float> samples)
```

or for PCM16:

```csharp
public static double CalculatePcm16Rms(ReadOnlySpan<byte> pcm16LittleEndian)
```

The energy scale must be consistent between mic and playback if comparing directly. If scales differ, use normalized/relative values and tune thresholds.

---

# Part 3 - Add self-speech suppression gate

## Goal

Create a decision layer between VAD and ducking/barge-in.

Suggested service:

```text
Merlin.Backend/Services/BargeIn/SelfSpeechSuppressionGate.cs
Merlin.Backend/Services/BargeIn/Interfaces/ISelfSpeechSuppressionGate.cs
```

or put interface in existing `BargeInInterfaces.cs` if that is the style.

Suggested input model:

```csharp
public sealed record SelfSpeechGateInput(
    bool AssistantPlaybackActive,
    double MicEnergy,
    double PlaybackEnergy,
    bool AecVerified,
    bool VadSaysSpeech,
    DateTimeOffset Timestamp,
    TimeSpan? PlaybackAge,
    string Reason);
```

Suggested output model:

```csharp
public sealed record SelfSpeechGateResult(
    SelfSpeechDecision Decision,
    double Confidence,
    string Reason,
    double MicEnergy,
    double PlaybackEnergy,
    double EstimatedEchoEnergy,
    double UserSpeechScore);
```

Suggested enum:

```csharp
public enum SelfSpeechDecision
{
    Allow,
    SuppressAsSelfEcho,
    Uncertain
}
```

---

## First-pass decision rules

Use deterministic rules first.

### If assistant is not playing

```text
Allow normal VAD path.
```

### If VAD does not say speech

```text
Suppress / no action.
```

### If playback just started

Speaker output transients can trigger mic energy.

For the first:

```text
150ms - 300ms
```

after playback start, suppress weak VAD triggers unless mic energy is very strong.

This should not block strong user speech, but it can prevent instant self-trigger loops.

### If playback active and mic energy is consistent with expected echo

Estimate:

```text
estimatedEcho = playbackEnergy * EchoLeakageMultiplier
```

If:

```text
micEnergy <= estimatedEcho + EchoMargin
```

then suppress as self-echo.

### If mic energy clearly exceeds expected echo

If:

```text
micEnergy >= estimatedEcho * UserSpeechRatio
```

or:

```text
micEnergy - estimatedEcho >= UserSpeechMargin
```

then allow as likely user speech.

### If uncertain

Prefer suppressing ducking/capture for weak uncertain frames, but allow if sustained across multiple frames.

This avoids rapid duck/restore loops.

---

## Suggested configuration

Add or extend config:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "Enabled": true,
    "SuppressDuringPlayback": true,
    "PlaybackOnsetGraceMs": 250,
    "EchoLeakageMultiplier": 0.35,
    "EchoMargin": 0.02,
    "UserSpeechRatio": 1.8,
    "UserSpeechMargin": 0.05,
    "RequireSustainedUserSpeechFrames": 2,
    "AllowFastHardStopOverride": true,
    "LogDecisions": true
  }
}
```

These values are starting points. Tune based on actual energy scales.

If energy scale is 0-1 RMS, values above may work. If energy scale differs, adjust.

---

# Part 4 - Integrate gate before ducking

## Goal

Merlin should not duck on its own TTS leakage.

Current likely flow:

```text
VAD active
↓
SpeakerDuckingService.StartDucking()
```

Change to:

```text
VAD active
↓
SelfSpeechSuppressionGate.Evaluate(...)
↓
if Allow:
    SpeakerDuckingService.StartDucking()
else if SuppressAsSelfEcho:
    do not duck
```

Important:

Ducking should be triggered by user-likely speech, not just any speech-like mic audio.

---

## Prevent duck/restore loop

The current loop likely happens because:

```text
Merlin output leaks into mic -> duck
duck lowers output -> mic energy falls -> restore
restore raises output -> mic energy rises -> duck again
```

The gate should suppress ducking when mic energy can be explained by playback leakage.

This prevents the loop at the source.

---

# Part 5 - Integrate gate before barge-in capture

## Goal

Merlin should not start interruption capture on its own voice.

Current likely flow:

```text
VAD possible speech
↓
start capture / CapturingInterruption
```

Change to:

```text
VAD possible speech
↓
SelfSpeechSuppressionGate.Evaluate(...)
↓
if Allow:
    start capture
else:
    ignore as self-echo
```

Do not send self-echo audio to STT/classifier.

This reduces:

```text
empty transcripts
weird self-transcriptions
false corrections
false backchannels
self-cancels
```

---

# Part 6 - Interaction with fast hard-stop

A previous/future task may add a fast hard-stop path for short phrases like:

```text
stop
abort
cancel
```

This self-speech gate must not make hard stop unusable.

Suggested behavior:

```text
if self-speech gate says Allow:
    fast hard-stop path can run

if self-speech gate says SuppressAsSelfEcho:
    fast hard-stop path should not run

if self-speech gate says Uncertain:
    allow a very short hard-stop probe only if mic energy is significantly above recent playback energy or sustained across several frames
```

Do not let assistant playback leakage alone trigger fast hard-stop.

---

# Part 7 - Optional AEC integration

If WebRTC APM/AEC exposes useful state, integrate it.

Possible inputs:

```text
AEC enabled
AEC verified
near-end speech probability
residual echo estimate
echo return loss
double-talk detector
```

If AEC says:

```text
near-end speech present
```

then allow barge-in more readily.

If AEC says:

```text
dominant echo / no near-end speech
```

then suppress.

Do not block this task on AEC if metrics are not available. The first pass can use playback/mic energy.

---

# Part 8 - Diagnostics and logging

Add logs that make self-interruption diagnosable.

At minimum:

```text
Self-speech gate evaluated.
AssistantPlaybackActive=...
VadSaysSpeech=...
MicEnergy=...
PlaybackEnergy=...
EstimatedEcho=...
Decision=...
Reason=...
```

When suppressing:

```text
Barge-in suppressed as assistant self-echo.
```

When allowing:

```text
Barge-in allowed as likely user speech.
```

When ducking is suppressed:

```text
Ducking suppressed as assistant self-echo.
```

Avoid logging too much per frame in normal mode. Use debug logging or sampling.

Suggested config:

```text
LogDecisions = false by default
```

But allow enabling for debugging.

---

# Part 9 - Tests

Add deterministic tests. Do not require real speakers/microphone.

Likely test files:

```text
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/AssistantPlaybackMonitorTests.cs
```

Use fake playback monitor and fake VAD/mic frame inputs.

---

## Required gate tests

```text
Allows_when_assistant_not_playing_and_vad_says_speech
Suppresses_when_assistant_playing_and_mic_energy_matches_estimated_echo
Allows_when_assistant_playing_and_mic_energy_exceeds_echo_threshold
Suppresses_weak_vad_during_playback_onset_grace
Allows_strong_user_speech_during_playback_onset_grace
Returns_uncertain_for_borderline_energy
Sustained_uncertain_frames_can_allow_if_configured
Disabled_gate_allows_existing_behavior
```

---

## Required ducking integration tests

```text
Self_echo_does_not_start_ducking
Likely_user_speech_starts_ducking
Self_echo_does_not_create_duck_restore_loop
Playback_start_transient_does_not_duck
User_speech_over_playback_ducks_quickly
```

If direct ducking integration is difficult, test the coordinator decision that calls ducking.

---

## Required barge-in integration tests

```text
Self_echo_does_not_start_interruption_capture
Likely_user_speech_starts_interruption_capture
Self_echo_does_not_run_gated_stt
User_stop_over_playback_still_reaches_fast_hard_stop_path
Noise_during_playback_does_not_trigger_capture
```

---

## Regression tests

Make sure these still pass:

```text
stop while speaking still works
please stop while speaking still works
correction while speaking still works
family card -> family car correction still works
backchannel behavior remains stable
headphone/no-playback-leak scenario still allows normal barge-in
```

---

# Part 10 - Manual verification checklist

After implementation, manually test with speakers.

## Self-echo suppression

1. Use speakers, not headphones.
2. Ask Merlin a long question.
3. Do not speak.
4. Observe:
   - no repeated duck/restore loop
   - no false capture
   - no false correction
   - no self-interruption

## User interruption still works

While Merlin is speaking through speakers:

```text
stop
please stop
abort
I mean family car
No, I meant medium.en
```

Expected:

```text
Merlin detects real user speech
Ducking occurs quickly
Hard stop cancels
Correction regenerates
```

## Headphone regression

Repeat with headphones if possible.

Expected:

```text
normal interruption remains responsive
no added delay for stop/correction
```

---

# Acceptance criteria

This task is complete when:

- [ ] Merlin speaking through speakers does not repeatedly duck on its own voice.
- [ ] Merlin's own TTS leakage does not start barge-in capture.
- [ ] Merlin's own TTS leakage does not run STT/classification.
- [ ] User speech over Merlin still starts ducking quickly.
- [ ] User hard stop over speakers still works.
- [ ] User correction over speakers still works.
- [ ] The fix does not simply reduce VAD sensitivity globally.
- [ ] Self-speech suppression uses assistant playback state.
- [ ] First pass uses playback energy or documented playback-active fallback.
- [ ] Logs/debug mode can show suppress/allow decisions.
- [ ] Unit tests cover echo-vs-user decisions.
- [ ] Integration tests cover ducking/capture suppression.
- [ ] Existing backend tests pass.

---

# Known acceptable first-pass limitation

If playback energy is hard to extract immediately, an acceptable first cut is:

```text
assistant playback active
+ weak mic energy
+ playback onset/transient window
=> suppress

assistant playback active
+ strong/sustained mic energy
=> allow
```

But document that this is weaker than real energy comparison.

The better second cut is:

```text
track playback RMS
estimate echo leakage
compare mic RMS against expected echo
```

Do not implement heavy voice embeddings unless simpler playback-aware methods fail.

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. What caused the self-interruption loop.
4. How assistant playback state is exposed.
5. Whether playback energy is tracked.
6. Whether mic energy is tracked.
7. How the self-speech suppression gate decides suppress vs allow.
8. Where the gate is applied before ducking.
9. Where the gate is applied before barge-in capture/STT.
10. How fast hard-stop/user interruption remains responsive.
11. Config values added or changed.
12. Tests added.
13. Tests run and results.
14. Known limitations.
15. Recommended tuning values for speakers.

Do not simply say "implemented." Explain the lifecycle with the actual classes/methods changed.

---

# Recommended first implementation cut

If the full version is too large, implement this first:

```text
1. Add AssistantPlaybackMonitor with IsPlaybackActive.
2. Add SelfSpeechSuppressionGate with playback-active + mic-energy rules.
3. Apply gate before ducking.
4. Apply gate before starting interruption capture.
5. Add tests proving self-echo does not duck/capture, but strong user speech does.
6. Run full backend tests.
```

Then add playback-energy comparison as the next improvement if not included in the first cut.
