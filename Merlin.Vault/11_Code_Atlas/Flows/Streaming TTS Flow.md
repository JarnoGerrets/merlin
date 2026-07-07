---
type: flow
status: partial
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Streaming TTS Flow

## Summary

TTS routing uses configured providers and segmented/chunked playback, with interruption timing still fragile.

## Current Flow

1. assistant text
2. TtsRouter/Chatterbox/Piper
3. audio chunks
4. AssistantSpeechPlaybackService
5. playback drain
6. UI idle

## Mermaid Diagram

```mermaid
flowchart LR
    N0[assistant text] --> N1[TtsRouter/Chatterbox/Piper]
    N1[TtsRouter/Chatterbox/Piper] --> N2[audio chunks]
    N2[audio chunks] --> N3[AssistantSpeechPlaybackService]
    N3[AssistantSpeechPlaybackService] --> N4[playback drain]
    N4[playback drain] --> N5[UI idle]
```

## Related Feature And Architecture Notes

- [[Streaming TTS Architecture]]
- [[AssistantSpeechPlaybackService]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
