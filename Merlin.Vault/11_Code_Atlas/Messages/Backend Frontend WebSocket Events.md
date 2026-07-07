---
type: protocol
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Backend Frontend WebSocket Events

| Event / Message | Direction | Payload / Notes |
| --- | --- | --- |
| assistant response JSON | backend -> Godot | Text/tool/browser response consumed by `MerlinWebSocketClient.response_received`. |
| assistant UI state | backend -> Godot | Canonical state sequence, base/overlay state, speech metadata, timing class. |
| `visual_state` | backend -> Godot | Legacy/orb visual state patch. |
| `UI_CONTROL_MODE_STARTED` / `UI_CONTROL_MODE_STOPPED` | backend -> Godot | Dashboard gesture-control mode gate used by `Main.gd`. |
| `GESTURE_POINTER_MOVE` | backend -> Godot | Normalized pointer event for dashboard UI-control cursor. |
| `GESTURE_PINCH_START` / `GESTURE_PINCH_MOVE` / `GESTURE_PINCH_END` | backend -> Godot | Pinch events for dashboard select/drag/resize/crumple. |
| `BROWSER_WORKSPACE_STATE` | backend -> Godot | BrowserWorkspace active/bounds state used to hide/restore main UI and mini orb. |
| `UI_CHATLOG_APPEND` | backend -> Godot | Chat log entry event. |
| `voice_transcript` / barge-in debug packets | backend -> Godot | Live transcript/debug UI payloads. |
| text assistant request | Godot -> backend | `message`, correlation id, interaction source, client mode. |
| `voice_stream_start/chunk/end/cancel` | Godot -> backend | Streamed microphone audio packet lifecycle. |
| speech presence marker | Godot -> backend | User started speaking marker for interruption handling. |

## Related Notes

- [[Main.gd]]
- [[MerlinWebSocketClient.gd]]
- [[CommandRouter]]
- [[AssistantSpeechPlaybackService]]
- [[Motion Gesture Dispatch Flow]]
