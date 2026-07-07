---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/DONE state_management/merlin_assistant_ui_state_contract.md
classification: implementation-plan
related_features:
  - Cross-cutting
status: implemented
imported_to_vault: true
---

# Merlin Assistant UI State Contract

## Status

Design draft.

## Implementation Status

### PR 1 - Backend Canonical Event Skeleton

Status: Implemented

Summary:
- Added backend `assistant_ui_state` WebSocket event.
- Event includes baseState, overlayState, reason, correlationId, turnId, speechItemType, audiblePlaybackActive, interruptionState, timestampUtc.
- Emitted canonical state transitions in parallel with existing visual/speech events.
- Added server-side `assistant_ui_state_emitted` diagnostics.
- Did not remove existing frontend visual events.
- Did not migrate frontend state rendering yet.

Tests:
- `AssistantSpeechPlaybackServiceTests.BeginProvisionalAudioHoldAsync_EmitsCanonicalListeningState`
- `AssistantSpeechPlaybackServiceTests.PlaybackEmitsCanonicalSpeakingOnlyWhenAudioStarts`
- `AssistantSpeechPlaybackServiceTests.FinalAnswerIntermediateChunkGapEmitsIdleTtsChunkGap`
- `AssistantSpeechPlaybackServiceTests.ProgressCompletionEmitsThinking`
- `AssistantSpeechPlaybackServiceTests.StopConfirmationEmitsCanonicalSpeakingThenIdle`
- `WebSocketHandlerTests.BuildResponseUiState_MapsConfirmationErrorAndNoneOverlays`

### PR 1.1 - Final Chunk / Response Completion Semantics

Status: Implemented

Summary:
- Distinguished final-answer inter-chunk idle from true final-answer completion.
- `reason=tts_chunk_gap` now means more final-answer audio is expected.
- `reason=final_answer_completed` now means the full final-answer speech item is complete.
- Preserved progress/acknowledgement completion mapping to thinking.
- Preserved stop-confirmation completion mapping to idle.
- Did not migrate frontend rendering yet.

Tests:
- `AssistantSpeechPlaybackServiceTests.FinalAnswerIntermediateChunkGapEmitsIdleTtsChunkGap`
- `AssistantSpeechPlaybackServiceTests.FinalAnswerFinalCompletionEmitsIdleFinalAnswerCompletedAndKeepsDistinctIdleReasons`
- `AssistantSpeechPlaybackServiceTests.ProgressCompletionEmitsThinking`
- `AssistantSpeechPlaybackServiceTests.StopConfirmationEmitsCanonicalSpeakingThenIdle`

### PR 1.2 - Backend Coordinator, Sequencing, and Coalescing

Status: Implemented

Summary:
- Added monotonic `sequence` to `assistant_ui_state`.
- Centralized canonical UI state ownership in backend coordinator/broadcaster.
- Added immediate vs coalesced vs terminal timing classes.
- Implemented queue-size-1 coalescing for soft UI states.
- Immediate listening/speaking/overlay events bypass coalescing.
- Added duplicate suppression rules without suppressing changed reasons.
- Preserved existing visual/speech events and frontend behavior.

Tests:
- `AssistantUiStateBroadcasterTests.EmitImmediateAsync_AssignsMonotonicSequences`
- `AssistantUiStateBroadcasterTests.ImmediateSpeakingSupersedesPendingCoalescedThinking`
- `AssistantUiStateBroadcasterTests.RapidCoalescedStatesCollapseToLatest`
- `AssistantUiStateBroadcasterTests.ImmediateListeningSupersedesPendingCoalescedIdle`
- `AssistantUiStateBroadcasterTests.ChangedIdleReasonIsNotSuppressed`
- `AssistantUiStateBroadcasterTests.ExactDuplicateIsSuppressed`
- `AssistantUiStateBroadcasterTests.OverlayClearEmitsWithHigherSequence`

### PR 2 - Frontend Canonical State Consumption

Status: Implemented

Summary:
- Main.gd now consumes `assistant_ui_state_received`.
- Added frontend sequence guard.
- Mapped canonical baseState to existing MerlinState.
- Legacy SPEAKING_* / SPEECH_ENERGY events no longer overwrite main state once canonical mode is active.
- Legacy events remain fallback before canonical state is received.
- Existing response, transcript, visual_state, and debug routing preserved.

Tests/Validation:
- Diff-based validation of `Main.gd` signal connection, sequence guard, baseState mapping, and legacy fallback guard.
- Godot CLI validation not run; Godot executable is not available in this shell.

This document defines the intended user-facing UI state model for Merlin. It exists before implementation so backend, WebSocket, and frontend changes can be made against one shared contract instead of continuing to derive UI state from scattered speech, turn, and interruption events.

## Purpose

Merlin currently has several internal state systems:

- live assistant turn state
- speech playback state
- audible playback / playback monitor state
- provisional audio hold state
- barge-in / interruption capture state
- confirmation / error result state
- frontend visual/orb state

These systems are useful internally, but the frontend should not have to infer the user-facing UI state from all of them independently.

The goal is to introduce a canonical UI state contract that answers two simple questions:

```text
What should Merlin visually appear to be doing right now?
Is there a special overlay condition on top of that state?
```

The frontend should render from this canonical contract, while lower-level events such as `SPEAKING_START`, `SPEECH_ENERGY`, `SPEAKING_END`, barge-in debug snapshots, and live-turn logs can remain as diagnostics or audio telemetry.

---

## Core UX Model

Merlin has four user-facing base states:

```text
idle
listening
thinking
speaking
```

Merlin also has overlay states:

```text
none
confirmation
error
```

The frontend should render primarily from:

```text
baseState + overlayState
```

Internal backend concepts such as `ProcessingTurn`, `ExecutingTool`, `PausedByUser`, `CapturingInterruption`, `FinalAnswer`, `Progress`, `StopConfirmation`, and `provisional_audio_hold` may still exist, but they should be mapped into these user-facing UI states.

---

## Base State Definitions

### idle

Merlin is quiet and not currently receiving user speech, not visibly processing a request, and not audibly speaking.

This includes quiet gaps between TTS chunks during an already-started spoken response.

Examples:

```text
No active user request
No user speech detected
No audible playback
-> idle
```

```text
Final answer is active
Current TTS chunk ended
Next TTS chunk is still generating
No audible audio is currently playing
-> idle
```

Important: a quiet gap between spoken chunks is **not** thinking. It is only a temporary audio-generation or playback gap inside an already-started response. Showing thinking here would cause visual flicker and make the UX feel noisy.

### listening

Merlin is actively receiving or capturing the user's speech.

This state must be extremely fast because it reflects user-perceived responsiveness.

Examples:

```text
User starts speaking from idle
-> listening
```

```text
Merlin is speaking
User interrupts
Floor-yield/provisional hold starts
-> listening immediately
```

```text
Barge-in capture starts
-> listening
```

Listening should not wait for:

```text
STT completion
interruption classification
route decision
DeepInfra
tool execution
```

As soon as Merlin has yielded to the user or decided user speech is happening, the UI should leave speaking and become listening.

### thinking

Merlin is processing accepted input.

Thinking starts after the user input is captured/accepted and Merlin is working on the request, interruption, tool call, correction, or response.

Examples:

```text
User speech captured
STT completed
request accepted
-> thinking
```

```text
Intent routing / tool planning starts
-> thinking
```

```text
Waiting on DeepInfra
-> thinking
```

```text
Tool execution in progress
-> thinking
```

```text
Interruption transcript accepted
Merlin is classifying/handling it
-> thinking
```

```text
Correction/replacement request accepted
Merlin is preparing new answer
-> thinking
```

Thinking should **not** be used for quiet pauses between TTS chunks after the spoken answer has already started.

### speaking

Merlin is audibly outputting speech.

Speaking must be driven by the playback/audio monitor, not by logical turn state alone.

Examples:

```text
First audio bytes of acknowledgement/progress/final answer/stop confirmation begin playing
-> speaking
```

```text
Audio output is actively playing
-> speaking
```

If playback context is active but audio is held, paused, not yet started, waiting for next chunk, or already drained, the user-facing base state should not be speaking.

Important distinctions:

```text
Live turn state = Speaking
does not automatically mean
audible audio is currently playing
```

```text
Playback context active but held
-> not speaking
```

```text
TTS chunk generating but not yet audible
-> thinking if response has not started
-> idle if response is already underway and this is only an inter-chunk gap
```

---

## Overlay State Definitions

### none

Default state. Normal blue visual style.

### confirmation

The confirmation overlay indicates Merlin is waiting for user confirmation.

Examples:

```text
Merlin needs confirmation before opening an app, URL, mapping, or trusted action
-> overlayState = confirmation
```

The base state still continues independently:

```text
speaking + confirmation
idle + confirmation
listening + confirmation
thinking + confirmation
```

Confirmation overlay clearing rule:

```text
Clear confirmation overlay on the next accepted user utterance.
```

Two possible outcomes:

```text
User confirms
-> overlay clears
-> normal processing continues
```

```text
User says something else
-> overlay clears
-> confirmation flow is broken/replaced by the new request
```

The overlay should not wait until the confirmed action completes. Once the user has responded, the previous confirmation prompt is no longer the current visual condition.

### error

The error overlay indicates the latest turn produced an error.

Examples:

```text
Tool failed
Command could not be completed
Backend generated error response
-> overlayState = error
```

Error overlay clearing rule:

```text
Clear error overlay when the next accepted user turn starts.
```

If the next turn also generates an error:

```text
clear old error
process new turn
new error happens
-> overlayState = error again
```

This prevents the UI from staying red while Merlin is already listening/thinking for the next request.

---

## Canonical WebSocket Event

Introduce a canonical backend-to-frontend event:

```json
{
  "type": "assistant_ui_state",
  "baseState": "idle",
  "overlayState": "none",
  "reason": "no_active_request",
  "correlationId": null,
  "turnId": null,
  "speechItemType": "none",
  "audiblePlaybackActive": false,
  "interruptionState": "none",
  "timestampUtc": "2026-06-25T00:00:00Z"
}
```

### Required Fields

#### type

Always:

```text
assistant_ui_state
```

#### baseState

Allowed values:

```text
idle
listening
thinking
speaking
```

#### overlayState

Allowed values:

```text
none
confirmation
error
```

#### reason

Short machine-readable reason for the state transition.

Examples:

```text
no_active_request
user_speech_detected
backend_idle_voice_capture_started
provisional_audio_hold_started
request_accepted
intent_routing_started
deepinfra_waiting
tool_execution_started
tts_first_audio_started
tts_chunk_gap
speech_playback_completed
stop_confirmation_started
confirmation_required
confirmation_response_accepted
error_response_generated
next_turn_started_clears_error
```

#### correlationId

Current request/turn correlation id when available.

#### turnId

Current assistant turn id when available.

#### speechItemType

Allowed values:

```text
none
acknowledgement
progress
final_answer
stop_confirmation
clarification
continuation
replacement_bridge
```

This is not necessarily used for the main visual state, but it is important for diagnostics and optional future visual nuance.

#### audiblePlaybackActive

Boolean. True only when audio is actually being output.

#### interruptionState

Allowed values:

```text
none
possible_user_speech
held_for_user_speech
capturing
classifying
handling
resuming
stopped
```

This is primarily diagnostic/future-facing. The frontend should not need to render all of these as separate major states.

#### timestampUtc

Backend timestamp for event ordering/debugging.

---

## Timing Rules

The UI state contract must be timing-aware. Not every state should be emitted with the same delay or from the same backend layer.

### Listening must be instant

Listening is a fast user-perception state.

Emit listening immediately when:

```text
user speech is detected
backend idle voice capture starts
barge-in capture starts
floor-yield starts
provisional audio hold starts
Merlin yields to user speech
```

Do not wait for:

```text
STT completion
classification
interruption handling
route dispatch
tool/LLM processing
```

Reason: if Merlin visibly remains speaking after it has yielded, the UI feels lagged and wrong.

### Thinking starts after input is accepted

Thinking should begin when Merlin has captured or accepted a user utterance and has started processing.

Emit thinking when:

```text
STT completed and request accepted
intent routing begins
tool planning/execution begins
DeepInfra request begins
interruption handling begins
correction/replacement request is accepted
```

Do not emit thinking while the user is still speaking/capturing. During capture, the UI should remain listening.

### Speaking waits for actual audible playback

Speaking should be emitted only when audio actually starts playing.

Do not emit speaking when:

```text
response text is ready
TTS generation starts
speech is queued
live turn state becomes Speaking
playback context is active but held
```

Emit speaking when:

```text
audio output starts
first audio bytes are written/played
playback monitor reports audiblePlaybackActive = true
```

### Chunk gaps should be idle, not thinking

During an already-started spoken response:

```text
audio chunk ends
next chunk is still generating
no audible audio is currently playing
-> baseState = idle
```

When the next chunk starts:

```text
audio output resumes
-> baseState = speaking
```

This keeps UX quiet and avoids flashing thinking states between chunks.

### Overlay clearing must be fast

Confirmation overlay:

```text
next accepted user utterance
-> overlayState = none
```

Error overlay:

```text
next accepted user turn starts
-> overlayState = none
```

If the new turn creates a new confirmation or error, set the overlay again as part of that new turn.

---

## State Transition Examples

### Normal voice request

```text
idle
user starts speaking
-> listening

STT completed / request accepted
-> thinking

intent routing / DeepInfra / tool work
-> thinking

first audio starts
-> speaking

audio ends
-> idle
```

### Spoken response with TTS chunk gaps

```text
thinking
first audio chunk starts
-> speaking

chunk ends, next chunk generating
-> idle

next chunk starts
-> speaking

chunk ends, next chunk generating
-> idle

final chunk starts
-> speaking

final audio drains
-> idle
```

### User interrupts final answer

```text
speaking
user speech detected / floor-yield hold starts
-> listening immediately

capture continues
-> listening

STT completed / interruption accepted
-> thinking

interruption handled
-> speaking if Merlin replies audibly
-> idle if Merlin stops without further speech
```

### Stop command

```text
speaking
user says "Merlin, stop"
yield/capture starts
-> listening

stop transcript accepted / CI handles stop
-> thinking

stop confirmation audio starts
-> speaking

stop confirmation audio completes
-> idle
```

### Confirmation required

```text
thinking
Merlin asks for confirmation
-> speaking + confirmation overlay

confirmation prompt ends
-> idle + confirmation overlay
```

User confirms:

```text
user starts speaking
-> listening + confirmation overlay

confirmation response accepted
-> listening/thinking + none

confirmed action proceeds
-> thinking
-> speaking or idle
```

User says something else:

```text
user starts speaking
-> listening + confirmation overlay

new utterance accepted
-> thinking + none

new request flow continues
```

### Error

```text
thinking
error response generated
-> speaking or idle + error overlay
```

Next user turn:

```text
user starts speaking
-> listening + error overlay

new turn accepted
-> listening/thinking + none
```

If new turn errors:

```text
new error generated
-> error overlay again
```

---

## Ownership Rules

### Backend owns canonical state

The backend should emit canonical `assistant_ui_state` events.

The frontend should not infer main UI state from scattered low-level signals once this contract is active.

### Frontend owns rendering

Frontend renders:

```text
baseState + overlayState
```

Frontend may use additional fields like `speechItemType`, `reason`, or `interruptionState` for debugging labels or future visual nuance, but they should not create extra major states without a deliberate design decision.

### Low-level events may remain

Existing events may continue to exist:

```text
SPEAKING_START
SPEECH_ENERGY
SPEAKING_END
SPEAKING_CANCELLED
visual_state
barge_in_debug_snapshot
```

But after migration, they should be treated as:

```text
audio telemetry
debug data
legacy compatibility
```

They should not be the main source of truth for the orb state.

---

## Priority Rules

If multiple state inputs happen close together, use this priority order:

### 1. Immediate user speech/yield beats speaking

```text
if user_speech_detected or provisional_hold_started:
    baseState = listening
```

This must override stale speaking state.

### 2. Actual audible playback beats idle/thinking

```text
if audiblePlaybackActive:
    baseState = speaking
```

### 3. Active capture beats thinking

```text
if user speech is still being captured:
    baseState = listening
```

### 4. Accepted processing beats idle

```text
if request/interruption accepted and no audible playback/capture:
    baseState = thinking
```

### 5. No activity becomes idle

```text
if no capture, no processing, no audible playback:
    baseState = idle
```

Overlay state is independent and should not override base state.

---

## Mapping From Existing Backend Concepts

### LiveAssistantTurnState

Suggested mapping:

```text
IdleListening -> idle or listening depending capture state
CapturingUserSpeech -> listening
Interpreting -> thinking
ProcessingTurn -> thinking
PlanningTool -> thinking
AwaitingToolCommit -> thinking + confirmation overlay if confirmation is required
ExecutingTool -> thinking
Speaking -> speaking only if audiblePlaybackActive is true; otherwise idle/thinking depending context
PausedByUser -> listening if caused by user speech
Interrupted -> thinking or listening depending current stage
Completed -> idle if no audible playback
Failed -> idle/speaking + error overlay
Cancelled -> idle unless stop confirmation is speaking
Superseded -> thinking if replacement request is active, else idle
```

### SpeechPlaybackItemType

Suggested mapping:

```text
Acknowledgement -> speaking when audible, otherwise idle
Progress -> speaking when audible, otherwise idle
FinalAnswer -> speaking when audible, idle during chunk gaps
StopConfirmation -> speaking when audible, idle after completion
InterruptionClarification -> speaking when audible
InterruptionContinuation -> speaking when audible
```

### Provisional audio hold

Suggested mapping:

```text
BeginProvisionalAudioHoldAsync succeeds
-> baseState = listening
   interruptionState = held_for_user_speech
   audiblePlaybackActive = false
```

Resume:

```text
ResumeProvisionalAudioHoldAsync
-> baseState = speaking only when audio actually resumes
```

Flush/cancel:

```text
FlushProvisionalAudioHoldAsync
-> baseState = thinking if processing interruption
-> speaking if stop confirmation or clarification starts
-> idle if no further action
```

### RequestProgressSpeechService

Progress request waiting on LLM/tool:

```text
before progress speech audio starts
-> thinking
```

Progress phrase audio starts:

```text
-> speaking
speechItemType = progress
```

Progress phrase ends while main request still waiting:

```text
-> thinking
```

Final answer begins:

```text
-> speaking
speechItemType = final_answer
```

Note: progress speech is different from final-answer chunk gaps. After progress speech ends, the request is still genuinely waiting/processing, so thinking is appropriate.

---

## Overlay Lifecycle

### Confirmation overlay lifecycle

Set overlay to confirmation when:

```text
tool/router/backend produces confirmation-required response
frontend existing confirmation flow is entered
trusted action requires confirmation
```

Clear overlay when:

```text
next accepted user utterance starts/arrives
confirmation response is accepted
new request supersedes confirmation flow
```

If the confirmation response is itself invalid and Merlin asks for confirmation again, set overlay to confirmation again.

### Error overlay lifecycle

Set overlay to error when:

```text
backend response has success = false
tool result maps to an error
fatal request processing error occurs
frontend receives explicit error state
```

Clear overlay when:

```text
next accepted user turn starts
```

If the next turn also fails, set error again.

---

## Non-Goals For First Implementation PR

Do not implement all visual nuance immediately.

Do not remove existing events immediately.

Do not make frontend depend on every diagnostic field.

Do not refactor all live turn lifecycle ownership in the first PR.

Do not change VAD/AEC/STT/interruption logic.

Do not change confirmation business logic.

Do not change Chatterbox/TTS generation settings.

---

## Suggested PR Plan

### PR 1: Backend canonical UI state event skeleton

Goal:

```text
Emit assistant_ui_state from backend in parallel with existing events.
Do not change frontend behavior yet except optional logging.
```

Likely files:

```text
WebSocketHandler.cs
AssistantSpeechPlaybackService.cs
LiveAssistantTurnService.cs
BargeInCoordinator.cs
FloorYieldController.cs
RequestProgressSpeechService.cs
CommandRouter.cs
```

Validation:

```text
Logs/WebSocket output show correct baseState/overlayState timelines for normal request, progress, interruption, stop, confirmation, and error.
```

### PR 2: Frontend passive receiver/logging

Goal:

```text
Frontend receives assistant_ui_state and logs/displays debug state without driving the orb yet.
```

Likely files:

```text
MerlinWebSocketClient.gd
Main.gd
BargeInDebugOverlay.gd or a small debug display if present
```

Validation:

```text
frontend receives every canonical state event with expected payload
no visual behavior changed yet
```

### PR 3: Frontend consumes canonical baseState

Goal:

```text
Main orb/activity state is driven by assistant_ui_state.baseState.
Existing SPEAKING_* events become audio energy/telemetry only.
```

Likely files:

```text
Main.gd
CoreOrb3D.gd
MerlinWebSocketClient.gd
```

Validation:

```text
idle -> listening -> thinking -> speaking -> idle works for normal flow
speaking -> listening is instant on interruption yield
chunk gaps show idle, not thinking
```

### PR 4: Frontend consumes overlayState

Goal:

```text
confirmation and error overlays are driven by assistant_ui_state.overlayState.
Clear overlay on next accepted user turn.
```

Likely files:

```text
Main.gd
CoreOrb3D.gd
confirmation/error UI handlers
```

Validation:

```text
confirmation overlay clears on confirm or unrelated request
error overlay clears on next accepted turn and can reappear if next turn errors
```

### PR 5: Cleanup legacy UI derivation

Goal:

```text
Remove or demote scattered state derivation from pending requests, SPEAKING_ENERGY, visual_state patches, and ad-hoc route dispatches.
```

Validation:

```text
No regressions in voice/text request flow, interruption flow, confirmation flow, or error flow.
```

---

## Manual Validation Matrix

### Normal request

```text
idle
listening
thinking
speaking
idle
```

### Request with acknowledgement/progress

```text
idle
listening
thinking
speaking       acknowledgement
thinking
speaking       progress
thinking
speaking       final answer
idle
```

### Final answer with chunk gaps

```text
speaking
idle           chunk gap
speaking
idle           chunk gap
speaking
idle
```

### Interruption

```text
speaking
listening      immediate on yield/hold
thinking       after transcript accepted
speaking or idle depending handling result
```

### Stop

```text
speaking
listening
thinking
speaking       stop confirmation
idle
```

### Confirmation

```text
speaking + confirmation
idle + confirmation
listening + confirmation
thinking + none
speaking + none
idle + none
```

### Error

```text
thinking
speaking/idle + error
listening + error
thinking + none
```

---

## Open Decisions

1. Should acknowledgement speech visually count as speaking?  
   Recommended: yes, while audio is audible.

2. Should progress speech return to thinking after it finishes?  
   Recommended: yes, because the request is still processing.

3. Should final-answer chunk gaps return to thinking?  
   Recommended: no. Use idle for quiet chunk gaps.

4. Should stop confirmation visually count as speaking?  
   Recommended: yes, while audible.

5. Should held playback show as listening?  
   Recommended: yes, because Merlin yielded to the user.

6. Should overlay clear when user starts speaking or when utterance is accepted?  
   Recommended: clear on accepted utterance to avoid clearing overlays on accidental noise, but switching baseState to listening can happen immediately.

7. Should frontend timers derive state?  
   Recommended: no for canonical state. Timers can smooth visuals but should not invent state.

---

## Final Recommendation

Implement **PR 1: Backend canonical UI state event skeleton** next.

The first implementation should emit `assistant_ui_state` events in parallel with existing events and log all transitions. It should not yet remove old frontend logic. This lets Merlin verify the state timeline under real voice, interruption, confirmation, stop, and error scenarios before making the frontend depend on the new contract.
