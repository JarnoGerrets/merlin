# Merlin Gesture UI Selection + Crumple Throw Dismiss Design

## Purpose

This document defines a new gesture-first UI interaction model for Merlin.

Instead of continuing to invest in a visual trashcan/drop-zone, Merlin should support a more natural gesture:

```text
select a UI element
crumple it with both hands
throw it off-screen
dismiss/delete that specific visible UI element
```

This document intentionally splits the implementation into two phases:

```text
P1 — Select a UI element with point + pinch tap
P2 — Crumple + throw the selected element to dismiss/delete it
```

The goal is to create a clean, safe, and intuitive gesture interaction model that works with Merlin's existing universal window/control layer.

---

# High-Level Decision

The trashcan visual/drop-zone is removed from the core direction.

Instead of:

```text
drag a UI window into a trashcan
```

Merlin should support:

```text
select a UI window
physically crumple/compress it with both hands
throw it away off-screen
```

This is a better fit for hand gesture control because it is direct manipulation:

```text
the user acts on the object itself
```

not:

```text
the user has to move the object to a separate target
```

---

# Important Semantics

In this document, the word `delete` means:

```text
delete/dismiss the visible UI element from the screen
```

It must **not** mean:

```text
delete underlying user data
clear chat history
delete files
delete messages
remove saved memory
destroy content permanently
```

For example:

```text
Crumple throwing the chatlog hides/dismisses the chatlog window.
It does not clear the chatlog messages.
When reopened, the chatlog content should still exist unless the user explicitly cleared it.
```

This is critical.

The gesture deletes/dismisses the **visible UI surface**, not the underlying data model.

---

# Existing Gesture Context

Current Merlin gesture assumptions:

```text
Point / cursor:
  hand controls visible UI cursor / hover target

Pinch hold:
  grab and move UI window

Two-hand pinch:
  resize selected/grabbed UI window

UI control mode:
  webcam/vision is only active while UI control mode is enabled
```

New model should integrate with this instead of fighting it.

---

# Final Gesture Vocabulary

Recommended final gesture map:

```text
Point:
  aim / hover only

Pinch tap:
  select/focus hovered UI element

Pinch hold:
  grab/move selected or hovered UI element

Pinch hold + movement:
  drag/move UI element

Two-hand pinch on selected element:
  resize UI element

Two-hand compress selected element:
  enter crumple candidate mode

Crumple + throw off-screen:
  dismiss/delete selected visible UI element

Release before throw:
  cancel crumple and restore element
```

The important distinction:

```text
pointing does not select by itself
```

Pointing only aims. Selection requires a deliberate pinch tap.

This avoids accidental selection caused by hand jitter or camera tracking noise.

---

# Phase P1 — Select A UI Element

## Goal

Implement reliable gesture-based selection of UI elements.

The user should be able to:

```text
point at a UI element
perform a quick pinch tap
have that element become selected/focused
```

This should be the foundation for later actions like resize, crumple, throw, contextual controls, etc.

---

## P1 User Experience

Expected behavior:

```text
1. User enters UI control mode.
2. User points at a Merlin UI element.
3. Hover feedback appears on the element.
4. User performs a quick pinch tap.
5. The hovered element becomes selected.
6. The selected element shows a stronger visual state.
```

Example:

```text
User points at chatlog.
Chatlog gets subtle hover glow.
User pinch taps.
Chatlog becomes selected.
Chatlog gets selected outline/glow.
```

---

## P1 Gesture Definitions

## Point

Point means:

```text
index finger extended / primary pointer active
```

System behavior:

```text
moves the gesture cursor
updates hover target
does not select
does not grab
does not dismiss
```

Pointing alone should never commit actions.

---

## Pinch Tap

A pinch tap means:

```text
pinch closes
pinch releases quickly
hand movement remains small
```

Suggested thresholds:

```text
max pinch tap duration: 150–250 ms
movement tolerance: 12–24 px screen-space
must have valid hover target at pinch start or release
```

The exact values should be configurable.

Pinch tap result:

```text
select/focus hovered UI element
```

---

## Pinch Hold

A pinch hold means:

```text
pinch closes and stays closed longer than tap threshold
```

or:

```text
pinch closes and pointer moves beyond drag threshold
```

Pinch hold result:

```text
grab/move behavior
```

This preserves the existing interaction model.

---

## Tap vs Hold Decision

The same physical pinch starts both selection and grab.

Merlin decides the meaning by duration and movement.

Recommended logic:

```text
On pinch down:
  store pinch start time
  store pointer position
  store hover target

While pinch remains down:
  if movement exceeds drag threshold:
    begin grab/move
  else if duration exceeds hold threshold:
    begin grab/move

On pinch up before hold/drag:
  if duration <= tap threshold and movement <= tap movement tolerance:
    select hover target
```

Suggested thresholds:

```text
tap_max_duration_ms = 220
tap_max_movement_px = 18
hold_to_grab_ms = 260
drag_start_movement_px = 20
```

These are starting values only. They should be configurable.

---

## P1 Selection Targeting

Selection should target the topmost eligible UI surface under the pointer.

Selection priority:

```text
1. Topmost visible MerlinWindow under cursor
2. Topmost eligible UI surface under cursor
3. Explicit interactive sub-element if supported later
4. No selection
```

For now, P1 can target windows/surfaces only.

Do not attempt to select every internal button/control unless the existing window system already supports it safely.

---

## P1 Visual Feedback

Hover state:

```text
soft outline
soft glow
subtle z-lift or brightness
```

Selected state:

```text
stronger outline
selected glow
possibly small corner/edge handles
slightly elevated z-order
```

Grabbed state:

```text
window follows hand
outline changes
cursor/hand feedback changes
```

No selection:

```text
hover clears
selected element remains selected unless user selects another element or clears selection
```

---

## P1 Selection Rules

## Selecting a new element

If user pinch taps another eligible element:

```text
previous element deselects
new element selects
```

## Selecting the already selected element

If user pinch taps the selected element:

Recommended behavior:

```text
keep it selected
maybe pulse selected outline
```

Do not toggle deselect by default, because accidental deselection can be annoying.

## Deselect

Deselect can be one of:

```text
pinch tap empty space
voice command: "deselect"
UI control mode stop
selecting another element
```

Recommended MVP:

```text
pinch tap empty space clears selection
```

But this should not happen if the pointer is noisy and only briefly leaves the window during a tap. Use hover lock if needed.

---

## P1 Hover Lock Recommendation

During pinch tap detection, lock the target from pinch down.

Reason:

```text
the pointer can jitter during tap
```

Recommended:

```text
target_at_pinch_down = current_hover_target
```

Then on pinch release, select that target if the tap is valid.

If there is no target at pinch down but there is a target at release, using release target is optional.

Safer MVP:

```text
only select target_at_pinch_down
```

---

## P1 Interaction With Existing Move

Existing move behavior should not break.

Current likely behavior:

```text
pinch immediately grabs/moves
```

Change to:

```text
pinch enters pending state first
```

Then:

```text
quick release = select
hold/move = grab
```

This prevents every pinch tap from accidentally moving the window.

---

## P1 Required Backend/Frontend Roles

The exact architecture may vary, but recommended split:

## Vision sidecar / gesture detection

Should emit low-level gesture events:

```text
pointer position
pinch down/up
pinch confidence
hand id / primary hand
timestamp
```

Avoid making high-level selection decisions in the Python vision sidecar.

## Backend gesture router

Can normalize and forward gesture events.

May also detect high-level gesture candidates if that already fits architecture.

## Frontend window manager

Should own:

```text
hit testing
hover target
selected window
visual selection state
grab/move state
```

Selection is UI-specific and should generally be owned by frontend/window manager.

---

## P1 Suggested Events

Low-level events:

```json
{
  "type": "GESTURE_POINTER",
  "x": 0.51,
  "y": 0.42,
  "hand": "primary",
  "confidence": 0.93
}
```

```json
{
  "type": "GESTURE_PINCH",
  "phase": "down",
  "hand": "primary",
  "x": 0.51,
  "y": 0.42,
  "confidence": 0.91
}
```

```json
{
  "type": "GESTURE_PINCH",
  "phase": "up",
  "hand": "primary",
  "x": 0.52,
  "y": 0.43,
  "confidence": 0.89
}
```

Frontend can derive:

```text
pinch_tap
pinch_hold
grab
```

or backend can send derived events if that is how the current code is structured.

---

## P1 Acceptance Criteria

P1 is complete when:

```text
1. User can point at a UI window and see hover feedback.
2. User can pinch tap a hovered window to select it.
3. Selected window shows a clear selected visual state.
4. Pinch tap does not accidentally move the window.
5. Pinch hold still grabs/moves the window.
6. Pinch hold + movement still works as existing drag behavior.
7. Selecting a new element deselects the previous element.
8. Pinch tap on empty space clears selection or intentionally does nothing, based on chosen rule.
9. Selection state is owned cleanly by the universal window/control layer.
10. UI control mode stop clears unsafe transient gesture state.
```

---

# Phase P2 — Crumple + Throw To Delete/Dismiss Selected UI Element

## Goal

Implement a deliberate two-hand gesture to dismiss/delete a selected visible UI element.

The desired interaction:

```text
select UI element
grab/compress it with both hands
crumple it into a compact ball/proxy
throw it off-screen
specific UI element is dismissed
```

This replaces the trashcan/drop-zone concept.

---

## P2 User Experience

Expected behavior:

```text
1. User selects a UI element with P1.
2. User uses both hands on the selected element.
3. Both hands pinch/hold and move inward.
4. The selected UI element visually compresses/crumples.
5. User throws the crumpled element off-screen in any direction.
6. The element flies away and is dismissed.
```

If the user releases too early or too slowly:

```text
the crumple cancels
the UI element springs back to normal
```

This makes the gesture recoverable and reduces accidental dismissals.

---

## P2 Important Safety Rule

Dismiss must require a deliberate sequence.

Do not dismiss from a single accidental fast hand movement.

Required sequence:

```text
selected element exists
two hands active
both hands are pinching/holding
hands are associated with selected element
hands compress inward enough
crumple candidate becomes armed
throw velocity exceeds threshold
release confirms dismissal
```

No crumple-armed state means no delete.

---

## P2 Gesture States

Suggested state machine:

```text
Idle
Selected
TwoHandCandidate
Crumpling
CrumpleArmed
ThrowCandidate
Dismissing
CanceledRestore
```

---

## State: Idle

No selected element.

Crumple is impossible.

---

## State: Selected

One UI element is selected.

Waiting for two-hand interaction.

---

## State: TwoHandCandidate

Entered when:

```text
selected element exists
two hands are tracked
both hands are pinching or grabbing
both hands are near/over selected element
```

Store:

```text
initial hand positions
initial hand distance
selected element original rect
selected element original transform
start timestamp
```

---

## State: Crumpling

Entered when two-hand candidate is valid and hands begin moving inward.

Track:

```text
current hand distance
compression ratio
hand midpoint
hand velocities
```

Suggested compression ratio:

```text
compression_ratio = current_hand_distance / initial_hand_distance
```

Example:

```text
1.0 = no compression
0.65 = significant compression
0.45 = strong compression
```

Visual response:

```text
window scales down
corners pull inward or proxy appears
rotation/wobble increases slightly
opacity or glow changes
```

---

## State: CrumpleArmed

Entered when:

```text
compression_ratio <= crumple_arm_ratio
```

Suggested:

```text
crumple_arm_ratio = 0.60 to 0.70
```

Also require minimum movement confidence and duration to avoid false positives.

When armed:

```text
visual feedback clearly changes
window looks compact/crumpled
possibly show stronger glow/outline
```

---

## State: ThrowCandidate

After crumple is armed, detect outward/throw motion.

Commit if:

```text
hand velocity exceeds threshold
and movement direction indicates throwing away/off-screen
and release happens during or immediately after throw
```

Throw should be possible:

```text
left
right
up
down
diagonal
```

Because the user said:

```text
throw in any direction off the screen
```

Do not require a specific target.

---

## State: Dismissing

Entered when throw is accepted.

Behavior:

```text
hide/disable real window interaction
create or use crumpled visual proxy
animate proxy off-screen in throw direction
fade/shrink/spin
call dismiss/delete on the selected visible UI element
clear selection
destroy proxy
```

---

## State: CanceledRestore

Entered when:

```text
user releases before crumple is armed
or throw velocity too low
or tracking is lost before commit
or gesture times out
```

Behavior:

```text
restore window to original rect/scale/opacity
clear crumple transient state
keep the element selected
```

Keeping it selected lets the user try again.

---

# P2 Crumple Visuals

## MVP Visual

Do not start with real paper physics.

MVP visual can be:

```text
selected window scales down
window rotates slightly
corners pull visually inward if practical
opacity fades slightly
outline/glow intensifies
on throw, compact proxy flies off-screen
```

This is enough to prove the UX.

## Better Visual Later

Later polish can include:

```text
mesh deformation
corner folding
crumpled ball proxy
trail
particles
elastic spring-back
window texture capture
```

Do not block P2 MVP on perfect crumple visuals.

---

## Visual Proxy Recommendation

Actually deforming a live Godot `Control` can be awkward.

Recommended approach:

```text
during crumple:
  optionally transform the real window visually

on commit:
  create CrumpledWindowProxy
  hide/disable real window
  animate proxy off-screen
  dismiss real window after animation
```

The proxy can initially be:

```text
a rounded glowing rectangle
a smaller crumpled-looking card
a simplified copy of the window silhouette
```

Later it can become:

```text
captured texture of the actual window
distorted mesh
crumpled ball shader
```

---

# P2 Relation To Resize

Existing two-hand pinch already means resize.

Crumple must not break resize.

Differentiate them by intent and motion.

## Resize

Resize behavior:

```text
two-hand pinch on selected/grabbed window
hands move apart/together gradually
window edges/corners resize
hands stay near opposite sides/corners
no rapid inward compression past crumple threshold
no throw velocity
```

## Crumple

Crumple behavior:

```text
selected window
both hands move inward strongly toward center
compression passes crumple_arm_ratio
window becomes compact
then fast throw motion/release off-screen
```

Important:

```text
normal two-hand resize should not accidentally dismiss
```

Recommended rule:

```text
Crumple only arms if the selected window's visual scale/compression crosses a strong threshold.
Resize remains normal until that threshold is crossed.
```

Alternative stricter rule:

```text
Crumple only starts if both hands begin near opposite sides of selected element and move toward the center with sufficient inward velocity.
```

---

# P2 Throw Detection

Throw direction should be based on recent hand velocity and/or crumple proxy velocity.

Possible direction calculation:

```text
throw_vector = average_velocity_of_both_hands_over_last_100_to_200_ms
```

Commit if:

```text
length(throw_vector) >= throw_velocity_threshold
```

Then animate proxy toward the nearest off-screen exit in that direction.

Suggested:

```text
throw_velocity_threshold_px_per_sec = 900 to 1400
```

This must be tuned in real testing.

---

## Release Timing

Throw should commit when:

```text
crumple is armed
hands move fast enough
then user releases one or both pinches
```

Allow a small grace window:

```text
release_grace_ms = 150 to 250
```

This prevents tracking/release timing from feeling too strict.

---

## Off-Screen Animation

Given throw vector:

```text
normalize throw direction
find intersection with expanded viewport bounds
animate proxy beyond that point
```

Suggested animation:

```text
duration: 250–500 ms depending on speed
ease out
spin slightly
fade out
scale down
optional trail
```

---

# P2 Delete/Dismiss Behavior

When dismiss is committed:

```text
call window.dismiss()
or equivalent universal window hide/dismiss method
```

Do not:

```text
clear underlying data
delete stored content
delete files
remove chat messages
```

For chatlog:

```text
hide/dismiss chatlog window
keep chat content available for reopen
```

For future surfaces:

```text
dismiss visible surface instance
preserve underlying data unless explicitly designed otherwise
```

---

# P2 Undo / Recovery Recommendation

Because crumple throw is a dismiss/delete gesture, add a recovery path.

MVP recovery can be:

```text
voice command: "bring it back"
```

or:

```text
temporary toast: "Dismissed Chatlog — Undo"
```

If implementing undo is too much for P2, at least keep the architecture ready:

```text
last_dismissed_window_id
last_dismissed_window_type
last_dismissed_restore_state
```

Recommended:

```text
P2 should store last dismissed UI surface for possible restore.
```

---

# P2 Visual Feedback Requirements

The user must always understand what Merlin thinks is happening.

## Selected

```text
strong outline / selected glow
```

## Two-hand candidate

```text
subtle second-hand indicator
maybe corner/edge handles appear
```

## Crumpling

```text
window visibly compresses
glow changes
```

## Crumple armed

```text
clear stronger feedback
compact state
maybe "release/throw" readiness feel
```

## Throw commit

```text
proxy flies away
window disappears
```

## Cancel

```text
spring-back animation
selection remains
```

---

# Suggested Implementation Phases

## P1.1 — Gesture Selection Foundation

Implement:

```text
hover target
selected target
pinch tap detection
selection visual state
tap vs hold separation
```

Do not implement crumple yet.

## P1.2 — Selection Integration With Existing Move/Resize

Ensure:

```text
pinch tap selects
pinch hold moves
two-hand pinch still resizes selected window
selection state works with window manager/z-order
```

## P2.1 — Crumple Gesture Detector

Implement:

```text
two-hand candidate
compression ratio tracking
crumple armed state
cancel/restore
```

Use simple visual scaling first.

## P2.2 — Throw Commit + Dismiss

Implement:

```text
throw velocity detection
off-screen proxy animation
window.dismiss()
clear selection
```

## P2.3 — Polish

Implement:

```text
better crumple proxy
spring-back animation
trail/fade/spin
undo/restore path
threshold tuning
```

---

# Integration With Universal Window Layer

This should integrate with Merlin's universal window/control layer.

The window manager should support:

```text
hovered_window
selected_window
grabbed_window
crumple_candidate_window
dismissed_window
```

Window capabilities should include something like:

```text
can_select
can_move
can_resize
can_dismiss_by_gesture
```

Not every future UI element must be dismissible.

For example:

```text
chatlog: dismissible
music player: dismissible
critical confirmation dialog: maybe not dismissible by throw
system warning: maybe not dismissible
```

---

# Capability Safety

Before dismissing, check:

```text
selected element exists
selected element is visible
selected element has can_dismiss_by_gesture
selected element is not locked/modal/critical
gesture confidence is sufficient
crumple was armed
throw was committed
```

If not dismissible:

```text
do not dismiss
show blocked feedback
spring back
```

---

# Suggested Config / Thresholds

Create configurable gesture settings.

```text
pinch_tap_max_duration_ms = 220
pinch_tap_max_movement_px = 18
pinch_hold_to_grab_ms = 260
drag_start_movement_px = 20

two_hand_candidate_max_target_distance_px = 80
crumple_arm_ratio = 0.65
crumple_min_duration_ms = 120
crumple_cancel_timeout_ms = 1200

throw_velocity_threshold_px_per_sec = 1100
throw_release_grace_ms = 220
throw_animation_min_ms = 250
throw_animation_max_ms = 500
```

These are starting values and must be tuned by testing.

---

# Testing Plan

## P1 Tests

Test:

```text
point hover over chatlog
pinch tap selects chatlog
pinch tap empty space clears selection or keeps selection according to chosen rule
pinch hold moves chatlog
quick pinch tap does not move chatlog
selection visual is clear
selecting another window changes selection
UI control mode stop clears transient pinch state
```

## P2 Tests

Test:

```text
selected chatlog can enter two-hand candidate
slow two-hand resize does not dismiss
two-hand compress arms crumple
release before armed restores window
release after armed but without throw restores window
strong throw after crumple dismisses selected window
dismissed chatlog content is not cleared
gesture cannot dismiss non-dismissible surfaces
tracking loss cancels safely
```

---

# Required Report Back

After implementation, report:

```text
files changed
where selection state lives
how pinch tap is detected
how tap vs hold is separated
how selected visual state is rendered
how crumple candidate is detected
how throw velocity is calculated
how dismiss/delete is executed
what safety checks exist
what thresholds are used
what manual tests were run
known limitations
next tuning recommendations
```

---

# Non-Goals

Do not keep building the trashcan visual.

Do not implement a 3D trashcan.

Do not implement real data deletion.

Do not clear chatlog data.

Do not modify backend/STT/TTS/LLM behavior unless required for gesture event plumbing.

Do not make pointing alone select.

Do not allow accidental throw dismissal from ordinary resize/move gestures.

Do not attempt perfect crumpled-paper physics in the MVP.

---

# Summary

The new direction is:

```text
P1:
  point to hover
  pinch tap to select

P2:
  with selected element
  two-hand compress to crumple
  throw off-screen to dismiss/delete that visible UI element
```

This replaces the trashcan with a more natural gesture-native delete/dismiss interaction.
