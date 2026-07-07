---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/Merlin_Instant_Ducking_And_Natural_Hard_Stop_Implementation.md
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
Original source: `Merlin.ToDo/Merlin_Instant_Ducking_And_Natural_Hard_Stop_Implementation.md`

# Instant Ducking And Natural Hard Stop Plan

## Intended repo location

Place this file in the Merlin repository at:

```text
Merlin.ToDo/Merlin_Instant_Ducking_And_Natural_Hard_Stop_Implementation.md
```

This document is an implementation guide for an agent working inside the Merlin repo.

---

## Mission

Fix two core interruption UX problems in Merlin:

1. Speaker ducking is too late or barely audible.
2. Hard-stop commands are too strict and only work reliably as "Merlin, stop".

This task is about making Merlin feel controllable while it is speaking.

The user experience we want:

```text
Merlin is speaking
↓
User starts talking
↓
Merlin's playback volume ducks immediately
↓
User can hear themselves clearly while speaking
↓
User stops talking
↓
Merlin playback volume restores quickly
```

And:

```text
Merlin is speaking
↓
User says "stop", "please stop", "abort", "cancel that", "wait", etc.
↓
Merlin stops immediately
↓
No wake word required while Merlin is already speaking
↓
The active live turn is cancelled
↓
Old late responses are suppressed
```

This task should be implemented as a focused fix to the barge-in/ducking/hard-stop path.

Do not redesign the whole voice system.

---

## Current reported behavior

The user reports:

```text
Ducking takes way too long.
I cannot hear myself talk and I lose track of my sentence.
Ducking barely happens, or when it does, it happens seconds after I started speaking.
Only "Merlin, stop!" seems to work.
"stop", "abort", "please stop", and similar phrases do not work.
```

A previous agent inspected the code and suspected:

```text
VAD detects possible speech
↓
SpeakerDuckingService marks itself as ducked
↓
but actual WaveOut/output volume is only updated when playback code later touches waveOut.Volume
↓
audible ducking is delayed or barely applied
```

Also suspected:

```text
RequireWakeWordForFirstVersion = true
WakeWords = ["merlin"]
HardStopPhrases = ["stop", "cancel", "shut up", "quiet", "enough", "never mind"]

"Merlin, stop" -> wake word stripped -> "stop" -> works
"stop" -> rejected because no wake word
"Merlin, please stop" -> wake word stripped -> "please stop" -> exact match fails
"Merlin, abort" -> wake word stripped -> "abort" -> phrase missing
```

Verify these suspicions in code before implementing.

---

## Important distinction

There are two separate systems that must not be confused.

### Live ducking

Ducking should track whether the user is speaking right now.

It should be driven by VAD / live mic energy, not by full STT/classification completion.

Target behavior:

```text
speech active -> duck now
speech inactive for short hangover -> restore now
```

### Interruption classification

Classification decides whether the utterance means:

```text
hard stop
correction
backchannel
clarification
ignore/noise
```

This can take longer because it may require capture, STT, and classification.

Ducking must not wait for classification.

---

## Non-goals

Do not implement these in this task:

- semantic correction rewriting
- web search
- web research
- capability routing changes
- Codex integration
- memory redesign
- new TTS provider
- new STT provider
- full acoustic echo cancellation rewrite
- frontend visual redesign
- destructive tool support

This task is only about:

```text
instant audible ducking
natural hard-stop recognition
tests/diagnostics around both
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
Merlin.Backend/Services/SpeechPolicyService.cs
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/Interfaces/IAssistantSpeechPlaybackService.cs
Merlin.Backend/Services/Interfaces/ILiveAssistantTurnService.cs
Merlin.Backend/Configuration/
Merlin.Backend/appsettings.json
Merlin.Backend.Tests/BargeInTests.cs
Merlin.Backend.Tests/CorrectionRegenerationTests.cs
Merlin.Backend.Tests/WebSocketHandlerTests.cs
```

Search for:

```text
Ducking
Duck
SpeakerDucking
CurrentVolumeMultiplier
DuckingFadeMs
DuckingVolumePercent
waveOut.Volume
Volume
VAD
VoiceActivity
PossibleSpeech
CapturingInterruption
HardStop
HardStopPhrases
WakeWords
RequireWakeWordForFirstVersion
AllowNaturalSoftBargeInWhenAecVerified
AllowNatural
BargeInAction.HardCancel
BargeInAction.Correction
ClearQueueAsync
CancelTurnAsync
```

Before editing, answer these questions for yourself:

1. When VAD detects speech, does the actual playback output volume change immediately?
2. Or does only a state value such as `CurrentVolumeMultiplier` change?
3. Is there an event/callback from `SpeakerDuckingService` to `AssistantSpeechPlaybackService`?
4. Is `DuckingFadeMs` actually implemented, or only stored/logged?
5. Can active playback volume be changed safely while audio is already playing?
6. Does hard-stop classification happen before or after wake-word enforcement?
7. Are hard-stop phrases exact matches only?
8. Are polite wrappers like "please stop" normalized?
9. Is "abort" included in hard-stop phrases?
10. Is natural hard-stop allowed while Merlin is speaking?

Mention the answers in the final report.

---

# Part 1 - Fix instant audible ducking

## Goal

When user speech is detected while Merlin is speaking, Merlin's active playback volume should audibly reduce immediately.

When user speech stops, volume should restore quickly.

Ducking should not wait for:

```text
end-of-utterance capture
gated STT
interruption classification
correction regeneration
hard cancel handling
```

---

## Current likely problem

The current system likely does something like:

```text
VAD possible speech detected
↓
SpeakerDuckingService.StartDucking()
↓
_isDucked = true
↓
CurrentVolumeMultiplier returns lower value
```

But active playback only applies this lower value when something later calls:

```csharp
waveOut.Volume = _speakerDuckingService.CurrentVolumeMultiplier;
```

That makes the ducking state correct but the audible output late.

The fix is to make ducking an active volume-control signal.

---

## Desired architecture

`SpeakerDuckingService` should not be only a passive state holder.

It should publish a volume-change notification whenever the target volume changes.

Example:

```text
SpeakerDuckingService
  - knows target multiplier
  - knows ducked/restored state
  - exposes event/callback/observable when multiplier changes

AssistantSpeechPlaybackService
  - subscribes to ducking volume changes
  - applies the new multiplier to active output immediately
```

Suggested interface addition:

```csharp
public interface ISpeakerDuckingService
{
    float CurrentVolumeMultiplier { get; }

    event EventHandler<SpeakerDuckingChangedEventArgs>? DuckingChanged;

    Task StartDuckingAsync(string reason, CancellationToken cancellationToken = default);
    Task StopDuckingAsync(string reason, CancellationToken cancellationToken = default);
}
```

Suggested event args:

```csharp
public sealed class SpeakerDuckingChangedEventArgs : EventArgs
{
    public float VolumeMultiplier { get; init; }
    public bool IsDucked { get; init; }
    public string Reason { get; init; } = "";
    public TimeSpan FadeDuration { get; init; }
}
```

If the repo already has a different pattern for events, use that.

---

## Immediate output volume application

`AssistantSpeechPlaybackService` or the actual output owner should update the active output device immediately when ducking changes.

Conceptually:

```csharp
private void OnDuckingChanged(object? sender, SpeakerDuckingChangedEventArgs e)
{
    ApplyOutputVolume(e.VolumeMultiplier);
}
```

The apply method must be thread-safe.

Potential concerns:

- playback may not currently be active
- output device may be null/disposed
- event may arrive from non-audio thread
- fade may require async/timer behavior
- multiple duck/restore calls may overlap

Handle these safely.

---

## Fade behavior

Current `DuckingFadeMs` may only be logged or configured. Verify.

Implement one of these options.

### Option A - Immediate volume jump

Fastest and simplest.

```text
speech detected -> set volume to ducked multiplier immediately
speech stopped -> set volume to normal immediately
```

This is acceptable if fade implementation is risky.

### Option B - Short fade

Preferred if easy.

```text
speech detected -> fade down over 30-80ms
speech stopped -> fade up over 120-250ms
```

For barge-in, fade down must be very fast. A slow fade defeats the purpose.

Do not use long fade durations for ducking start.

Suggested config:

```json
"BargeIn": {
  "Ducking": {
    "Enabled": true,
    "DuckingVolumePercent": 20,
    "DuckingFadeMs": 60,
    "RestoreFadeMs": 150,
    "SpeechHangoverMs": 200
  }
}
```

If the current config model has these values elsewhere, adapt to it.

---

## Speech-active hangover

Ducking should not rapidly flutter on/off for tiny pauses.

Add or use a short speech hangover:

```text
VAD says active -> duck
VAD says inactive -> wait 150-300ms
if still inactive -> restore
```

Suggested default:

```text
SpeechHangoverMs = 200
```

This is different from the full interruption capture timeout. Capture may wait longer, but ducking should restore quickly when the user stops speaking.

---

## Decouple ducking window from capture window

The capture window may last up to several seconds to avoid clipping the user's correction.

But ducking should only happen while the user is actively talking.

Do not do this:

```text
duck on possible speech
stay ducked until STT/classification is done
```

Do this:

```text
duck while mic indicates active speech
restore shortly after speech stops
capture/classification continues independently
```

---

## Manual tuning guidance

If user still cannot hear themselves, `DuckingVolumePercent = 20` may be too loud depending on headphones/speakers.

But do not only change the percentage. The main bug is likely late application.

After immediate volume control works, test with:

```text
DuckingVolumePercent = 20
DuckingVolumePercent = 10
DuckingVolumePercent = 5
```

Prefer making this config-controlled.

---

## Ducking logging requirements

Add timing logs so future diagnosis is easy.

At minimum log:

```text
VAD active speech detected
Ducking state requested
Ducking volume applied
Ducking restored requested
Ducking volume restored
```

Include timestamps or elapsed ms where possible.

The important metric:

```text
VAD speech detected -> actual output volume applied
```

This should be near-immediate, not seconds.

Example log:

```text
Speaker ducking requested. Reason=VADActive, Target=0.20
Speaker ducking applied to active output. Target=0.20, ElapsedMs=12
Speaker ducking restore requested. Reason=VADInactiveHangoverElapsed
Speaker ducking restored to active output. Target=1.00, ElapsedMs=8
```

---

## Ducking tests

Add or update tests for:

```text
DuckingChanged event fires immediately when ducking starts
DuckingChanged event fires when ducking stops
AssistantSpeechPlaybackService applies ducking change while playback is active
Ducking does not wait for STT/classification completion
Ducking restore happens after short hangover, not after full capture
Repeated StartDucking calls do not spam/apply duplicate state
StopDucking is idempotent
Ducking disabled means no volume-change event
Playback starting while already ducked uses current ducked multiplier
Playback after restore uses normal multiplier
```

If `AssistantSpeechPlaybackService` is hard to unit test because of audio device classes, add a small abstraction around output volume application and test that abstraction/fake.

---

# Part 2 - Fix natural hard-stop recognition

## Goal

When Merlin is currently speaking, hard-stop phrases should work without requiring the wake word.

Examples that should work while Merlin is speaking:

```text
stop
please stop
stop talking
abort
cancel
cancel that
shut up
quiet
enough
never mind
wait
hold on
pause
pause that
no stop
Merlin stop
Merlin please stop
```

Hard stop is an emergency brake. It should be forgiving.

---

## Important policy

While Merlin is actively speaking:

```text
natural hard-stop phrases should not require wake word
```

When Merlin is idle/listening for new commands:

```text
normal wake-word policy can still apply
```

This avoids accidental triggers when Merlin is idle while still making interruption usable.

---

## Hard-stop classification should be early

Hard-stop detection should happen as early as safely possible in the barge-in/interruption flow.

It should not require a full complex semantic classification if a clear stop phrase is present.

Suggested order while Merlin is speaking:

```text
1. Normalize transcript
2. Strip wake word if present
3. Strip polite wrappers/fillers
4. Check hard-stop phrase/intent
5. If hard stop -> HardCancel immediately
6. Else continue normal correction/backchannel/clarification classification
```

---

## Normalize hard-stop text

Add or improve a hard-stop normalizer.

Normalization should:

- lowercase
- trim punctuation
- collapse whitespace
- strip wake words at the beginning
- strip polite wrappers
- strip common fillers

Examples:

```text
"Merlin, stop!" -> "stop"
"please stop" -> "stop"
"can you please stop" -> "stop"
"Merlin please stop talking" -> "stop talking"
"abort!" -> "abort"
"cancel that" -> "cancel that"
"wait wait stop" -> maybe hard stop
```

Suggested polite/filler prefixes:

```text
please
can you
could you
would you
hey
uh
um
no
nope
merlin
```

Be careful: do not strip meaningful content from correction phrases too aggressively.

This normalizer should be used for hard-stop classification only, not necessarily for all routing.

---

## Hard-stop phrase list

Expand hard-stop phrases.

Suggested config/default phrases:

```text
stop
stop talking
please stop
cancel
cancel that
abort
abort that
shut up
quiet
be quiet
enough
that's enough
never mind
nevermind
wait
hold on
pause
pause that
no stop
```

Support both exact and safe contains-style matching.

Exact match examples:

```text
stop
abort
cancel
wait
hold on
```

Contains/phrase examples:

```text
please stop
stop talking
can you stop
cancel that
```

Avoid dangerous overmatching.

Do not classify these as hard stop:

```text
how do I stop a process in C#
what does abort mean
tell me about cancellation tokens
when should I use pause in audio
```

Context matters: permissive natural hard stop should primarily apply while Merlin is speaking / in barge-in mode.

---

## Wake-word behavior

Current settings may include:

```text
RequireWakeWordForFirstVersion = true
AllowNaturalSoftBargeInWhenAecVerified = false
WakeWords = ["merlin"]
```

Do not simply turn off wake-word requirement globally.

Instead implement:

```text
if assistant is speaking and transcript is hard-stop-like:
    allow without wake word
else:
    apply existing wake-word policy
```

This preserves safety while fixing interruption.

---

## "Abort" behavior

Add `abort` and `abort that` as hard-stop phrases.

The user explicitly expects "abort" to work.

---

## "Please stop" behavior

Make sure polite wrappers work.

These should all classify as hard stop while Merlin is speaking:

```text
please stop
can you stop
could you please stop
Merlin please stop
please stop talking
```

---

## Hard-stop tests

Add or update tests for:

```text
"Merlin, stop" -> hard cancel
"stop" while speaking -> hard cancel
"please stop" while speaking -> hard cancel
"Merlin please stop" -> hard cancel
"abort" while speaking -> hard cancel
"abort that" while speaking -> hard cancel
"cancel that" while speaking -> hard cancel
"wait" while speaking -> hard cancel or pause depending current desired policy
"hold on" while speaking -> hard cancel or pause depending current desired policy
"stop" while idle still respects wake-word policy if configured
"how do I stop a process in C#" while speaking does not hard cancel if routed as a correction/question transcript
```

If current architecture cannot distinguish speaking vs idle in that test, add that state as an explicit test input.

---

# Part 3 - Preserve live turn cancellation and correction regeneration

The repo now has live turn cancellation and correction regeneration.

Do not break them.

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
use new correlation id
```

Backchannel/clarification must still:

```text
not cancel unless classified as hard stop/correction
not dispatch correction regeneration
preserve current pause/resume behavior
```

Add regression tests if needed.

---

# Part 4 - Suggested implementation plan

## Phase 0 - Inspect and report actual current flow

Before code changes, inspect and note:

```text
where VAD triggers ducking
where SpeakerDuckingService stores state
where active playback volume is applied
whether DuckingFadeMs is implemented
where hard-stop phrases are checked
where wake-word requirement blocks phrases
where assistant speaking state is known
```

## Phase 1 - Make ducking service emit live changes

Add event/callback or equivalent volume-change notification.

Ensure it fires when:

```text
ducking starts
ducking stops
current multiplier changes
```

Keep idempotency.

## Phase 2 - Apply volume immediately to active playback

Subscribe from playback/output owner.

When ducking changes:

```text
apply to active output immediately
```

Make it thread-safe.

If necessary, add a tiny output-volume abstraction to make this testable.

## Phase 3 - Add speech-active hangover

Ducking should restore shortly after VAD active speech ends.

Do not wait for STT/classification.

## Phase 4 - Improve hard-stop phrase recognition

Add hard-stop normalizer and expanded phrase list.

Allow natural hard stops without wake word while assistant is speaking.

Keep stricter behavior while idle.

## Phase 5 - Tests

Add focused unit/integration tests.

Run full backend tests.

---

# Acceptance criteria

This task is complete when:

- [ ] VAD active speech causes an immediate audible ducking volume update during active playback.
- [ ] Ducking no longer waits for a new audio chunk or playback callback.
- [ ] Ducking restores quickly after user speech stops, using a short hangover.
- [ ] Ducking does not stay tied to full STT/classification lifecycle.
- [ ] `DuckingFadeMs` is either implemented or explicitly replaced with immediate application and documented.
- [ ] `stop` works while Merlin is speaking, even without wake word.
- [ ] `please stop` works while Merlin is speaking.
- [ ] `abort` works while Merlin is speaking.
- [ ] `cancel that`, `stop talking`, `hold on`, or chosen hard-stop variants work according to configured phrase list.
- [ ] Natural hard-stop without wake word does not become global idle-command behavior unless explicitly intended.
- [ ] Hard stop still cancels the active live turn.
- [ ] Hard stop still suppresses old late responses.
- [ ] Correction regeneration still works.
- [ ] Backchannel/clarification behavior is not broken.
- [ ] Tests cover ducking volume application and natural hard-stop phrases.
- [ ] Full backend test suite passes.

---

# Manual verification checklist

After implementation, manually test with Merlin running:

## Ducking

1. Start a long spoken Merlin response.
2. Begin talking over Merlin.
3. Confirm Merlin volume ducks immediately.
4. Stop talking.
5. Confirm Merlin volume restores quickly.
6. Repeat with short pauses and make sure volume does not flutter badly.
7. Check logs for VAD-to-volume-apply delay.

## Hard stops

While Merlin is speaking, test:

```text
stop
please stop
abort
cancel that
stop talking
Merlin stop
Merlin please stop
```

Expected:

```text
speech stops
active turn cancels
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
should respect current idle wake-word policy
should not accidentally trigger broad behavior unless explicitly configured
```

---

# Final agent report required

When finished, report:

1. Files changed.
2. Files added.
3. What the actual pre-fix ducking issue was.
4. How ducking now applies volume immediately.
5. Whether fade is implemented or immediate.
6. What hangover/restore behavior is used.
7. What the actual pre-fix hard-stop issue was.
8. How natural hard-stop without wake word is allowed while speaking.
9. Which hard-stop phrases are supported now.
10. How idle wake-word behavior is preserved.
11. How live turn cancellation/correction regeneration were preserved.
12. Tests added.
13. Tests run and results.
14. Known limitations.
15. Recommended tuning values for `DuckingVolumePercent`, fade, and hangover.

Do not simply say "implemented." Explain the actual lifecycle with the classes/methods changed.
