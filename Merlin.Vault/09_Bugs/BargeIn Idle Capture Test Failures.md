---
type: bug
status: open
area: voice
first_seen: 2026-07-07
last_updated: 2026-07-07
---

# BargeIn Idle Capture Test Failures

## Summary

Four BargeIn idle-capture tests fail in broad voice validation. They are adjacent to the voice interruption system but separate from AskClarification PR10.4 pending clarification ownership.

## Failing Tests

- `BargeInCoordinatorTests.ProcessMicrophoneFrame_BackendIdleVoice_AddsCaptureIdAndTimelineLogs`
- `BargeInCoordinatorTests.ProcessMicrophoneFrame_IdleAecOnlyEnergy_DoesNotExtendCapture`
- `BargeInCoordinatorTests.ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest`
- `BargeInCoordinatorTests.ProcessMicrophoneFrame_IdleRawMicSpeech_KeepsCaptureActive`

## Evidence

These failures were reproduced before PR10.4d and PR10.4e changes and were classified as pre-existing adjacent failures in:

- [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]]
- [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]]
- [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]]

## Impact

The broad BargeIn/voice filter remains red, which makes voice regression validation noisier.

## Fix Direction

Use [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]].

## Related Notes

- [[Voice Interruption System]]
- [[BargeInCoordinator]]
- [[Current Test Coverage]]
