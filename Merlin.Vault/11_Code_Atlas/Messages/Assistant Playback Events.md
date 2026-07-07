---
type: protocol
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Assistant Playback Events

| Event / Message | Direction | Payload / Notes |
| --- | --- | --- |
| `audio_playback_started` | playback -> UI state | assistant speaking |
| `final_answer_completed` | playback -> UI state | assistant idle |
| `provisional_audio_hold_timeout_resumed` | playback -> UI state | held audio resumed |
| `barge-in monitor started/stopped` | barge-in diagnostics | interruption state |

## Related Notes

- [[Code Atlas Index]]
- [[Current System Map]]
