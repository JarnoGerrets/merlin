---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
  - layer/backend
---

# Responsive Feedback

## Summary

Short acknowledgements and feedback during interruptions/requests.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- ResponsiveFeedbackOrchestrator.cs and feedback services exist.
- AcknowledgementIntegrationTests.cs exists.

## What Exists Today

Feedback/acknowledgement services exist and integrate with speech playback.

## Current Behavior

Partial UX layer; dependent on voice/playback timing stability.

## Planned Behavior

Make feedback surface-aware and non-blocking.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/Feedback/ResponsiveFeedbackOrchestrator.cs` | ResponsiveFeedbackOrchestrator | Feedback orchestration | Selects/emits feedback. |
| `Merlin.Backend/Services/AcknowledgementSpeechService.cs` | AcknowledgementSpeechService | Acknowledgement speech | Short spoken feedback. |

## Code Atlas

- [[AssistantSpeechPlaybackService]]
- [[Assistant Playback Events]]

## Related Systems

- Better command UX
- [[Voice Interruption System]]

## Dependencies

- [[Voice Interruption System]]

## Dependents

- Better command UX

## Readiness

Ready for implementation: no

Reason:
Voice timing tests should be stabilized first.

Blocked by:
- [[Voice Interruption System]]

Next safe action:
Fix voice timing regressions first.

## Non-Goals / Do Not Build Yet

- Do not add more chatty confirmations while command outcomes are unstable.

## Known Bugs / Fragility

- Feedback can conflict with calibration/timed prompts if not gated by playback completion.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/AcknowledgementIntegrationTests.cs` | Acknowledgements | End-to-end timing remains manual. |

## Relevant Implementation Plans

- [[Responsive Feedback Migration V2 Plan]]
- [[Responsive Feedback Migration Original Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (3 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
