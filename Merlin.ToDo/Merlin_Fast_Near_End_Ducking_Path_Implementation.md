# Merlin Fast Near-End Ducking Path Implementation

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Fast_Near_End_Ducking_Path_Implementation.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Fix the current interruption UX issue where Merlin does not duck quickly enough when the user starts speaking.

The user reports:

```text
Merlin is no longer constantly hearing itself with the headset mic.
But when I start speaking, Merlin does not accurately and quickly duck.
```

The current bug is not primarily:

```text
hard-stop classification
correction regeneration
STT transcript quality
```

The immediate UX problem is earlier:

```text
User starts talking
↓
Merlin should lower playback volume immediately
↓
Only later should Merlin decide whether this is hard stop, correction, side comment, backchannel, etc.
```

The goal of this task is to add a **fast near-end ducking path** that reacts to likely user speech onset before the normal barge-in capture threshold completes.

---

## Current observed behavior

Logs indicate ducking often starts for reasons like:

```text
fast_hard_stop_candidate
vad_triggered
vad_active_frame
```

and after accumulated evidence like:

```text
ConsecutiveSpeechMs: 161
ConsecutiveSpeechMs: 355
ConsecutiveSpeechMs: 413
```

This means ducking is still tied too closely to:

```text
confirmed VAD speech
fast hard-stop candidate
barge-in capture trigger
```

That is too late for comfort.

Ducking should feel immediate:

```text
near-end speech onset detected
↓
playback volume ducks quickly
```

Then the slower system can decide:

```text
Should this become capture?
Should STT run?
Is it stop/correction/side comment?
Should playback resume?
```

---

## Core design principle

Ducking is not the same as interruption.

### Ducking

Ducking is a fast comfort/safety response:

```text
The user might be speaking.
Lower Merlin now so the user can hear themselves.
```

It should be fast and reversible.

### Barge-in capture

Capture is a stronger decision:

```text
The user is probably saying something meaningful.
Collect audio and maybe run STT/classification.
```

It can use slower thresholds.

### Hard stop / correction

Hard stop and correction are semantic outcomes:

```text
STT + classifier decides what the user meant.
```

They are slower than ducking.

Therefore:

```text
Ducking must not wait for STT or classifier.
Ducking should not even wait for full capture threshold.
```

---

## Desired behavior

### User starts speaking over Merlin

```text
Merlin speaking
↓
User starts speaking
↓
within ~50-100ms Merlin ducks playback volume
↓
user can hear themselves
↓
normal VAD/capture/classification continues independently
```

### User stops speaking quickly

```text
User stops
↓
after short hangover, around 150-250ms
↓
Merlin restores playback volume
```

### Self-echo

```text
Merlin's own speaker/headset leakage
↓
self-speech/correlation gate suppresses it
↓
no fast duck
```

### Normal capture

```text
If user keeps speaking long enough
↓
normal capture path starts
↓
STT/classification happens
```

---

## Non-goals

Do not implement these here:

- semantic correction rewrite
- web search
- Codex integration
- new STT provider
- new TTS provider
- full AEC rewrite
- speaker identity / Jarno voice learning
- playback/mic correlation ring-buffer redesign
- hard-stop STT probe redesign
- capability routing changes

This task is only about:

```text
fast user-speech-onset ducking
separating duck threshold from capture threshold
diagnostics and tests
```

---

## Required investigation before coding

Inspect these files/classes if they exist:

```text
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/SpeakerDuckingService.cs
Merlin.Backend/Services/BargeIn/SelfSpeechSuppressionGate.cs
Merlin.Backend/Services/BargeIn/PlaybackReferenceTap.cs
Merlin.Backend/Services/BargeIn/BargeInInterfaces.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/BargeIn/BargeInDiagnosticsLogger.cs
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Configuration/BargeInOptions.cs
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
```

Search for:

```text
vad_active_frame
vad_triggered
fast_hard_stop_candidate
StartDucking
StopDucking
SoftPausedForUserSpeech
CapturingInterruption
ConsecutiveSpeechMs
MinimumSpeechMs
MinSpeechMs
SpeechHangover
Ducking
SelfSpeechSuppressionGate
live_ducking
normal_capture
vad_triggered_capture
fast_hard_stop_candidate
```

Before editing, determine:

1. What currently triggers `SpeakerDuckingService.StartDucking`.
2. Whether ducking waits for full VAD trigger / consecutive speech threshold.
3. Whether the self-speech gate is called for each live frame before ducking.
4. Whether there is a lightweight per-frame signal before `VAD possible speech detected`.
5. Whether raw mic energy, echo-reduced energy, or VAD confidence is available per frame.
6. Whether the current self-speech gate can evaluate `live_ducking` on every frame.
7. Whether there is already a speech hangover timer for restoring ducking.
8. Whether ducking can be applied repeatedly/idempotently without audio glitches.
9. Whether ducking is currently applied immediately to active playback output.
10. Whether diagnostics can measure onset latency.

Mention findings in the final report.

---

# Part 1 - Add fast near-end ducking signal

## Goal

Create a fast path that detects likely user speech onset before the normal barge-in/capture threshold.

Suggested concepts:

```text
near_end_speech_onset
fast_ducking_candidate
live_near_end_ducking
```

This should be lighter and faster than:

```text
VAD possible speech detected
fast hard-stop candidate
CapturingInterruption
```

---

## Candidate input signals

Use whichever are already available:

```text
raw mic frame energy
echo-reduced mic frame energy
VAD confidence
WebRTC APM near-end speech signal if available
noise floor ratio
self-speech gate result
```

The fast duck path should still pass through self-speech suppression, otherwise Merlin will duck on itself again.

---

## Suggested fast duck trigger

While assistant playback is active:

```text
if mic/VAD frame suggests possible near-end speech
and self-speech gate does not suppress as assistant echo
and signal is present for FastDuckingMinSpeechMs
then StartDuckingAsync("near_end_speech_onset")
```

Suggested first values:

```text
FastDuckingMinSpeechMs = 30-60ms
FastDuckingMinVadConfidence = 0.35-0.50
FastDuckingEnergyRatioOverNoise = 3.0-5.0
FastDuckingHangoverMs = 180-250ms
```

Do not wait for the normal capture threshold, which may be 150-350ms.

---

## Distinguish fast duck from capture

There should be separate thresholds:

```text
Fast duck threshold:
  low latency, reversible, comfort response

Capture threshold:
  higher confidence, starts STT/capture pipeline
```

Example:

```text
30-60ms likely user speech -> duck
150-350ms confirmed speech -> capture
```

Do not automatically start capture just because fast ducking started.

---

# Part 2 - Add configuration

Extend `BargeInOptions`.

Suggested config:

```json
"BargeIn": {
  "FastNearEndDucking": {
    "Enabled": true,
    "RequireAssistantPlayback": true,
    "MinSpeechMs": 50,
    "MinVadConfidence": 0.4,
    "MinEnergyRatioOverNoise": 4.0,
    "MinAbsoluteEnergy": 0.008,
    "HangoverMs": 220,
    "UseSelfSpeechGate": true,
    "InputReason": "fast_near_end_ducking",
    "LogDecisions": true
  }
}
```

If the repo prefers not to add a nested object, integrate with existing ducking options.

---

# Part 3 - Add/extend self-speech gate input reason

The self-speech gate currently appears to use reasons like:

```text
live_ducking
normal_capture
vad_triggered_capture
fast_hard_stop_candidate
```

Add:

```text
fast_near_end_ducking
```

Policy:

```text
if assistant playback inactive:
  normal no-playback behavior

if assistant playback active:
  SelfEcho -> suppress
  Uncertain -> suppress unless signal is strong/sustained enough
  LikelyUser/Allow -> allow fast duck
```

Do not let weak self-echo trigger fast ducking.

---

# Part 4 - Apply ducking immediately

Once fast near-end ducking allows:

```text
await SpeakerDuckingService.StartDuckingAsync(
    reason: "near_end_speech_onset" or "fast_near_end_ducking")
```

Requirements:

- idempotent if already ducked
- does not restart fade constantly
- applies volume to active output immediately
- does not start STT/capture by itself
- records timing

---

# Part 5 - Restore with short hangover

Fast ducking should restore after short silence if capture does not take over.

Suggested behavior:

```text
fast near-end speech active -> duck
no allowed near-end speech for HangoverMs -> restore
```

But if normal capture starts, let capture lifecycle own ducking/restore.

Avoid rapid flutter:

```text
do not restore on tiny gaps
use 180-250ms hangover
```

---

# Part 6 - Diagnostics

Add useful logs and/or extend diagnostics file.

The key metric:

```text
time from first likely user speech frame to actual volume duck applied
```

Add log fields:

```text
timestampUtc
correlationId
inputReason = fast_near_end_ducking
micEnergy
noiseFloor
energyRatioOverNoise
vadConfidence
selfSpeechDecision
duckingDecision
fastDuckingConsecutiveMs
fastDuckingMinSpeechMs
duckingAlreadyActive
duckApplied
duckApplyLatencyMs
reason
```

If `SELF_SPEECH_GATE_DIAGNOSTICS.jsonl` is already used, add `fast_near_end_ducking` entries there.

Also add normal app logs:

```text
Fast near-end ducking started. ConsecutiveMs=..., VadConfidence=..., Energy=..., Reason=...
Fast near-end ducking restored. HangoverMs=...
Fast near-end ducking suppressed by self-speech gate. Decision=...
```

---

# Part 7 - Tests

Add deterministic tests. Do not require real audio devices.

Likely files:

```text
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/FastNearEndDuckingTests.cs
Merlin.Backend.Tests/SelfSpeechSuppressionGateTests.cs
```

## Required tests

### Fast duck starts before normal capture

```text
Fast_near_end_speech_ducks_before_capture_threshold
```

Example:

```text
FastDuckingMinSpeechMs = 50
NormalCaptureMinSpeechMs = 300
Simulate 60ms likely user speech
Assert ducking started
Assert capture not started yet
```

### Self-echo does not duck

```text
Self_echo_frames_do_not_start_fast_ducking
```

### User speech does duck

```text
Likely_user_speech_starts_fast_ducking
```

### Hangover restores

```text
Fast_ducking_restores_after_hangover_when_capture_does_not_start
```

### Capture takes over

```text
Normal_capture_takes_over_ducking_if_speech_continues
```

### Idempotency

```text
Repeated_allowed_frames_do_not_restart_ducking_fade
```

### Existing behavior

```text
Hard_stop_still_works_when_capture_threshold_reached
Correction_regeneration_still_works
Self_echo_suppression_still_blocks_speaker_leakage
No_playback_behavior_unchanged
```

---

# Part 8 - Manual verification checklist

Use the headset mic first.

Clear diagnostics if useful:

```powershell
Remove-Item .\Merlin.Backend\Logs\SELF_SPEECH_GATE_DIAGNOSTICS.jsonl -ErrorAction SilentlyContinue
```

## Test A - user speaks over Merlin

1. Ask Merlin a long question.
2. While Merlin speaks, start talking normally.
3. Observe whether Merlin ducks immediately.

Expected:

```text
Merlin volume drops almost as soon as you begin speaking.
You can hear yourself.
Ducking does not wait until STT/capture.
```

## Test B - short phrase

While Merlin speaks, say:

```text
stop
```

Expected for this task:

```text
Merlin ducks immediately.
```

It may still not cancel correctly until hard-stop STT/probe is fixed. That is okay for this task.

## Test C - silence

Let Merlin speak while you are silent.

Expected:

```text
No self-ducking.
```

## Test D - open-room mic later

Repeat after buying/using the room mic.

Expected:

```text
self-echo may require tuning, but fast ducking should react to real speech if gate allows.
```

---

# Acceptance criteria

This task is complete when:

- [ ] Ducking has a fast near-end path separate from capture/STT/classification.
- [ ] User speech can trigger ducking before normal VAD/capture threshold.
- [ ] Ducking can start within roughly 50-100ms of likely user speech onset.
- [ ] Fast ducking uses self-speech suppression so Merlin does not duck on itself.
- [ ] Fast ducking does not start STT/capture by itself.
- [ ] Normal capture/hard-stop/correction behavior remains separate.
- [ ] Ducking restores after short hangover if speech stops and capture does not take over.
- [ ] Tests cover fast duck before capture threshold.
- [ ] Existing backend tests pass.

---

# Known limitation

This task only fixes:

```text
Merlin ducks quickly when user starts speaking.
```

It does not guarantee:

```text
Merlin correctly transcribes stop.
Merlin correctly classifies correction.
Merlin cancels immediately.
```

Those are later steps.

This is intentional. The current UX bug is ducking onset latency.

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. Where ducking was previously triggered.
4. Where the new fast near-end ducking path is triggered.
5. How fast ducking differs from capture.
6. Config values added.
7. How self-speech gate is used for fast ducking.
8. How ducking restore/hangover works.
9. How duck-apply latency is logged.
10. Tests added.
11. Tests run and results.
12. Known limitations.
13. What the user should test next.

Do not simply say "implemented." Explain the lifecycle with actual classes and methods changed.

---

# Recommended first implementation cut

If the full implementation is too large, implement this first:

```text
1. Add FastNearEndDuckingOptions.
2. Add a fast ducking state counter in BargeInCoordinator.
3. On each mic frame while assistant playback is active, evaluate fast_near_end_ducking through self-speech gate.
4. Duck after 50ms of allowed likely user speech.
5. Restore after 220ms silence if capture did not start.
6. Add tests proving duck starts before capture.
```
