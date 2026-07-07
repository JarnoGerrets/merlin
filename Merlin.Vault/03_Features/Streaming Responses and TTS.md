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

# Streaming Responses and TTS

## Summary

Streaming answer/TTS pipeline and assistant speech playback.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- AssistantSpeechPlaybackService.cs exists.
- ChatterboxTtsProvider and streaming services are registered in Program.cs.
- AssistantSpeechPlaybackServiceTests.cs exists.

## What Exists Today

Assistant can generate/stream speech and broadcast UI playback state.

## Current Behavior

Chatterbox/Piper routing and playback state are implemented; latency and interruption coupling remain sensitive.

## Planned Behavior

More robust playback checkpoints and responsive feedback timing.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/AssistantSpeechPlaybackService.cs` | AssistantSpeechPlaybackService | Playback owner | Enqueue/playback/drain/complete. |
| `Merlin.Backend/Services/ChatterboxTtsProvider.cs` | ChatterboxTtsProvider | TTS | Chunk generation. |

## Code Atlas

- [[AssistantSpeechPlaybackService]]
- [[Assistant Speech Playback Flow]]
- [[Assistant Playback Events]]

## Related Systems

- TTS providers
- [[Correction Layer]]
- [[Responsive Feedback]]
- [[Voice Interruption System]]

## Dependencies

- TTS providers
- [[Voice Interruption System]]

## Dependents

- [[Responsive Feedback]]
- [[Correction Layer]]

## Readiness

Ready for implementation: yes

Reason:
Targeted playback fixes are ready.

Blocked by:
- Fuzzy timing behavior in barge-in/correction tests.

Next safe action:
Stabilize playback completion/checkpoint behavior.

## Non-Goals / Do Not Build Yet

- Do not redesign TTS provider routing during browser/motion work.

## Known Bugs / Fragility

- Playback completion and interruption timing can affect calibration/confirmation flows.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/AssistantSpeechPlaybackServiceTests.cs` | Playback behavior | Real audio device timing is manual. |

## Relevant Implementation Plans

- None currently promoted for this feature.

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
