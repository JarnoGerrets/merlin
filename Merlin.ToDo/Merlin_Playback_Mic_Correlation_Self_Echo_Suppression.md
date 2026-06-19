# Merlin Playback-Mic Correlation Self-Echo Suppression

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Playback_Mic_Correlation_Self_Echo_Suppression.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Improve Merlin's speaker-mode barge-in reliability by adding **playback/microphone correlation-based self-echo suppression**.

Current energy-only self-speech suppression is not good enough in the user's speaker setup. Diagnostics showed the self-speech gate allows almost everything because `micEnergy` is much higher than `estimatedEchoEnergy`, even when the user is silent and Merlin is only hearing its own speaker playback.

The goal is to stop relying only on energy and add a stronger signal:

```text
Does the microphone waveform look like delayed Merlin playback?
```

If yes:

```text
Suppress as assistant self-echo.
```

If no, and mic energy is high:

```text
Allow as likely user speech.
```

This is the practical version of "teaching Merlin its own voice" without using a heavy voice-embedding model.

---

## Current observed problem

Recent diagnostics showed patterns like:

```text
Total entries: many
Decision: almost always Allow
SuppressAsSelfEcho: almost none
Reason: Mic energy clearly exceeds expected playback echo
```

Typical values were roughly:

```text
micEnergy       ≈ 0.12 - 0.19
playbackEnergy  ≈ 0.04 - 0.05
estimatedEcho   ≈ 0.02
```

The gate says:

```text
micEnergy is much higher than estimated echo, therefore this must be user speech.
```

But when the user is silent and Merlin is speaking through speakers, this is wrong. It means the fixed energy estimate is not reliable in this room/device setup.

Energy alone cannot robustly distinguish:

```text
Merlin's voice leaking from speakers into the mic
```

from:

```text
the user speaking over Merlin
```

Correlation can help because speaker echo should resemble the playback waveform with a small delay.

---

## Important scope

This task focuses on:

```text
speaker self-echo suppression
playback/mic correlation
diagnostics
tests
```

This task does **not** focus on:

```text
hard-stop STT quality
semantic correction rewrite
web search
capability routing
new TTS/STT providers
full AEC rewrite
voice embedding/speaker recognition
```

First prove:

```text
Merlin no longer hears itself.
```

Only after that should we tune:

```text
Merlin hears the user better over speakers.
```

---

## Non-goals

Do not implement:

- full ML speaker recognition
- voice biometrics
- new Whisper/STT provider
- new Chatterbox/TTS provider
- full WebRTC AEC replacement
- semantic correction rewrite
- unrelated routing changes
- global VAD sensitivity reduction

Do not solve self-echo by simply raising thresholds globally. That risks breaking short hard stops again.

---

## Desired behavior

### Silent speaker playback test

User asks Merlin a long question and then stays silent.

Expected:

```text
Merlin speaks through speakers.
Mic hears Merlin's playback leakage.
Correlation detects that mic waveform resembles delayed playback reference.
Self-speech gate suppresses ducking/capture.
No repeated duck/restore loop.
No false barge-in capture.
No false STT/classification.
```

### Real user interruption test

User speaks over Merlin.

Expected:

```text
Mic contains user speech plus playback leakage.
Correlation with playback may be weaker or mixed.
Mic energy is high and not explained only by playback correlation.
Gate allows user-likely speech.
Ducking/capture/hard-stop/correction can proceed.
```

---

## Required investigation before coding

Inspect these files/classes if they exist:

```text
Merlin.Backend/Services/BargeIn/PlaybackReferenceTap.cs
Merlin.Backend/Services/BargeIn/SelfSpeechSuppressionGate.cs
Merlin.Backend/Services/BargeIn/SelfSpeechGateDiagnosticsWriter.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/BargeInInterfaces.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Configuration/BargeInOptions.cs
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
Merlin.Backend.Tests/BargeInTests.cs
```

Search for:

```text
PlaybackReferenceTap
Push
Pcm
Reference
CurrentPlaybackEnergy
RecentPlaybackEnergy
SelfSpeechSuppressionGate
correlationScore
bestDelayMs
correlationDecision
MicEnergy
PlaybackEnergy
DiagnosticsFilePath
SELF_SPEECH_GATE_DIAGNOSTICS
live_ducking
normal_capture
vad_triggered_capture
fast_hard_stop_candidate
```

Before editing, determine:

1. Does `PlaybackReferenceTap` store recent playback PCM or only energy?
2. What PCM format is playback reference stored in?
3. What PCM format does the mic/VAD frame use at the gate point?
4. Are playback and mic frames at the same sample rate?
5. Does AEC alter mic audio before the gate receives it?
6. Is the gate receiving enough mic samples to run correlation?
7. Where can a short playback reference ring buffer be maintained safely?
8. Where should correlation diagnostics be added?
9. Is existing `correlationScore` real or placeholder/partial?
10. Does energy currently override correlation when correlation says self-echo?

Mention findings in the final report.

---

# Part 1 - Add or complete playback reference ring buffer

## Goal

Maintain enough recent playback PCM to compare against microphone frames with delay.

Speaker echo reaches the mic after a delay caused by:

```text
audio output buffering
speaker hardware
room propagation
microphone capture buffering
AEC/APM buffering
```

The delay can be tens to hundreds of milliseconds.

The system needs recent playback reference PCM covering at least:

```text
0ms - 250ms
```

and preferably a little more:

```text
0ms - 500ms
```

for debugging/tolerance.

---

## Suggested service

Extend existing:

```text
PlaybackReferenceTap
```

rather than creating a second playback pipeline.

It reportedly already receives PCM before output and tracks playback active/energy.

Add:

```text
recent playback PCM ring buffer
sample rate
channel info
format normalization
method to read delayed windows
```

Suggested interface additions:

```csharp
public interface IPlaybackReferenceTap
{
    bool IsPlaybackActive { get; }

    double CurrentPlaybackEnergy { get; }

    double RecentPlaybackEnergy { get; }

    int SampleRate { get; }

    bool TryGetReferenceWindow(
        int delayMs,
        int sampleCount,
        Span<float> destination);
}
```

If existing interface names differ, adapt.

---

## Ring buffer requirements

- Store mono float samples normalized to `[-1, 1]` if possible.
- If playback is stereo, downmix to mono.
- Keep at least 500ms of audio.
- Avoid allocations per frame in hot path where reasonable.
- Be thread-safe enough for playback writing and gate reading.
- If no enough reference exists yet, correlation should return unavailable.

Suggested buffer size:

```text
sampleRate * 1 second
```

At 16kHz:

```text
16,000 float samples ≈ 64 KB
```

At 48kHz:

```text
48,000 float samples ≈ 192 KB
```

This is trivial.

---

# Part 2 - Add microphone frame access for correlation

## Goal

The self-speech gate needs a mic frame as samples, not only mic energy.

If the gate currently receives only energy, extend the input model to optionally include mic samples.

Suggested input addition:

```csharp
public sealed record SelfSpeechGateInput(
    // existing fields...
    IReadOnlyList<float>? MicSamples,
    int? MicSampleRate);
```

Better for performance:

```csharp
ReadOnlyMemory<float> MicSamples
```

or a project-consistent audio frame type.

Requirements:

- Convert mic PCM to mono float `[-1, 1]`.
- Keep sample rate consistent with playback reference if possible.
- If sample rates differ, either:
  - skip correlation and log unavailable, or
  - add simple resampling later.
- Do not log raw mic samples.

---

# Part 3 - Implement normalized cross-correlation

## Goal

Compute how similar the current mic frame is to recent delayed playback reference.

Suggested service:

```text
Merlin.Backend/Services/BargeIn/PlaybackMicCorrelationDetector.cs
Merlin.Backend/Services/BargeIn/Interfaces/IPlaybackMicCorrelationDetector.cs
```

or add to existing BargeIn interfaces.

Suggested input:

```csharp
public sealed record PlaybackMicCorrelationInput(
    ReadOnlyMemory<float> MicSamples,
    int SampleRate,
    int MinDelayMs,
    int MaxDelayMs,
    int DelayStepMs);
```

Suggested result:

```csharp
public sealed record PlaybackMicCorrelationResult(
    bool IsAvailable,
    double BestCorrelationScore,
    int? BestDelayMs,
    string Reason);
```

---

## Correlation formula

For each delay:

```text
reference = playback samples delayed by delayMs, same length as mic frame
score = dot(mic, reference) / sqrt(sum(mic^2) * sum(reference^2))
```

Use absolute correlation or positive-only correlation.

Start with positive-only unless tests show sign inversion is possible.

Clamp/handle:

```text
if mic energy too low -> unavailable/0
if reference energy too low -> unavailable/0
if sample count too small -> unavailable/0
```

---

## Delay search

Suggested first settings:

```json
"SelfSpeechSuppression": {
  "CorrelationDetectionEnabled": true,
  "CorrelationMinDelayMs": 0,
  "CorrelationMaxDelayMs": 250,
  "CorrelationDelayStepMs": 10,
  "CorrelationMinScore": 0.65
}
```

At 16kHz, 20ms frame, 0-250ms in 10ms steps:

```text
320 samples * 26 delays ≈ 8,320 multiply/adds per frame
```

At 48kHz:

```text
960 samples * 26 delays ≈ 24,960 multiply/adds per frame
```

This is cheap compared to STT/TTS.

---

## Performance requirements

- Only run correlation when assistant playback is active.
- Only run when VAD says speech or mic energy is above a minimum.
- Skip if playback reference energy is too low.
- Skip if mic frame is too small.
- Avoid large allocations in the audio path.
- Keep delay step coarse at first, e.g. 10ms.
- Log timing if practical.

---

# Part 4 - Integrate correlation into self-speech gate

## Goal

Correlation should be a primary self-echo signal during assistant playback.

Current likely problem:

```text
correlationDecision = SelfEcho
but energy rule still returns Allow
```

Fix that.

For these input reasons:

```text
live_ducking
normal_capture
vad_triggered_capture
```

apply:

```text
if correlationScore >= CorrelationMinScore:
    SuppressAsSelfEcho
```

before energy-based allow rules.

For:

```text
fast_hard_stop_candidate
```

be slightly more careful:

```text
if correlationScore is very high and mic energy is not clearly above echo:
    suppress
else:
    allow/probe according to fast hard-stop policy
```

But since this phase is specifically "stop Merlin hearing itself," it is acceptable to suppress correlated self-echo strongly. Hard-stop tuning can come after self-echo is fixed.

---

## Decision priority

Suggested order during assistant playback:

```text
1. If gate disabled -> allow old behavior.
2. If VAD says no speech -> suppress/no action.
3. If playback inactive -> allow normal VAD behavior.
4. If correlation available and correlationScore >= threshold:
     SuppressAsSelfEcho for live_ducking/capture.
5. If mic energy clearly exceeds echo by strong margin and correlation is low:
     Allow as likely user speech.
6. If uncertain:
     suppress for live_ducking/capture during playback.
```

The key change:

```text
Correlation SelfEcho beats energy Allow for ducking/capture.
```

---

# Part 5 - Extend diagnostics file

The existing diagnostics file is:

```text
Merlin.Backend/Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl
```

Keep using this file.

Add fields:

```text
correlationAvailable
correlationScore
correlationDecision
bestDelayMs
correlationMinScore
correlationMinDelayMs
correlationMaxDelayMs
correlationDelayStepMs
correlationReason
```

Example JSONL line:

```json
{
  "timestampUtc": "2026-06-18T12:30:00.123Z",
  "inputReason": "live_ducking",
  "decision": "SuppressAsSelfEcho",
  "reason": "Playback/mic correlation indicates assistant self-echo.",
  "micEnergy": 0.16,
  "playbackEnergy": 0.045,
  "estimatedEchoEnergy": 0.02,
  "correlationAvailable": true,
  "correlationScore": 0.82,
  "correlationDecision": "SelfEcho",
  "bestDelayMs": 180,
  "correlationMinScore": 0.65,
  "assistantPlaybackActive": true,
  "vadSaysSpeech": true
}
```

This is critical so the user can test and send the file back.

Do not log raw audio.

---

# Part 6 - Config

Add/extend config under the existing self-speech suppression options.

Suggested:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "CorrelationDetectionEnabled": true,
    "CorrelationMinScore": 0.65,
    "CorrelationMinDelayMs": 0,
    "CorrelationMaxDelayMs": 250,
    "CorrelationDelayStepMs": 10,
    "CorrelationSuppressInputReasons": [
      "live_ducking",
      "normal_capture",
      "vad_triggered_capture"
    ],
    "CorrelationSuppressFastHardStopWhenVeryHigh": true,
    "CorrelationVeryHighScore": 0.85
  }
}
```

Defaults can be conservative, but for testing this feature it should be enabled.

---

# Part 7 - Tests

Add deterministic tests. Do not require real microphone/speakers.

Likely files:

```text
Merlin.Backend.Tests/PlaybackMicCorrelationDetectorTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
Merlin.Backend.Tests/PlaybackReferenceTapTests.cs
Merlin.Backend.Tests/BargeInTests.cs
```

---

## Correlation detector tests

Required:

```text
High_correlation_when_mic_matches_delayed_playback
Low_correlation_when_mic_is_unrelated_noise
Finds_best_delay_within_search_window
Unavailable_when_no_playback_reference
Unavailable_when_reference_energy_too_low
Unavailable_when_mic_energy_too_low
Handles_short_frames_without_throwing
Does_not_allocate_excessively_if_testable
```

Use simple synthetic signals:

```text
sine wave
random deterministic signal
delayed copy
unrelated random signal
```

A deterministic random signal is often better than sine because sine can correlate at multiple delays.

---

## Self-speech gate tests

Required:

```text
Correlation_self_echo_suppresses_live_ducking_even_when_energy_would_allow
Correlation_self_echo_suppresses_normal_capture_even_when_energy_would_allow
Correlation_self_echo_suppresses_vad_triggered_capture_even_when_energy_would_allow
Low_correlation_high_energy_allows_likely_user_speech
Correlation_unavailable_falls_back_to_energy_policy
Uncertain_during_playback_still_suppresses_ducking_capture
Diagnostics_include_correlation_fields
```

---

## Playback reference tests

Required if modifying `PlaybackReferenceTap`:

```text
Stores_recent_playback_pcm
Returns_delayed_reference_window
Downmixes_or_normalizes_if_needed
Returns_false_when_not_enough_history
Tracks_playback_active_started_stopped
Tracks_recent_playback_energy
```

---

## Regression tests

Make sure these still pass:

```text
silent speaker self-echo does not duck/capture
stop while speaking still reaches intended path where not self-echo
correction regeneration still works
family card -> family car still works
backchannel behavior stable
no playback behavior unchanged
```

---

# Part 8 - Manual verification checklist

After implementation, run with speakers.

Clear diagnostics file first:

```powershell
Remove-Item .\Merlin.Backend\Logs\SELF_SPEECH_GATE_DIAGNOSTICS.jsonl -ErrorAction SilentlyContinue
```

## Test 1 - silent speaker playback

1. Use speakers.
2. Ask Merlin a long-answer question.
3. Do not speak for 15-30 seconds.
4. Observe:
   - no repeated duck/restore
   - no capture
   - no self-interruption

Expected diagnostics:

```text
Many SuppressAsSelfEcho decisions.
correlationAvailable = true
correlationScore often above threshold
correlationDecision = SelfEcho
decision = SuppressAsSelfEcho
```

## Test 2 - user interruption later

Only after Test 1 succeeds:

1. Use speakers.
2. Ask Merlin a long-answer question.
3. Say:
   - stop
   - please stop
   - I mean family car

Expected:

```text
Merlin should still allow real user speech eventually.
If it does not, collect diagnostics and then tune user-speech-over-speaker behavior.
```

For this task, Test 1 is the main success criterion.

---

# Acceptance criteria

This task is complete when:

- [ ] PlaybackReferenceTap or equivalent exposes recent playback reference PCM.
- [ ] A correlation detector compares mic frames against delayed playback reference.
- [ ] Correlation score and best delay are logged.
- [ ] For live ducking and normal capture, high playback/mic correlation suppresses as self-echo before energy can allow.
- [ ] Energy allow no longer overrides obvious correlated self-echo.
- [ ] Silent speaker playback no longer causes repeated ducking/capture.
- [ ] Diagnostics file clearly shows correlation decisions.
- [ ] Unit tests cover delayed playback self-echo.
- [ ] Unit tests cover unrelated user-like mic audio.
- [ ] Existing backend tests pass.

---

# Known limitation

This task may make Merlin more conservative during speaker playback. That is acceptable for this phase.

The priority is:

```text
first stop Merlin hearing itself
then tune Merlin hearing the user over speakers
```

After self-echo suppression works, a follow-up task can improve:

```text
hard-stop STT during speaker playback
duck-before-STT probe
near-end speech detection
adaptive thresholds
```

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. How playback PCM is stored.
4. How mic samples are passed to correlation.
5. How correlation is calculated.
6. Delay search settings.
7. Correlation threshold/settings.
8. How correlation affects gate decisions.
9. How energy fallback still works.
10. Diagnostics file fields added.
11. Example diagnostic JSON line with correlation fields.
12. Tests added.
13. Tests run and results.
14. Known limitations.
15. What the user should test first.
16. What diagnostics file the user should send back.

Do not simply say "implemented." Explain the lifecycle with actual classes and methods changed.
