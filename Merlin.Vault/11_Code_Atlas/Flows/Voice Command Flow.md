---
type: flow
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Voice Command Flow

## Summary

Frontend or backend captures user speech, STT produces text, LiveUtteranceGate evaluates it, CommandRouter routes it, and AssistantSpeechPlaybackService speaks the response.

## Current Flow

1. Microphone audio
2. STT transcript
3. LiveUtteranceGate.Evaluate
4. CommandRouter.RouteAsync
5. Service/tool/browser/motion action
6. AssistantSpeechPlaybackService
7. MerlinWebSocketClient.gd visual events

## Mermaid Diagram

```mermaid
flowchart LR
    N0[Microphone audio] --> N1[STT transcript]
    N1[STT transcript] --> N2[LiveUtteranceGate.Evaluate]
    N2[LiveUtteranceGate.Evaluate] --> N3[CommandRouter.RouteAsync]
    N3[CommandRouter.RouteAsync] --> N4[Service/tool/browser/motion action]
    N4[Service/tool/browser/motion action] --> N5[AssistantSpeechPlaybackService]
    N5[AssistantSpeechPlaybackService] --> N6[MerlinWebSocketClient.gd visual events]
```

## Related Feature And Architecture Notes

- [[Voice Pipeline Architecture]]
- [[CommandRouter]]
- [[LiveUtteranceGate]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
