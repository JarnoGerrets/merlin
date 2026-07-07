---
type: bug
status: open
area: correction
first_seen: 2026-07-07
last_updated: 2026-07-07
---

# Correction Regeneration Test Failures

## Summary

Correction regeneration tests fail in full backend validation. They are separate from AskClarification PR10.4 pending clarification ownership.

## Failing Tests

- `CorrectionRegenerationDispatcherTests.Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest` row `i mean family car`
- `CorrectionRegenerationDispatcherTests.Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest` row `i mean what is the purpose of a voice`
- `CorrectionRegenerationDispatcherTests.OldCancelledCorrelationIdCannotSuppressNewCorrectionCorrelationId`
- `CorrectionRegenerationDispatcherTests.Correction_NewCorrectedResponseCanEnqueueSpeech`
- `CorrectionRegenerationDispatcherTests.Correction_CancelsOldTurnAndDispatchesNewRequest_WithNewCorrelationId`

## Evidence

The failures were classified as pre-existing unrelated failures during AskClarification safe fallback and closure work.

Related run evidence:

- [[RUN-2026-07-07-003 AskClarification Dead-End Safe Fallback]]
- [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]]

## Impact

Correction regeneration behavior cannot be treated as fully stable until these tests are fixed.

## Fix Direction

Use [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]].

## Related Notes

- [[Correction Layer]]
- [[Correction Flow]]
- [[CorrectionRequestBuilder]]
- [[Current Test Coverage]]
