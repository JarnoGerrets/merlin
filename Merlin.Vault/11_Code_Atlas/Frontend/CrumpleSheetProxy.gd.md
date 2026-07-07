---
type: code-atlas
status: current
project: Merlin.Frontend
tags:
  - merlin
  - code-atlas
---

# CrumpleSheetProxy.gd

## File

`Merlin.Frontend/Scripts/UI/Windows/CrumpleSheetProxy.gd`

Verified present in current repo.

## Purpose

Animated proxy for crumple-and-throw window dismissal. It captures a source window texture, subdivides it into cells, animates sheet deformation into a ball, and throws/frees the proxy.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control]]
- [[Browser Workspace]]
- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Main Types / Classes

- `CrumpleSheetProxy.gd` GDScript class or script.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `configure_from_window` | func | Captures source window texture/size, builds cells, disables mouse on proxy. | `_build_source_texture`; `_build_cells` | Main.gd crumple start | Setup path. |
| `set_crumple_progress` | func | Updates crumple progress and redraws. | `queue_redraw` | Main.gd crumple update | Visual deformation. |
| `throw_and_free` | func | Animates thrown proxy by velocity and frees node. | tween | Main.gd commit dismiss | End of gesture delete. |
| `bake_final_ball_async` / `configure_as_bake_renderer` | func | Creates a baked ball texture using a secondary renderer path. | viewport/texture helpers | Main.gd/proxy internals | Avoids live complex draw after throw. |
| `_draw` / `_draw_sheet_cell` | func | Draws subdivided textured cells with alpha/shading/deformation. | cell helpers | Godot draw | Main rendering. |
| `_build_cells` / `_sheet_vertex_position` | func | Creates per-cell random-ish timing/shape data and computes animated vertices. | constants; easing helpers | drawing/setup | Visual character. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_cells` | array | Grid cell animation metadata. | draw/update | configure/build | per proxy |
| `_crumple`, `_throw_t`, `_throw_offset`, `_ball_formed` | values | Animation progress/state. | draw/throw | update/tween | per interaction |
| `_texture_override` / `_baked_texture` | Texture2D | Source or baked texture. | draw | configure/bake | freed with node |

## Dependencies

| Dependency | Used For |
| --- | --- |
| source MerlinWindow/SubViewport | Captures visual content. |
| Godot drawing/tween APIs | Animation. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| visual animation | frontend scene | crumple/delete effect. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| selected window and throw velocity | Main.gd | configure/update/throw methods |

## External Side Effects

Creates textures/viewports/tweens and frees proxy nodes after animation.

## Safety / Guardrails

Keep frontend gesture state gated by `_ui_control_mode_active` or browser workspace state as appropriate. Backend owns command routing; frontend owns visual/window manipulation.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| No automated frontend tests | Manual crumple gesture validation. | No rendering snapshots. |

## Known Risks / Fragility

Texture capture and async bake can be timing-sensitive. Large windows increase draw cost; stale proxy state can leave visual artifacts.

## Change Notes for Agents

Godot frontend behavior is mostly manually validated. Keep UI state and gesture state resets explicit when browser workspace or UI-control mode changes.
