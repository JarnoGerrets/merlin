---
type: bug
status: fixed
area: voice
first_seen: 2026-07-07
last_updated: 2026-07-07
verification_status: pending-live-validation
---

# AskClarification Live Dead-End

## Summary

Live interruption utterances classified as `AskClarification` could enter a stale PR7 deferred branch with no executable live owner. The observed failure mode was a short utterance such as `in the pool` suppressing playback resume and leaving Merlin in an interruption handling state.

## User-Visible Impact

Merlin appeared to stop listening or stop responding after a live interruption. Internally, STT could still run, but the live interruption path had not resolved to resume, cancel, clarify, or route.

## Root Cause

`LiveInterruptionIntegrationService` still had a branch:

```text
Ask-user-to-clarify is not executed live in PR7.
```

That branch returned control to legacy semantic routing even though live playback resume had already been suppressed. Without a pending clarification owner, the result could become a terminal dead end.

## Fix Applied

PR10.4a through PR10.4e:

- Remove the stale PR7 defer branch for live `AskUserToClarifyInterruption`.
- Treat short unclear fragments as handled fallback outcomes.
- Resume provisional holds when available.
- Suppress legacy semantic routing for handled fallback outcomes.
- Add regression coverage for `in the pool`.
- Add durable pending unclear-interruption clarification ownership.
- Add awaiting clarification state, timeout/cancel cleanup, and stale handling watchdog.
- Bind consumed pending clarification answers to `LiveInterruptionIntegrationService` for recomposed continuation generation.
- Suppress legacy cleanup and generic command routing while the pending clarification owner handles the response.

## Current Status

Fixed.

The observed stale PR7 AskClarification short-fragment wedge is fixed. The broader PR10.4 AskClarification pending clarification/recomposition owner is implemented.

Manual live UX validation is still pending before this bug should be marked `verified`.

## PR10.4 Investigation Status

PR10.4b has added the durable pending unclear-interruption clarification owner.

PR10.4c has added explicit `awaiting_interruption_clarification` state and timeout recovery.

PR10.4d has added an owner-aware stale `InterruptionState=handling` watchdog. Ownerless stale handling now clears to `none`, while active capture, pending clarification, held playback, and interruption-owned speech are preserved.

PR10.4e has added executable full clarification/recomposition ownership for consumed pending clarification responses.

## Remaining Risk

Known adjacent correction regeneration and BargeIn idle-capture test failures remain tracked separately. They are not caused by the AskClarification dead-end recovery sequence.

Live UX validation still needs to prove the full STT/TTS/playback path behaves correctly outside automated fakes.

## Regression Test

`ConversationalInterruptionLiveIntegrationTests.TryHandleYieldedInterruptionAsync_AskClarificationShortFragmentInThePool_ResumesHoldAndSuppressesSemanticRouting`

## Related Notes

- [[AskClarification Live Dead-End Recovery Plan]]
- [[AskClarification PR10.4 Live UX Validation Checklist]]
- [[LiveInterruptionIntegrationService]]
- [[Voice Interruption System]]
