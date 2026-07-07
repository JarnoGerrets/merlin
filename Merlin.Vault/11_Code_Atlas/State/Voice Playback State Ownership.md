---
type: state
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Voice Playback State Ownership

| State | Owner | Readers | Writers | Lifetime | Reset Conditions | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Playback active/item/turn | AssistantSpeechPlaybackService | UI broadcaster, interruption services | Playback methods | turn/audio lifetime | complete/stop/cancel | Barge-in timing fragility |

## Related Notes

- [[Current System Map]]
- [[Code Atlas Index]]
