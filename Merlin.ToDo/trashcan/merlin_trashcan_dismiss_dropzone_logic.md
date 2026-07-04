# Merlin Gesture UI — Trashcan / Dismiss Drop Zone Logic

## Purpose

This document defines the logic for a Merlin-native **trashcan drop zone** used to dismiss visible UI elements through gesture control.

The trashcan is not a data deletion feature. It is a spatial UI cleanup tool.

```text
Throw visible UI element into trashcan
  =
dismiss / close / hide that visible UI surface
```

The underlying data must remain untouched.

Examples:

```text
chatlog thrown into trashcan
  -> chatlog window hides
  -> conversation text remains available

tool result thrown into trashcan
  -> visible tool result card closes
  -> underlying result/conversation history remains safe

music player thrown into trashcan
  -> music player UI closes/minimizes
  -> media files/playlists are not deleted

file browser thrown into trashcan
  -> file browser UI closes
  -> files are not deleted
```

---

## Core UX

The user can be controlling a UI element with hand gestures and then say:

```text
Merlin, open trashcan
```

Merlin shows a high-quality animated trashcan/drop zone.

The user can then drag a visible UI element over the trashcan and release it.

The UI element animates into the trashcan and disappears from the screen.

The trashcan then closes and moves away.

The user can also say:

```text
Merlin, close trashcan
```

This hides the trashcan without dismissing anything.

---

## Important Semantic Rule

The trashcan must only affect **visible UI elements**.

It must never delete persistent data.

```text
Allowed:
  hide panel
  close panel
  dismiss card
  remove visible overlay
  minimize UI surface

Not allowed:
  delete conversation history
  delete files
  delete memories
  delete saved settings
  delete media
  clear database entries
```

Internally, this should be treated as:

```text
DismissDropZone
```

User-facing, it can visually be a trashcan because the metaphor is immediately understandable.

---

## Desired User Flow

### Flow A — User is already holding a UI element

```text
1. User starts UI control mode.
2. User pinches and grabs a visible UI element.
3. User says: "Merlin, open trashcan."
4. Trashcan appears.
5. Current grabbed element remains grabbed.
6. User drags the element over the trashcan.
7. Trashcan reacts and opens.
8. User releases the element.
9. Element animates into trashcan.
10. Element is dismissed.
11. Trashcan closes and disappears.
```

Important:

```text
Opening trashcan must not cancel the current grab.
```

### Flow B — User opens trashcan first

```text
1. User starts UI control mode.
2. User says: "Merlin, open trashcan."
3. Trashcan appears.
4. User chooses a visible UI element.
5. User grabs it.
6. User drags it over the trashcan.
7. User releases it.
8. Element is dismissed.
9. Trashcan closes and disappears.
```

### Flow C — User cancels trashcan

```text
1. Trashcan is visible.
2. User says: "Merlin, close trashcan."
3. Trashcan closes.
4. No UI element is dismissed.
5. Any currently grabbed surface remains available for normal movement unless UI control mode itself is stopped.
```

### Flow D — UI control mode stops

```text
1. Trashcan is visible.
2. User says: "Merlin, I'm done with the UI."
3. UI control mode stops.
4. Trashcan closes immediately.
5. Cursor disappears.
6. Grab/resize/drop-zone state clears.
7. Camera stops.
```

---

## Voice Commands

Trashcan commands should be deterministic, high-priority UI commands.

They should bypass:

```text
LLM
DeepInfra
general conversation routing
normal app/website opening logic
```

### Open phrases

```text
open trashcan
show trashcan
open the trashcan
show the trashcan
open trash can
show trash can
open the trash can
show the trash can
open bin
show bin
open the bin
show the bin
open cleanup
show cleanup
clean this up
```

### Close phrases

```text
close trashcan
hide trashcan
close the trashcan
hide the trashcan
close trash can
hide trash can
close the trash can
hide the trash can
close bin
hide bin
close the bin
hide the bin
cancel trashcan
cancel bin
stop trashcan
```

### Recommended behavior outside UI control mode

For the first version, keep this simple:

```text
If UI control mode is off:
  "open trashcan" should either:
    A. start UI control mode and show trashcan, or
    B. say "UI control mode needs to be active first."
```

Recommended initial behavior:

```text
open trashcan while UI control mode is off
  -> start UI control mode
  -> show trashcan
```

This feels natural because the trashcan is a gesture UI feature.

---

## Backend Responsibilities

The backend should own only the high-level command lifecycle.

Backend responsibilities:

```text
detect open/close trashcan voice commands
bypass LLM/DeepInfra
emit UI_DISMISS_DROPZONE_SHOW
emit UI_DISMISS_DROPZONE_HIDE
close trashcan when UI control mode stops
optionally auto-start UI control mode before showing trashcan
```

The backend should not own:

```text
exact trashcan position
drop-zone hit testing
which UI element overlaps trashcan
animation details
surface bounds
visual hover state
actual dismiss animation
```

Those belong in the frontend.

### Suggested backend events

```json
{
  "type": "UI_DISMISS_DROPZONE_SHOW"
}
```

```json
{
  "type": "UI_DISMISS_DROPZONE_HIDE"
}
```

Optional state event:

```json
{
  "type": "UI_DISMISS_DROPZONE_STATE",
  "state": "visible"
}
```

---

## Frontend Responsibilities

The frontend owns the actual trashcan experience.

Frontend responsibilities:

```text
render trashcan
animate trashcan in/out
track trashcan states
detect trashcan overlap with currently grabbed UI element
arm trashcan when a dismissible element is over it
animate element into trashcan
call the element's dismiss behavior
close trashcan after successful dismiss
cancel trashcan on command
clear state when UI control mode stops
```

The frontend already knows:

```text
visible UI elements
their bounds
grabbed element
gesture cursor state
current drag state
panel capabilities
```

So the frontend is the right place for trashcan hit-testing and animation.

---

## Required Surface Capability

Any UI element that can be thrown into the trashcan must declare itself dismissible.

Conceptual capability:

```text
canDismiss = true
```

Dismiss means:

```text
remove this visible UI element from the screen
```

It does not mean:

```text
delete persistent data
```

Examples:

```text
Chatlog:
  canDismiss = true
  dismiss action = hide chatlog window

ToolResultCard:
  canDismiss = true
  dismiss action = remove visible card

MusicPlayer:
  canDismiss = true
  dismiss action = close/minimize player UI

FileBrowser:
  canDismiss = true
  dismiss action = close browser UI

PermanentOrb:
  canDismiss = false
```

Trashcan should only accept UI elements where:

```text
canDismiss == true
```

---

## Trashcan State Machine

The trashcan should have an explicit state machine.

Suggested states:

```text
Hidden
Appearing
Idle
HoverArmed
AcceptingDrop
ConsumingSurface
Closing
```

### Hidden

```text
trashcan not visible
no drop-zone hit testing
```

### Appearing

```text
trashcan animates into view
drop-zone may become active near end of animation
```

### Idle

```text
trashcan visible
ready to accept dismissible surfaces
lid closed or slightly open
subtle glow/presence
```

### HoverArmed

```text
a grabbed dismissible surface overlaps the trashcan drop zone
trashcan highlights
lid opens
optional suction/glow effect appears
```

### AcceptingDrop

```text
user releases the grabbed surface while HoverArmed
drop is committed
normal panel release behavior is suppressed
```

### ConsumingSurface

```text
surface animates into the trashcan
surface scales down/fades/moves along a smooth path
trashcan lid closes after surface enters
surface dismiss action runs at the correct point in animation
```

### Closing

```text
trashcan closes and slides/fades away
drop-zone hit testing disabled
state returns to Hidden
```

---

## Drop Commit Logic

The safest first implementation is:

```text
release over trashcan = dismiss
```

Flow:

```text
if trashcan visible
and grabbedSurface exists
and grabbedSurface.canDismiss
and grabbedSurface overlaps trashcan drop zone
and user releases pinch:
    commit dismiss
```

Pseudo-logic:

```text
onGestureRelease:
  if Trashcan.IsHoverArmed and grabbedSurface.canDismiss:
      Trashcan.Consume(grabbedSurface)
  else:
      normalDrop(grabbedSurface)
```

During consumption:

```text
Trashcan.Consume(surface):
  block normal grab/drop behavior for this surface
  play surface-into-trashcan animation
  call surface.Dismiss()
  close trashcan
```

---

## Hover / Arming Logic

The trashcan should visibly react before dismissal happens.

Suggested arming logic:

```text
if grabbedSurface != null
and grabbedSurface.canDismiss
and Trashcan.DropZoneIntersects(grabbedSurface.DropPoint or Bounds):
    state = HoverArmed
else:
    state = Idle
```

Use a simple first approach:

```text
Use the surface center point as the drop point.
```

Optional later improvement:

```text
Use the leading edge / cursor grab point / closest point to trashcan.
```

Recommended first version:

```text
If the grabbed surface center enters the trashcan drop-zone bounds:
  arm trashcan
```

This avoids accidental dismissal when only a corner touches the trashcan.

---

## Drop Zone Placement

The trashcan should appear somewhere predictable and not block the main panel too much.

Possible placements:

```text
bottom-right
right side center
bottom center
near screen edge opposite active panel
```

Recommended first version:

```text
bottom-right of the Merlin UI viewport
```

Reason:

```text
common trash/recycle bin metaphor
unlikely to cover chatlog if chatlog starts on left
easy target for drag gesture
```

Later placement can become smarter.

Example placement rule:

```text
if grabbed surface is near bottom-right:
  place trashcan bottom-left
else:
  place trashcan bottom-right
```

But do not overcomplicate the first version.

---

## Visual Design Requirements

The trashcan should feel high-quality and Merlin-native.

Minimum visual requirements:

```text
polished trashcan icon/model
smooth appear animation
hover glow
lid/open reaction when armed
surface-consuming animation
smooth close animation
```

Avoid:

```text
plain debug rectangle
static icon with no feedback
instant disappear
harsh snapping
```

Desired feel:

```text
physical
fluent
intentional
safe
satisfying
```

### Suggested visual behavior

```text
Open command:
  trashcan slides/fades into view
  subtle bounce/settle

Idle:
  soft glow
  lid closed or slightly open

Dragged item nearby:
  glow intensifies

Dragged item over drop zone:
  lid opens
  inner glow appears
  optional pull/suction effect

Release over trashcan:
  surface scales down
  surface follows curved path into trashcan
  lid closes
  small settle animation
  trashcan slides/fades away
```

---

## Animation Timing

Suggested timings:

```text
Trashcan appear: 180-250 ms
Hover open lid: 100-180 ms
Consume surface: 250-450 ms
Trashcan close: 180-300 ms
```

Keep animations short enough to feel responsive.

Do not block UI control for too long after dismissal.

---

## What Gets Dismissed

Dismiss should be defined by the surface.

Examples:

```text
Chatlog:
  dismiss = hide panel

Temporary tool card:
  dismiss = remove card from visible overlay list

Music player:
  dismiss = hide/minimize player UI

Video player:
  dismiss = close video player UI

File browser:
  dismiss = close file browser UI

Settings window:
  dismiss = close settings window
```

Again:

```text
Dismiss visible UI only.
Do not delete underlying data.
```

---

## Trashcan Auto-Close

The trashcan should not stay open forever.

Auto-close options:

```text
close after successful dismiss
close when user says close trashcan
close when UI control mode stops
close after inactivity
```

Suggested inactivity behavior:

```text
if trashcan visible
and no grabbed surface enters hover zone for 20 seconds:
    close trashcan
```

Optional shorter behavior:

```text
if opened while a surface is already grabbed
and that grab ends outside trashcan:
    keep trashcan open for 5 seconds
    then close if unused
```

For first version, simple is fine:

```text
close after successful dismiss
close by voice
close on UI control stop
```

Add inactivity timeout later if needed.

---

## Interaction With Current Grab/Resize System

### While dragging

Trashcan should not interfere with normal primary-hand drag unless the surface is released over the armed drop zone.

```text
drag outside trashcan:
  normal drag/drop behavior

drag over trashcan:
  trashcan arms

release over trashcan:
  dismiss behavior
```

### While resizing

First version should avoid dismissing during resize.

Recommended rule:

```text
Trashcan only accepts a surface when it is being moved/grabbed, not actively resized.
```

If currently resizing:

```text
ignore trashcan hover
or show trashcan but do not arm
```

This prevents accidental dismissal while doing two-hand resize.

### If trashcan opens during resize

Recommended first behavior:

```text
show trashcan
but it only arms after resize ends and the surface is in normal grabbed/move state
```

---

## Safety Rules

Mandatory safety rules:

```text
trashcan only accepts registered visible UI surfaces
surface must have canDismiss = true
trashcan never deletes persistent data
release outside trashcan behaves like normal drop
close trashcan does not dismiss anything
UI control stop cancels trashcan
trashcan cannot dismiss non-dismissible core UI
```

Core UI that should probably not be dismissible:

```text
main orb
global camera/gesture indicator
system error overlay unless explicitly allowed
critical confirmation dialogs
```

---

## Backend Command Routing Requirements

Trashcan commands should be detected before generic chat/LLM routing.

High-priority deterministic routing:

```text
Speech transcript
  ↓
normalize wake words / filler / punctuation
  ↓
UI control command detector
  ↓
trashcan command detector
  ↓
chatlog/surface command detector
  ↓
normal command router / LLM
```

Normalization should allow:

```text
Merlin, open trashcan
Hey Merlin open the trashcan
please show bin
close the bin
hide trash can
```

Include aliases:

```text
trashcan
trash can
bin
cleanup
```

---

## Event Flow

### Open trashcan

```text
User says:
  "Merlin, open trashcan"

Backend:
  detects deterministic command
  emits UI_DISMISS_DROPZONE_SHOW

Frontend:
  shows trashcan
  enters Idle state
```

### Close trashcan

```text
User says:
  "Merlin, close trashcan"

Backend:
  detects deterministic command
  emits UI_DISMISS_DROPZONE_HIDE

Frontend:
  closes trashcan
  no dismiss action occurs
```

### Drop surface into trashcan

```text
User drags surface over trashcan

Frontend:
  detects overlap
  enters HoverArmed

User releases pinch

Frontend:
  enters AcceptingDrop
  plays consume animation
  calls surface dismiss
  closes trashcan
```

---

## Suggested Frontend Components

Possible components:

```text
DismissDropZone
DismissDropZoneController
DismissAnimationController
DismissibleSurfaceAdapter
```

### DismissDropZone

Responsible for:

```text
visual trashcan
drop-zone bounds
state animations
hover/open/close visuals
```

### DismissDropZoneController

Responsible for:

```text
show/hide commands
checking grabbed surface overlap
arming/disarming drop zone
committing dismiss on release
auto-closing after successful dismiss
```

### DismissAnimationController

Responsible for:

```text
animating surface into trashcan
scaling/fading/moving surface
timing surface.Dismiss()
```

### DismissibleSurfaceAdapter

Responsible for:

```text
calling the correct dismiss behavior for a surface
hide panel
close card
minimize player
```

---

## Suggested Backend Components

Possible backend additions:

```text
DismissDropZoneCommandDetector
UiDismissDropZoneCommand
UiDismissDropZoneBroadcaster
```

But if there is already a generic UI command/event mechanism, reuse it.

Backend should stay thin.

---

## Tests

### Backend tests

```text
"open trashcan" routes to UI_DISMISS_DROPZONE_SHOW
"show trashcan" routes to UI_DISMISS_DROPZONE_SHOW
"open trash can" routes to UI_DISMISS_DROPZONE_SHOW
"open bin" routes to UI_DISMISS_DROPZONE_SHOW
"close trashcan" routes to UI_DISMISS_DROPZONE_HIDE
"hide bin" routes to UI_DISMISS_DROPZONE_HIDE
trashcan commands bypass LLM/DeepInfra
trashcan commands work with "Hey Merlin" prefix
trashcan commands work with "please"
```

### Frontend tests

```text
show event makes trashcan visible
hide event hides trashcan
grabbed dismissible surface over trashcan arms drop zone
grabbed non-dismissible surface does not arm drop zone
release over armed trashcan triggers dismiss
release outside trashcan does not dismiss
close trashcan does not dismiss anything
UI control stop hides trashcan and clears state
trashcan closes after successful dismiss
```

### Manual tests

```text
1. Start Merlin.
2. Say: "Merlin, show chatlog."
3. Say: "Merlin, let me control the UI."
4. Grab chatlog with hand.
5. Say: "Merlin, open trashcan."
6. Confirm trashcan appears.
7. Drag chatlog over trashcan.
8. Confirm trashcan opens/glows.
9. Release chatlog.
10. Confirm chatlog animates into trashcan.
11. Confirm chatlog disappears.
12. Confirm trashcan closes.
13. Say: "Merlin, show chatlog."
14. Confirm chatlog comes back and messages are still there.
15. Open trashcan again.
16. Say: "Merlin, close trashcan."
17. Confirm trashcan closes and no UI element disappears.
18. Say: "Merlin, I'm done with the UI."
19. Confirm cursor disappears and camera stops.
```

---

## First Version Scope

The first implementation should only support:

```text
voice open trashcan
voice close trashcan
trashcan appears/disappears
trashcan does not cancel current grab
drag chatlog over trashcan
release to dismiss/hide chatlog
trashcan closes after successful dismiss
underlying chatlog content remains safe
```

Do not support every possible surface yet.

Chatlog can be the first dismissible test surface.

But implement it through generic dismiss concepts where practical so future surfaces can reuse the behavior.

---

## Future Extensions

Later additions could include:

```text
multiple dismissible surfaces
smarter trashcan placement
auto-close timeout
sound effect
particle effect
snap/pull effect near trashcan
undo dismiss
minimize-to-dock instead of hide
different themed drop zones
archive shelf drop zone
pin board drop zone
```

Potential future command:

```text
Merlin, bring back last dismissed window
```

This would restore the last dismissed visible UI surface.

Not required for first version.

---

## Acceptance Criteria

This feature is successful when:

```text
trashcan can be opened by voice
trashcan can be closed by voice
trashcan is visually polished enough to feel intentional
currently grabbed chatlog can be dragged over trashcan
trashcan visibly reacts when armed
release over trashcan dismisses/hides chatlog
chatlog animates into trashcan
trashcan closes after dismiss
chatlog data remains available when reopened
close trashcan does not dismiss anything
UI control stop clears trashcan state
no persistent data is deleted
```

---

## Final Direction

The trashcan is not a delete feature.

It is a Merlin spatial UI cleanup tool.

The intended mental model:

```text
I can grab any visible Merlin UI element.
I can ask Merlin to open a trashcan.
I can throw the UI element away.
It disappears from my workspace.
The underlying data remains safe.
```

This gives Merlin a more natural, physical-feeling UI control system without adding risky destructive gestures.
