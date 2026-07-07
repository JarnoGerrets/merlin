---
type: code-atlas
status: current
project: Merlin.Frontend
tags:
  - merlin
  - code-atlas
---

# MerlinWindowCapabilities.gd

## File

`Merlin.Frontend/Scripts/UI/Windows/MerlinWindowCapabilities.gd`

Verified present in current repo.

## Purpose

Small capability model describing whether a Merlin window can move, resize, dismiss, focus, and accept gesture grab/resize/dismiss operations.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control]]
- [[Browser Workspace]]
- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Main Types / Classes

- `MerlinWindowCapabilities.gd` GDScript class or script.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `has_capability` | func | Maps capability string constants to booleans and combines dismiss gesture with `can_dismiss`. | constants | Main.gd/window manager/MerlinWindow | Gate for gesture targeting. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `can_move`, `can_resize`, `can_dismiss`, `can_focus` | bools | Mouse/window permissions. | window logic | definitions | per definition |
| `accepts_gesture_*` | bools | Motion control permissions. | Main.gd/window manager | definitions | per definition |
| `preserve_aspect_ratio`, `dismiss_mode` | value/string | Resize/dismiss behavior. | window logic | definitions | per definition |

## Dependencies

| Dependency | Used For |
| --- | --- |
| MerlinWindowConstants | capability names/dismiss modes. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| capability decision | Main/window manager | bool result from `has_capability`. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| capability string | Main/window manager | `has_capability` |

## External Side Effects

No external side effects.

## Safety / Guardrails

Keep frontend gesture state gated by `_ui_control_mode_active` or browser workspace state as appropriate. Backend owns command routing; frontend owns visual/window manipulation.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| No automated frontend tests | Capability use is manual/indirect. | No unit coverage. |

## Known Risks / Fragility

Wrong capability defaults make windows draggable/dismissible when they should not be, or block gesture control.

## Change Notes for Agents

Godot frontend behavior is mostly manually validated. Keep UI state and gesture state resets explicit when browser workspace or UI-control mode changes.
