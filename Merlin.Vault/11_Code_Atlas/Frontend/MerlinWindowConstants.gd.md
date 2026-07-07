---
type: code-atlas
status: current
project: Merlin.Frontend
tags:
  - merlin
  - code-atlas
---

# MerlinWindowConstants.gd

## File

`Merlin.Frontend/Scripts/UI/Windows/MerlinWindowConstants.gd`

Verified present in current repo.

## Purpose

String constants shared by Merlin window definitions, capabilities, window manager, and gesture control.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control]]
- [[Browser Workspace]]
- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Main Types / Classes

- `MerlinWindowConstants.gd` GDScript class or script.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| constants | script-level | Defines window types, layer groups, capability names, and dismiss modes. | n/a | Main.gd, MerlinWindow, capabilities/definitions | Contract strings. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| constants | strings | Shared IDs for windowing/gesture behavior. | frontend scripts | script load | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| Godot preload/import users | Shared string contract. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| constant values | frontend scripts | window type/capability/layer routing. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| script preload | frontend scripts | constants loaded by Godot. |

## External Side Effects

No side effects.

## Safety / Guardrails

Keep frontend gesture state gated by `_ui_control_mode_active` or browser workspace state as appropriate. Backend owns command routing; frontend owns visual/window manipulation.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| No automated frontend tests | Manual/static use. | No string contract test. |

## Known Risks / Fragility

Renaming constants without updating all scripts breaks gesture targeting and window registration.

## Change Notes for Agents

Godot frontend behavior is mostly manually validated. Keep UI state and gesture state resets explicit when browser workspace or UI-control mode changes.
