---
type: code-atlas
status: current
project: Merlin.Frontend
tags:
  - merlin
  - code-atlas
---

# MerlinWindow.gd

## File

`Merlin.Frontend/Scripts/UI/Windows/MerlinWindow.gd`

Verified present in current repo.

## Purpose

Reusable Godot window shell used by dashboard UI. It owns header/close/resize UI, gesture visual states, movement/resizing, viewport clamping, and focus/close signals.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control]]
- [[Browser Workspace]]
- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Main Types / Classes

- `MerlinWindow.gd` GDScript class or script.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `configure` | func | Applies window definition, title, size, position, capabilities. | `_build_shell`; `clamp_to_viewport` | Main/window manager | Required before show. |
| `show_window` / `hide_window` / `dismiss` | func | Toggle visibility or emit close request according to dismiss mode. | signals | window manager/Main | Dashboard lifecycle. |
| `focus_window` | func | Emits focus request. | signal | header/input/window manager | Brings window to front. |
| `move_to` / `move_by` / `resize_to` | func | Manipulate rect with clamping/min-max constraints. | `_clamp_size`; `clamp_to_viewport` | mouse and gesture handlers | Shared mouse/gesture API. |
| `set_gesture_visual_state` | func | Stores hover/selected/grabbed/resizing/crumpling flags and reapplies style. | `_window_style` | Main.gd gesture state | Visual feedback. |
| `_on_header_gui_input` / `_on_resize_gui_input` | func | Mouse drag/resize path. | move/resize helpers | Godot GUI input | Parallel to gesture path. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `window_type`, `definition`, `content_host` | values/nodes | Window identity/config/content mount. | Main/window manager | configure/build | lifetime |
| `_gesture_*` flags | bools | Hover/select/grab/resize/crumple visual state. | style builder | Main.gd | reset by setter |
| `_dragging` / `_resizing` and offsets | bools/vectors | Mouse-driven manipulation state. | input handlers | input start/end | per interaction |

## Dependencies

| Dependency | Used For |
| --- | --- |
| MerlinWindowDefinition/Capabilities/Constants | configuration. |
| Godot Control/Panel/Button nodes | shell UI. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `close_requested`, `focus_requested` | MerlinWindowManager/Main | window lifecycle requests. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| mouse/gesture calls | user/Main.gd | input handlers and public movement APIs |

## External Side Effects

Manipulates scene node position/size/style and emits local signals.

## Safety / Guardrails

Keep frontend gesture state gated by `_ui_control_mode_active` or browser workspace state as appropriate. Backend owns command routing; frontend owns visual/window manipulation.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| No automated frontend tests | Manual scene validation. | Full gesture/window UX manual. |

## Known Risks / Fragility

Viewport clamping and gesture visual style are easy to break when changing sizes or margins. The same APIs are used by mouse and motion control.

## Change Notes for Agents

Godot frontend behavior is mostly manually validated. Keep UI state and gesture state resets explicit when browser workspace or UI-control mode changes.
