---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/ExternalOpenOverlayAndAnimation.md
related_features:
  - External App Control
status: future
ready_for_agent: false
---

## Plan Status

Status: future
Ready for agent use: no
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: [[External App Control]]
Related architecture: [[System Architecture Overview]]
Related code atlas: [[Code Atlas Index]]
Original source: `Merlin.ToDo/ExternalOpenOverlayAndAnimation.md`

# External Open Overlay And Animation Plan

## Goal

Implement a full external-open UX for Merlin.

When the user asks Merlin to open a website, application, file, or folder, Merlin should not feel like it simply triggered a plain system launch. Instead:

1. Merlin detects the external open action.
2. Merlin enters a tool/action visual state.
3. Merlin shows a target-specific visual tile near the orb.
4. Merlin visually pulls/drags that tile outward.
5. The real website/app/file/folder opens.
6. Merlin shrinks into a compact always-on-top overlay so the opened thing becomes the primary focus.
7. Merlin remains available for listening, speaking, and status while staying out of the way.

This should be implemented as a complete feature, not a throwaway prototype.

---

## Important UX Principles

1. The opened thing must become the main focus.
2. Merlin must not remain as a large foreground window after opening an external target.
3. Merlin should remain present as a small overlay.
4. Animation should feel intentional and tactile.
5. Animation should be short, not like a slow cutscene.
6. Target animation duration: roughly `600ms` to `1200ms`.
7. Compact overlay should not block important parts of the opened app/browser.
8. Speech should remain short during external opens.
9. If launch fails, Merlin should not enter compact overlay.
10. If confirmation or disambiguation is required, do not play the pull-open animation yet.

---

## Target User Examples

### Website

User says:

```text
open facebook.com
```

Expected behavior:

1. Intent routes to external open URL.
2. Orb enters tool/action state.
3. Browser/page tile appears near orb.
4. Orb pulls tile toward screen edge.
5. Browser opens.
6. Merlin shrinks into compact overlay.
7. Merlin says something short, such as:

```text
Opening the website for you, sir.
```

### Application

User says:

```text
open notepad
```

Expected behavior:

1. Intent routes to external open application.
2. Orb enters tool/action state.
3. Generic app/window tile appears.
4. Orb pulls/expands tile outward.
5. App opens.
6. Merlin shrinks into compact overlay.
7. Merlin says something short, such as:

```text
Opening the app for you, sir.
```

---

## Implementation Philosophy

Do not only implement a simple visual demo.

This feature needs proper:

1. Backend event contract.
2. Frontend visual event handling.
3. Compact overlay mode.
4. Window mode changes.
5. Animation system.
6. Error handling.
7. Confirmation handling.
8. Voice command exits.
9. Logging.
10. Tests where practical.

The feature should be maintainable and extensible for future target types like folders, files, terminals, and custom app icons.

---

# Part 1: Discover Existing Code Structure

## 1.1 Inspect existing backend open flow

Search for existing open URL and open application code.

Likely areas:

```text
Merlin.Backend
Merlin.Backend/WebSocket
Merlin.Backend/Models
Merlin.Backend/CommandRouter
Merlin.Backend/Tools
Merlin.Backend/LocalTools
Merlin.Backend/Intent
```

Look for:

```text
open_url
open_application
Open URL
Open Application
ToolName
Intent
CommandRouter
WebSocketHandler
AssistantVisualEvent
```

Tasks:

* [ ] Find where URL opens are executed.
* [ ] Find where app opens are executed.
* [ ] Find where tool responses are created.
* [ ] Find where WebSocket events are sent to frontend.
* [ ] Find existing visual/audio/status event models.
* [ ] Find existing logs for tool execution timing.
* [ ] Find existing intent names and response objects.
* [ ] Document discovered files in implementation notes.

---

## 1.2 Inspect existing frontend state handling

Search in frontend.

Likely files:

```text
Merlin.Frontend/Scripts/Main.gd
Merlin.Frontend/Scripts/CoreOrb3D.gd
Merlin.Frontend/Scripts/MerlinOrganism3D.gd
Merlin.Frontend/Scripts/MerlinWebSocketClient.gd
Merlin.OrbLab
```

Look for:

```text
listening
thinking
speaking
tool
state
orb
window
WebSocket
visual
animation
tween
```

Tasks:

* [ ] Find main app state management.
* [ ] Find orb visual state management.
* [ ] Find WebSocket event parsing.
* [ ] Find where speech/listening/thinking states are applied.
* [ ] Find how window size/position is controlled.
* [ ] Find whether Godot window flags are already used.
* [ ] Find whether OrbLab mirrors frontend scripts.
* [ ] Document discovered files in implementation notes.

---

# Part 2: Backend Visual Event Contract

## 2.1 Add visual action event model

Create or extend a backend model for visual events.

Preferred event shape:

```json
{
  "type": "visual_action",
  "action": "external_open_started",
  "target_type": "website",
  "correlation_id": "guid-or-tool-run-id",
  "label": "facebook.com"
}
```

Completion event:

```json
{
  "type": "visual_action",
  "action": "external_open_completed",
  "target_type": "website",
  "correlation_id": "same-id",
  "success": true,
  "label": "facebook.com"
}
```

Failure event:

```json
{
  "type": "visual_action",
  "action": "external_open_completed",
  "target_type": "website",
  "correlation_id": "same-id",
  "success": false,
  "label": "facebook.com",
  "error_code": "launch_failed"
}
```

Tasks:

* [ ] Add `AssistantVisualEvent` model if not already present.
* [ ] Add `VisualAction` or equivalent enum/constants.
* [ ] Add `ExternalOpenStarted`.
* [ ] Add `ExternalOpenCompleted`.
* [ ] Add `TargetType` values:

  * [ ] `website`
  * [ ] `application`
  * [ ] `file`
  * [ ] `folder`
  * [ ] `unknown`
* [ ] Include `CorrelationId`.
* [ ] Include safe display `Label`.
* [ ] Include `Success` for completion.
* [ ] Include optional `ErrorCode`.
* [ ] Ensure JSON serialized field names match frontend expectations.

---

## 2.2 Do not expose sensitive launch details

The frontend does not need full paths, arguments, private URLs with tokens, or internal tool data.

Tasks:

* [ ] For websites, send a display label only, such as domain or sanitized URL.
* [ ] For apps, send safe app label, such as `notepad`.
* [ ] For files/folders, send safe display name only, not necessarily full path.
* [ ] Strip query strings from URLs unless intentionally needed.
* [ ] Avoid exposing tokens or auth parameters.
* [ ] Add helper method for `BuildSafeExternalOpenLabel(...)`.

Example:

```csharp
private static string BuildSafeExternalOpenLabel(string rawTarget, ExternalOpenTargetType targetType)
{
    // website: domain only if possible
    // app: friendly app name
    // file/folder: filename or folder name only
    // fallback: short sanitized label
}
```

---

## 2.3 Add target type detection

Map intent/tool result to target type.

Rules:

```text
Intent open_url => website
Intent open_application => application
File open => file
Folder open => folder
Fallback => unknown
```

Tasks:

* [ ] Add `DetectExternalOpenTargetType(...)`.
* [ ] Detect URL opens as `website`.
* [ ] Detect application opens as `application`.
* [ ] Prepare structure for future file/folder support.
* [ ] Keep unknown fallback.
* [ ] Make detection resilient to different casing:

  * [ ] `open_url`
  * [ ] `Open URL`
  * [ ] `open_application`
  * [ ] `Open Application`

---

# Part 3: Backend Timing And Launch Flow

## 3.1 Prefer start event before or during launch

The frontend needs to become always-on-top before the newly opened app steals focus.

Use this flow:

```text
intent recognized as external open
send external_open_started visual event
execute launch
send external_open_completed visual event with success/fail
send short speech/status response
```

Tasks:

* [ ] Send `external_open_started` before launching when possible.
* [ ] If existing architecture only knows after tool success, refactor enough to send a pre-launch visual event.
* [ ] Ensure the frontend can start animation immediately.
* [ ] Ensure the launched app/browser does not hide Merlin before animation starts.
* [ ] Send `external_open_completed` after launch result is known.

---

## 3.2 Correlation IDs

Each external open action needs a correlation ID.

Tasks:

* [ ] Generate correlation ID per external-open action.
* [ ] Use same correlation ID in started and completed events.
* [ ] Include correlation ID in logs.
* [ ] Include correlation ID in frontend animation tracking.
* [ ] Ignore stale completion events if correlation ID no longer matches active animation.

---

## 3.3 Launch delay option

Support a tiny launch delay if needed.

Default behavior:

```text
send start event
launch immediately
```

Optional setting:

```text
send start event
wait 250-400ms
launch
```

Tasks:

* [ ] Add a configurable launch visual lead time.
* [ ] Default to `0ms` or very small value.
* [ ] Allow later tuning to `250ms`.
* [ ] Do not make commands feel sluggish.
* [ ] Document setting.

Possible config:

```json
{
  "ExternalOpenVisualLeadMs": 0
}
```

or:

```csharp
private const int ExternalOpenVisualLeadMs = 0;
```

---

## 3.4 Confirmation and ambiguity handling

Do not play pull-open animation until the action is actually confirmed.

Tasks:

* [ ] If app is ambiguous, do not send external open started event.
* [ ] If URL fallback requires confirmation, do not send external open started event.
* [ ] If user cancels, do not send external open event.
* [ ] After user confirms and launch begins, send external open started event.
* [ ] Add tests or manual test cases for confirmation-required flows.

---

## 3.5 Failure handling

If launch fails:

```text
external_open_started was sent
launch fails
send external_open_completed success=false
frontend plays error pulse
frontend returns to previous/full state
frontend does not enter compact overlay
```

Tasks:

* [ ] Send failure completion event.
* [ ] Include `success=false`.
* [ ] Include safe error code.
* [ ] Keep speech short.
* [ ] Do not enter compact overlay on failure.
* [ ] Log error details backend-side, but do not expose sensitive details to frontend.

---

# Part 4: Speech Coordination

## 4.1 Keep external open speech short

External open speech should not be long.

Use simple lines:

Website:

```text
Opening the website for you, sir.
```

Application:

```text
Opening the app for you, sir.
```

Folder:

```text
Opening the folder for you, sir.
```

File:

```text
Opening the file for you, sir.
```

Unknown:

```text
Opening it for you, sir.
```

Tasks:

* [ ] Ensure open URL responses use short speech.
* [ ] Ensure open app responses use short speech.
* [ ] Do not include full long target names unless necessary.
* [ ] Do not say things like “the model is taking longer”.
* [ ] Do not let this feature interrupt active speech.
* [ ] Continue mouth/audio visualization in compact overlay if speaking.

---

# Part 5: Frontend State Model

## 5.1 Add or formalize frontend states

Add states if missing:

```text
normal
listening
thinking
speaking
tool
external_open_animation
compact_overlay
error
```

Tasks:

* [ ] Find existing state enum/string handling.
* [ ] Add `external_open_animation`.
* [ ] Add `compact_overlay`.
* [ ] Ensure state transitions are centralized.
* [ ] Avoid scattered boolean flags where possible.
* [ ] Ensure existing listening/thinking/speaking behavior still works.
* [ ] Ensure compact overlay can also show speaking/listening sub-states.

Recommended structure:

```text
Primary mode:
- full
- compact_overlay

Activity state:
- idle
- listening
- thinking
- speaking
- tool
- external_open_animation
- error
```

This is better than making every possible combination one flat state.

Tasks:

* [ ] If practical, separate layout mode from activity state.
* [ ] If not practical, use existing state approach but keep transitions clear.

---

## 5.2 Add state transition methods

Potential methods:

```gdscript
func play_external_open_animation(target_type: String, label: String, correlation_id: String) -> void
func complete_external_open_animation(correlation_id: String, success: bool) -> void
func enter_compact_overlay_mode() -> void
func exit_compact_overlay_mode() -> void
func set_activity_state(state: String) -> void
func set_layout_mode(mode: String) -> void
```

Tasks:

* [ ] Add clear method for starting external open animation.
* [ ] Add clear method for completing external open animation.
* [ ] Add compact overlay enter method.
* [ ] Add compact overlay exit method.
* [ ] Ensure methods are safe to call repeatedly.
* [ ] Guard against duplicate events.
* [ ] Guard against stale correlation IDs.

---

# Part 6: Frontend WebSocket Event Handling

## 6.1 Parse visual action events

Update WebSocket handling to recognize:

```json
{
  "type": "visual_action",
  "action": "external_open_started"
}
```

and:

```json
{
  "type": "visual_action",
  "action": "external_open_completed"
}
```

Tasks:

* [ ] Update `MerlinWebSocketClient.gd` or equivalent.
* [ ] Parse `type`.
* [ ] Parse `action`.
* [ ] Parse `target_type`.
* [ ] Parse `correlation_id`.
* [ ] Parse `label`.
* [ ] Parse `success`.
* [ ] Parse `error_code`.
* [ ] Route started event to `play_external_open_animation`.
* [ ] Route completed event to `complete_external_open_animation`.
* [ ] Log unknown visual actions without crashing.
* [ ] Ignore malformed events safely.

---

## 6.2 Event ordering resilience

Events may arrive close together.

Tasks:

* [ ] If completion arrives before animation visually finishes, store completion result and finish animation cleanly.
* [ ] If animation finishes before completion arrives, hold in pending state briefly.
* [ ] Add timeout fallback so Merlin is not stuck forever.
* [ ] Suggested timeout: `3000ms` to `5000ms`.
* [ ] On timeout after started event, either enter compact overlay if launch likely happened or return normal depending on available result.
* [ ] Prefer using completion success if available.

Recommended behavior:

```text
Started event:
- begin animation
- set active correlation id
- set completion unknown

Completed success=true:
- store success
- if minimum animation done, enter compact overlay

Completed success=false:
- store failure
- play error pulse
- return full mode

Animation done:
- if success known true, enter compact overlay
- if success known false, return full/error
- if success unknown, wait briefly for completion
```

---

# Part 7: Compact Overlay Mode

## 7.1 Visual design

Compact overlay should:

1. Be small.
2. Be always-on-top.
3. Use black or near-black background.
4. Render the orb smaller than normal.
5. Not cover too much of the screen.
6. Keep listening/speaking visuals visible.
7. Feel like Merlin is still present but not blocking the opened app.

Suggested dimensions:

```text
Desktop: 220x220 or 260x220
Orb scale: 0.55 to 0.65 of full view
Background: black or very dark translucent panel
Border: subtle or none
Position: fixed corner/edge for first complete version
```

Tasks:

* [ ] Add compact overlay layout.
* [ ] Resize Godot window.
* [ ] Move window to fixed corner.
* [ ] Set always-on-top.
* [ ] Optionally set borderless.
* [ ] Set dark background/viewbox.
* [ ] Scale orb down.
* [ ] Keep orb centered in compact view.
* [ ] Ensure speaking animation still works.
* [ ] Ensure listening animation still works.
* [ ] Ensure thinking/tool animation still works.
* [ ] Add expand affordance if frontend UI supports it.
* [ ] Add click/tap affordance to return to full mode if practical.

---

## 7.2 Window behavior

Use Godot window APIs.

Tasks:

* [ ] Find current Godot version and correct APIs.
* [ ] Implement always-on-top flag.
* [ ] Implement resize.
* [ ] Implement position.
* [ ] Implement borderless/minimal frame if appropriate.
* [ ] Store previous full window size.
* [ ] Store previous full window position.
* [ ] Restore full window size on exit.
* [ ] Restore full window position on exit.
* [ ] Avoid losing input handling after resizing.
* [ ] Test on Windows.

Potential Godot 4 style APIs may include:

```gdscript
DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
DisplayServer.window_set_size(Vector2i(260, 220))
DisplayServer.window_set_position(Vector2i(x, y))
```

Verify against the actual project version before using.

---

## 7.3 Overlay placement

First full version should use fixed placement, not foreground window tracking.

Preferred order:

1. Bottom-right.
2. Top-right if bottom-right blocks taskbar or important area.
3. Configurable later.

Tasks:

* [ ] Determine screen usable size.
* [ ] Place overlay with margin, for example `24px`.
* [ ] Avoid taskbar if possible.
* [ ] Keep placement stable.
* [ ] Add constants/config values:

  * [ ] overlay width
  * [ ] overlay height
  * [ ] overlay margin
  * [ ] overlay corner
  * [ ] orb compact scale

Example:

```gdscript
const COMPACT_OVERLAY_SIZE := Vector2i(260, 220)
const COMPACT_OVERLAY_MARGIN := 24
const COMPACT_ORB_SCALE := 0.60
```

---

## 7.4 Exit compact overlay

Exit paths:

Voice commands:

```text
Merlin come back
show full view
return to full view
expand Merlin
```

UI paths:

```text
click/tap expand affordance
possibly double-click compact overlay
```

Tasks:

* [ ] Add local intent route for “Merlin come back”.
* [ ] Add local intent route for “show full view”.
* [ ] Add local intent route for “return to full view”.
* [ ] Add local intent route for “expand Merlin”.
* [ ] Frontend exits compact overlay.
* [ ] Restore previous full size/position.
* [ ] Disable always-on-top if normal Merlin should not remain always-on-top.
* [ ] Restore normal background.
* [ ] Restore full orb scale.
* [ ] Confirm speech/listening still works after restoring.

---

# Part 8: Pull/Open Animation System

## 8.1 General animation requirements

Animation should be:

1. Short.
2. Nonblocking.
3. Deterministic.
4. Asset-light.
5. Generated from simple UI shapes or meshes.
6. Different per target type.
7. Safe if interrupted.
8. Cleaned up after completion.

Tasks:

* [ ] Create animation container node.
* [ ] Create generated tile nodes.
* [ ] Create generated tether/trail node.
* [ ] Use Tween for first complete implementation.
* [ ] Do not rely on external image assets.
* [ ] Clean up tile/trail after animation.
* [ ] Make animation callable by target type.
* [ ] Make animation safe to call while previous one is finishing.
* [ ] Cancel or finish previous animation cleanly before starting another.

---

## 8.2 Website animation

For URL opens, use a browser/page tile, not a folder.

Visual elements:

1. Small rounded browser card.
2. Top bar.
3. Tiny address/content line shapes.
4. Optional tiny globe/dot icon.
5. Tether/light streak from orb to tile.
6. Tile gets pulled outward.
7. Tile scales/fades as browser opens.
8. Merlin enters compact overlay after success.

Tasks:

* [ ] Implement `create_website_tile(label: String)`.
* [ ] Draw browser card using generated Control nodes or 3D planes.
* [ ] Add top bar.
* [ ] Add minimal content lines.
* [ ] Add optional label, shortened if needed.
* [ ] Animate tile from near orb outward.
* [ ] Animate orb pulse/lean toward tile.
* [ ] Animate tether/trail.
* [ ] Fade/scale tile at end.
* [ ] Remove tile after animation.

---

## 8.3 Application animation

For application opens, use generic app/window tile.

Visual elements:

1. Window-like panel.
2. Title bar.
3. Small control dots/blocks.
4. Interior rectangle/lines.
5. Tile expands slightly like it is becoming a real app.
6. Tile moves outward.
7. Merlin enters compact overlay after success.

Tasks:

* [ ] Implement `create_application_tile(label: String)`.
* [ ] Draw app/window tile.
* [ ] Use label if short enough.
* [ ] Animate outward pull.
* [ ] Animate slight expansion.
* [ ] Animate fade/scale at end.
* [ ] Remove tile after animation.

---

## 8.4 Folder animation

For future folder actions, implement folder tile.

Do not use folder tile for websites.

Tasks:

* [ ] Implement `create_folder_tile(label: String)`.
* [ ] Draw folder-like shape.
* [ ] Use for target type `folder`.
* [ ] Animate outward pull.
* [ ] Remove after animation.

---

## 8.5 File animation

For future file actions, implement file/document tile.

Tasks:

* [ ] Implement `create_file_tile(label: String)`.
* [ ] Draw file/page-like shape.
* [ ] Use for target type `file`.
* [ ] Animate outward pull.
* [ ] Remove after animation.

---

## 8.6 Unknown target animation

Fallback to generic tile.

Tasks:

* [ ] Implement `create_unknown_tile(label: String)`.
* [ ] Use generic panel shape.
* [ ] Animate outward pull.
* [ ] Remove after animation.

---

## 8.7 Orb motion during animation

Orb should feel like it performs the action.

Possible effects:

1. Slight lean toward tile.
2. Short pulse.
3. Small squash/stretch.
4. Glow/tether.
5. Return to compact state.

Tasks:

* [ ] Add tool/action pulse to orb.
* [ ] Add short lean or translation toward tile.
* [ ] Avoid breaking existing speaking/listening animation.
* [ ] Ensure orb returns to valid transform after animation.
* [ ] Keep animation subtle.

---

## 8.8 Tether or trail

Add a thin tether, streak, or trail connecting orb and tile.

Tasks:

* [ ] Implement simple generated tether.
* [ ] Animate opacity.
* [ ] Animate length or endpoint if practical.
* [ ] Clean it up after animation.
* [ ] Keep it subtle.
* [ ] Do not require external assets.

---

# Part 9: Compact Overlay And Animation Coordination

## 9.1 Recommended transition timeline

Target timeline:

```text
0ms:
- external_open_started received
- Merlin sets always-on-top
- Merlin enters external_open_animation state

0-150ms:
- tile appears near orb
- orb pulses/leans

150-700ms:
- tile pulled outward
- tether/trail visible

300-900ms:
- real browser/app appears depending on backend timing

700-1100ms:
- tile fades/scales
- Merlin shrinks into compact overlay

After success:
- compact overlay active
```

Tasks:

* [ ] Ensure always-on-top happens immediately at animation start.
* [ ] Ensure tile animation starts without waiting for speech.
* [ ] Ensure launch completion can arrive at any point.
* [ ] Ensure compact overlay happens only on success.
* [ ] Ensure failure cancels compact transition.

---

## 9.2 Do not interrupt speech

If speech starts during animation:

* [ ] Continue speaking.
* [ ] Keep mouth/audio visualization active.
* [ ] Do not reset audio state when resizing window.
* [ ] Do not stop TTS playback because compact overlay starts.
* [ ] Avoid duplicate speech.

---

# Part 10: Backend-Frontend Contract Details

## 10.1 Final event fields

Use this final structure unless existing conventions require slight changes.

Started:

```json
{
  "type": "visual_action",
  "action": "external_open_started",
  "target_type": "website",
  "correlation_id": "5e4f1c3a-4c82-4eb2-a93e-2b5150ea2c6e",
  "label": "facebook.com"
}
```

Completed success:

```json
{
  "type": "visual_action",
  "action": "external_open_completed",
  "target_type": "website",
  "correlation_id": "5e4f1c3a-4c82-4eb2-a93e-2b5150ea2c6e",
  "success": true,
  "label": "facebook.com"
}
```

Completed failure:

```json
{
  "type": "visual_action",
  "action": "external_open_completed",
  "target_type": "website",
  "correlation_id": "5e4f1c3a-4c82-4eb2-a93e-2b5150ea2c6e",
  "success": false,
  "label": "facebook.com",
  "error_code": "launch_failed"
}
```

Tasks:

* [ ] Backend emits exactly this or compatible shape.
* [ ] Frontend handles exactly this or compatible shape.
* [ ] Add comments documenting field meanings.
* [ ] Add safe default handling for missing fields.

---

# Part 11: Logging And Diagnostics

## 11.1 Backend logs

Add logs for:

* [ ] External open detected.
* [ ] Target type.
* [ ] Safe label.
* [ ] Correlation ID.
* [ ] Visual started event sent.
* [ ] Launch started.
* [ ] Launch succeeded.
* [ ] Launch failed.
* [ ] Visual completed event sent.
* [ ] Total launch duration.

Example log fields:

```text
ExternalOpenCorrelationId
ExternalOpenTargetType
ExternalOpenLabel
ExternalOpenIntent
ExternalOpenToolName
ExternalOpenSuccess
ExternalOpenDurationMs
```

---

## 11.2 Frontend logs

Add logs for:

* [ ] Visual started event received.
* [ ] Visual completed event received.
* [ ] Target type.
* [ ] Correlation ID.
* [ ] Animation start.
* [ ] Animation finished.
* [ ] Compact overlay entered.
* [ ] Compact overlay exited.
* [ ] Failure/error pulse.
* [ ] Ignored stale event.
* [ ] Timeout waiting for completion.

---

# Part 12: Error And Edge Cases

## 12.1 Launch fails

Expected:

```text
Merlin plays error pulse
Merlin does not enter compact overlay
Merlin stays full size or returns to full size
Merlin says short failure response if appropriate
```

Tasks:

* [ ] Implement frontend failure animation.
* [ ] Keep error pulse short.
* [ ] Restore normal/full state.
* [ ] Ensure no stale compact flag remains.

---

## 12.2 Confirmation required

Expected:

```text
No pull-open animation
No compact overlay
Merlin asks confirmation/disambiguation
Only after confirmed launch starts should animation play
```

Tasks:

* [ ] Verify confirmation flows do not emit start event.
* [ ] Verify confirmed action emits start event.
* [ ] Verify cancellation emits no event.

---

## 12.3 Ambiguous application

Expected:

```text
Merlin asks which app
No pull animation yet
No compact overlay yet
```

Tasks:

* [ ] Test ambiguous app name.
* [ ] Ensure animation only starts after resolved app is launched.

---

## 12.4 Multiple open commands quickly

Expected:

```text
Do not crash
Do not leave duplicate tiles
Do not get stuck compact/full
Latest command wins or commands queue cleanly
```

Tasks:

* [ ] Add guard for active external open animation.
* [ ] Decide behavior:

  * [ ] queue
  * [ ] ignore duplicate while active
  * [ ] cancel previous and start new
* [ ] Prefer cancel previous and start new if simple.
* [ ] Clean up old nodes/tweens.
* [ ] Reset correlation ID correctly.

---

## 12.5 Browser/app opens slowly

Expected:

```text
Animation can finish
Compact overlay can still enter after success
Merlin does not wait forever visibly
```

Tasks:

* [ ] Completion event controls final state.
* [ ] Add timeout fallback.
* [ ] Do not block UI thread.
* [ ] Keep frontend responsive.

---

## 12.6 Speech active during launch

Expected:

```text
Speech continues
Orb visualization continues
Window resize does not cut off audio
```

Tasks:

* [ ] Test while Merlin is speaking.
* [ ] Ensure compact transition does not stop playback.
* [ ] Ensure audio visualizer remains connected.

---

# Part 13: Voice Commands To Return From Overlay

## 13.1 Local intent routing

Add local routing for:

```text
Merlin come back
come back Merlin
show full view
return to full view
expand Merlin
open full Merlin
```

Tasks:

* [ ] Add capability/intent for returning to full view.
* [ ] Route locally, not through deep model if possible.
* [ ] Send frontend event to exit compact overlay.
* [ ] Keep response short.
* [ ] Example response:

```text
I am back, sir.
```

---

## 13.2 Frontend event for full view

If backend controls this through WebSocket, add visual event:

```json
{
  "type": "visual_action",
  "action": "exit_compact_overlay"
}
```

Tasks:

* [ ] Backend sends event when local command recognized.
* [ ] Frontend handles event.
* [ ] Frontend restores previous full window state.

Alternative:

If the frontend itself handles a UI click, directly call:

```gdscript
exit_compact_overlay_mode()
```

---

# Part 14: Optional But Desired Full-Version Enhancements

These are not prototype-only extras. Implement them if they fit cleanly.

## 14.1 Draggable compact overlay

Tasks:

* [ ] Allow dragging overlay by grabbing empty background outside orb.
* [ ] Do not interfere with orb interaction.
* [ ] Clamp to screen bounds.
* [ ] Prevent dragging off-screen.

---

## 14.2 Remember overlay position

Tasks:

* [ ] Store compact overlay position in local config.
* [ ] Restore position on next compact overlay entry.
* [ ] If saved position is off-screen, reset to default corner.
* [ ] Add config migration/default.

---

## 14.3 Snap to corners

Tasks:

* [ ] On drag release, optionally snap to nearest corner.
* [ ] Keep margin.
* [ ] Make this configurable if easy.

---

## 14.4 Expand affordance

Tasks:

* [ ] Add small expand button or corner glyph.
* [ ] On click, exit compact overlay.
* [ ] Keep it subtle.
* [ ] Do not clutter orb.

---

# Part 15: Later Windows-Specific Enhancement

Do not block the core implementation on this unless it is already easy in the project.

Possible later improvement:

```text
Detect foreground window after launch
Get its bounds
Place Merlin overlay near upper-right/lower-right of that window
Avoid browser address bar/app controls
```

Tasks for later:

* [ ] Add Windows foreground window detection.
* [ ] Get active window bounds.
* [ ] Place overlay relative to opened window.
* [ ] Avoid title bar/address bar.
* [ ] Fallback to fixed corner.

This should be separated from the main implementation so it does not destabilize the feature.

---

# Part 16: Frontend Implementation Details

## 16.1 Suggested files

Likely files:

```text
Merlin.Frontend/Scripts/Main.gd
Merlin.Frontend/Scripts/CoreOrb3D.gd
Merlin.Frontend/Scripts/MerlinOrganism3D.gd
Merlin.Frontend/Scripts/MerlinWebSocketClient.gd
Merlin.OrbLab
```

Tasks:

* [ ] Keep responsibilities separated.
* [ ] WebSocket client should parse events, not own animation details.
* [ ] Main or visual controller should coordinate state transitions.
* [ ] Orb script should only handle orb-specific visual changes.
* [ ] Create a separate external-open visual controller if the current structure supports it.

Possible new file:

```text
Merlin.Frontend/Scripts/ExternalOpenVisualController.gd
```

Responsibilities:

* Create tiles.
* Create tether/trail.
* Run animation tweens.
* Track active correlation ID.
* Signal animation finished.
* Clean up nodes.

---

## 16.2 Suggested frontend methods

```gdscript
func handle_visual_action(event: Dictionary) -> void:
    match event.get("action", ""):
        "external_open_started":
            play_external_open_animation(
                event.get("target_type", "unknown"),
                event.get("label", ""),
                event.get("correlation_id", "")
            )
        "external_open_completed":
            complete_external_open_animation(
                event.get("correlation_id", ""),
                event.get("success", false)
            )
        "exit_compact_overlay":
            exit_compact_overlay_mode()
        _:
            push_warning("Unknown visual action: " + str(event))
```

---

## 16.3 Suggested animation controller state

```gdscript
var active_external_open_correlation_id: String = ""
var external_open_animation_running: bool = false
var external_open_animation_min_done: bool = false
var external_open_completion_known: bool = false
var external_open_completion_success: bool = false
```

Tasks:

* [ ] Use equivalent state tracking.
* [ ] Avoid stale completions.
* [ ] Reset after success/failure.
* [ ] Reset on manual full view exit.

---

# Part 17: Backend Implementation Details

## 17.1 Suggested files

Likely files:

```text
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend/Models/AssistantVisualEvent.cs
Merlin.Backend/CommandRouter
Merlin.Backend/Tools
Merlin.Backend/LocalTools
```

Possible new helper:

```text
Merlin.Backend/Services/ExternalOpenVisualEventService.cs
```

Responsibilities:

* Detect external open.
* Build safe label.
* Build target type.
* Send start/completion visual events.
* Log correlation IDs.

Tasks:

* [ ] Avoid duplicating JSON construction in multiple places.
* [ ] Use a typed model where possible.
* [ ] Keep WebSocket send logic consistent with existing event patterns.

---

## 17.2 Suggested backend helper methods

```csharp
private static bool IsExternalOpenIntent(string? intent, string? toolName)
{
    return string.Equals(intent, "open_url", StringComparison.OrdinalIgnoreCase)
        || string.Equals(intent, "open_application", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "Open URL", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "Open Application", StringComparison.OrdinalIgnoreCase);
}
```

```csharp
private static string DetectExternalOpenTargetType(string? intent, string? toolName, string? target)
{
    if (string.Equals(intent, "open_url", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "Open URL", StringComparison.OrdinalIgnoreCase))
    {
        return "website";
    }

    if (string.Equals(intent, "open_application", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "Open Application", StringComparison.OrdinalIgnoreCase))
    {
        return "application";
    }

    return "unknown";
}
```

Adjust to actual project types and naming.

---

# Part 18: Testing Plan

## 18.1 Manual tests

### Website success

* [ ] Say: `open facebook.com`
* [ ] Browser opens.
* [ ] Website tile appears.
* [ ] Tile is pulled outward.
* [ ] Merlin becomes compact overlay.
* [ ] Overlay is always-on-top.
* [ ] Overlay is small.
* [ ] Speech is short.
* [ ] Audio visualization still works.

### Application success

* [ ] Say: `open notepad`
* [ ] Notepad opens.
* [ ] App/window tile appears.
* [ ] Tile is pulled outward.
* [ ] Merlin becomes compact overlay.
* [ ] Overlay is always-on-top.
* [ ] Speech is short.

### Return to full view

* [ ] While compact, say: `Merlin come back`
* [ ] Merlin returns to full view.
* [ ] Window size and position restore.
* [ ] Always-on-top state restores to normal.
* [ ] Orb scale restores.

### Confirmation required

* [ ] Trigger an ambiguous app open.
* [ ] Merlin asks for clarification.
* [ ] No pull animation happens.
* [ ] No compact overlay happens.
* [ ] Confirm target.
* [ ] Pull animation happens after confirmation.

### Failure

* [ ] Try opening invalid app.
* [ ] Merlin does not enter compact overlay.
* [ ] Error pulse plays.
* [ ] Speech failure response is short.
* [ ] Logs show failure.

### Speech continuity

* [ ] Trigger external open while Merlin is speaking, if possible.
* [ ] Speech continues.
* [ ] Compact resize does not cut off playback.
* [ ] Mouth/audio visualization continues.

---

## 18.2 Backend tests

Add tests where the backend has test coverage.

Tasks:

* [ ] Test external open detection for `open_url`.
* [ ] Test external open detection for `open_application`.
* [ ] Test target type mapping.
* [ ] Test safe label generation for URL.
* [ ] Test safe label generation strips query tokens.
* [ ] Test no visual event emitted for confirmation-required response.
* [ ] Test failure emits completion with `success=false`.
* [ ] Test start and completion events share correlation ID.

---

## 18.3 Frontend tests or debug commands

If Godot automated tests are not practical, add debug/dev menu commands.

Tasks:

* [ ] Add dev command to simulate website external open started.
* [ ] Add dev command to simulate website external open completed success.
* [ ] Add dev command to simulate app external open started.
* [ ] Add dev command to simulate app external open completed success.
* [ ] Add dev command to simulate failure.
* [ ] Add dev command to enter compact overlay.
* [ ] Add dev command to exit compact overlay.

Example debug actions:

```text
debug_external_open_website
debug_external_open_app
debug_external_open_failure
debug_enter_compact_overlay
debug_exit_compact_overlay
```

---

# Part 19: Acceptance Criteria

The implementation is complete when all of these are true:

1. Saying `open facebook.com` opens the browser and plays a short website pull animation.
2. The website animation uses a browser/page tile, not a folder.
3. After successful launch, Merlin switches to compact overlay mode.
4. Compact overlay is small and always-on-top.
5. Compact overlay uses a dark/near-black background or viewbox.
6. The orb is scaled down to roughly `0.55` to `0.65`.
7. Existing speaking/listening visuals continue in compact overlay.
8. Saying `open notepad` or another app plays an app/window-style pull animation.
9. If confirmation is required, no pull animation plays until after confirmation succeeds.
10. If launch fails, Merlin stays in normal/full mode or returns to it.
11. If launch fails, Merlin plays a small error pulse.
12. If launch fails, Merlin does not enter compact overlay.
13. “Merlin come back” or “show full view” exits compact overlay.
14. Full window size and position are restored after exiting compact overlay.
15. Existing speech and timing logs still work.
16. Existing open URL/app behavior still works functionally.
17. No sensitive launch details are sent to the frontend.
18. Started and completed visual events use matching correlation IDs.
19. Stale or malformed visual events do not crash the frontend.
20. The implementation is documented enough for future file/folder/terminal animations.

---

# Part 20: Done Definition

This task is not done until:

* [ ] Backend emits visual events for external opens.
* [ ] Backend handles success and failure.
* [ ] Backend avoids emitting animation events for unresolved confirmations.
* [ ] Frontend parses visual events.
* [ ] Frontend plays website animation.
* [ ] Frontend plays app animation.
* [ ] Frontend has folder/file fallback tile support or prepared implementation.
* [ ] Frontend enters compact overlay after success.
* [ ] Frontend does not enter compact overlay after failure.
* [ ] Voice command exists to return to full view.
* [ ] Compact overlay restores correctly.
* [ ] Speech/listening visuals still work.
* [ ] Manual tests pass.
* [ ] Relevant logs are added.
* [ ] Code is kept clean and separated.
* [ ] No large unrelated refactor is mixed into this task.

---

# Part 21: Notes For Agent

Before editing code:

1. Inspect current architecture.
2. Reuse existing event and state patterns where possible.
3. Do not invent a totally separate communication path if WebSocket visual/status events already exist.
4. Keep backend launch logic and frontend animation logic separated.
5. Prefer typed models over raw anonymous JSON if the backend already uses models.
6. Prefer a dedicated frontend visual controller if Main.gd is already large.
7. Mirror changes in `Merlin.OrbLab` only if OrbLab shares or tests the orb visuals.
8. Keep all speech short for open actions.
9. Do not add filler phrases.
10. Do not say “the model is taking longer”.
11. Do not make the animation block the actual launch for too long.
12. Do not use folder visuals for websites.
13. Do not leak full paths or sensitive URLs to the frontend.
14. Make the feature robust, not just visually impressive.

---

# Part 22: Suggested Implementation Order

Use this order to reduce breakage while still implementing the full feature:

1. Discover backend open flow.
2. Discover frontend WebSocket and state flow.
3. Add visual event models.
4. Add backend external-open detection and correlation IDs.
5. Emit start/completion events around open URL/app launch.
6. Add frontend event parsing.
7. Add compact overlay mode.
8. Add exit compact overlay mode.
9. Add dev/debug commands for compact mode.
10. Add external open animation controller.
11. Add website tile animation.
12. Add app tile animation.
13. Add folder/file/unknown tile fallbacks.
14. Wire started event to animation.
15. Wire completed success to compact overlay.
16. Wire completed failure to error pulse/full mode.
17. Add voice commands to return to full mode.
18. Add logs.
19. Add backend tests.
20. Add frontend debug/manual tests.
21. Run full manual acceptance checklist.
22. Clean up code and comments.
23. Update project notes if relevant.

---

# Part 23: Final User Experience Target

When this is done, external opens should feel like this:

```text
User: open facebook.com

Merlin immediately reacts.
The orb enters a purposeful action state.
A small browser tile appears.
Merlin pulls it outward.
The browser opens.
Merlin shrinks into a small dark always-on-top overlay.
Merlin remains alive and available, but the browser is now clearly the main focus.
```

This should feel like Merlin caused the action, not like a detached operating system command happened in the background.
