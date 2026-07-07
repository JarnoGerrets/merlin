---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/DONE merlin-voice-naturalness-roadmap/phase-02-conversational-playback-control.md
classification: implementation-plan
related_features:
  - Voice Interruption System
status: implemented
imported_to_vault: true
---

# Phase 02 — Conversational Playback Control And Floor Handoff

## Purpose

This phase makes Merlin behave naturally when the user starts talking while Merlin is speaking.

The goal is not yet full semantic answer steering. The goal is the mechanical and conversational foundation:

```text
Merlin speaks.
User starts speaking.
Merlin fades/ducks only briefly while uncertain.
If user speech is confirmed, Merlin yields the floor.
Merlin pauses or stops main playback.
Merlin captures the user's full phrase.
Merlin may play a tiny acknowledgement after the user stops.
Merlin can continue, stay paused, or stop based on the route.
```

The core principle:

```text
Ducking is allowed during uncertainty.
Confirmed user speech gets the floor.
```

## Why this matters

Ducking was useful as a diagnostic and transition tool. It helped prove that Merlin could detect user speech while its own audio was playing and could distinguish user speech from self-output.

But ducking should not be the long-term conversational behavior. In natural conversation, people do not keep talking softer while the other person speaks. They yield.

Target behavior:

```text
Possible user speech → brief fade/duck/pre-yield
Confirmed user speech → pause main playback and listen
Explicit "stop" → stop immediately and silently
```

## Explicit non-goals

Do not implement Live Answer Steering in this phase.

Do not regenerate DeepInfra continuations based on spoken/unspoken text.

Do not implement browser rollback.

Do not build a complex task queue.

Do not physically play two voices at the same time.

The “second playback channel” is a logical channel for micro-responses, not overlapping audio.

## Existing repo areas to inspect first

```text
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/SpeakerDuckingService.cs
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/BargeIn/BargeInModels.cs
Merlin.Backend/Services/Acknowledgement/
Merlin.Backend/Services/ChatterboxTtsProvider.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend/Models/LiveAssistantTurn.cs
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend.Tests/
```

Important current finding:

`AssistantSpeechPlaybackService.PauseCurrentSpeechAsync` currently appears to be more of a ducking/fallback behavior than true pause/resume. This phase should determine whether true pause/resume can be implemented safely in the current audio pipeline. If not, implement the closest safe behavior and document the limitation.

## Desired user-facing behavior

### Explicit stop during long answer

```text
Merlin: long explanation...
User: stop
Merlin: [stops immediately]
```

No spoken acknowledgement. No confirmation. No resume.

### Wait / hold on / no no

```text
Merlin: long explanation...
User: wait, that's not what I meant
Merlin: [fades/pauses]
Merlin: [keeps listening]
User: I mean the rollback system, not the interruption system
Merlin: [phase 2 can acknowledge and route; later phase will steer answer]
```

Merlin should not resume just because the user pauses for one second after saying `wait`.

### Continue

```text
Merlin: [paused]
User: continue
Merlin: [resumes original answer if still available]
```

If exact resume is not technically possible yet, Merlin should safely restart from the next chunk or say a very short explanation.

### Micro acknowledgement

After the user finishes, Merlin may say something short:

```text
Got it.
Paused.
Go ahead.
Okay.
```

But Merlin must not speak while the user is still speaking.

## Playback states

Add or formalize states similar to:

```text
Speaking
SoftDuckUncertain
PausedForUserSpeech
PausedForUserClarification
StoppedByUser
ResumePending
```

Do not overfit naming; use existing style.

Distinguish:

```text
StopPlaybackOnly
PausePlaybackForUserSpeech
PauseAndClarify
ResumePlayback
CancelActiveTurn
```

## Ducking policy

Ducking should be demoted from long-running conversation behavior to a short transition/uncertainty state.

Suggested policy:

```text
0–150 ms possible noise/echo: ignore or no visible change
150–300 ms likely user speech: fast fade/duck/pre-yield
confirmed speech or known phrase: pause main playback fully
explicit stop: immediate hard stop
```

Do not keep Merlin talking softly over confirmed user speech.

## Floor yield behavior

When Merlin yields the floor:

```text
- Pause or stop main speech output.
- Prevent the next TTS chunk from starting.
- Keep capture open until user finishes.
- Do not micro-acknowledge while user is still talking.
- After final transcript, route via Live Utterance Gate / existing router.
```

## Paused clarification behavior

If the user said an intentional floor-taking phrase:

```text
wait
hold on
no no
that's not what I meant
actually
I mean
sorry I meant
```

Merlin should enter a longer pause/listening mode.

Suggested behavior:

```text
state = PausedForUserClarification
listen for useful follow-up
if useful follow-up arrives → route it
if user says continue → resume
if user says cancel/never mind → cancel/stop
if long silence, e.g. 5–10 seconds → ask "What should I change?"
```

Do not auto-resume quickly after `wait`.

## Micro-response channel

Implement a logical micro-response lane, likely reusing the existing acknowledgement/progress speech services.

Rules:

```text
- It must not overlap with main speech.
- It must only play after the user has stopped speaking.
- It should be short, preferably one sentence or less.
- It should not restart a speech loop after explicit stop.
- It should not speak when silence/visual feedback is better.
```

Examples:

```text
Paused.
Go ahead.
Got it.
Okay.
```

Avoid long responses such as:

```text
I'm sorry sir, I am now pausing the active turn while waiting for your clarification.
```

## Integration with Live Utterance Gate

This phase should use Phase 01 if implemented.

Expected mapping:

```text
AcceptPlaybackControl + stop + Speaking → hard stop, no acknowledgement
AcceptPlaybackControl + wait/hold/no-no + Speaking → pause and listen
HoldForMoreSpeech + Speaking → stay yielded/paused
AskClarification + paused state → short clarification prompt
AcceptContinuation + paused state → resume
AcceptCancellation → cancel/stop
```

If Phase 01 is not present yet, implement minimal compatible logic but keep boundaries clean so it can later call the gate.

## Implementation options

### True pause/resume

If the playback system can pause the current stream and resume from the same buffer position, implement it.

### Chunk-level pause/resume

If true sample-level pause is too risky, pause after current buffer/chunk and prevent future chunks. Store the unplayed queued chunks and resume them on `continue`.

### Stop-and-regenerate fallback

If neither is safe, implement stop and document that resume will need Active Answer Playback Context later. Do not pretend true resume exists if it does not.

## Logging requirements

Add logs:

```text
PlaybackPossibleUserSpeechDetected
PlaybackDuckingForUncertainty
PlaybackYieldedToUser
PlaybackPausedForUserSpeech
PlaybackPausedForClarification
PlaybackStoppedByUser
PlaybackResumeRequested
PlaybackResumed
PlaybackResumeUnavailable
MicroResponseQueued
MicroResponsePlayed
MicroResponseSuppressedUserStillSpeaking
```

Logs should include:

```text
turn id
answer/playback id if available
speech type
route decision
reason
current playback state
```

## Tests to add

Unit/integration tests around playback service and coordinator behavior:

1. `stop` during speaking stops playback immediately and does not ask confirmation.
2. `wait` during speaking pauses/yields and does not continue talking underneath.
3. `no no no` during speaking pauses/yields and keeps listening.
4. Micro-response does not play until after user speech ends.
5. `continue` during paused playback resumes if resume is supported.
6. If resume is unsupported, behavior is logged and safe.
7. Unknown short noise does not permanently stop playback.
8. Confirmed user speech does not cause long-running ducked playback.
9. Explicit cancel cancels active turn, not just playback.
10. Paused clarification stays paused through short silence and asks only after long timeout.

## Acceptance criteria

This phase is done when:

```text
- Ducking is no longer the long-running behavior for confirmed user speech.
- Merlin yields the floor on confirmed user speech while speaking.
- "stop" during speech stops silently with no confirmation.
- "wait", "hold on", "no no" pause and keep listening.
- Micro acknowledgements can play after the user finishes, without overlap.
- "continue" can resume where technically supported or fails gracefully.
- Logs clearly show pause/yield/resume/stop decisions.
- Tests cover the main conversational playback states.
```


