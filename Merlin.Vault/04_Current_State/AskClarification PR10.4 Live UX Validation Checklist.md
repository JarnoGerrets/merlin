---
type: validation-checklist
status: pending
area: voice
related_feature: Voice Interruption System
related_bug: AskClarification Live Dead-End
last_updated: 2026-07-07
---

# AskClarification PR10.4 Live UX Validation Checklist

## Purpose

Manual live validation for the completed AskClarification PR10.4 implementation sequence.

The implementation is covered by focused automated tests, but this checklist verifies the real STT/TTS/playback timing path with Merlin running.

## Preconditions

- Backend is built from the PR10.4a-e implementation state.
- Merlin voice playback, live interruption monitoring, and barge-in are enabled.
- `EnablePendingInterruptionClarification` is enabled for the test profile/config being used.
- Logs are visible or captured for backend voice, live interruption, BargeIn, pending clarification, playback, and UI state events.

## Happy Path

| Step | Action | Expected Result | Evidence To Check |
| --- | --- | --- | --- |
| 1 | Ask Merlin a question that produces a long spoken answer. | Merlin starts a long final answer. | `backend playback started`; active final-answer turn id/correlation id. |
| 2 | Interrupt while Merlin is speaking with an unclear correction or fragment. | Merlin treats the utterance as an unclear interruption, not a generic command. | `AskClarification`/pending owner logs; no generic command route for the unclear fragment. |
| 3 | Wait for Merlin's clarification prompt. | Merlin asks a clarification question. | pending clarification created; `awaiting_interruption_clarification` state emitted. |
| 4 | Answer the clarification naturally. | The answer is consumed by the pending owner. | pending clarification consumed; response does not route to CommandRouter/backend voice request. |
| 5 | Let Merlin continue. | Merlin recomposes and continues correctly from the interrupted answer context. | continuation model call; interruption continuation speech output; no stale final-answer resume. |
| 6 | Inspect state after continuation starts/completes. | Merlin does not remain stuck in `handling`. | UI/backend state returns to `none` when ownership resolves. |
| 7 | Inspect awaiting state after answer consumption. | Merlin does not remain stuck in `awaiting_interruption_clarification`. | pending owner removed/consumed; state clears. |

## Timeout Path

| Step | Action | Expected Result | Evidence To Check |
| --- | --- | --- | --- |
| 1 | Trigger an unclear interruption and wait for clarification prompt. | Pending owner created and awaiting state emitted. | pending id, active turn id, expiry timestamp. |
| 2 | Do not answer before timeout. | Pending owner expires and state returns to `none`. | timeout/expiry log; `awaiting_interruption_clarification` -> `none`. |
| 3 | Speak a normal command after timeout. | The utterance is routed normally, not as stale clarification. | no pending consume; normal gate/route logs. |

## Stop / Cancel Path

| Step | Action | Expected Result | Evidence To Check |
| --- | --- | --- | --- |
| 1 | Trigger an unclear interruption and wait for clarification prompt. | Pending owner created. | pending id and awaiting state. |
| 2 | Say a global stop/cancel phrase during clarification waiting. | Pending clarification is cancelled or bypassed safely and state clears. | cancel/stop route; pending cancelled; interruption state `none`. |
| 3 | Confirm playback state. | Merlin does not resume stale held speech over the stop/cancel result. | no stale hold resume after cancel; playback generation remains consistent. |

## Required Log Evidence

The run should capture enough evidence to diagnose:

- original final-answer turn id and correlation id,
- yielded unclear utterance capture id/transcript,
- pending clarification id and expiry,
- transition to `awaiting_interruption_clarification`,
- consumed clarification response text,
- suppression of generic semantic routing,
- playback hold flush/resume decision,
- continuation model call or safe failure,
- interruption continuation speech output,
- final transition out of `handling`/`awaiting_interruption_clarification`.

## Pass Criteria

- Merlin asks for clarification after unclear interruption.
- The clarification answer is bound to the pending owner.
- The answer is not treated as a normal command.
- Merlin recomposes/continues without replaying stale held speech.
- Timeout and stop/cancel paths clear state safely.
- Logs contain enough evidence to debug the full flow.

## Fail Criteria

- Clarification answer routes to CommandRouter/backend voice request as a new command.
- Merlin stays in `handling` or `awaiting_interruption_clarification`.
- Original held speech resumes after recomposition.
- Timeout leaves pending state active.
- Stop/cancel leaves pending state active or resumes stale speech.

## Related Notes

- [[AskClarification Live Dead-End]]
- [[AskClarification Live Dead-End Recovery Plan]]
- [[Voice Interruption System]]
- [[LiveInterruptionIntegrationService]]
- [[BargeInCoordinator]]
- [[PendingInterruptionClarificationService]]
