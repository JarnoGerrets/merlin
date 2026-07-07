---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/Merlin_Playback_Clock_Aligned_Reference_Tap_For_Correlation.md
classification: implementation-plan
related_features:
  - Voice Interruption System
status: current
imported_to_vault: true
---

# Merlin Playback-Clock-Aligned Reference Tap For Correlation

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Playback_Clock_Aligned_Reference_Tap_For_Correlation.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Fix the architectural flaw in Merlin's playback/microphone correlation system: the playback reference must represent **audio that is actually being played or consumed by the output device**, not audio that was generated, cached, queued, or planned.

The current correlation approach can fail if `PlaybackReferenceTap` receives full TTS chunks when they are generated or enqueued.

That is not playback-clock aligned.

The correct model is:

```text
TTS chunk generated / loaded / cached
↓
audio enters playback queue
↓
audio output system consumes samples over real time
↓
PlaybackReferenceTap receives only the samples that are actually being consumed for output
↓
mic frame arrives
↓
correlation compares mic frame against recently consumed playback samples
```

The goal of this task is to make correlation compare against the audio Merlin is **actually speaking right now**, not audio Merlin has merely generated.

---

## Why this matters

Recent logs showed a final answer with 6 TTS chunks.

Some chunks were cache hits and became available almost instantly, but each contained around 10 seconds of audio:

```text
Chunk 1: generated/loaded in ~23ms, audio duration ~10.28s
Chunk 2: generated/loaded in ~15ms, audio duration ~10.00s
```

Later chunks were generated while playback was already happening and took several seconds:

```text
Chunk 3: generation/total around 3.4s, audio duration ~8.68s
Chunk 4: total around 4.4s, audio duration ~8.96s
Chunk 5: total around 7.2s, audio duration ~7.2s
Chunk 6: total around 8.0s, audio duration ~8.08s
```

Total generated audio duration was around 53 seconds, while total generation time was around 23 seconds.

This proves:

```text
generation timeline != playback timeline
```

If the entire PCM chunk is pushed into `PlaybackReferenceTap` at generation/cache/queue time, the reference ring buffer can run far ahead of what speakers are actually outputting.

Then correlation compares:

```text
mic hears second 5 of actual speaker playback
```

against:

```text
reference buffer may already contain second 20+ of generated/queued audio
```

That will not match.

---

## Core rule

```text
PlaybackReferenceTap must mean "audio that has reached the playback consumption point",
not "audio that has been generated or queued".
```

The reference tap must be driven by the playback clock.

---

## Current observed failure pattern

The self-speech diagnostics showed:

```text
correlation unavailable or weak
energy gate allows almost everything
STT captures Merlin-like fragments
classifier ignores them
Merlin ducks/resumes/loops
```

The normal logs also showed STT transcribing fragments that look like Merlin's own speech, such as:

```text
"Merlin voice commands may include app names..."
"It is us, so that you can just listen as much as we can."
"That's it. That's it."
```

This strongly suggests the mic is hearing speaker playback, but correlation is not aligned enough to suppress it.

---

## Important distinction

Do not confuse these moments:

### TTS generated

```text
Chatterbox chunk complete
AudioBytes available
DurationSeconds known
```

This is **not** when the user hears it.

### Audio queued

```text
chunk added to playback queue / buffer
```

This is **not necessarily** when the user hears it.

### Audio consumed by output

```text
WaveOut / sample provider / output callback reads samples
```

This is the closest backend moment to "speaker is playing this audio now."

### Physical speaker/mic echo

```text
consumed samples leave speakers
room/mic captures delayed echo
```

Correlation should compare mic frames against samples consumed recently, with a delay search.

---

## Non-goals

Do not implement unrelated work in this task:

- semantic correction rewrite
- web search
- Codex integration
- new TTS provider
- new STT provider
- full WebRTC AEC replacement
- voice embeddings / Jarno voice learning
- hard-stop STT tuning
- global VAD threshold changes

This task is specifically about aligning the playback reference to the playback clock.

---

## Required investigation before coding

Inspect these files/classes if they exist:

```text
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/BargeIn/PlaybackReferenceTap.cs
Merlin.Backend/Services/BargeIn/PlaybackMicCorrelationDetector.cs
Merlin.Backend/Services/BargeIn/SelfSpeechSuppressionGate.cs
Merlin.Backend/Services/BargeIn/SelfSpeechGateDiagnosticsWriter.cs
Merlin.Backend/Services/ChatterboxTtsProvider.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Configuration/BargeInOptions.cs
Merlin.Backend.Tests/PlaybackReferenceTapTests.cs
Merlin.Backend.Tests/PlaybackMicCorrelationDetectorTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
Merlin.Backend.Tests/BargeInTests.cs
```

Search for:

```text
PlaybackReferenceTap
Push
PushPlayback
ReportPlaybackFrameEnergy
AudioBytes
BufferedBytes
BufferedWaveProvider
WaveOut
WaveOutEvent
IWaveProvider
ISampleProvider
Read(
AddSamples
QueueSpeech
Playback
Drain
CurrentPlaybackEnergy
RecentPlaybackEnergy
ReferenceWindow
TryGetReferenceWindow
```

Before editing, determine:

1. Where `PlaybackReferenceTap` is currently fed.
2. Is it fed at TTS chunk generation/cache time?
3. Is it fed when audio is enqueued?
4. Is it fed when audio is actually consumed by the output provider?
5. What audio output type is used: `WaveOut`, `WaveOutEvent`, `BufferedWaveProvider`, custom sample provider, etc.?
6. Is there a central `Read(...)`/dequeue point that provides samples to the audio device?
7. Can that point copy the exact consumed samples into `PlaybackReferenceTap`?
8. Does the playback output include volume changes/ducking before or after the tap?
9. Should reference tap receive pre-duck or post-duck samples?
10. How much render/output buffering exists between queue and speaker?

Mention findings in the final report.

---

# Part 1 - Identify the correct playback consumption point

## Goal

Find the backend point closest to actual playback output.

Ideal points:

```text
IWaveProvider.Read(...)
ISampleProvider.Read(...)
custom playback buffer dequeue
audio output callback
```

Bad points:

```text
TTS chunk complete
TTS cache hit
audio bytes returned from provider
chunk added to queue
full buffer added to BufferedWaveProvider
```

The tap should be called in the good point, not the bad point.

---

## Best implementation pattern

Wrap the provider that feeds the output device.

Example concept:

```csharp
public sealed class PlaybackReferenceWaveProvider : IWaveProvider
{
    private readonly IWaveProvider _inner;
    private readonly IPlaybackReferenceTap _tap;

    public WaveFormat WaveFormat => _inner.WaveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _inner.Read(buffer, offset, count);

        if (bytesRead > 0)
        {
            _tap.PushConsumedPcm(buffer.AsSpan(offset, bytesRead), WaveFormat);
        }

        return bytesRead;
    }
}
```

If using `ISampleProvider`, analogous:

```csharp
public int Read(float[] buffer, int offset, int count)
{
    var samplesRead = _inner.Read(buffer, offset, count);

    if (samplesRead > 0)
    {
        _tap.PushConsumedSamples(buffer.AsSpan(offset, samplesRead), sampleRate, channels);
    }

    return samplesRead;
}
```

Use the actual project audio stack.

---

## Reference tap location

The tap should receive samples:

```text
after queue/dequeue
before or at output consumption
```

It should not receive an entire chunk at once unless that entire chunk is truly consumed at once, which it is not.

---

# Part 2 - Pre-duck vs post-duck reference

## Decision required

Should `PlaybackReferenceTap` receive audio before or after speaker ducking volume is applied?

Recommended:

```text
Use post-duck samples if the output volume multiplier is applied in software before output.
```

Reason:

The mic hears the actual speaker output level, including ducking/restoration.

If the tap receives pre-duck audio while speakers output post-duck lower volume, energy/correlation scale may mismatch.

However, for normalized correlation, amplitude matters less than waveform shape. But energy and echo estimation do care.

If applying volume at hardware/output device level instead of sample multiplication, the tap may only see pre-volume samples. In that case, log the current output volume multiplier and include it in diagnostics.

Required diagnostic field if practical:

```text
playbackOutputVolumeMultiplier
```

---

# Part 3 - Keep generated/queued audio separate from consumed audio

## Goal

Do not throw away existing generation/queue behavior. Just avoid using it as playback reference.

Possible names:

```text
GeneratedAudio
QueuedAudio
ConsumedPlaybackReference
```

The correlation path should use:

```text
ConsumedPlaybackReference
```

not:

```text
GeneratedAudio
QueuedAudio
```

If existing `PlaybackReferenceTap.Push(...)` is called from generation/queue code, either:

1. Move that call to the consumption point, or
2. Rename old method to make it clear it is not valid for correlation, or
3. Add a new method like `PushConsumedPlaybackSamples(...)` and make correlation use only that stream.

Preferred:

```text
PlaybackReferenceTap receives only consumed output samples.
```

---

# Part 4 - Playback reference timing diagnostics

## Goal

Diagnostics should prove that the reference is playback-clock aligned.

Extend `SELF_SPEECH_GATE_DIAGNOSTICS.jsonl` or related diagnostics with:

```text
playbackReferenceSource
playbackReferenceIsConsumptionAligned
playbackConsumedSamplesTotal
playbackQueuedSamplesEstimate
playbackBufferedBytes
playbackElapsedMs
referenceNewestAgeMs
referenceOldestAgeMs
referenceBufferedMs
bestDelayMs
correlationScore
correlationAvailable
referenceWindowEnergy
```

Add if available:

```text
outputReadBytes
outputReadSamples
outputReadDurationMs
outputProviderReadIntervalMs
lastOutputReadAtUtc
ttsChunkIndex
ttsChunkGeneratedAtUtc
ttsChunkQueuedAtUtc
```

The important diagnostic:

```text
Is the reference newest sample close to the current playback output time?
```

---

## Required log line during playback consumption

Add low-noise logs or diagnostics counters, not necessarily every frame at Information.

Example debug line or diagnostics file summary:

```json
{
  "event": "playback_reference_consumed",
  "timestampUtc": "2026-06-18T13:45:00.123Z",
  "correlationId": "...",
  "samples": 480,
  "sampleRate": 48000,
  "durationMs": 10,
  "totalConsumedSamples": 123456,
  "referenceBufferedMs": 500,
  "source": "output_read"
}
```

If per-frame logging is too noisy, expose counters in the gate diagnostics instead.

---

# Part 5 - Correlation delay range after alignment

Once reference is consumption-aligned, delay search should cover physical/output/input latency, not generation queue latency.

Suggested first range:

```text
0-250ms
```

But for diagnostics, allow wider:

```json
"CorrelationMaxDelayMs": 500
```

or:

```json
"CorrelationMaxDelayMs": 750
```

Use a coarse step like:

```text
10ms or 20ms
```

during diagnostics.

If best delay consistently lands near the max, increase range.

---

# Part 6 - Tests

Add deterministic tests. Do not require real audio output device.

Likely files:

```text
Merlin.Backend.Tests/PlaybackReferenceTapTests.cs
Merlin.Backend.Tests/PlaybackReferenceWaveProviderTests.cs
Merlin.Backend.Tests/PlaybackMicCorrelationDetectorTests.cs
Merlin.Backend.Tests/AssistantSpeechPlaybackServiceTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
```

---

## Playback reference provider/wrapper tests

If adding an `IWaveProvider`/`ISampleProvider` wrapper, test:

```text
Read_forwards_to_inner_provider
Read_pushes_only_bytes_actually_read_to_reference_tap
Read_does_not_push_when_inner_returns_zero
Multiple_reads_push_stream_in_consumption_order
Does_not_push_entire_source_buffer_at_once
Preserves_wave_format
```

Use a fake inner provider returning known chunks over multiple reads.

---

## Consumed reference timing tests

```text
TTS_chunk_available_does_not_populate_correlation_reference_until_read
Queued_audio_does_not_advance_reference_until_consumed
Output_reads_advance_reference_by_exact_samples_read
Reference_ring_contains_last_consumed_samples
Reference_window_matches_mid_chunk_playback_position
```

The "mid-chunk" test is critical.

Example:

```text
fake audio = samples 0..9999
enqueue all 10000 samples
simulate output reads first 2000 samples
reference should contain only 0..1999
not 2000..9999
```

---

## Correlation tests after alignment

```text
Mic_frame_matching_mid_chunk_consumed_audio_correlates_high
Mic_frame_matching_future_unconsumed_audio_does_not_correlate
Delayed_mic_frame_finds_expected_bestDelay
SelfEcho_correlation_suppresses_ducking_after_consumed_reference
```

---

# Part 7 - Integration with current diagnostics

After implementing, run silent speaker playback.

Expected:

```text
correlationAvailable = true for most frames after playback starts
referenceWindowEnergy similar scale to consumed playback energy
bestDelayMs plausible and stable
correlationScore high when mic hears Merlin speaker output
decision = SuppressAsSelfEcho for live_ducking/capture
```

If correlation is still unavailable, diagnostics should now distinguish:

```text
not enough consumed history
reference window low energy
sample rate mismatch
wrong delay range
mic/playback waveforms too different
```

---

# Part 8 - Manual verification checklist

Clear diagnostics:

```powershell
Remove-Item .\Merlin.Backend\Logs\SELF_SPEECH_GATE_DIAGNOSTICS.jsonl -ErrorAction SilentlyContinue
```

Run:

```text
Use speakers.
Ask Merlin a long question.
Do not speak for 15-30 seconds.
```

Expected:

```text
No duck/restore loop.
No capture.
No STT.
No self-interruption.
Diagnostics show correlation suppressing self-echo.
```

Do not tune user interruption yet. First prove silent playback works.

---

# Acceptance criteria

This task is complete when:

- [ ] `PlaybackReferenceTap` is fed at playback consumption time, not TTS generation/queue time.
- [ ] Entire TTS chunks are not pushed to correlation reference immediately unless actually consumed.
- [ ] Mid-chunk playback reference works.
- [ ] Correlation ring buffer contains recently consumed samples.
- [ ] Diagnostics prove reference source is consumption-aligned.
- [ ] Correlation availability improves during active playback.
- [ ] High correlation suppresses self-echo before energy can allow.
- [ ] Silent speaker playback no longer self-ducks/self-captures.
- [ ] Existing backend tests pass.

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. Where `PlaybackReferenceTap` was previously fed.
4. Where it is now fed.
5. Whether it was previously generation/queue aligned.
6. How the new playback-consumption wrapper works.
7. Whether reference uses pre-duck or post-duck samples.
8. How mid-chunk playback is handled.
9. What diagnostics fields were added.
10. Example diagnostic line proving consumption alignment.
11. Tests added.
12. Tests run and results.
13. Known limitations.
14. What the user should test next.
15. Which diagnostics file the user should send back.

Do not simply say "implemented." Explain the lifecycle with actual classes/methods changed.

---

# Recommended first implementation cut

If the full version is too large, implement this first:

```text
1. Find where audio is actually read by the output device.
2. Add a playback-reference wrapper around that read/dequeue point.
3. Ensure reference tap receives only consumed samples.
4. Add mid-chunk tests.
5. Keep existing correlation detector, but feed it aligned reference.
6. Run full backend tests.
```

Then use diagnostics to decide whether delay range/correlation threshold still need tuning.
