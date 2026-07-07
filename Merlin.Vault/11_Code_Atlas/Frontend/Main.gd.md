---
type: code-atlas
status: current
project: Merlin.Frontend
tags:
  - merlin
  - code-atlas
---

# Main.gd

## File

`Merlin.Frontend/Scripts/Main.gd`

Verified present in current repo.

## Purpose

Main Godot scene controller. It manages backend WebSocket connection, voice capture/playback UI, assistant/orb state, BrowserWorkspace UI hiding/restoring, and dashboard gesture visual control for window hover/select/drag/resize/crumple/delete.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control]]
- [[Browser Workspace]]
- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Main Types / Classes

- `Main.gd` GDScript class or script.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `_ready` | func | Wires UI controls, WebSocket signals, gesture cursor, chat/window systems, voice mode, and debug overlays. | setup helpers | Godot lifecycle | Central startup. |
| `_process` | func | Polls voice/WebSocket/streaming perf state and updates pending gesture pinches, resize, crumple, overlays. | gesture/update helpers | Godot frame loop | Frame-sensitive. |
| `_handle_ui_control_visual_event` | func | Enables/disables dashboard UI-control mode from backend events. | mode state helpers | WebSocket visual event route | Gates gesture handling. |
| `_handle_gesture_visual_event` | func | Routes backend gesture events by type to pointer move/pinch handlers. | `_gesture_pointer_move`; `_gesture_pinch_start`; `_gesture_pinch_end` | WebSocket visual event route | Dashboard motion UI path. |
| `_gesture_event_position` | func | Converts normalized event x/y into viewport coordinates. | viewport size | gesture handlers | Must match backend coordinate contract. |
| `_gesture_pointer_move` | func | Stores pointer position/history, moves cursor, updates hover/grab/resize/crumple state. | window manager helpers | gesture visual events | Core dashboard pointer path. |
| `_gesture_pinch_start` / `_gesture_pinch_end` | func | Starts selection/grab/resize/crumple candidates or releases/commits actions. | grab/resize/crumple helpers | gesture visual events | Multi-pointer logic. |
| `_try_start_gesture_resize` / `_update_gesture_resize` | func | Uses two pinched pointers to resize selected window. | MerlinWindow APIs | process/pinch | Uses separate resize sensitivity constants. |
| `_try_start_crumple_candidate` / `_update_crumple` / `_commit_crumple_dismiss` | func | Forms a crumple proxy from selected window and dismisses it on throw. | CrumpleSheetProxy; window manager | process/pinch release | Gesture delete UX. |
| browser workspace helpers | funcs | Hide main Merlin UI, manage mini orb, restore UI after browser close. | BrowserWorkspace state events | WebSocket browser state route | Important for browser overlay UX. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_ui_control_mode_active` | bool | Whether dashboard gesture controls should manipulate UI. | gesture handlers | backend visual event | false on stop |
| `_gesture_pointer_positions` / `_gesture_pointer_pinched` | dictionaries | Current pointers and pinch states. | gesture/resize/crumple helpers | visual event handlers | cleared on mode stop/reset |
| `_gesture_grabs` | dictionary | Active window drags by pointer. | move/release helpers | pinch start/end | cleared on release/reset |
| `_gesture_resize_state` | dictionary | Active two-hand resize session. | process/update | resize start/finish | cleared on finish/reset |
| `_selected_surface_id` / `_crumple_state` | strings/dicts | Selected window and crumple/delete animation state. | crumple helpers | pinch/selection helpers | cleared on commit/cancel |
| `_browser_workspace_active/bounds/hidden_nodes/mini_window` | values | BrowserWorkspace visual integration state. | browser helpers | WebSocket state events | reset on close/restore |

## Dependencies

| Dependency | Used For |
| --- | --- |
| MerlinWebSocketClient | Backend messages and visual events. |
| MerlinWindowManager/MerlinWindow | Dashboard window manipulation. |
| CrumpleSheetProxy | Dismiss animation. |
| CoreOrb3D | Assistant visual state and mini-orb state. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| user text/voice messages | backend WebSocket | text command, voice stream, speech presence. |
| UI visual changes | local scene | window movement/resize/dismiss, orb state, chat log. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| backend WebSocket packets | MerlinWebSocketClient signals | response, visual events, assistant state, browser workspace state. |
| Godot input/microphone/audio events | engine | `_input`, voice helpers. |

## External Side Effects

Records microphone audio, plays audio, manipulates Godot windows/scene nodes, opens secondary mini-orb window, writes user WAV for transcription path.

## Safety / Guardrails

Keep frontend gesture state gated by `_ui_control_mode_active` or browser workspace state as appropriate. Backend owns command routing; frontend owns visual/window manipulation.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| Backend tests only | Backend emits expected visual events. | No automated Godot scene tests in repo. |
| manual Godot headless/load checks | Parse/runtime smoke when performed. | Gesture UX manual. |

## Known Risks / Fragility

This file centralizes many responsibilities. Gesture state can get stuck if UI-control mode, browser workspace hiding, or pointer release events are missed.

## Change Notes for Agents

Godot frontend behavior is mostly manually validated. Keep UI state and gesture state resets explicit when browser workspace or UI-control mode changes.
