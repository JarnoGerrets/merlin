---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
---

# Voice Interruption System

## Summary

Handles barge-in, stop/cancel, live utterances, and conversational interruptions.

## Status

partial

## What Exists Today

- BargeInCoordinator exists.
- LiveUtteranceGate exists.
- Conversational interruption intelligence exists.
- Playback state and assistant UI broadcaster exist.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs` | Coordinator | Capture, barge-in, routing. |
| `Merlin.Backend/Services/LiveUtterance/LiveUtteranceGate.cs` | Gate | Accept/reject/hold decisions. |
| `Merlin.Backend/Services/InterruptionIntelligence/*` | Intelligence | Recomposition/correction support. |
| `Merlin.Backend/Services/AssistantSpeechPlaybackService.cs` | Playback | State and drain completion. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Supports live interruption and routing while assistant may be speaking.

## Planned Behavior

Stabilize tests and context-aware command acceptance.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Full test suite currently shows failures in barge-in/correction timing tests.
- Confirm/correction dead ends have been recurring fragility.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Read interruption docs before changing.
