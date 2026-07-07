# Merlin Frontend UI Current Architecture Report

## 1. Executive Summary

The current Merlin frontend UI is concentrated in one Godot scene, `Merlin.Frontend/Main.tscn`, and one large script, `Merlin.Frontend/Scripts/Main.gd`. The floating chatlog is not a separate scene or reusable window component. It is created programmatically inside `_setup_chatlog_panel()` and its show/hide, movement, resizing, message rendering, gesture behavior, styling, z-index, and state cleanup are all owned directly by `Main.gd`.

What currently works:

- Voice commands can open and close the chat panel through backend WebSocket events.
- The chat panel can receive appended user/assistant transcript messages.
- Mouse drag works through a chatlog header.
- Mouse resize works through a bottom resize handle labeled `///`.
- UI control mode can show a glowing gesture cursor.
- Webcam gesture events can move the cursor, pinch-grab the chat panel, one-hand drag it, and two-hand resize it.
- Current gesture resize uses primary hand as the cursor/grab hand and secondary hand as a modifier.

What is chatlog-specific:

- Window creation, chrome, title, close button, message list, drag handle, resize handle, sizing constants, clamp logic, style, gesture hit-testing, and gesture state all directly reference `_chatlog_panel` or chatlog constants in `Main.gd`.
- Backend panel events use `panelId = "chatlog"` and the frontend only knows that panel id.

What is already somewhat generic:

- WebSocket visual events have a generic event envelope with `event` and optional payload fields.
- Backend intents are already mapped to UI events in one location, `WebSocketHandler.SendUiPanelEventIfNeededAsync()`.
- Gesture events use generic event names (`GESTURE_POINTER_MOVE`, `GESTURE_PINCH_START`, etc.) and `pointerId`, although the frontend target logic is chatlog-specific.
- The gesture cursor is separate from the chatlog as a `CanvasLayer`, which is a good future layering anchor.

Risky refactor areas:

- `Main.gd` mixes root app state, chat, voice UI, floating chatlog, gesture input, WebSocket event routing, and visual styling.
- Mouse and gesture movement/resize are separate code paths with different assumptions.
- Gesture targeting is hardcoded to `_chatlog_panel`.
- Z-order is manually incremented with `_chatlog_z_index`; there is no central z-order manager.
- The chatlog content and window chrome are mixed in the same creation function.

Main coupling:

- `Main.gd` is the coupling hub.
- Backend `CommandRouter` and `WebSocketHandler` are coupled to chatlog through intent names and `panelId = "chatlog"`.
- Gesture routing is generic until it reaches `Main.gd`; then it becomes chatlog-only.

## 2. Frontend Scene / Node Structure

File: `Merlin.Frontend/Main.tscn`

Class/Scene: root `Control` node named `Main`

Responsibility:

- Main Godot UI scene.
- Hosts status UI, orb, notification panel, legacy chat panel, command input, voice controls, overlay container, WebSocket client, HTTP requests, audio players.

Relevant nodes:

- `Main`: root `Control`, script `res://Scripts/Main.gd`.
- `Background`: full-screen `ColorRect`.
- `MerlinWebSocketClient`: `Node`, script `res://Scripts/MerlinWebSocketClient.gd`.
- `VoiceTranscribeRequest`, `VoiceSynthesisRequest`: `HTTPRequest`.
- `VoicePlayback`, `MicrophoneInput`: `AudioStreamPlayer`.
- `StatusPanel`: top status panel with `ConnectionStateLabel`, `ReconnectButton`, `ShowDebugCheckBox`.
- `CoreOrb`: instance of `res://Scenes/CoreOrb3D.tscn`.
- `ActivityPanel`: bottom status text.
- `NotificationPanel`: right-side notifications.
- `ChatPanel`: existing bottom chat panel, hidden in voice mode.
- `CommandInput`: hidden command input panel.
- `VoiceControl`: voice button container.
- `OverlayContainer`: full-screen overlay host with `ErrorLabel`.

Notes:

- The floating chatlog/chatwindow is not present in `Main.tscn`. It is created at runtime in `Main.gd`.
- The gesture cursor is also not present in `Main.tscn`. It is created at runtime in a `CanvasLayer`.

File: `Merlin.Frontend/Scripts/Main.gd`

Class/Scene: script on `Main`

Responsibility:

- Root UI controller.
- Owns WebSocket wiring, voice mode setup, notifications, orb state, chatlog floating panel, gesture cursor, gesture-to-chatlog interactions, and backend visual event handling.

Relevant onready nodes:

- `web_socket_client`
- `voice_transcribe_request`
- `voice_synthesis_request`
- `voice_playback`
- `microphone_input`
- `background`
- `core_orb`
- `status_panel`
- `activity_panel`
- `notification_panel`
- `overlay_container`
- `chat_panel`
- `history_panel`
- `message_scroll`
- `message_list`
- `thinking_label`
- `command_input_panel`
- `message_input`
- `send_button`
- `voice_control`
- `voice_button`

File: `Merlin.Frontend/Scripts/MerlinWebSocketClient.gd`

Class/Scene: `class_name MerlinWebSocketClient`

Responsibility:

- Maintains WebSocket connection to backend.
- Parses small visual/control packets on the main thread.
- Parses larger payloads in a worker thread.
- Emits typed Godot signals for `visual_event_received`, `assistant_ui_state_received`, `response_received`, `voice_transcript_received`, and connection state.

File: `Merlin.Frontend/Scenes/CoreOrb3D.tscn`

Responsibility:

- Visual orb scene used by `Main.tscn`.
- Not directly part of floating window mechanics, but it is part of layering and root UI composition.

File: `Merlin.Frontend/Scripts/BargeInDebugOverlay.gd`

Responsibility:

- Optional debug overlay script instantiated into `OverlayContainer`.
- Has its own panel style helper.

## 3. Chatlog / Chatwindow Current Implementation

Location:

- Runtime-created in `Merlin.Frontend/Scripts/Main.gd`.
- Creation function: `_setup_chatlog_panel()` at `Main.gd:354`.

How it is created:

- `_ready()` calls `_setup_chatlog_panel()`.
- `_setup_chatlog_panel()` creates a `PanelContainer` named `ChatFloatingPanel`.
- It sets:
  - `visible = false`
  - `mouse_filter = Control.MOUSE_FILTER_STOP`
  - `custom_minimum_size = Vector2(CHATLOG_MIN_WIDTH, CHATLOG_MIN_HEIGHT)`
  - `position = Vector2(CHATLOG_INITIAL_LEFT, CHATLOG_INITIAL_TOP)`
  - `size = Vector2(CHATLOG_DEFAULT_WIDTH, CHATLOG_DEFAULT_HEIGHT)`
  - `z_index = _chatlog_z_index`
  - style from `_chatlog_panel_style()`
- It adds the panel directly as a child of root `Main`.

Runtime child structure:

- `ChatFloatingPanel` (`PanelContainer`)
  - `MarginContainer`
    - `VBoxContainer`
      - `DragHeader` (`HBoxContainer`)
        - title margin + `Label` text `Chat`
        - close `Button` text `X`
      - body margin
        - `_chatlog_message_scroll` (`ScrollContainer`)
          - `_chatlog_message_list` (`VBoxContainer`)
      - `ResizeHandle` (`Control`)
        - `Label` text `///`

How it is shown/hidden:

- `_show_chatlog_panel()` at `Main.gd:3376`
  - Creates panel if missing.
  - Increments `_chatlog_z_index`.
  - Assigns panel `z_index`.
  - Calls `_clamp_chatlog_panel_to_viewport()`.
  - Sets `visible = true`.
  - Calls `grab_focus()`.
- `_hide_chatlog_panel()` at `Main.gd:3386`
  - Sets `visible = false`.
  - Clears mouse drag/resize flags.
  - Calls `_release_all_gesture_grabs()`.
  - Clears gesture hover state.

How voice command events reach it:

- Backend `ChatLogCommandMatcher` maps deterministic commands like `show chat`, `open chat`, `hide chat`, and `close chat` to intents `ui_panel_show` / `ui_panel_hide`.
- `CommandRouter` detects the chat command before general intent parsing.
- `WebSocketHandler.SendUiPanelEventIfNeededAsync()` maps `ui_panel_show` to `UI_PANEL_SHOW` and `ui_panel_hide` to `UI_PANEL_HIDE`, with `panelId = "chatlog"`.
- `MerlinWebSocketClient.gd` emits `visual_event_received` for packets with `event`.
- `Main.gd._on_visual_event_received()` calls `_handle_chatlog_visual_event()`.
- `_handle_chatlog_visual_event()` checks `panelId == "chatlog"` and calls `_show_chatlog_panel()` or `_hide_chatlog_panel()`.

How messages are appended:

- `_handle_chatlog_visual_event()` handles `UI_CHATLOG_APPEND`.
- It calls `_append_chatlog_message(role, text, source, timestampUtc)`.
- `_append_chatlog_message()` stores messages in `_chatlog_messages` and creates visual entries with `_create_chatlog_entry()`.

How scroll/autoscroll works:

- `_chatlog_should_auto_scroll()` checks the vertical scrollbar:
  - `bar.max_value - _chatlog_message_scroll.scroll_vertical <= CHATLOG_BOTTOM_SCROLL_THRESHOLD`
- If the user is near the bottom before appending, `_append_chatlog_message()` defers `_scroll_chatlog_to_bottom()`.
- `_scroll_chatlog_to_bottom()` sets `scroll_vertical` to the scrollbar max.

Mouse drag:

- Header node `DragHeader` connects `gui_input` to `_on_chatlog_header_gui_input()`.
- Left mouse press:
  - increments `_chatlog_z_index`
  - sets panel `z_index`
  - sets `_chatlog_dragging = true`
  - records `_chatlog_drag_offset`
  - accepts event
- Mouse motion while dragging:
  - panel position becomes global mouse position minus offset
  - clamps to viewport

Mouse resize:

- `ResizeHandle` connects `gui_input` to `_on_chatlog_resize_gui_input()`.
- Left mouse press:
  - increments z-index
  - sets `_chatlog_resizing = true`
  - records `_chatlog_resize_start_mouse`
  - records `_chatlog_resize_start_size`
- Mouse motion while resizing:
  - `target_size = _chatlog_resize_start_size + delta`
  - clamps width/height to constants
  - clamps panel position to viewport

Minimum/maximum size:

- Constants in `Main.gd`:
  - `CHATLOG_MIN_WIDTH`
  - `CHATLOG_MIN_HEIGHT`
  - `CHATLOG_MAX_WIDTH`
  - `CHATLOG_MAX_HEIGHT`
- Mouse resize and gesture resize both use these constants.

Initial position/size:

- Constants in `Main.gd`:
  - `CHATLOG_DEFAULT_WIDTH`
  - `CHATLOG_DEFAULT_HEIGHT`
  - `CHATLOG_INITIAL_LEFT`
  - `CHATLOG_INITIAL_TOP`

Persistence:

- No position/size persistence was found.
- Hide/show preserves the node and current size/position during the app session because the panel is not destroyed, but no disk or backend persistence is implemented.

Generic interface:

- None. The chatlog does not expose a reusable surface/window interface.
- All behavior is direct function and variable access in `Main.gd`.

Chatlog-specific code likely to become generic later:

- `_show_chatlog_panel()`
- `_hide_chatlog_panel()`
- `_on_chatlog_header_gui_input()`
- `_on_chatlog_resize_gui_input()`
- `_clamp_chatlog_panel_to_viewport()`
- `_chatlog_panel_style()`
- `_apply_chatlog_gesture_state()`
- `_is_gesture_over_chatlog_panel()`
- `_try_start_gesture_resize()`
- `_update_gesture_resize()`

## 4. Current Mouse Drag/Resize Architecture

Mouse drag is implemented only for the floating chatlog.

Script:

- `Merlin.Frontend/Scripts/Main.gd`

Functions:

- `_on_chatlog_header_gui_input()`
- `_on_chatlog_resize_gui_input()`
- `_clamp_chatlog_panel_to_viewport()`

Input events:

- `InputEventMouseButton` with `button_index == MOUSE_BUTTON_LEFT`
- `InputEventMouseMotion`

Drag region:

- Only the runtime-created `DragHeader` receives drag input.

Resize region:

- Only the runtime-created bottom `ResizeHandle` receives resize input.
- There is no edge resize, corner resize beyond this handle, or generic hit region.

Clamping:

- Size is clamped in `_on_chatlog_resize_gui_input()`.
- Position is clamped in `_clamp_chatlog_panel_to_viewport()`.
- The clamp allows panel position up to viewport minus fixed values `80.0` and `60.0`, not viewport minus panel size.

Z-order/focus:

- Drag press increments `_chatlog_z_index` and assigns it to `_chatlog_panel.z_index`.
- Resize press does the same.
- Show also increments z-index and calls `grab_focus()`.

Reusability:

- Not reusable today. It is tied to `_chatlog_panel`, `_chatlog_dragging`, `_chatlog_resizing`, and chatlog constants.

Likely future extraction:

- Window drag controller.
- Window resize controller.
- Shared clamp/snap policy.
- Z-order manager.
- Window chrome with drag region and resize handles.

## 5. Current Gesture Cursor Architecture

Location:

- `Main.gd`, functions around `_setup_gesture_cursor()` at `Main.gd:2950`.

Creation/rendering:

- `_setup_gesture_cursor()` creates a `CanvasLayer` named `GestureCursorLayer`.
- Layer constant: `GESTURE_CURSOR_CANVAS_LAYER = 128`.
- `_ensure_gesture_cursor()` creates one `PanelContainer` named `GestureCursor`.
- The cursor is styled by `_gesture_cursor_style(pinched)`.
- `mouse_filter = Control.MOUSE_FILTER_IGNORE`.
- `z_index = 4096`, `z_as_relative = false`.

Show/hide:

- `_set_ui_control_mode_active(true)` shows it and seeds primary position.
- `_set_ui_control_mode_active(false)` hides it, clears gesture states, and resets style.

Coordinate mapping:

- `_gesture_event_position(event)` maps normalized event `x`, `y` from backend to viewport coordinates:
  - `Vector2(x * viewport_size.x, y * viewport_size.y)`

Pointer IDs:

- `GESTURE_POINTER_PRIMARY = "primary"`.
- Backend PR4.1 now emits `primary` and `secondary`.
- Frontend treats only `primary` as visible cursor owner.
- `secondary` is stored for resize modifier state.

Event names handled:

- `GESTURE_POINTER_MOVE`
- `GESTURE_PINCH_MOVE`
- `GESTURE_PINCH_START`
- `GESTURE_PINCH_END`

Movement:

- `_handle_gesture_visual_event()` calls `_gesture_pointer_move(pointer_id, position)`.
- `_gesture_pointer_move()` stores all pointer positions.
- If pointer is `primary`, it updates the visible cursor position.
- If resize is active, movement updates resize.
- If `primary` has a grab, it moves the grabbed chatlog.
- Secondary movement does not update hover or drag outside resize.

Pinch:

- `_gesture_pinch_start()` stores `_gesture_pointer_pinched[pointer_id] = true`.
- For `primary`, if the cursor is inside the chatlog, it creates a chatlog grab.
- For `secondary`, pinch alone does not grab UI.
- `_try_start_gesture_resize()` can start resize only when primary is already grabbing chat and secondary is pinched.
- `_gesture_pinch_end()` clears pinch, releases resize if needed, clears grabs, and updates style for primary.

Grab state:

- `_gesture_grabs` dictionary keyed by pointer id.
- Current meaningful grab key is `primary`.
- Grab value contains:
  - `surface_id`
  - `start_position`
  - `panel_start_position`

Resize state:

- `_gesture_resize_state` dictionary.
- Contains:
  - `surface_id`
  - `pointer_a`
  - `pointer_b`
  - `start_a`
  - `start_b`
  - `start_delta`
  - `start_distance`
  - `start_midpoint`
  - `start_rect`
  - `resize_axes`

Synthetic/debug input:

- `_handle_synthetic_gesture_input()` uses Alt+mouse.
- Alt+mouse movement maps to primary pointer movement.
- Alt+left mouse button press/release maps to primary pinch start/end.

## 6. Current Gesture-to-UI Targeting

Targeting is hardcoded to chatlog.

Hit testing:

- `_surface_at_gesture_position(position)` returns `"chatlog"` if `_is_gesture_over_chatlog_panel(position)` returns true.
- `_is_gesture_over_chatlog_panel(position)` checks `Rect2(_chatlog_panel.global_position, _chatlog_panel.size).has_point(position)`.

No registry:

- There is no surface registry.
- There is no list of grabbable/resizable windows.
- There are no declared surface capabilities.
- There is no z-aware hit testing across multiple windows.

Grab target storage:

- `_gesture_grabs[pointer_id] = { "surface_id": "chatlog", ... }`
- The code currently expects chatlog and ignores any other surface id.

Secondary resize target:

- Current PR4.1/PR4.2 rule:
  - primary must already have a chatlog grab
  - secondary must be pinched
  - secondary position must exist
  - resize starts from primary and secondary positions
- Secondary no longer needs its own hover/grab target for normal movement.

Release/drop:

- Secondary release during resize exits resize and attempts to continue primary drag if primary is still pinched.
- Primary release during resize drops/commits by erasing primary grab and resetting cursor style.
- `_release_all_gesture_grabs()` clears grabs and resize state.

UI control stop:

- `_set_ui_control_mode_active(false)` calls `_release_all_gesture_grabs()`, clears hover/pinch dictionaries, hides cursor.

Answer to important question:

- Gesture targeting is not generic. It is chatlog-hardcoded with a thin string label `"chatlog"`.

## 7. Current Two-Hand Resize Architecture

Location:

- `Main.gd`
- Start detection: `_try_start_gesture_resize()`
- Update math: `_update_gesture_resize()`
- Release handling: `_finish_gesture_resize()`

Resize start:

- Requires:
  - UI control mode active indirectly because gesture handlers early-return if inactive.
  - `_chatlog_panel` valid.
  - primary has a grab.
  - primary is pinched.
  - primary grab `surface_id == "chatlog"`.
  - primary and secondary pointer positions exist.
  - secondary is pinched.
  - start distance >= `GESTURE_MIN_RESIZE_START_DISTANCE_PX`.
  - start delta maps to a valid axis mode.

State captured:

- `pointer_a`: primary.
- `pointer_b`: secondary.
- `start_a`
- `start_b`
- `start_delta`
- `start_distance`
- `start_midpoint`
- `start_rect`
- `resize_axes`

Resize is currently axis-aware:

- `GESTURE_RESIZE_AXIS_WIDTH`
- `GESTURE_RESIZE_AXIS_HEIGHT`
- `GESTURE_RESIZE_AXIS_BOTH`
- `_gesture_resize_axes_for_delta()` locks the mode at resize start.
- Horizontal separation controls width.
- Vertical separation controls height.
- Diagonal enough controls both independently.

Midpoint movement:

- `_update_gesture_resize()` computes current midpoint.
- Target center is `start_center + (current_midpoint - start_midpoint)`.
- Target position is center minus half target size.

Smoothing:

- Size and position are smoothed using `lerp(..., GESTURE_RESIZE_SMOOTHING)`.

Clamping:

- Target size is clamped with chatlog min/max constants.
- Position is then clamped to viewport.

Primary/secondary roles:

- `primary`: visible cursor and normal grab/move hand.
- `secondary`: resize modifier only.

Release:

- Secondary releases:
  - exits resize.
  - resets cursor size.
  - continues primary drag if primary is still pinched and panel is valid.
- Primary releases:
  - exits resize.
  - drops primary grab.
  - resets cursor pinched style.

Future universal compatibility:

- The resize math can become generic if the target provides:
  - current rect
  - min/max size
  - clamp policy
  - resize capabilities
  - target id
- Current implementation still directly reads and writes `_chatlog_panel`.

## 8. Current Z-Order / Focus / Layering

Chatlog z-order:

- `_chatlog_z_index` starts at `200`.
- `_show_chatlog_panel()`, drag press, and resize press increment `_chatlog_z_index` and assign it to `_chatlog_panel.z_index`.

Central manager:

- None.
- Z-order is hardcoded for chatlog only.

Node order:

- Runtime-created chatlog is added as child of root `Main`.
- Gesture cursor is added under `GestureCursorLayer`, not under the same normal root ordering.

Gesture cursor layering:

- `GestureCursorLayer` uses `layer = 128`.
- Cursor itself uses `z_index = 4096` and `z_as_relative = false`.
- This is why it can appear above the floating chatlog.

Overlays:

- `OverlayContainer` is present in `Main.tscn`.
- `BargeInDebugOverlay` is added to `OverlayContainer`.
- Error label lives inside `OverlayContainer`.

Future trashcan/drop zone:

- Likely should be a separate overlay layer or a managed window/drop-target layer.
- Current architecture has no reserved layer scheme for cursor, windows, overlays, drop zones, debug overlays, and modal surfaces.

Z-order status:

- Not centralized.
- Partly implicit through node order.
- Partly hardcoded per element.
- Gesture cursor layering is separate and manually high.

## 9. Current WebSocket / UI Event Flow

Frontend receiver:

- `MerlinWebSocketClient.gd` parses WebSocket packets.
- Packets with `"event"` are emitted via `visual_event_received`.
- `Main.gd._on_visual_event_received()` routes UI events.

Relevant backend-to-frontend UI events:

Event: `UI_PANEL_SHOW`

- Backend source: `WebSocketHandler.SendUiPanelEventIfNeededAsync()`.
- Trigger: `AssistantResponse.Intent == "ui_panel_show"`.
- Payload includes `panelId = "chatlog"`.
- Frontend handler: `_handle_chatlog_visual_event()`.
- Effect: `_show_chatlog_panel()`.
- Genericity: event name generic-ish, payload currently chatlog-only.

Event: `UI_PANEL_HIDE`

- Backend source: `WebSocketHandler.SendUiPanelEventIfNeededAsync()`.
- Trigger: `AssistantResponse.Intent == "ui_panel_hide"`.
- Payload includes `panelId = "chatlog"`.
- Frontend handler: `_handle_chatlog_visual_event()`.
- Effect: `_hide_chatlog_panel()`.
- Genericity: event name generic-ish, payload currently chatlog-only.

Event: `UI_CHATLOG_APPEND`

- Backend source: `WebSocketHandler` sends chatlog append events for voice transcript / chat history sync paths.
- Payload includes `panelId = "chatlog"`, `role`, `text`, `source`, `timestampUtc`.
- Frontend handler: `_handle_chatlog_visual_event()`.
- Effect: `_append_chatlog_message()`.
- Genericity: chatlog-specific.

Event: `UI_CHATLOG_CLEAR`

- Frontend handler exists in `_handle_chatlog_visual_event()`.
- Effect: clears `_chatlog_messages` and frees current message list children.
- Genericity: chatlog-specific.

Event: `UI_CONTROL_MODE_STARTED`

- Backend source: `WebSocketHandler.SendUiPanelEventIfNeededAsync()`.
- Trigger: `AssistantResponse.Intent == "ui_control_mode_start"`.
- Frontend handler: `_handle_ui_control_visual_event()`.
- Effect: `_set_ui_control_mode_active(true)`.
- Genericity: mode-level event, not window-specific.

Event: `UI_CONTROL_MODE_STOPPED`

- Backend source: `WebSocketHandler.SendUiPanelEventIfNeededAsync()`.
- Trigger: `AssistantResponse.Intent == "ui_control_mode_stop"`.
- Frontend handler: `_handle_ui_control_visual_event()`.
- Effect: `_set_ui_control_mode_active(false)`.
- Genericity: mode-level event.

Event: `GESTURE_POINTER_MOVE`

- Backend source: `WebSocketHandler.SendVisionGestureEventAsync()`.
- Trigger: `VisionGestureEvent.Type == "gesture.pointer.move"`.
- Payload includes `pointerId`, `x`, `y`, `confidence`, `source`.
- Frontend handler: `_handle_gesture_visual_event()`.
- Effect: `_gesture_pointer_move()`.
- Genericity: generic pointer event, chatlog-specific targeting later.

Event: `GESTURE_PINCH_START`

- Backend source: `SendVisionGestureEventAsync()`.
- Trigger: `gesture.pinch.start`.
- Frontend handler: `_gesture_pinch_start()`.
- Effect: primary may grab chatlog; secondary may help start resize.
- Genericity: generic input event, chatlog-specific action.

Event: `GESTURE_PINCH_MOVE`

- Backend source: `SendVisionGestureEventAsync()`.
- Trigger: `gesture.pinch.move`.
- Frontend handler: treated like pointer move.
- Effect: updates pointer and drag/resize if relevant.

Event: `GESTURE_PINCH_END`

- Backend source: `SendVisionGestureEventAsync()`.
- Trigger: `gesture.pinch.end`.
- Frontend handler: `_gesture_pinch_end()`.
- Effect: releases pinch/grab/resize state.

Assistant UI state:

- Packets with `type = "assistant_ui_state"` route to `assistant_ui_state_received`.
- Used for orb/activity state, not central to floating window mechanics.

## 10. Current Backend Command Routing for UI Events

Chatlog open/close:

- `Merlin.Backend/Services/ChatLogCommandMatcher.cs`
- `CommandRouter.RouteAsync()` checks `ChatLogCommandMatcher.TryMatch()` before general intent parsing.
- Returns an `AssistantResponse` with intent:
  - `ui_panel_show`
  - `ui_panel_hide`
- `WebSocketHandler` converts that to frontend events.

UI control mode:

- `Merlin.Backend/Services/UiControlModeController.cs`
- `UiControlModeCommandMatcher` detects phrases like:
  - `let me control the ui`
  - `start gesture mode`
  - `open your eyes`
  - `close your eyes`
- `CommandRouter` starts/stops `UiControlModeController`.
- Start also calls `IVisionSidecarHost.StartTrackingAsync()`.
- Stop calls `IVisionSidecarHost.StopTrackingAsync()` before stopping mode.

Gesture forwarding:

- `VisionSidecarHost` parses Python sidecar stdout messages.
- Gesture messages become `VisionGestureEvent`.
- `VisionGestureEventRouter` forwards only when UI control mode is active.
- `WebSocketHandler` serializes forwarded gestures to frontend visual events.

Future trashcan commands:

- Likely would fit as either:
  - a new deterministic command matcher producing a new UI intent, or
  - a generic UI surface event/capability routed through a future `WindowManager` / `SurfaceRegistry`.
- Current backend UI events are not rich enough to describe arbitrary window targets or dismiss/drop zones.

## 11. Existing Generic Concepts

Existing reusable-ish pieces:

- WebSocket visual event envelope with `event`.
- `UI_PANEL_SHOW` and `UI_PANEL_HIDE` are named generically, but currently only emit `panelId = "chatlog"`.
- Gesture protocol events are generic pointer/pinch events.
- `VisionGestureEventRouter` gates gesture forwarding by UI control mode and does not know about chatlog.
- `GestureCursorLayer` is a separate runtime layer and could become a generic cursor/input overlay.
- Styling helpers like `_panel_style()` and `_style_button()` are shared within `Main.gd`.

Missing generic concepts:

- No `MerlinWindow`.
- No `MerlinWindowManager`.
- No `SurfaceRegistry`.
- No reusable draggable component.
- No reusable resize component.
- No capability declaration.
- No generic hit-test registry.
- No generic dismiss/drop target.
- No persistent window placement model.

## 12. Current Coupling / Pain Points

Main coupling problems:

- `Main.gd` is too broad. It owns root app behavior and floating window behavior.
- Chatlog content and window chrome are mixed in `_setup_chatlog_panel()`.
- Chatlog drag/resize state lives as global variables on `Main.gd`.
- Mouse drag/resize is chatlog-only and not reusable.
- Gesture targeting directly references `_chatlog_panel`.
- Gesture grab/resize state uses `"chatlog"` strings but no registry.
- Backend event `panelId` is hardcoded to `"chatlog"` in `WebSocketHandler`.
- Show/hide is generic in name but chatlog-specific in practice.
- Z-order is manual and single-window-oriented.
- Mouse resize and gesture resize share constants but not a shared resize controller.
- UI control mode state and gesture cursor state live in the same script as chatlog content.
- No layout persistence; a universal window system would need to decide whether to preserve current behavior or add persistence deliberately.

Specific files:

- `Merlin.Frontend/Scripts/Main.gd`: primary coupling hotspot.
- `Merlin.Backend/WebSocket/WebSocketHandler.cs`: maps UI intents to events and hardcodes `panelId = "chatlog"`.
- `Merlin.Backend/Services/ChatLogCommandMatcher.cs`: command-level chat panel coupling.
- `Merlin.Backend/Services/CommandRouter.cs`: deterministic UI command handling and vision lifecycle coupling.

## 13. Refactor Constraints

Preserve these behaviors:

- Chatlog opens by voice.
- Chatlog closes by voice.
- Chatlog appends transcript/chat messages.
- Chatlog clear still works if used.
- Mouse drag via header continues to work.
- Mouse resize via bottom handle continues to work.
- Gesture cursor appears only in UI control mode.
- Primary hand still controls cursor.
- Primary pinch still grabs/moves chat.
- Secondary pinch still acts as resize modifier.
- Two-hand axis-aware resize keeps working.
- UI control mode start/stop keeps camera lifecycle working.
- Synthetic Alt+mouse gesture input keeps working.
- Backend WebSocket event compatibility remains unless intentionally migrated.
- Existing `ChatPanel` bottom UI should not be broken, even if it is currently hidden in voice mode.
- Orb, activity, notification, and overlay state should not regress.

## 14. Recommended Refactor Strategy

PR UI-1: Introduce a no-op window data model

- Goal: Add names/types for future windows without moving chatlog behavior.
- Likely files:
  - new frontend script or resource for window definitions
  - maybe `Main.gd` minimal registry dictionary
- Risk level: low.
- Acceptance criteria:
  - No behavior changes.
  - Existing chatlog still created exactly as today.
  - A `chatlog` window definition can be queried for id, title, min/max/default size, and capabilities.

PR UI-2: Extract chatlog content builder from window chrome

- Goal: Separate chat message content creation from floating panel shell.
- Likely files:
  - `Main.gd`
  - new `ChatLogContent.gd` or scene
- Risk level: medium.
- Acceptance criteria:
  - Message append/autoscroll unchanged.
  - Floating panel visual unchanged.
  - Chatlog content can be mounted inside another parent.

PR UI-3: Introduce `MerlinWindow` shell for chatlog only

- Goal: Create reusable shell owning header, close button, resize handle, base style.
- Likely files:
  - new `MerlinWindow.gd`
  - possibly new `MerlinWindow.tscn`
  - `Main.gd`
- Risk level: medium-high.
- Acceptance criteria:
  - Mouse drag/resize unchanged.
  - Chatlog layout visually equivalent.
  - No gesture regression.

PR UI-4: Move z-order and focus into `MerlinWindowManager`

- Goal: Replace `_chatlog_z_index` with a manager.
- Likely files:
  - `Main.gd`
  - new `MerlinWindowManager.gd`
- Risk level: medium.
- Acceptance criteria:
  - Show/drag/resize brings window forward.
  - Cursor still stays above all windows.
  - Future windows can request focus/z.

PR UI-5: Move gesture hit-testing to a `SurfaceRegistry`

- Goal: Replace direct `_is_gesture_over_chatlog_panel()` with registry lookup.
- Likely files:
  - `Main.gd`
  - new `GestureSurfaceRegistry.gd`
- Risk level: high.
- Acceptance criteria:
  - Primary hover/grab still targets chatlog.
  - Secondary resize modifier still works.
  - Registry returns topmost eligible surface.
  - No behavior change for one-window case.

PR UI-6: Unify mouse and gesture movement/resize through window controller methods

- Goal: Both input paths call shared window move/resize APIs.
- Likely files:
  - `MerlinWindow.gd`
  - `Main.gd`
- Risk level: high.
- Acceptance criteria:
  - Mouse drag/resize and gesture drag/resize use same clamp/min/max policy.
  - Axis-aware gesture resize remains intact.
  - Mouse resize still behaves as current bottom-right resize.

PR UI-7: Add generic show/hide/dismiss events

- Goal: Evolve `UI_PANEL_SHOW/HIDE` into a more general surface/window event contract.
- Likely files:
  - `WebSocketHandler.cs`
  - frontend WebSocket event handlers
  - backend UI command result models if introduced
- Risk level: medium.
- Acceptance criteria:
  - Old chatlog events still work during transition.
  - New event shape can address arbitrary window id.

PR UI-8: Build dismiss/drop-zone on top of registry

- Goal: Add trashcan/dismiss behavior only after windows and gestures are generic.
- Risk level: medium.
- Acceptance criteria:
  - Dragging eligible window/card to drop zone dismisses it.
  - Chatlog behavior follows decided hide/destroy policy.

## 15. Proposed Future Universal Window Concepts

`MerlinWindow`

- Godot Control/PanelContainer shell.
- Owns chrome, header, close button, resize handles, focus visuals, and base style.
- Exposes methods:
  - `show_window()`
  - `hide_window()`
  - `set_window_rect(rect)`
  - `move_by(delta)`
  - `resize_from_gesture(snapshot, current)`
  - `set_gesture_state(hovered, grabbed, resizing)`

`MerlinWindowContent`

- Interface-like convention for content nodes.
- Chatlog messages, video controls, file browser, etc.
- Does not own drag/resize/z-order.

`MerlinWindowManager`

- Owns window registry, z-order, focus, show/hide, topmost hit testing, persistence hooks.

`MerlinWindowDefinition`

- Data object:
  - id
  - title
  - default rect
  - min/max size
  - capabilities
  - persistence key

`MerlinWindowCapabilities`

- Booleans/flags:
  - movable
  - resizable
  - dismissible
  - closable
  - acceptsGestureGrab
  - acceptsGestureResize
  - preserveAspectRatio

`GestureWindowController`

- Owns primary/secondary pointer state, grab sessions, resize sessions.
- Talks to `MerlinWindowManager` and surface registry.
- Should not know chatlog internals.

`GestureSurfaceRegistry`

- Maps screen points to surfaces.
- Returns topmost eligible target based on capabilities.

`WindowZOrderManager`

- Could be part of `MerlinWindowManager`.
- Owns z allocation and layer groups.

`DismissDropZone`

- Overlay/drop target registered in surface registry.
- Has capabilities for accepting dragged/dismissible items.

## 16. Open Questions

- Should all future windows be Godot `Control` nodes in the same app window, or can some become separate OS windows later?
- Should chatlog hide, destroy, or minimize on close/dismiss?
- Should chatlog position/size persist across app restarts?
- Should persistence be per window id, per content type, or per conversation?
- Should z-order support layer groups now, such as windows, modals, overlays, cursor, debug, drop zones?
- Should tool result cards use the full `MerlinWindow` shell or a lighter card shell?
- Should media windows preserve aspect ratio by default?
- Should gesture resize support edge/corner semantics later, or remain two-hand axis-aware?
- Should UI control mode auto-start for drag/drop actions like trashcan, or must the user explicitly enter control mode?
- Should backend UI events become generic now, or should frontend maintain backwards compatibility around `UI_PANEL_SHOW/HIDE` first?
- Should close button commands emit backend state changes or remain frontend-only for some windows?
- Should focus changes be visible/audible?
- Should the gesture cursor remain global, or should windows provide cursor affordance hints?

## 17. File Map Appendix

Path: `Merlin.Frontend/Main.tscn`

- Why it matters: root scene and static UI node map.
- Main nodes: `Main`, `Background`, `MerlinWebSocketClient`, `CoreOrb`, `StatusPanel`, `NotificationPanel`, `ChatPanel`, `CommandInput`, `VoiceControl`, `OverlayContainer`.
- Future role: remains app root; should host a future window manager node/layer.

Path: `Merlin.Frontend/Scripts/Main.gd`

- Why it matters: current frontend UI hub and main coupling hotspot.
- Main functions:
  - `_setup_chatlog_panel()`
  - `_handle_chatlog_visual_event()`
  - `_on_visual_event_received()`
  - `_setup_gesture_cursor()`
  - `_handle_gesture_visual_event()`
  - `_set_ui_control_mode_active()`
  - `_handle_synthetic_gesture_input()`
  - `_gesture_pointer_move()`
  - `_gesture_pinch_start()`
  - `_try_start_gesture_resize()`
  - `_update_gesture_resize()`
  - `_show_chatlog_panel()`
  - `_hide_chatlog_panel()`
  - `_on_chatlog_header_gui_input()`
  - `_on_chatlog_resize_gui_input()`
  - `_append_chatlog_message()`
- Future role: should shrink into app coordinator after window/content/gesture controllers are extracted.

Path: `Merlin.Frontend/Scripts/MerlinWebSocketClient.gd`

- Why it matters: WebSocket event ingestion and signal boundary.
- Main signals:
  - `visual_event_received`
  - `assistant_ui_state_received`
  - `response_received`
  - `voice_transcript_received`
- Future role: likely remains transport layer; should not know windows.

Path: `Merlin.Frontend/Scenes/CoreOrb3D.tscn`

- Why it matters: major root visual element layered behind/among UI.
- Future role: likely separate from window system but relevant to layer planning.

Path: `Merlin.Frontend/Scripts/BargeInDebugOverlay.gd`

- Why it matters: existing overlay-style UI outside chatlog.
- Future role: candidate for a debug window or overlay layer migration.

Path: `Merlin.Backend/WebSocket/WebSocketHandler.cs`

- Why it matters: maps backend responses and vision gestures to frontend visual events.
- Main relevant methods:
  - `SendUiPanelEventIfNeededAsync()`
  - `SendVisionGestureEventAsync()`
- Future role: event contract migration point for generic windows.

Path: `Merlin.Backend/Services/CommandRouter.cs`

- Why it matters: routes deterministic UI commands before general intent parsing.
- Main relevant behavior:
  - chatlog command response
  - UI control mode start/stop
  - vision sidecar start/stop
- Future role: command boundary for new UI capabilities.

Path: `Merlin.Backend/Services/ChatLogCommandMatcher.cs`

- Why it matters: deterministic chat panel voice command matcher.
- Future role: may become one of several panel/window command matchers, or feed a generic UI command layer.

Path: `Merlin.Backend/Services/UiControlModeController.cs`

- Why it matters: owns UI control mode active/inactive state and deterministic command matcher.
- Future role: remains safety boundary for gesture manipulation.

Path: `Merlin.Backend/Services/Vision/VisionGestureEventRouter.cs`

- Why it matters: gates gesture forwarding by UI control mode.
- Future role: should remain backend safety authority; not a UI layout interpreter.

Path: `Merlin.Backend/Services/Vision/VisionGestureEvent.cs`

- Why it matters: DTO for pointer/pinch gesture events.
- Future role: could add optional fields but should stay UI-layout-agnostic.

Path: `Merlin.Backend/VisionScripts/vision_worker.py`

- Why it matters: emits `primary` and `secondary` pointer/pinch events.
- Future role: should remain vision/gesture signal producer, not window-aware.
