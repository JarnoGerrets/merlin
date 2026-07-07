---
type: flow
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Assistant Speech Playback Flow

## Summary

Responses become TTS audio and AssistantSpeechPlaybackService emits playback state used by UI and interruption logic.

## Current Flow

1. response text
2. TTS provider
3. AssistantSpeechPlaybackService
4. AssistantUiStateBroadcaster
5. BargeIn monitor
6. final_answer_completed

## Mermaid Diagram

```mermaid
flowchart LR
    N0[response text] --> N1[TTS provider]
    N1[TTS provider] --> N2[AssistantSpeechPlaybackService]
    N2[AssistantSpeechPlaybackService] --> N3[AssistantUiStateBroadcaster]
    N3[AssistantUiStateBroadcaster] --> N4[BargeIn monitor]
    N4[BargeIn monitor] --> N5[final_answer_completed]
```

## Related Feature And Architecture Notes

- [[Streaming Responses and TTS]]
- [[AssistantSpeechPlaybackService]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
