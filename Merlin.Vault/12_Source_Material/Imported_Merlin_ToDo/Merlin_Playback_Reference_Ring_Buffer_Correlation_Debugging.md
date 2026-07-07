---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/Merlin_Playback_Reference_Ring_Buffer_Correlation_Debugging.md
classification: bug-report
related_features:
  - Voice Interruption System
status: still-useful
imported_to_vault: true
---

# Merlin Playback Reference Ring Buffer And Correlation Availability Debugging

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Playback_Reference_Ring_Buffer_Correlation_Debugging.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Debug and fix the playback/microphone correlation path used for self-echo suppression.

The current correlation implementation is not useful yet because diagnostics show correlation is mostly unavailable, even while assistant playback is active and playback energy exists.

The goal of this task is **not** to tune thresholds blindly.

The goal is to make the correlation path trustworthy:

```text
Merlin is speaking through speakers
↓
PlaybackReferenceTap has recent playback PCM
↓
SelfSpeechSuppressionGate receives mic frame samples
↓
PlaybackMicCorrelationDetector can compare mic frame to delayed playback reference
↓
correlationAvailable is true most of the time during playback
↓
high correlation suppresses self-echo before energy can allow
```

---

## Current observed problem

Recent `SELF_SPEECH_GATE_DIAGNOSTICS.jsonl` analysis showed:

```text
Total entries: 1961
Allow: 1960
Uncertain: 1
SuppressAsSelfEcho: 0
```

Correlation stats:

```text
correlationDecision = Unavailable: 1461 entries
correlationDecision = LikelyUser: 466 entries
correlationDecision = WeakCorrelation: 34 entries
correlationDecision = SelfEcho: 0 entries
```

So correlation is failing mostly before it can make a decision.

The diagnostics repeatedly indicate:

```text
Correlation unavailable because playback reference energy is below threshold.
```

But the same entries show non-zero playback energy:

```text
playbackEnergy median:        ~0.045
currentPlaybackEnergy median: ~0.045
recentPlaybackEnergy median:  ~0.043
assistantPlaybackActive: true
```

This is contradictory enough to suspect a bug in the reference window / ring buffer / delay lookup path.

---

## Main hypothesis

The correlation detector is probably not reading the same playback audio that `PlaybackReferenceTap` uses for energy.

Possible causes:

```text
1. Playback PCM ring buffer is not populated correctly.
2. Playback energy is calculated from one path, but correlation reads another path.
3. TryGetReferenceWindow returns zeros/silence.
4. Ring buffer indexing is wrong.
5. Delay-to-sample conversion is wrong.
6. The requested delayed window points to unwritten/future/cleared samples.
7. Correlation requests more samples than are available.
8. Sample rate mismatch between mic frame and playback reference.
9. Mic frame and playback frame are at different processing stages or formats.
10. AEC-processed mic audio no longer correlates with raw playback reference.
```

Do not assume threshold tuning will solve this until reference-window diagnostics prove the buffer is valid.

---

## Important scope

This task focuses on:

```text
PlaybackReferenceTap ring buffer
TryGetReferenceWindow correctness
correlation availability
reference window energy diagnostics
sample rate / sample count alignment
correlation diagnostics
tests
```

This task does **not** focus on:

```text
hard-stop STT quality
semantic correction rewrite
web search
new TTS provider
new STT provider
full AEC rewrite
voice embeddings
global VAD sensitivity changes
```

First make correlation available and trustworthy.

Then later tune suppression/user interruption behavior.

---

## Required investigation before coding

Inspect these files/classes if they exist:

```text
Merlin.Backend/Services/BargeIn/PlaybackReferenceTap.cs
Merlin.Backend/Services/BargeIn/PlaybackMicCorrelationDetector.cs
Merlin.Backend/Services/BargeIn/SelfSpeechSuppressionGate.cs
Merlin.Backend/Services/BargeIn/SelfSpeechGateDiagnosticsWriter.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/BargeInInterfaces.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Configuration/BargeInOptions.cs
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/PlaybackMicCorrelationDetectorTests.cs
Merlin.Backend.Tests/PlaybackReferenceTapTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
```

Search for:

```text
PlaybackReferenceTap
TryGetReferenceWindow
ReportPlaybackFrameEnergy
Push
PCM
Reference
Ring
Buffer
WritePosition
ReadPosition
DelayMs
SampleRate
SampleCount
MicSamples
Correlation
ReferenceEnergy
BelowThreshold
correlationAvailable
correlationDecision
bestDelayMs
```

Before editing, determine and report:

1. How playback PCM is pushed into `PlaybackReferenceTap`.
2. Whether PCM is mono/stereo and how it is normalized.
3. Whether playback energy is calculated from the same samples stored for correlation.
4. How much PCM history is retained.
5. Whether the ring buffer has enough samples for 0-250ms delay searches.
6. How `TryGetReferenceWindow(delayMs, sampleCount, destination)` calculates start/end positions.
7. Whether delay means "playback samples from delayMs ago" or something else.
8. Whether mic sample rate matches playback sample rate.
9. Whether mic sample count is a reasonable frame size.
10. Whether correlation is using echo-reduced mic samples or raw mic samples.
11. Whether AEC may destroy waveform similarity enough to make raw-playback correlation weak.
12. Whether diagnostics currently log enough to distinguish "no history" from "low reference energy."

Mention findings in the final report.

---

# Part 1 - Add detailed reference-window diagnostics

## Goal

The diagnostics file must explain why correlation is unavailable.

Currently, it only says approximately:

```text
playback reference energy below threshold
```

That is not enough.

Add fields to the existing diagnostics file:

```text
Merlin.Backend/Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl
```

---

## Required new diagnostic fields

Add these fields where available:

```text
correlationAvailable
correlationScore
correlationDecision
bestDelayMs
correlationReason

referenceWindowAvailable
referenceWindowEnergy
referenceWindowSampleCount
requestedMicSampleCount
requestedDelayMs
requestedDelayMinMs
requestedDelayMaxMs
requestedDelayStepMs

playbackRingBufferedSamples
playbackRingCapacitySamples
playbackRingBufferedMs
playbackTapSampleRate
micSampleRate
sampleRateMatches

playbackHistoryOldestAgeMs
playbackHistoryNewestAgeMs
playbackWritePosition
playbackReferenceReadFailures
correlationUnavailableReason
```

Add if easy:

```text
micSampleCount
micFrameDurationMs
micEnergyForCorrelation
referenceEnergyThreshold
micEnergyThreshold
numberOfDelayWindowsChecked
numberOfDelayWindowsAvailable
numberOfDelayWindowsSkippedLowEnergy
maxReferenceEnergySeen
```

It is fine if some fields are null in early versions, but the final file must make correlation unavailability diagnosable.

---

## Example diagnostic JSONL line

```json
{
  "timestampUtc": "2026-06-18T13:10:00.123Z",
  "inputReason": "live_ducking",
  "decision": "Allow",
  "reason": "Mic energy clearly exceeds expected playback echo.",
  "micEnergy": 0.17,
  "playbackEnergy": 0.045,
  "estimatedEchoEnergy": 0.02,
  "correlationAvailable": false,
  "correlationDecision": "Unavailable",
  "correlationReason": "Reference window energy below threshold.",
  "referenceWindowAvailable": true,
  "referenceWindowEnergy": 0.0003,
  "referenceWindowSampleCount": 960,
  "requestedMicSampleCount": 960,
  "requestedDelayMinMs": 0,
  "requestedDelayMaxMs": 250,
  "requestedDelayStepMs": 10,
  "playbackRingBufferedSamples": 24000,
  "playbackRingCapacitySamples": 48000,
  "playbackRingBufferedMs": 500,
  "playbackTapSampleRate": 48000,
  "micSampleRate": 48000,
  "sampleRateMatches": true,
  "numberOfDelayWindowsChecked": 26,
  "numberOfDelayWindowsAvailable": 26,
  "numberOfDelayWindowsSkippedLowEnergy": 26,
  "maxReferenceEnergySeen": 0.0003
}
```

The key goal is to know whether:

```text
ring buffer has no data
window lookup is wrong
sample rate mismatch
reference energy threshold is wrong
reference samples are zero
delay search is wrong
```

---

# Part 2 - Add a correlation debug snapshot model

## Goal

Make correlation debugging structured rather than ad hoc strings.

Suggested models:

```text
PlaybackReferenceDebugSnapshot
CorrelationDebugSnapshot
```

Suggested shape:

```csharp
public sealed record PlaybackReferenceDebugSnapshot(
    bool IsPlaybackActive,
    int SampleRate,
    int BufferedSamples,
    int CapacitySamples,
    double BufferedMilliseconds,
    double CurrentPlaybackEnergy,
    double RecentPlaybackEnergy,
    int WritePosition,
    DateTimeOffset? PlaybackStartedAt,
    DateTimeOffset? LastWriteAt);
```

Suggested correlation debug snapshot:

```csharp
public sealed record CorrelationDebugSnapshot(
    bool CorrelationAvailable,
    string CorrelationDecision,
    string Reason,
    double BestCorrelationScore,
    int? BestDelayMs,
    int DelayWindowsChecked,
    int DelayWindowsAvailable,
    int DelayWindowsSkippedLowEnergy,
    double MaxReferenceEnergySeen,
    double ReferenceEnergyThreshold,
    int RequestedSampleCount,
    int MicSampleRate,
    int PlaybackSampleRate,
    bool SampleRateMatches);
```

Use these internally or directly in diagnostics.

---

# Part 3 - Verify and fix ring-buffer indexing

## Goal

`TryGetReferenceWindow(delayMs, sampleCount, destination)` should return the playback samples that would plausibly arrive in the mic after that delay.

For a current mic frame at time `now`, and delay `D`:

```text
reference window should end approximately D milliseconds before the latest playback write
```

Pseudo:

```text
endIndex = writePosition - delaySamples
startIndex = endIndex - sampleCount
copy [startIndex, endIndex) from ring buffer with wraparound
```

This must handle:

```text
wraparound
not enough history
partial buffer during startup
delay larger than available history
sampleCount larger than available history
```

---

## Required tests for ring buffer

Add or update tests:

```text
TryGetReferenceWindow_returns_latest_samples_for_zero_delay
TryGetReferenceWindow_returns_delayed_samples_for_known_delay
TryGetReferenceWindow_handles_wraparound
TryGetReferenceWindow_returns_false_when_not_enough_history
TryGetReferenceWindow_does_not_return_zero_window_when_buffer_has_signal
TryGetReferenceWindow_sample_count_matches_request
Playback_energy_matches_energy_of_recent_reference_samples
```

Use a deterministic sequence, for example:

```text
0, 1, 2, 3, 4, ...
```

or a deterministic waveform, so expected delayed windows can be asserted exactly.

---

# Part 4 - Verify sample formats and sample rates

## Goal

Correlation is only meaningful if mic and playback samples are comparable.

Check:

```text
playback reference sample rate
mic sample rate
sample format
channel count
normalization
AEC stage
```

---

## Sample rate policy

First pass:

```text
If micSampleRate != playbackSampleRate:
    mark correlation unavailable
    log sampleRateMatches = false
```

Do not silently compare mismatched sample rates.

Later improvement can resample.

---

## Sample format policy

Normalize both mic and playback to mono float in roughly `[-1, 1]`.

If playback is stereo:

```text
mono = (left + right) / 2
```

If PCM16:

```text
float = sample / 32768.0
```

If float already:

```text
clamp/validate reasonable range
```

Add tests if conversion code exists.

---

# Part 5 - Verify correlation detector energy thresholds

## Goal

Determine whether `ReferenceEnergyThreshold` is too high or whether reference windows are actually too quiet/zero.

Add config if missing:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "CorrelationReferenceEnergyThreshold": 0.00001,
    "CorrelationMicEnergyThreshold": 0.00001
  }
}
```

Do not just lower thresholds blindly. Use diagnostics first.

If diagnostics show:

```text
PlaybackReferenceTap.RecentPlaybackEnergy = 0.045
maxReferenceEnergySeen = 0.0003
```

then the ring buffer/window energy scale differs from playback energy scale, or the ring buffer lookup is wrong.

If diagnostics show:

```text
RecentPlaybackEnergy = 0.045
maxReferenceEnergySeen = 0.043
```

but threshold is higher than that, then threshold is wrong.

---

# Part 6 - Make correlation decision authoritative only when available

## Goal

Keep the existing desired policy, but only after correlation availability is reliable.

For:

```text
live_ducking
normal_capture
vad_triggered_capture
```

policy should be:

```text
if correlationAvailable && correlationDecision == SelfEcho:
    SuppressAsSelfEcho
else:
    fall back to energy policy
```

Do not allow energy to override `SelfEcho`.

But do not force suppression if correlation is unavailable. Instead, make unavailable visible in logs.

---

# Part 7 - Optional: test with raw vs AEC-processed mic samples

## Goal

If correlation remains weak after ring-buffer correctness is fixed, AEC may be altering the mic waveform before correlation.

Experiment:

```text
correlate raw mic frame against playback reference
correlate echo-reduced/AEC mic frame against playback reference
log both scores
```

Do not commit a complicated dual-path unless it is clean. But diagnostics should help determine whether this is the reason.

Suggested diagnostic fields:

```text
rawCorrelationScore
aecCorrelationScore
correlationInputStage
```

If raw mic correlates strongly but AEC-processed mic does not, run self-echo correlation before AEC or use raw mic for correlation only.

---

# Part 8 - Tests

Add deterministic tests. Do not require real audio devices.

Likely files:

```text
Merlin.Backend.Tests/PlaybackReferenceTapTests.cs
Merlin.Backend.Tests/PlaybackMicCorrelationDetectorTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
```

Required test groups:

## Ring buffer tests

```text
Stores_recent_playback_pcm
Returns_zero_delay_window_correctly
Returns_delayed_window_correctly
Handles_wraparound
Returns_false_when_not_enough_samples
Reports_buffered_sample_count
Reports_buffered_ms
Does_not_return_silence_for_known_signal
Energy_of_reference_window_matches_expected_signal
```

## Correlation detector tests

```text
Correlation_available_when_reference_window_has_signal
Unavailable_reason_no_reference_history
Unavailable_reason_reference_energy_below_threshold
Unavailable_reason_sample_rate_mismatch
High_correlation_for_delayed_copy
Low_correlation_for_unrelated_signal
Best_delay_matches_known_delay
Threshold_self_echo_decision_when_score_above_min
Weak_correlation_when_score_below_min
```

## Gate integration tests

```text
SelfEcho_correlation_suppresses_live_ducking_before_energy_allows
SelfEcho_correlation_suppresses_capture_before_energy_allows
Correlation_unavailable_logs_debug_fields_and_falls_back_to_energy
Diagnostics_include_reference_window_fields
```

---

# Part 9 - Manual verification after implementation

After implementation, clear the diagnostics file:

```powershell
Remove-Item .\Merlin.Backend\Logs\SELF_SPEECH_GATE_DIAGNOSTICS.jsonl -ErrorAction SilentlyContinue
```

Then run silent speaker playback:

```text
Use speakers.
Ask Merlin a long question.
Do not speak for 15-30 seconds.
```

Expected diagnostics:

```text
correlationAvailable = true for most frames after playback warmup
referenceWindowAvailable = true
referenceWindowEnergy is similar scale to playbackEnergy
correlationScore often above threshold if mic hears playback
decision = SuppressAsSelfEcho for live_ducking/capture
```

If not, the diagnostics should now reveal why.

---

# Acceptance criteria

This task is complete when:

- [ ] Correlation unavailability is fully explained by diagnostics.
- [ ] Diagnostics include reference window availability/energy/sample count/ring buffer state.
- [ ] `TryGetReferenceWindow` is tested with deterministic delayed samples.
- [ ] Playback reference ring buffer does not return zeros when it should contain signal.
- [ ] Sample rate mismatch is detected and logged.
- [ ] Correlation is available for most frames during active playback after warmup.
- [ ] If correlation says `SelfEcho`, energy allow cannot override it for ducking/capture.
- [ ] Existing backend tests pass.
- [ ] User has a clear diagnostics file to send back.

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. Why correlation was mostly unavailable.
4. Whether ring-buffer indexing was wrong.
5. Whether sample rate/format mismatch existed.
6. Whether reference windows were zero/too quiet and why.
7. What new diagnostics fields were added.
8. Example JSONL line with reference window diagnostics.
9. How `TryGetReferenceWindow` now works.
10. What tests were added.
11. Tests run and results.
12. Known limitations.
13. What the user should test next.
14. Which diagnostics file the user should send back.

Do not simply say "implemented." Explain the lifecycle with actual classes and methods changed.
