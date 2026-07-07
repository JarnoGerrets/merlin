# Merlin Frontend Universal Window Refactor Plan

## Purpose

This document defines a staged refactor plan for Merlin's frontend UI so future visual elements can reuse one universal window/surface framework.

The goal is not to make every UI element look identical.

The goal is to make every major UI element depend on the same reusable foundation for:

```text
show / hide
move
resize
focus
z-order
gesture targeting
dismiss
animation hooks
window capabilities
surface registration
```

Different UI elements can still have completely different content and presentation:

```text
chatlog
debug panel
memory viewer
tool result cards
web result panels
video player
music player
file browser
image viewer
settings panel
temporary confirmation cards
trashcan / dismiss drop zone
```

But their bare framework should be shared.

---

## Source Context

This plan is based on the current architecture report:

```text
Merlin.ToDo/frontend_ui/merlin_frontend_ui_current_architecture_report.md
```

The report found that the current frontend UI is concentrated in:

```text
Merlin.Frontend/Main.tscn
Merlin.Frontend/Scripts/Main.gd
```

The floating chatlog is currently created programmatically in `Main.gd` through `_setup_chatlog_panel()` and owns its own:

```text
window chrome
title/header
close button
message list
drag behavior
resize behavior
z-index
gesture hit-testing
gesture grab state
gesture resize state
styling
show/hide behavior
```

The current gesture and WebSocket protocols are already somewhat generic, but they become chatlog-specific once handled by `Main.gd`.

This refactor should move Merlin toward:

```text
Main.gd
  = app coordinator

MerlinWindowManager
  = owns windows, z-order, focus, show/hide

MerlinWindow
  = reusable shell/chrome

WindowContent
  = specialized UI content

GestureWindowController
  = gesture interaction with registered windows

DismissDropZone
  = future trashcan / UI cleanup target
```

---

# High-Level Direction

## Current shape

```text
Main.gd
  owns almost everything:
    WebSocket event handling
    chatlog creation
    chatlog content
    chatlog movement
    chatlog resizing
    chatlog z-index
    gesture cursor
    gesture hit-testing
    gesture drag
    gesture resize
    UI control mode visual state
```

## Target shape

```text
Main.gd
  coordinates app-level systems only

MerlinWindowManager
  owns active windows, focus, z-order, show/hide

MerlinWindow
  owns reusable chrome, dragging, resizing, focus visuals, capabilities

ChatLogContent
  owns only chat message content and scroll behavior

GestureWindowController
  owns gesture cursor interaction against registered windows

WindowRegistry / SurfaceRegistry
  provides topmost hit-testing and capabilities

DismissDropZone
  later dismisses visible UI surfaces without deleting data
```

---

# Core Principles

## 1. Shell and content must be separated

Bad:

```text
Chatlog creates and owns:
  header
  drag logic
  resize logic
  message list
  close behavior
  z-index
  gesture state
```

Good:

```text
MerlinWindow
  owns header, drag, resize, close, z-index, focus, gesture affordance

ChatLogContent
  owns message rendering, scroll, auto-scroll
```

---

## 2. Gesture control should target windows, not chatlog

Bad:

```text
if cursor over _chatlog_panel:
  grab chatlog
```

Good:

```text
window = WindowRegistry.get_topmost_at(cursor_position)

if window.capabilities.accepts_gesture_grab:
  grab window
```

---

## 3. Z-order must be centralized

Z-order should not be controlled by individual windows.

A future gesture may manipulate depth/layering, for example:

```text
pinch + move away from camera -> bring forward
pinch + move toward camera -> send backward
```

Even though that gesture is not part of this refactor, the architecture should already make it possible.

Central ownership:

```text
MerlinWindowManager / WindowZOrderManager
  BringToFront(window)
  SendToBack(window)
  MoveForward(window)
  MoveBackward(window)
  SetRelativeOrder(windowA, windowB)
```

---

## 4. Backend should stay high-level

Backend should not know exact frontend bounds or layout.

Backend owns:

```text
voice command routing
UI control mode lifecycle
vision sidecar lifecycle
high-level UI events
```

Frontend owns:

```text
window placement
hit-testing
drag/resize behavior
z-order
animations
dismiss/drop-zone behavior
```

---

## 5. Dismiss means visible UI cleanup, not data deletion

Future trashcan/drop-zone behavior must only affect visible UI elements.

```text
throw chatlog into trashcan
  -> hide chatlog window
  -> conversation data remains safe
```

Never:

```text
delete memories
delete files
delete conversation history
delete settings
```

---

# Refactor Overview

The refactor is split into 3 phases.

```text
Phase 1 — Foundation Without Behavior Risk
  Introduce window vocabulary, definitions, manager skeleton, registry, and layer model.
  Keep current chatlog behavior mostly intact.

Phase 2 — Migrate Chatlog Into Universal Window System
  Extract chatlog content, create MerlinWindow shell, move drag/resize/z-order/focus into reusable systems.

Phase 3 — Generalize Gesture + Dismiss + Future Surfaces
  Make gesture targeting window-generic, add dismiss capability, then build trashcan/drop-zone on top.
```

Each phase is designed to be split into small PRs.

---

# Phase 1 — Foundation Without Behavior Risk

## Goal

Introduce the universal window architecture vocabulary and skeletons without breaking the current working chatlog.

This phase should mostly add structure, not change runtime behavior.

Current chatlog can still be created by `_setup_chatlog_panel()` during this phase.

The key outcome is that Merlin now has a place to define and register future windows.

---

## Phase 1 Target State

After Phase 1:

```text
MerlinWindowDefinition exists.
MerlinWindowCapabilities exists.
MerlinWindowManager exists as a real node/service.
MerlinWindowRegistry exists.
Layer/z-order concepts exist.
Chatlog has a window definition.
Existing chatlog behavior still works.
Main.gd begins delegating small pieces, but does not yet lose ownership of chatlog behavior.
```

---

## Phase 1 Non-Goals

Do not migrate the chatlog into a new scene yet.

Do not replace mouse drag/resize yet.

Do not replace gesture targeting yet.

Do not build trashcan yet.

Do not introduce multiple real window types yet.

Do not rewrite backend command routing yet.

---

## Phase 1 PRs

## PR 1.1 — Add Window Definitions and Capabilities

### Goal

Create data structures that describe UI windows/surfaces without changing behavior.

Suggested files:

```text
Merlin.Frontend/Scripts/UI/Windows/MerlinWindowDefinition.gd
Merlin.Frontend/Scripts/UI/Windows/MerlinWindowCapabilities.gd
Merlin.Frontend/Scripts/UI/Windows/MerlinWindowConstants.gd
```

Names can differ, but keep the concept clear.

### Definition fields

A window definition should describe:

```text
window_type
title
default_width
default_height
min_width
min_height
max_width
max_height
default_position_mode
can_move
can_resize
can_dismiss
can_focus
accepts_gesture_grab
accepts_gesture_resize
remember_position
remember_size
preserve_aspect_ratio
layer_group
```

Example chatlog definition:

```text
window_type: chatlog
title: Chat
default_width: current CHATLOG_DEFAULT_WIDTH
default_height: current CHATLOG_DEFAULT_HEIGHT
min_width: current CHATLOG_MIN_WIDTH
min_height: current CHATLOG_MIN_HEIGHT
max_width: current CHATLOG_MAX_WIDTH
max_height: current CHATLOG_MAX_HEIGHT
can_move: true
can_resize: true
can_dismiss: true
can_focus: true
accepts_gesture_grab: true
accepts_gesture_resize: true
remember_position: false for now
remember_size: false for now
layer_group: normal_window
```

### Acceptance criteria

```text
No runtime behavior changes.
Chatlog still opens/closes by voice.
Chatlog still moves/resizes by mouse.
Chatlog still moves/resizes by gesture.
A chatlog window definition can be queried.
Current chatlog constants are mirrored into the definition or mapped from it.
```

---

## PR 1.2 — Add MerlinWindowManager Skeleton

### Goal

Add a manager that will eventually own windows, focus, z-order, and registration.

Suggested file:

```text
Merlin.Frontend/Scripts/UI/Windows/MerlinWindowManager.gd
```

Suggested responsibilities:

```text
register_window_definition(definition)
get_window_definition(window_type)
register_window_instance(window)
unregister_window_instance(window)
show_window(window_type)
hide_window(window_type)
focus_window(window)
bring_to_front(window)
get_topmost_window_at(position, capability_filter)
```

During this PR, many methods can be no-op or route to current chatlog functions.

### Important

Do not break existing `Main.gd` behavior yet.

The manager can be initialized by `Main.gd` and given a reference to the existing chatlog panel once it exists.

### Acceptance criteria

```text
WindowManager exists.
Main.gd can create/register the manager.
Chatlog definition is registered.
Existing chatlog behavior still works.
No behavior regression.
```

---

## PR 1.3 — Add Layer and Z-Order Model

### Goal

Introduce centralized z-order concepts without fully migrating chatlog yet.

Suggested concepts:

```text
WindowLayerGroup
WindowZOrderManager
```

Layer groups:

```text
background
normal_windows
floating_tools
drop_zones
modals
debug_overlays
gesture_cursor
critical_overlays
```

Initial expected layering:

```text
normal_windows:
  chatlog and future windows

gesture_cursor:
  always above normal windows

drop_zones:
  future trashcan, above normal windows but below cursor or as decided

critical_overlays:
  above everything
```

### Current behavior to preserve

The gesture cursor currently uses a high `CanvasLayer` and should remain above the chatlog.

### Acceptance criteria

```text
Central z-order constants exist.
Chatlog still comes forward on show/drag/resize.
Gesture cursor still appears above chatlog.
No visual layering regression.
```

---

## PR 1.4 — Add Window Registry / Surface Registry Skeleton

### Goal

Create the registry that will later replace chatlog-specific hit-testing.

Suggested file:

```text
Merlin.Frontend/Scripts/UI/Windows/MerlinWindowRegistry.gd
```

Suggested responsibilities:

```text
register_window(window)
unregister_window(window)
update_window_bounds(window)
get_visible_windows()
get_topmost_at_position(position)
get_topmost_with_capability_at_position(position, capability)
get_focused_window()
get_grabbed_window()
```

During Phase 1, it can register the current chatlog panel as a simple adapter.

### Acceptance criteria

```text
Registry exists.
Chatlog can be registered as a window/surface.
Registry can report chatlog bounds.
Registry can answer topmost-at-position for chatlog.
Existing gesture targeting is not replaced yet.
No behavior regression.
```

---

## Phase 1 Completion Criteria

Phase 1 is complete when:

```text
Window definitions exist.
Window capabilities exist.
Window manager skeleton exists.
Window registry skeleton exists.
Layer/z-order concepts exist.
Chatlog can be described as a window.
Existing chatlog behavior still works exactly as before.
Main.gd is still allowed to own current behavior, but the future migration path is visible.
```

---

# Phase 2 — Migrate Chatlog Into Universal Window System

## Goal

Move the current chatlog implementation out of `Main.gd` special-case logic and into the reusable window system.

This phase transforms the chatlog from:

```text
runtime-created custom panel inside Main.gd
```

into:

```text
MerlinWindow shell + ChatLogContent
```

The chatlog remains the only real window initially, but it now uses the same foundation future UI elements will use.

---

## Phase 2 Target State

After Phase 2:

```text
Chatlog content is separate from window chrome.
MerlinWindow shell owns header, close button, drag handle, resize handle, base style.
WindowManager owns show/hide/focus/z-order.
WindowRegistry owns bounds and hit-testing.
Mouse drag/resize calls shared window methods.
Gesture drag/resize targets MerlinWindow, not _chatlog_panel.
Main.gd no longer directly owns chatlog movement/resize.
```

---

## Phase 2 Non-Goals

Do not add trashcan yet.

Do not add file browser/video/music windows yet.

Do not remove backend compatibility events yet.

Do not make gesture depth/z gestures yet.

Do not overbuild persistence yet.

---

## Phase 2 PRs

## PR 2.1 — Extract ChatLogContent

### Goal

Separate the message rendering and scroll behavior from floating panel chrome.

Suggested files:

```text
Merlin.Frontend/Scripts/UI/ChatLog/ChatLogContent.gd
Merlin.Frontend/Scenes/UI/ChatLog/ChatLogContent.tscn
```

ChatLogContent owns:

```text
message list
message entry creation
role/source/timestamp display
scroll container
auto-scroll
clear messages
append message
```

ChatLogContent does not own:

```text
window title bar
close button
drag behavior
resize handle
z-index
gesture behavior
surface capabilities
```

### Migration approach

Start by extracting the inner message body from `_setup_chatlog_panel()`.

Keep the outer panel in `Main.gd` temporarily.

### Acceptance criteria

```text
Chatlog still opens/closes.
Chatlog messages still append.
Auto-scroll still works.
Clear still works.
Visual layout is equivalent.
Mouse drag/resize still works.
Gesture drag/resize still works.
```

---

## PR 2.2 — Create MerlinWindow Shell

### Goal

Create the reusable window shell used by chatlog and future surfaces.

Suggested files:

```text
Merlin.Frontend/Scenes/UI/Windows/MerlinWindow.tscn
Merlin.Frontend/Scripts/UI/Windows/MerlinWindow.gd
```

MerlinWindow owns:

```text
PanelContainer / root Control
header/title area
close button
optional header controls
content host
resize handle(s)
focus visual
hover visual
grabbed visual
resizing visual
base panel style
min/max/default size
```

Suggested public methods:

```text
configure(definition)
set_content(node)
show_window()
hide_window()
focus_window()
set_window_rect(rect)
get_window_rect()
move_to(position)
move_by(delta)
resize_to(size)
set_gesture_visual_state(hovered, grabbed, resizing)
dismiss()
```

### Important

The first version can have the same bottom resize handle as the current chatlog.

Do not attempt full edge/corner resizing yet unless simple.

### Acceptance criteria

```text
MerlinWindow can host ChatLogContent.
MerlinWindow visually matches current chatlog closely enough.
Close button works.
Mouse drag works.
Mouse resize works.
Min/max size applies.
No gesture regression after wiring.
```

---

## PR 2.3 — Move Chatlog to MerlinWindow

### Goal

Replace runtime-created custom chatlog shell with `MerlinWindow`.

The chatlog should now be:

```text
MerlinWindow(window_type = chatlog)
  content = ChatLogContent
```

Main.gd should no longer create the entire chatlog chrome manually.

### Preserve

```text
UI_PANEL_SHOW chatlog
UI_PANEL_HIDE chatlog
UI_CHATLOG_APPEND
UI_CHATLOG_CLEAR
voice open/close
message content
scroll behavior
current visual style where reasonable
```

### Acceptance criteria

```text
Chatlog opens/closes by voice.
Chatlog content still renders.
Mouse drag/resize still works.
Gesture drag/resize still works.
Show brings chatlog to front.
Close button hides chatlog.
No duplicate chatlog windows.
```

---

## PR 2.4 — Move Focus and Z-Order Into WindowManager

### Goal

Replace `_chatlog_z_index` with manager-owned z-order/focus.

WindowManager should own:

```text
next_z_index
focused_window
bring_to_front(window)
focus_window(window)
clear_focus(window)
```

Initial behavior:

```text
show window -> bring to front
mouse press on window -> bring to front
gesture grab -> bring to front
voice focus/show -> bring to front
```

### Layer group note

The manager should support layer groups conceptually, but first implementation can be simple.

Example:

```text
normal window z-order is managed separately from gesture cursor layer.
```

### Acceptance criteria

```text
No _chatlog_z_index ownership in Main.gd.
Chatlog still comes forward on show.
Chatlog still comes forward on mouse drag/resize.
Chatlog still comes forward on gesture grab.
Gesture cursor remains above normal windows.
```

---

## PR 2.5 — Move Mouse Drag/Resize Into MerlinWindow

### Goal

Make mouse movement/resizing generic.

MerlinWindow should own:

```text
drag state
resize state
drag start offset
resize start mouse
resize start rect
clamp to viewport
min/max size enforcement
```

Main.gd should not own:

```text
_chatlog_dragging
_chatlog_resizing
_on_chatlog_header_gui_input
_on_chatlog_resize_gui_input
```

### Shared APIs

```text
begin_mouse_drag(mouse_position)
update_mouse_drag(mouse_position)
end_mouse_drag()

begin_mouse_resize(mouse_position)
update_mouse_resize(mouse_position)
end_mouse_resize()
```

or equivalent Godot signal/input handling inside `MerlinWindow`.

### Acceptance criteria

```text
Mouse drag works through MerlinWindow.
Mouse resize works through MerlinWindow.
Chatlog-specific drag/resize globals removed from Main.gd.
Clamp/min/max behavior preserved or improved.
No gesture regression.
```

---

## PR 2.6 — Move Gesture Targeting to WindowRegistry

### Goal

Replace chatlog-specific gesture target lookup with generic window hit-testing.

Current bad path:

```text
_surface_at_gesture_position(position)
  -> if over _chatlog_panel: "chatlog"
```

Target path:

```text
WindowRegistry.get_topmost_with_capability_at_position(position, accepts_gesture_grab)
  -> MerlinWindow
```

Gesture controller should store grabbed window reference or window id, not `"chatlog"` special cases.

### Preserve primary/secondary model

```text
primary hand:
  visible cursor
  hover
  grab/move

secondary hand:
  resize modifier only
```

### Acceptance criteria

```text
Primary cursor hover uses registry.
Primary pinch grabs topmost eligible MerlinWindow.
Primary drag moves grabbed MerlinWindow.
Secondary pinch resizes grabbed MerlinWindow if eligible.
Chatlog still works as before.
Synthetic Alt+mouse still works.
```

---

## PR 2.7 — Unify Gesture Movement/Resize Through Window APIs

### Goal

Both mouse and gesture should call common window movement/resize logic where practical.

Gesture controller should not directly write:

```text
_chatlog_panel.position
_chatlog_panel.size
```

It should call:

```text
window.move_to(position)
window.resize_to(size)
window.set_window_rect(rect)
```

Axis-aware resize should remain in the gesture controller or a reusable resize helper, but final clamping should belong to the window.

### Important correction

During two-hand resize:

```text
primary hand owns movement
secondary hand modifies size
```

Do not reintroduce midpoint-based movement if it conflicts with primary movement.

### Acceptance criteria

```text
Mouse and gesture movement use shared window APIs.
Mouse and gesture resize use shared window APIs/clamping.
Axis-aware gesture resize still works.
Primary hand movement still feels stable.
Secondary hand only modifies resize.
```

---

## Phase 2 Completion Criteria

Phase 2 is complete when:

```text
Chatlog is no longer a special runtime window shell in Main.gd.
Chatlog is MerlinWindow + ChatLogContent.
WindowManager owns show/hide/focus/z-order.
MerlinWindow owns drag/resize/chrome.
WindowRegistry owns hit-testing.
Gesture controller targets registered windows.
Main.gd is substantially thinner.
All existing chatlog, mouse, gesture, and WebSocket behavior still works.
```

---

# Phase 3 — Generalize Dismiss, Trashcan, and Future UI Elements

## Goal

Use the new window system to add reusable dismiss behavior and prepare for future UI surfaces.

This phase should not be started until Phase 2 is stable.

The first visible feature in this phase can be the trashcan/dismiss drop zone.

---

## Phase 3 Target State

After Phase 3:

```text
Windows can declare dismiss capability.
Dismiss only hides/closes visible UI, never deletes data.
Trashcan/drop-zone can dismiss eligible windows.
Backend can send generic window/drop-zone events.
Future window types can be added quickly.
```

---

## Phase 3 Non-Goals

Do not delete persistent data through trashcan.

Do not add file deletion.

Do not build every future UI window immediately.

Do not build depth/z gestures yet.

Do not remove existing chatlog backend compatibility until new generic events are stable.

---

## Phase 3 PRs

## PR 3.1 — Add Generic Dismiss Capability

### Goal

Add a standard dismiss path to `MerlinWindow`.

Capabilities:

```text
can_dismiss
dismiss_mode
```

Possible dismiss modes:

```text
hide
close
remove_temporary
minimize
```

First chatlog behavior:

```text
dismiss chatlog -> hide chatlog window
```

Important:

```text
dismiss != delete data
```

### Acceptance criteria

```text
Chatlog can be dismissed through generic window API.
Dismiss hides chatlog.
Reopening chatlog restores messages.
No data loss.
Close button can use same dismiss/hide path if appropriate.
```

---

## PR 3.2 — Add Generic UI Window Events

### Goal

Evolve backend/frontend event model to support future arbitrary windows.

Keep compatibility with current events.

Existing events:

```text
UI_PANEL_SHOW { panelId: "chatlog" }
UI_PANEL_HIDE { panelId: "chatlog" }
```

New preferred events:

```json
{
  "event": "UI_WINDOW_SHOW",
  "windowType": "chatlog"
}
```

```json
{
  "event": "UI_WINDOW_HIDE",
  "windowType": "chatlog"
}
```

```json
{
  "event": "UI_WINDOW_FOCUS",
  "windowType": "chatlog"
}
```

Chatlog content events can remain specific:

```text
UI_CHATLOG_APPEND
UI_CHATLOG_CLEAR
```

because content update is not generic window behavior.

### Acceptance criteria

```text
Old UI_PANEL_SHOW/HIDE still works.
New UI_WINDOW_SHOW/HIDE works.
Chatlog commands can use either path during transition.
Frontend routes generic events through WindowManager.
```

---

## PR 3.3 — Add DismissDropZone / Trashcan Visual

### Goal

Add the visual trashcan/drop-zone without data deletion.

Suggested files:

```text
Merlin.Frontend/Scenes/UI/Dismiss/TrashcanDropZone.tscn
Merlin.Frontend/Scripts/UI/Dismiss/DismissDropZone.gd
Merlin.Frontend/Scripts/UI/Dismiss/DismissDropZoneController.gd
```

States:

```text
Hidden
Appearing
Idle
HoverArmed
AcceptingDrop
ConsumingWindow
Closing
```

First behavior:

```text
show trashcan
hide trashcan
detect dragged eligible window over trashcan
release over trashcan -> animate window into trashcan -> dismiss window -> close trashcan
```

### Acceptance criteria

```text
Trashcan can appear/disappear.
Trashcan is visually polished enough to be recognizable.
Dragging chatlog over trashcan arms it.
Releasing chatlog over trashcan dismisses/hides chatlog.
Chatlog messages remain safe.
Trashcan closes after successful dismiss.
```

---

## PR 3.4 — Add Backend Trashcan Commands

### Goal

Add deterministic voice commands for trashcan/drop-zone.

Open phrases:

```text
open trashcan
show trashcan
open the trashcan
show the trashcan
open bin
show bin
open cleanup
clean this up
```

Close phrases:

```text
close trashcan
hide trashcan
close bin
hide bin
cancel trashcan
```

Backend should emit:

```text
UI_DISMISS_DROPZONE_SHOW
UI_DISMISS_DROPZONE_HIDE
```

or generic equivalent.

Commands should bypass LLM/DeepInfra.

### Recommended behavior

If UI control mode is off:

```text
open trashcan -> start UI control mode and show trashcan
```

or initially:

```text
open trashcan -> tell user UI control mode must be active
```

Choose the safer first implementation if uncertain.

### Acceptance criteria

```text
Voice open trashcan shows drop-zone.
Voice close trashcan hides drop-zone.
Commands bypass LLM.
UI control stop closes trashcan.
No item is dismissed by close command alone.
```

---

## PR 3.5 — Add First Non-Chatlog Test Window

### Goal

Prove the universal window system is real by adding a simple second window.

Do not choose a complex media player yet.

Recommended first test:

```text
DebugInfoWindow
or
SimpleToolResultWindow
```

It should use:

```text
MerlinWindow shell
WindowManager show/hide
WindowRegistry hit-testing
Mouse drag/resize if enabled
Gesture drag/resize if enabled
Dismiss if enabled
```

### Acceptance criteria

```text
Two windows can be visible.
Z-order works between them.
Topmost hit-testing works.
Gesture grab selects topmost eligible window.
Trashcan can dismiss eligible test window.
Chatlog still works.
```

---

## PR 3.6 — Prepare Future Media/File Windows

### Goal

Do not fully implement media/file windows yet, but define what they will need from the window system.

Future capabilities:

```text
preserve_aspect_ratio
fixed_min_height
compact_mode
media_controls_area
content_focus
keyboard_input
scroll_region
file_drop_acceptance
```

Add only capability flags or TODO hooks if needed.

### Acceptance criteria

```text
WindowDefinition can describe future media/file surfaces.
No current behavior regression.
No premature full media/file implementation.
```

---

## Phase 3 Completion Criteria

Phase 3 is complete when:

```text
Windows support generic dismiss.
Trashcan/drop-zone can dismiss visible UI windows.
Trashcan never deletes persistent data.
Generic window show/hide events exist.
At least one non-chatlog test window proves the system is reusable.
Future media/file surfaces have a clear path.
```

---

# 3-Phase Summary

## Phase 1 — Foundation

```text
Add definitions, capabilities, manager skeleton, registry skeleton, and z-layer model.
Keep current behavior intact.
```

Success means:

```text
We have the vocabulary and scaffolding for universal windows.
Nothing breaks.
```

## Phase 2 — Migration

```text
Move chatlog into MerlinWindow + ChatLogContent.
Move drag/resize/z-order/focus/gesture targeting into reusable systems.
```

Success means:

```text
Chatlog is no longer special-case UI shell code.
Gesture and mouse manipulation target generic windows.
```

## Phase 3 — Expansion

```text
Add dismiss capability, trashcan/drop-zone, generic UI events, and a second test window.
```

Success means:

```text
The framework supports future UI elements like video player, music player, file browser, and tool panels.
```

---

# Global Regression Checklist

Run after every PR.

## Basic UI

```text
Merlin launches.
Orb still displays.
Activity/status still works.
Notification panel still works.
Existing bottom ChatPanel is not accidentally broken.
OverlayContainer still works.
```

## Chatlog

```text
"Merlin, show chatlog" opens chatlog.
"Merlin, close chatlog" hides chatlog.
Chatlog appends STT user text.
Chatlog appends assistant spoken text.
Chatlog auto-scroll works.
Manual scroll does not get forced to bottom unexpectedly.
Chatlog clear still works if used.
```

## Mouse

```text
Mouse drag works.
Mouse resize works.
Min/max size respected.
Window stays in viewport.
Click/drag brings window to front.
```

## Gesture

```text
"Merlin, let me control the UI" starts UI control mode.
Camera starts.
Gesture cursor appears.
Primary hand moves cursor.
Primary pinch grabs window.
Primary movement moves window.
Secondary pinch modifies resize only.
Two-hand axis-aware resize works.
Synthetic Alt+mouse input still works.
"Merlin, I'm done with the UI" stops UI control.
Camera turns off.
Cursor disappears.
Gesture state clears.
```

## Backend / WebSocket

```text
Old UI_PANEL_SHOW/HIDE still works until deliberately removed.
Gesture events still forward only while UI control mode is active.
No LLM routing for deterministic UI commands.
No DeepInfra call for UI control/chatlog/trashcan commands.
```

## Layering

```text
Focused/grabbed window comes to front.
Gesture cursor stays above windows.
Future drop-zone layer does not cover cursor incorrectly.
Critical overlays remain visible.
```

---

# Global Risks and Mitigations

## Risk 1 — Main.gd refactor breaks everything

`Main.gd` currently owns too much. Moving too much at once is risky.

Mitigation:

```text
Use small PRs.
Extract data/model first.
Extract content before shell.
Extract shell before gesture targeting.
Keep compatibility methods during migration.
```

---

## Risk 2 — Chatlog content and chrome extraction causes message regressions

Mitigation:

```text
Extract ChatLogContent first while leaving outer panel unchanged.
Test append/autoscroll after every change.
Do not rewrite message rendering and window shell in the same PR.
```

---

## Risk 3 — Gesture targeting breaks during registry migration

Mitigation:

```text
Keep chatlog as the only registered surface at first.
Registry should return the same target as current hardcoded method.
Add debug logs for target selection.
Keep synthetic Alt+mouse available.
```

---

## Risk 4 — Z-order becomes inconsistent

Mitigation:

```text
Centralize z-order before adding multiple windows.
Add layer groups early.
Keep gesture cursor on a separate high layer.
Test focus/bring-to-front manually.
```

---

## Risk 5 — Trashcan becomes destructive

Mitigation:

```text
Use "dismiss" internally, not "delete".
Only allow visible UI dismissal.
Never delete persistent data.
Require can_dismiss capability.
```

---

# Recommended Agent Execution Strategy

For each PR:

```text
1. Read this document.
2. Read the current architecture report.
3. Implement only the requested PR slice.
4. Preserve all regression checklist items.
5. Add focused tests if practical.
6. Do not opportunistically implement future phases.
7. Report:
   - files changed
   - behavior preserved
   - tests run
   - manual validation needed
   - known risks
```

---

# First Agent Prompt Suggestion

The first implementation prompt should be:

```text
Implement Phase 1 / PR 1.1 only:
Add MerlinWindowDefinition and MerlinWindowCapabilities for the future universal frontend window system.
Register a chatlog definition matching current chatlog constants.
Do not change runtime chatlog behavior.
Do not refactor chatlog creation yet.
No behavior regression allowed.
```

Reason:

```text
This gives us the future model without risking the working chatlog/gesture stack.
```

---

# Final Direction

The correct long-term direction is:

```text
Stop creating special-case UI panels.
Create reusable Merlin windows.
Put specialized content inside reusable windows.
Let gesture/mouse/trashcan/z-order work against the window framework.
```

Once this refactor is done, future UI elements become much cheaper:

```text
video player
music player
file browser
image viewer
memory viewer
tool panels
web result panels
debug panels
settings
```

Each future UI element should only need:

```text
content implementation
window definition
optional custom behavior
```

It should automatically get:

```text
move
resize
focus
z-order
gesture compatibility
dismiss/trashcan support
Merlin styling
```
