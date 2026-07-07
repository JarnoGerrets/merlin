---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# AssistantSpeechPlaybackService

## File

`Merlin.Backend/Services/AssistantSpeechPlaybackService.cs`

Verified present in current repo.

## Purpose

Owns assistant speech playback, streaming final-answer playback, provisional audio holds for interruption handling, playback snapshots, UI state emission, reference tap audio, ducking volume, and queue generation cancellation.

## Related Features

- [[Streaming Responses and TTS]]
- [[Voice Interruption System]]
- [[Responsive Feedback]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `AssistantSpeechPlaybackService` implements `IAssistantSpeechPlaybackService`.
- Nested `StreamingFinalAnswerPlaybackSession`, `ActivePlaybackControlState`, and audio segment records manage streaming playback.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `EnqueueAsync` | public | Sanitizes text, increments queue/generation when required, and starts queued speech playback. | `SpeakQueuedAsync`; TTS sanitizer | WebSocketHandler/feedback services | Used for final answers and confirmations. |
| `StopCurrentAsync` | public | Cancels active playback and invalidates generation. | playback lock; generation helpers | barge-in/stop commands | Stops audible speech. |
| `PauseCurrentSpeechAsync` / `ResumeCurrentSpeechAsync` | public | Toggles active playback hold flags for assistant playback controls. | playback lock; UI state emit | LiveUtteranceGate routes | User-facing pause/resume. |
| `BeginProvisionalAudioHoldAsync` | public | Holds final-answer audio while interruption STT decides whether to route. | hold state; timeout refresh | BargeInCoordinator/WebSocketHandler | Prevents talking over possible user command. |
| `ResumeProvisionalAudioHoldAsync` / `FlushProvisionalAudioHoldAsync` | public | Resume held audio or flush buffered audio for accepted interruption. | `ResumeHeldPlaybackLocked`; clear hold | barge-in/live interruption flow | Critical for conversational interruption. |
| `BeginStreamingFinalAnswerAsync` | public | Creates streaming session with text input channel, TTS producer, playback consumer, and active playback snapshot. | nested session | streaming response path | Streams chunks as they arrive. |
| `SpeakQueuedAsync` / `SpeakAsync` | private | Serializes playback through `_speechGate`, streams/synthesizes audio, writes to NAudio wave output, emits energy/UI events, tracks spoken answer checkpoints. | TTS service; wave player; reference tap; UI broadcaster | `EnqueueAsync` | Core audio path. |
| `WaitWhilePlaybackHeldAsync` | private | Pauses audio writes while provisional hold/pause is active. | playback lock/hold state | playback loops | Prevents underruns where possible. |
| generation helpers | private | Detect obsolete final-answer playback by turn id/generation. | `_finalAnswerGenerations` | stop/new final answer/playback loops | Stops stale speech. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_speechGate` | `SemaphoreSlim` | Serializes queued speech playback. | enqueue/playback | constructor | service lifetime |
| `_playbackControlLock` | `object` | Protects active playback/hold state. | playback/control methods | constructor | service lifetime |
| `_activePlaybackSnapshot` | `ActiveSpeechPlaybackSnapshot?` | Public snapshot for barge-in/live logic. | `GetActivePlaybackSnapshot` | playback/control methods | cleared after completion |
| `_finalAnswerGenerations` | concurrent dictionary | Turn id to generation for stale final-answer cancellation. | generation helpers | enqueue/stop | per turn |
| `_currentStreamingSession` | nested session | Active streaming final-answer session. | streaming methods | begin/clear/cancel | one at a time |
| volume setter fields | delegate/lock | Active wave output volume control for ducking. | ducking callback | playback start/clear | active playback only |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IVoiceSynthesisService` | TTS audio generation. |
| `IPlaybackReferenceTap` | AEC/self-speech correlation reference audio. |
| `ISpeakerDuckingService` | Applies volume multiplier during user speech. |
| `ILiveSpokenAnswerTrackingService` | Spoken-answer checkpoints for interruption correction. |
| `AssistantUiStateBroadcaster` | Emits canonical UI speaking/idle state. |
| `IGpuWorkScheduler` | Coordinates GPU-heavy TTS work. |
| `IWavePlayer` factory | Audio output. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `AssistantVisualEvent` speech events | frontend WebSocket | speech energy, playback started/completed, stop confirmations. |
| assistant UI state | AssistantUiStateBroadcaster/frontend | speaking/idle/immediate/terminal state. |
| playback reference audio | reference tap | PCM bytes for self-speech suppression. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| enqueue/streaming requests | WebSocketHandler/feedback/router | public methods |
| barge-in hold/resume/flush requests | interruption services | hold methods |
| speaker ducking changes | ISpeakerDuckingService event | `OnDuckingChanged` |

## External Side Effects

Generates TTS, writes audio to output device, sends PCM to reference tap, emits frontend visual/UI events, and coordinates GPU work.

## Safety / Guardrails

Never block while holding `_playbackControlLock` on operations that call external services. Always clear/complete playback snapshots and held states; stale snapshots confuse barge-in and live gate behavior.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `AssistantSpeechPlaybackServiceTests.cs` | Queue generation, stop/pause/resume, provisional hold, stale final answer skip, streaming session behavior, UI state. | Uses fake TTS/wave output. |
| barge-in/live interruption tests | Interaction with playback snapshot/hold. | Timing sensitive. |

## Known Risks / Fragility

This is timing-sensitive. Small changes to hold timeouts, generation checks, or UI-state completion can make Merlin talk over the user or get stuck not listening.

## Change Notes for Agents

This is a central live path. Read the source plus linked flow/state notes before changing behavior, then run targeted and full backend tests.
