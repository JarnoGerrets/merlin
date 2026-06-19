# Merlin Self-Speech Gate Diagnostics And Stricter Echo Policy

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Self_Speech_Gate_Diagnostics_And_Stricter_Echo_Policy.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Fix and diagnose Merlin still ducking/interruption-triggering on its own speaker playback after the first self-speech suppression implementation.

The current suspicion is:

```text
The self-speech gate is too energy-only and too permissive for borderline echo.
```

Especially this behavior:

```text
Uncertain for 2 frames -> Allow
```

is likely wrong during assistant playback, because Merlin's own speaker echo is also sustained.

This task has two goals:

1. Add **real file-based diagnostics** so we can inspect why the gate allows/suppresses.
2. Make the self-speech gate stricter during assistant playback, especially for ducking and normal capture.

---

## Current symptom

Merlin still appears to duck or interruption-trigger on its own speaker playback even after increasing:

```json
"EchoLeakageMultiplier": 0.45
```

The config is likely loading correctly because `appsettings.Development.json` does not override `BargeIn:SelfSpeechSuppression`, so the value from `appsettings.json` should apply.

The problem is likely not simply that the multiplier is too low. The gate's borderline/uncertain behavior may be letting sustained self-echo through.

---

## Current implementation summary

Merlin currently appears to have this path:

```text
AssistantSpeechPlaybackService
↓
pushes PCM playback reference into PlaybackReferenceTap

PlaybackReferenceTap
↓
tracks IsPlaybackActive
tracks CurrentPlaybackEnergy / RecentPlaybackEnergy

BargeInCoordinator
↓
receives mic frame
AEC processes mic + playback reference
VAD runs on echo-reduced frame
SelfSpeechSuppressionGate decides Allow / Suppress / Uncertain
if Allow, ducking or capture can proceed
```

The current gate estimates expected echo as:

```text
estimatedEcho = max(playbackEnergy * EchoLeakageMultiplier, VadEnergyThreshold)
```

Then suppresses only if:

```text
micEnergy <= estimatedEcho + EchoMargin
```

It allows strong user speech if:

```text
micEnergy >= max(
  estimatedEcho * UserSpeechRatio,
  estimatedEcho + UserSpeechMargin
)
```

Borderline frames become `Uncertain`, but after:

```text
RequireSustainedUserSpeechFrames = 2
```

the gate allows them.

---

## Why the current tuning may not help

Example with current values:

```text
playbackEnergy = 0.20
EchoLeakageMultiplier = 0.45
estimatedEcho = 0.09
EchoMargin = 0.02
suppress if micEnergy <= 0.11
```

If Merlin's speaker echo at the mic is around:

```text
0.12 or 0.13
```

then it is not suppressed.

It becomes `Uncertain`.

After only two uncertain frames, the gate allows it:

```text
Borderline speech sustained across multiple frames.
```

That is probably wrong for speaker echo because self-echo is naturally sustained.

The current gate assumes:

```text
sustained borderline audio = probably user
```

But with speakers, this can be true instead:

```text
sustained borderline audio = probably Merlin leaking into mic
```

---

## Main hypothesis

The current `RequireSustainedUserSpeechFrames = 2` is too permissive and may actively work against the goal.

For assistant playback:

```text
Uncertain sustained frames should not automatically become Allow for ducking/capture.
```

At minimum, the policy should split behavior by input reason:

```text
live_ducking:
  uncertain should suppress

normal_capture:
  uncertain should suppress

fast_hard_stop_candidate:
  uncertain may be probed carefully, but only under stricter rules
```

---

## Important diagnostic gap

`SelfSpeechSuppressionOptions.LogDecisions` exists, but the current code reportedly logs only at `Debug`.

Current logging is usually `Information`, so these values are invisible in normal logs:

```text
MicEnergy
PlaybackEnergy
EstimatedEcho
Decision
Reason
PlaybackAge
VadConfidence
InputReason
UserSpeechScore
```

That makes tuning blind.

This task must add **file-based diagnostics** so the user can send the log file for external review.

---

## Required file-based diagnostic output

Add a dedicated diagnostics log file for self-speech gate decisions.

Suggested path:

```text
Merlin.Backend/Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl
```

or:

```text
logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl
```

Choose a path that fits the repo/runtime conventions.

The log must be easy for the user to find and upload/paste.

JSON Lines format is preferred:

```json
{"timestampUtc":"2026-06-18T10:51:12.123Z","inputReason":"live_ducking","decision":"SuppressAsSelfEcho","reason":"Mic energy within estimated echo margin","micEnergy":0.103,"playbackEnergy":0.22,"estimatedEcho":0.165,"userSpeechScore":0.0,"playbackAgeMs":1234,"vadSaysSpeech":true}
```

Each decision should be one line.

---

## Required diagnostic fields

Each logged gate decision should include at least:

```text
timestampUtc
inputReason
decision
reason
micEnergy
playbackEnergy
estimatedEchoEnergy
echoLeakageMultiplier
echoMargin
userSpeechRatio
userSpeechMargin
userSpeechScore
playbackAgeMs
assistantPlaybackActive
vadSaysSpeech
vadConfidence if available
aecVerified if available
sustainedUncertainFrames
requiredSustainedUserSpeechFrames
configPolicyMode
correlationId if available
```

Add these if available:

```text
recentPlaybackEnergy
currentPlaybackEnergy
micEnergyBeforeAec
micEnergyAfterAec
frameDurationMs
sampleRate
captureState
duckingState
wouldStartDucking
wouldStartCapture
wouldStartFastHardStop
```

Do not block the task if some are unavailable. Log what exists and set missing values to null.

---

## Diagnostic configuration

Add or extend config:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "LogDecisions": true,
    "DiagnosticsFileEnabled": true,
    "DiagnosticsFilePath": "Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl",
    "DiagnosticsMinLevel": "Information",
    "DiagnosticsSampleEveryNFrames": 1,
    "DiagnosticsIncludeSuppressed": true,
    "DiagnosticsIncludeAllowed": true,
    "DiagnosticsIncludeUncertain": true
  }
}
```

Defaults can be conservative, but for local debugging it should be easy to turn on.

If logging every frame is too noisy, add sampling. But while diagnosing this bug, every decision is useful.

---

## Diagnostics safety/privacy

Do not log raw audio.

Do not log full STT transcripts in this diagnostics file unless explicitly needed.

The file should contain numeric gate decisions and short reasons only.

If a correlation id is available, logging it is useful.

---

# Part 1 - Implement real file diagnostics

## Goal

When `LogDecisions` and/or `DiagnosticsFileEnabled` is true, write self-speech gate decisions to a file the user can share.

---

## Suggested service

Add a small diagnostics writer if no existing one fits:

```text
Merlin.Backend/Services/BargeIn/SelfSpeechGateDiagnosticsWriter.cs
Merlin.Backend/Services/BargeIn/Interfaces/ISelfSpeechGateDiagnosticsWriter.cs
```

or put the interface in existing `BargeInInterfaces.cs`.

Suggested interface:

```csharp
public interface ISelfSpeechGateDiagnosticsWriter
{
    Task WriteAsync(
        SelfSpeechGateDiagnosticEntry entry,
        CancellationToken cancellationToken = default);
}
```

Suggested model:

```csharp
public sealed record SelfSpeechGateDiagnosticEntry(
    DateTimeOffset TimestampUtc,
    string InputReason,
    string Decision,
    string Reason,
    double MicEnergy,
    double PlaybackEnergy,
    double EstimatedEchoEnergy,
    double UserSpeechScore,
    double EchoLeakageMultiplier,
    double EchoMargin,
    double UserSpeechRatio,
    double UserSpeechMargin,
    long? PlaybackAgeMs,
    bool AssistantPlaybackActive,
    bool VadSaysSpeech,
    double? VadConfidence,
    bool? AecVerified,
    int SustainedUncertainFrames,
    int RequiredSustainedUserSpeechFrames,
    string PolicyMode,
    string? CorrelationId);
```

Use JSON serialization.

Make the writer robust:

```text
create directory if missing
append one JSON object per line
do not crash barge-in if logging fails
avoid blocking audio thread heavily
```

If async file writes are risky in the hot audio path, buffer entries through a channel/queue and write in a background task. If not, keep first pass simple but safe.

---

## Required tests for diagnostics

Add tests:

```text
Diagnostics_writer_creates_directory_and_file
Diagnostics_writer_appends_jsonl_entries
Gate_writes_suppress_decision_when_file_logging_enabled
Gate_writes_allow_decision_when_file_logging_enabled
Gate_does_not_throw_when_diagnostics_file_path_invalid
Diagnostics_respects_enabled_false
Diagnostics_entry_contains_energy_threshold_decision_reason
```

Use a temp directory in tests.

---

# Part 2 - Make uncertain policy stricter during playback

## Goal

Stop treating sustained borderline self-echo as user speech for ducking and normal capture.

---

## Current bad behavior

Current behavior likely:

```text
borderline frame -> Uncertain
uncertain count reaches RequireSustainedUserSpeechFrames
gate returns Allow
```

This may work in quiet/headphone cases, but with speakers:

```text
assistant echo is sustained
```

So this path causes self-ducking/self-capture.

---

## New policy by input reason

Add or use an input reason enum/string.

Suggested values:

```text
live_ducking
normal_capture
fast_hard_stop_candidate
unknown
```

### live_ducking

When assistant playback is active:

```text
Suppress uncertain.
Only allow if clearly above echo threshold.
```

Do not allow sustained uncertain frames to trigger ducking while playback is active.

### normal_capture

When assistant playback is active:

```text
Suppress uncertain.
Only allow if clearly above echo threshold.
```

Do not send uncertain self-echo to STT/classification.

### fast_hard_stop_candidate

This path can be slightly more permissive because "stop" must remain usable.

But do not let echo alone start hard stop.

Suggested policy:

```text
Allow uncertain only if:
  - sustained for more frames than ducking/capture, for example 4-6 frames
  - mic energy exceeds estimated echo by a minimum hard-stop margin
  - optionally VAD confidence is high
```

Even then, it should only run a short hard-stop probe. It must not hard-cancel without STT/classifier text matching a hard-stop phrase.

---

## Suggested config

Add or extend:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "PolicyMode": "StrictDuringPlayback",
    "AllowSustainedUncertainForDucking": false,
    "AllowSustainedUncertainForCapture": false,
    "AllowSustainedUncertainForFastHardStop": true,
    "FastHardStopUncertainFrames": 6,
    "FastHardStopUncertainExtraMargin": 0.04
  }
}
```

If config naming differs, adapt.

---

## Required tests

Add tests:

```text
Uncertain_self_echo_does_not_allow_live_ducking_during_playback
Uncertain_self_echo_does_not_allow_normal_capture_during_playback
Sustained_uncertain_frames_do_not_allow_ducking_during_playback
Sustained_uncertain_frames_do_not_allow_capture_during_playback
Clearly_above_echo_allows_ducking_during_playback
Clearly_above_echo_allows_capture_during_playback
Fast_hard_stop_can_probe_under_stricter_uncertain_policy
Fast_hard_stop_does_not_probe_on_plain_self_echo
No_playback_behavior_remains_unchanged
```

---

# Part 3 - Add adaptive echo ratio calibration

## Goal

Replace or supplement a fixed `EchoLeakageMultiplier` with a learned room/speaker leakage estimate.

A fixed multiplier is fragile because real echo depends on:

```text
speaker volume
speaker position
microphone position
room reflections
output device
user distance from mic
ducking volume
```

---

## Basic idea

During assistant playback, when no user interruption is accepted, observe:

```text
observedEchoRatio = micEnergy / playbackEnergy
```

Track a rolling percentile, such as p90 or p95, to estimate likely speaker leakage.

Then use:

```text
learnedEcho = playbackEnergy * learnedEchoLeakageRatio
estimatedEcho = max(fixedEchoEstimate, learnedEcho, VadEnergyThreshold)
```

This adapts to the user's actual room/device setup.

---

## Suggested service

Add if needed:

```text
Merlin.Backend/Services/BargeIn/EchoLeakageCalibrator.cs
Merlin.Backend/Services/BargeIn/Interfaces/IEchoLeakageCalibrator.cs
```

Suggested interface:

```csharp
public interface IEchoLeakageCalibrator
{
    void ObserveSuppressedEcho(double micEnergy, double playbackEnergy, DateTimeOffset timestamp);

    double? CurrentEchoLeakageRatio { get; }

    double EstimateEcho(double playbackEnergy, double fallbackEchoEstimate);
}
```

Keep a bounded window:

```text
last 5-30 seconds of observations
or last 200-1000 frames
```

Use a robust statistic:

```text
p90 / p95 / max capped ratio
```

Cap it to avoid runaway:

```text
MinLearnedEchoLeakageRatio
MaxLearnedEchoLeakageRatio
```

Suggested config:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "AdaptiveEchoCalibrationEnabled": true,
    "AdaptiveWindowSeconds": 20,
    "AdaptivePercentile": 0.95,
    "MinLearnedEchoLeakageRatio": 0.15,
    "MaxLearnedEchoLeakageRatio": 1.25,
    "AdaptiveEchoSafetyMargin": 0.02
  }
}
```

---

## When to observe

Only update the calibrator when the gate is reasonably confident the frame is self-echo.

Good observations:

```text
decision = SuppressAsSelfEcho
assistant playback active
playbackEnergy > small minimum
micEnergy > 0
```

Do not observe when:

```text
decision = Allow
likely user speech
no playback active
playbackEnergy too low
```

Avoid learning the user's voice as echo.

---

## Required tests

Add tests:

```text
Calibrator_records_suppressed_echo_ratios
Calibrator_ignores_allowed_user_speech
Calibrator_uses_percentile_or_robust_estimate
Calibrator_caps_learned_ratio
Estimated_echo_uses_learned_ratio_when_available
Estimated_echo_falls_back_to_fixed_multiplier_when_no_data
Adaptive_ratio_reduces_borderline_self_echo_allows
```

---

# Part 4 - Consider playback/mic correlation as next or optional phase

## Goal

Energy alone cannot reliably distinguish:

```text
Merlin's voice from speaker
```

from:

```text
human voice near mic
```

A stronger method is correlation:

```text
compare mic frame against recent playback reference frames
try short delays, e.g. 0-250ms
calculate normalized cross-correlation
```

If mic strongly correlates with playback reference:

```text
suppress as self-echo
```

If mic energy is high but correlation is low:

```text
allow as likely user speech
```

---

## Optional implementation

This may be too much for this pass. Diagnostics + stricter uncertainty + adaptive echo ratio should come first.

If implemented, add:

```text
PlaybackMicCorrelationDetector
```

Inputs:

```text
recent playback reference PCM
current mic frame
sample rate
delay search window
```

Output:

```text
maxCorrelation
bestDelayMs
decision hint
```

Suggested config:

```json
"BargeIn": {
  "SelfSpeechSuppression": {
    "CorrelationDetectionEnabled": false,
    "CorrelationMinConfidence": 0.70,
    "CorrelationMaxDelayMs": 250
  }
}
```

Keep disabled by default until tested.

---

# Part 5 - Short-term tuning values

After logging exists and strict uncertainty policy is implemented, test these starting values:

```json
"SelfSpeechSuppression": {
  "EchoLeakageMultiplier": 0.75,
  "EchoMargin": 0.04,
  "UserSpeechRatio": 2.2,
  "UserSpeechMargin": 0.08,
  "RequireSustainedUserSpeechFrames": 6,
  "LogDecisions": true,
  "DiagnosticsFileEnabled": true
}
```

But do not rely only on tuning.

The real fix is:

```text
diagnostics
stricter uncertain policy
adaptive echo estimate
optional correlation
```

---

# Part 6 - Manual debugging workflow

After implementation, run Merlin with speakers and diagnostics enabled.

Ask Merlin a long spoken question.

Do not speak.

Expected:

```text
diagnostics file shows many SuppressAsSelfEcho decisions
no live ducking
no capture
no STT/classification
```

Then speak over Merlin:

```text
stop
please stop
I mean family car
```

Expected:

```text
diagnostics file shows Allow decisions with reason clearly above echo threshold
ducking/capture/hard-stop/correction works
```

Then send/upload the diagnostics file for review.

The file to share should be:

```text
Merlin.Backend/Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl
```

or the configured path.

---

# Acceptance criteria

This task is complete when:

- [ ] `LogDecisions` or equivalent produces useful diagnostics at a file path the user can share.
- [ ] Diagnostics include mic energy, playback energy, estimated echo, decision, reason, and input reason.
- [ ] Diagnostics do not require debug-level console logging to be visible.
- [ ] Sustained uncertain frames no longer allow live ducking during playback.
- [ ] Sustained uncertain frames no longer allow normal capture during playback.
- [ ] Fast hard-stop remains usable under stricter rules.
- [ ] Adaptive echo calibration is implemented or clearly left as next phase.
- [ ] Self-echo no longer causes repeated duck/restore loop.
- [ ] User speech over speakers can still interrupt.
- [ ] Tests cover diagnostic file writing.
- [ ] Tests cover stricter uncertain policy.
- [ ] Tests cover no-playback behavior unchanged.
- [ ] Full backend tests pass.

---

# Required tests summary

Add/update tests for:

```text
Diagnostics_file_is_written_when_enabled
Diagnostics_file_contains_required_fields
Diagnostics_logging_failure_does_not_crash_gate
Uncertain_does_not_allow_ducking_during_playback
Uncertain_does_not_allow_capture_during_playback
Sustained_uncertain_self_echo_does_not_become_allow_for_ducking
Sustained_uncertain_self_echo_does_not_become_allow_for_capture
Clearly_above_echo_allows_user_speech
Fast_hard_stop_policy_remains_available
Adaptive_calibrator_learns_from_suppressed_echo
Adaptive_calibrator_does_not_learn_from_allowed_user_speech
No_playback_behavior_unchanged
```

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. How diagnostics are enabled.
4. Exact diagnostics file path.
5. Example diagnostic JSON line.
6. Which fields are logged.
7. What the pre-fix allow/suppress behavior was.
8. How uncertain behavior changed.
9. How policy differs for live ducking, normal capture, and fast hard stop.
10. Whether adaptive echo calibration was implemented.
11. Whether correlation detection was implemented or left for later.
12. Recommended config values to test.
13. Tests added.
14. Tests run and results.
15. Known limitations.
16. What file the user should send back for analysis.

Do not simply say "implemented." Explain the lifecycle with actual classes and methods changed.

---

# Recommended first implementation cut

If the full task is too large, implement this first:

```text
1. Add file diagnostics for every self-speech gate decision when enabled.
2. Change uncertain policy so sustained uncertain does NOT allow ducking/capture during playback.
3. Keep fast hard-stop path possible but stricter.
4. Add tests.
5. Run full backend tests.
```

Then add adaptive echo calibration as the next cut.
