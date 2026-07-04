# Merlin UiCanvas — Procedural Holographic Trashcan Renderer Design

## Purpose

This document explains how to build a high-quality **holographic trashcan / dismiss drop-zone visual** in Godot using procedural drawing.

The goal is to move away from the current weak-looking flat wireframe trashcan and toward a premium Merlin-native holographic UI element.

This document is intended as a TODO/design reference for an implementation agent.

The key idea:

```text
Do not draw one trashcan.
Draw a layered holographic rendering system.
```

The trashcan should feel like:

```text
a premium sci-fi holographic dismiss portal shaped like a trashcan
```

not:

```text
a simple debug line drawing of a bin
```

---

## Current Problem

The current Godot-drawn trashcan is structurally correct but visually weak.

It has:

```text
trashcan body
lid
rings
cyan lines
dark background
```

But it looks like:

```text
flat CAD sketch
debug vector drawing
thin wireframe outline
```

It is missing the visual features that made the concept image work:

```text
soft bloom
thick luminous edges
transparent glass-like body
inner glow
layered rim highlights
depth
volumetric light
base projection rings
strong hover/armed state
premium Merlin feel
```

The problem is not Godot drawing itself.

The problem is that the trashcan was drawn as one simple object instead of a layered holographic UI renderer.

---

## Core Visual Direction

The target visual should resemble the generated concept:

```text
dark background
cyan/electric-blue holographic bin
transparent body
bright glowing top rim
bright glowing bottom rim
soft aura circle around it
subtle base projection rings
lid that can open
inner glow when armed
high-quality sci-fi UI feel
```

The trashcan should not look like:

```text
Windows recycle bin
flat icon
debug rectangle
simple wireframe
cartoon bin
realistic garbage can
```

It should look like:

```text
Merlin holographic dismiss portal
```

---

## High-Level Rendering Strategy

Instead of drawing everything in a single `_draw()` pass, split the component into layers.

Suggested node structure:

```text
TrashcanDropZone : Control
  ActiveDropArea : Control          # invisible, forgiving hitbox
  AuraLayer : Control               # large circular aura / pulse rings
  BaseProjectionLayer : Control     # floor/projection rings under bin
  BodyBackLayer : Control           # faint rear/internal construction lines
  BodyFillLayer : Control           # transparent glassy body fill
  InnerGlowLayer : Control          # soft internal light volume
  BodyFrontLayer : Control          # strong front body outlines/rims/grooves
  LidLayer : Control                # animated lid, separate part
  HighlightLayer : Control          # edge sparkles / overbright accents
  LabelLayer : Label                # optional "Drop to dismiss"
```

The exact node names can differ, but the separation matters.

Each layer should be independently drawable and animatable.

---

## Why Layering Matters

A premium hologram is not made from one thin line.

It is made from repeated shapes with different intensity and opacity:

```text
wide faint glow
medium cyan glow
sharp cyan edge
white-blue highlight
semi-transparent fill
subtle inner light
```

For example, every important line should be drawn multiple times:

```gdscript
draw_line(a, b, Color(0.0, 0.8, 1.0, 0.06), 14.0) # wide aura
draw_line(a, b, Color(0.0, 0.8, 1.0, 0.14), 7.0)  # medium glow
draw_line(a, b, Color(0.2, 0.95, 1.0, 0.75), 2.0) # crisp cyan edge
draw_line(a, b, Color(0.9, 1.0, 1.0, 0.85), 0.8)  # white highlight
```

This is how we fake bloom even if no real post-processing bloom is available.

---

## Core Geometry Model

Do not try to create perfect 3D perspective.

Use simple 2D perspective approximations:

```text
ellipses
trapezoid body
vertical grooves
rings
separate lid
```

Recommended logical size:

```text
Trashcan component: 300x300 or 320x320
Active drop zone: 260x260 to 340x340
Visible trashcan: around 150x190
```

Suggested proportions:

```text
center_x = width / 2

body_top_y = height * 0.36
body_bottom_y = height * 0.78

top_width = width * 0.56
bottom_width = width * 0.42

top_ellipse_height = height * 0.095
bottom_ellipse_height = height * 0.08
```

The body should look like a tapered cylinder:

```text
wide top rim
slightly narrower bottom rim
left and right side edges angled inward
vertical recessed grooves
translucent inner fill
```

---

## Important: Active Drop Area vs Visual Trashcan

The interaction area and the visual should be separate.

```text
ActiveDropArea = large invisible forgiving hitbox
TrashcanVisual = smaller beautiful holographic object
```

Example:

```text
ActiveDropArea: 300x300
Visible bin: 150x180
```

Reason:

```text
User should not need pixel-perfect placement.
If the dragged UI window visibly touches the drop zone aura, it should arm.
```

The visible aura should help communicate the active drop area.

---

## Layer 1 — AuraLayer

The aura layer gives the trashcan presence.

It should draw:

```text
large faint circular aura
subtle pulsing outer glow
soft blue/cyan radial field
optional small orbiting particles later
```

This aura should be visible even when the bin is idle, but stronger when armed.

### Visual behavior

Idle:

```text
soft low-alpha aura
slow pulse
subtle blue ring
```

HoverArmed:

```text
aura expands
aura alpha increases
rings brighten
pulse speed increases
```

### Drawing approach

Use squashed ellipses and circles.

A floor-like projection ring should be an ellipse, not a perfect circle:

```text
wide horizontal ellipse
low vertical height
```

Pseudo-code:

```gdscript
func _draw() -> void:
    var cx := size.x * 0.5
    var cy := size.y * 0.78
    var armed := armed_strength

    # Big soft aura
    draw_circle(
        Vector2(cx, cy),
        115.0 + armed * 30.0,
        Color(0.0, 0.55, 1.0, 0.035 + armed * 0.06)
    )

    # Multiple projection rings
    for i in range(6):
        var r := 55.0 + i * 18.0
        var pulse := sin(Time.get_ticks_msec() * 0.002 + i) * 2.0
        var alpha := max(0.02, 0.12 - i * 0.012 + armed * 0.06)
        _glow_ellipse(
            Vector2(cx, cy),
            Vector2((r + pulse) * 2.0, (r + pulse) * 0.55),
            alpha
        )
```

---

## Layer 2 — BaseProjectionLayer

The base projection is the hologram emitter feel under the bin.

It should include:

```text
concentric ellipses
small dotted grid feel
center bright point
subtle vertical light beam
```

### Visual behavior

Idle:

```text
quiet projection ring
soft center glow
```

HoverArmed:

```text
brighter center point
rings slightly expand
vertical beam intensifies
```

### Implementation ideas

Draw:

```text
5–8 horizontal ellipses
one bright center circle
a vertical translucent beam triangle/polygon
small random/fixed dots around the base
```

Use deterministic points, not random every frame, unless seeded.

A random every-frame particle field will shimmer too much.

---

## Layer 3 — BodyFillLayer

The body fill makes the bin feel like translucent holographic glass.

The fill should be subtle.

Draw a trapezoid polygon:

```text
top-left
top-right
bottom-right
bottom-left
```

Example:

```gdscript
var body_poly := PackedVector2Array([
    Vector2(cx - top_w * 0.5, top_y),
    Vector2(cx + top_w * 0.5, top_y),
    Vector2(cx + bottom_w * 0.5, bottom_y),
    Vector2(cx - bottom_w * 0.5, bottom_y),
])

draw_colored_polygon(
    body_poly,
    Color(0.0, 0.45, 0.85, 0.08 + armed_strength * 0.06)
)
```

Do not make the fill too opaque.

The body should remain airy and holographic.

---

## Layer 4 — BodyBackLayer

This layer creates depth.

It should draw faint internal/rear construction lines:

```text
faint vertical lines
faint rear ellipse
faint internal grid curves
```

These lines should have low alpha.

Example:

```text
rear/internal lines alpha: 0.08–0.18
front edges alpha: 0.7–1.0
```

This gives the impression that the bin is transparent and dimensional.

---

## Layer 5 — BodyFrontLayer

This is the most important drawing layer.

It should draw:

```text
top rim
bottom rim
side edges
front vertical grooves
front highlights
thicker outer silhouette
```

The top rim and bottom rim are the strongest visual anchors.

### Required front elements

```text
top rim ellipse
bottom rim ellipse
left side edge
right side edge
vertical grooves
white-blue rim highlights
```

### Top rim

The top rim should be bright and thick.

Draw it multiple times:

```text
wide faint glow
medium glow
sharp cyan line
white highlight
```

### Bottom rim

The bottom rim should also be bright because it anchors the hologram.

### Vertical grooves

Use 5–7 vertical rounded grooves.

They should follow the body taper:

```text
top x wider
bottom x narrower
```

The grooves can be simple long rounded vertical curves or line pairs.

A simple first version:

```text
draw rounded vertical arcs/lines as elongated capsules
```

If rounded capsules are hard, use paired vertical glow lines with small top/bottom arcs.

---

## Helper Functions

Create reusable drawing helpers.

### Glow line

```gdscript
func _glow_line(a: Vector2, b: Vector2, strength: float = 1.0) -> void:
    draw_line(a, b, Color(0.0, 0.8, 1.0, 0.05 * strength), 14.0)
    draw_line(a, b, Color(0.0, 0.8, 1.0, 0.12 * strength), 7.0)
    draw_line(a, b, Color(0.20, 0.95, 1.0, 0.70 * strength), 2.0)
    draw_line(a, b, Color(0.90, 1.0, 1.0, 0.85 * strength), 0.8)
```

### Glow polyline

```gdscript
func _glow_polyline(points: PackedVector2Array, strength: float = 1.0) -> void:
    draw_polyline(points, Color(0.0, 0.8, 1.0, 0.05 * strength), 14.0)
    draw_polyline(points, Color(0.0, 0.8, 1.0, 0.12 * strength), 7.0)
    draw_polyline(points, Color(0.20, 0.95, 1.0, 0.70 * strength), 2.0)
    draw_polyline(points, Color(0.90, 1.0, 1.0, 0.85 * strength), 0.8)
```

### Ellipse points

Godot does not have a simple universal `draw_ellipse()` in all contexts, so approximate it:

```gdscript
func _ellipse_points(center: Vector2, diameter: Vector2, segments: int = 96) -> PackedVector2Array:
    var points := PackedVector2Array()
    for i in range(segments + 1):
        var angle := TAU * float(i) / float(segments)
        points.append(center + Vector2(
            cos(angle) * diameter.x * 0.5,
            sin(angle) * diameter.y * 0.5
        ))
    return points
```

### Glow ellipse

```gdscript
func _glow_ellipse(center: Vector2, diameter: Vector2, strength: float = 1.0) -> void:
    _glow_polyline(_ellipse_points(center, diameter), strength)
```

---

## LidLayer

The lid must be its own drawable/animatable component.

Do not draw the lid as part of the body layer.

Suggested node:

```text
TrashcanLidLayer : Control
```

The parent `TrashcanDropZone` controls:

```text
lid_open_progress
```

Where:

```text
0.0 = closed
1.0 = open
```

### Lid closed state

```text
position: centered above top rim
rotation: 0
scale_y: 1.0
```

### Lid open state

For a 2D fake perspective:

```text
position moves upward and slightly right
rotation rotates slightly backward
scale_y compresses a little
```

Example:

```gdscript
var p := lid_open_progress

lid_layer.position = closed_position.lerp(open_position, p)
lid_layer.rotation = lerp(0.0, deg_to_rad(-14.0), p)
lid_layer.scale = Vector2(1.0, lerp(1.0, 0.82, p))
```

### Lid drawing

The lid layer should draw:

```text
flattened glowing ellipse
thick rim band
radial/segmented top lines
small handle
white-cyan highlights
```

The handle is important because it makes the object read as a bin/trashcan.

---

## InnerGlowLayer

This layer appears mostly when armed/open.

It should draw:

```text
soft cyan-blue glow inside the bin
subtle vertical light column
small bright center near base
```

Idle:

```text
low alpha
```

HoverArmed:

```text
higher alpha
```

Consuming:

```text
brief flash
```

Pseudo:

```gdscript
var alpha := 0.06 + armed_strength * 0.16 + consume_flash * 0.30
draw_circle(Vector2(cx, bottom_y - 10), bottom_w * 0.35, Color(0.0, 0.85, 1.0, alpha))
```

For vertical beam:

```gdscript
var beam := PackedVector2Array([
    Vector2(cx - 28, top_y + 5),
    Vector2(cx + 28, top_y + 5),
    Vector2(cx + 12, bottom_y),
    Vector2(cx - 12, bottom_y),
])
draw_colored_polygon(beam, Color(0.0, 0.65, 1.0, alpha * 0.35))
```

---

## HighlightLayer

This layer adds small high-quality accents.

Examples:

```text
tiny white-blue spark points on rim
small glints on lid handle
short highlight segments
subtle particle dots inside the bin
```

Do not overdo it.

A few highlights make the component feel expensive.

Example:

```gdscript
draw_circle(Vector2(cx + top_w * 0.35, top_y - 2), 2.0, Color(0.9, 1.0, 1.0, 0.8))
draw_circle(Vector2(cx - bottom_w * 0.25, bottom_y + 1), 1.4, Color(0.6, 0.95, 1.0, 0.6))
```

---

## State Model

The renderer should expose clear state variables.

Suggested:

```gdscript
var visible_progress: float = 0.0
var armed_strength: float = 0.0
var lid_open_progress: float = 0.0
var consume_flash: float = 0.0
var idle_pulse: float = 0.0
```

These values drive drawing.

### Hidden

```text
visible_progress = 0
armed_strength = 0
lid_open_progress = 0
consume_flash = 0
```

### Idle

```text
visible_progress = 1
armed_strength = 0
lid_open_progress = 0
consume_flash = 0
slow idle pulse active
```

### HoverArmed

```text
visible_progress = 1
armed_strength -> 1
lid_open_progress -> 1
inner glow brightens
aura expands
```

### ConsumingSurface

```text
consume_flash -> 1 briefly
lid stays open
window animates into bin
then lid closes
then trashcan hides or returns idle
```

### Closing

```text
visible_progress -> 0
armed_strength -> 0
lid_open_progress -> 0
```

---

## Animation Timing

Suggested timings:

```text
appear: 180–250 ms
hide: 180–250 ms
arm/open: 100–180 ms
disarm/close lid: 100–180 ms
consume flash: 120–200 ms
consume window travel: 250–450 ms
```

Use `Tween`.

Do not animate by manually stepping frames unless necessary.

---

## Public API

The component should expose methods that the real frontend can use later.

```gdscript
func show_drop_zone() -> void
func hide_drop_zone() -> void
func set_armed(is_armed: bool) -> void
func play_consume_preview() -> void
func consume_window(window: Control) -> void
func reset() -> void
func get_active_drop_rect() -> Rect2
```

In UiCanvas, `play_consume_preview()` can animate a fake test card.

In the real frontend, `consume_window(window)` can animate the actual grabbed `MerlinWindow`.

---

## Active Drop Rect

The active drop rect should be forgiving and not tied tightly to the visible bin.

```gdscript
func get_active_drop_rect() -> Rect2:
    return Rect2(global_position, size).grow(drop_padding)
```

Recommended:

```text
drop_padding = 32–56 px
```

The active rect should usually be larger than the visible trashcan.

If debug mode is enabled, draw the active rect:

```text
semi-transparent cyan rectangle/circle
only in UiCanvas debug mode
```

---

## Hit Testing Reminder

The interaction logic should use:

```text
grabbed window rect intersects active drop rect
```

not:

```text
window center point inside bin
```

The moment the dragged UI element visibly touches the active drop zone, it should arm.

---

## Why Not PNG Assets For This Version

The asset-extraction attempt produced overlapping image parts.

That is not suitable for clean rigging because:

```text
body layer includes lid/rim artifacts
lid layer includes surrounding glow
aura includes bin glow
stacking creates double-glow and bad seams
```

For this use case, procedural Godot drawing gives better control:

```text
theme colors are adjustable
glow strength is tunable
lid motion is smooth
scale is resolution-independent
interaction states are easy to animate
no asset alignment problems
```

Use generated concept images only as visual references.

Do not use extracted body/lid PNGs as production rig parts.

---

## What Makes The Concept Image Good

The agent should specifically aim for these features:

```text
thick bright top rim
thick bright bottom rim
transparent blue body fill
faint rear/internal construction lines
strong front body outline
soft aura behind/around the object
holographic base rings
separate lid with handle
inner glow when open/armed
subtle white-blue highlights
smooth opening motion
```

If the trashcan lacks these, it will look like a flat wireframe.

---

## Implementation Milestone Plan

## Milestone 1 — Refactor Into Layers

Create separate layers:

```text
AuraLayer
BaseProjectionLayer
BodyLayer
LidLayer
InnerGlowLayer
HighlightLayer
```

Acceptance:

```text
trashcan still appears
layers render in correct order
no feature behavior changes required yet
```

## Milestone 2 — Improve Body Rendering

Implement:

```text
translucent body fill
top rim with multi-pass glow
bottom rim with multi-pass glow
side edges with multi-pass glow
vertical grooves
faint rear construction lines
```

Acceptance:

```text
body no longer looks like flat debug wireframe
```

## Milestone 3 — Improve Lid Rendering

Implement:

```text
separate lid layer
handle
rim band
radial lid lines
open/close animation using lid_open_progress
```

Acceptance:

```text
lid visually opens and closes smoothly
```

## Milestone 4 — Improve Aura/Base

Implement:

```text
concentric projection rings
soft aura field
armed aura expansion
subtle idle pulse
```

Acceptance:

```text
trashcan feels like a holographic projection/drop zone
```

## Milestone 5 — Armed/Consume State Polish

Implement:

```text
stronger glow while armed
inner glow brightens
consume flash
fake consume preview in UiCanvas
```

Acceptance:

```text
armed state is obvious and satisfying
```

---

## UiCanvas Requirements

In UiCanvas, provide buttons:

```text
Show
Hide
Arm
Disarm
Consume Preview
Reset
Toggle Debug Hitbox
```

These should call the public API.

The user should be able to tune visuals without running the real frontend.

---

## Visual Acceptance Criteria

The procedural trashcan is successful when:

```text
it clearly resembles the concept direction
it looks premium enough for Merlin UI
it no longer looks like a thin debug sketch
closed state looks polished
open/armed state looks intentional
aura communicates the drop zone
lid motion is smooth
inner glow makes it feel alive
it scales cleanly
```

---

## Real Frontend Integration Later

Once the UiCanvas version looks good:

```text
copy TrashcanDropZone scene/script into Merlin.Frontend
replace current ugly trashcan visual
keep existing hit-testing and dismiss logic
wire state changes to real drop-zone controller
```

The visual component should remain mostly self-contained.

---

## Important Agent Instruction

Do not try to recreate the concept by drawing one outline.

Use a layered holographic rendering system.

Every major outline should be drawn at least three times:

```text
wide faint glow
medium glow
sharp cyan edge
small white highlight
```

The difference between bad and good is mostly:

```text
layering
glow depth
fill transparency
rim emphasis
animation state
```

not the basic trashcan silhouette.

---

## Summary

The right route is still Godot drawing, but it must be done properly.

The desired approach:

```text
procedural geometry
multi-pass glow strokes
layered rendering
separate animated lid
transparent body fill
strong top/bottom rims
holographic base projection
state-driven aura and inner glow
```

This gives Merlin total UI control while still approaching the premium look of the concept image.
