---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/fixes_after_context_layer/merlin_fixes_enabled_by_active_surface_context_layer.md
classification: implementation-plan
related_features:
  - Active Surface Layer
status: current
imported_to_vault: true
---

# Merlin Fixes Enabled By The Active Surface / Context Layer

## Purpose

The Active Surface / Context Layer gives Merlin a clean runtime answer to this question:

```text
Where is the user currently operating?
```

Before this layer, Merlin had to guess intent mostly from text. That created brittle routing for short commands such as:

```text
pause
play
skip
fullscreen
click that
open this
stop
```

Those phrases are naturally ambiguous. They mean different things depending on whether the user is operating the Merlin dashboard, the Browser Workspace, a future Spotify widget, a future file browser, or an external app.

The new layer separates responsibilities:

```text
ActiveSurfaceService tells Merlin WHERE the user is operating.
Command normalizers identify WHAT the utterance likely means.
CommandRouter/tools decide HOW to execute.
Safety/confirmation decides WHETHER execution is allowed.
```

This document lists the fixes we can now implement cleanly because the active surface exists.

## Current V1 Surface Model

V1 supports:

```text
Dashboard
BrowserWorkspace
Unknown
```

Current surface capabilities include assistant playback, browser navigation, browser page actions, browser scrolling, and browser media actions.

Important behavior already enabled:

```text
Dashboard + "pause"
  -> assistant playback pause/control

BrowserWorkspace + "pause"
  -> browser media pause

Any surface + "pause the video"
  -> browser media pause

BrowserWorkspace + "pause your answer"
  -> assistant playback pause/control

Any surface + "stop"
  -> existing global interruption behavior
```

## Priority Overview

### P0 - Fix Soon

These affect daily usability and reduce confusion immediately.

1. Frontend Dashboard focus and surface restore.
2. Finish moving browser media phrases out of broad browser parser tables.
3. Add surface-aware media commands beyond pause/play.
4. Make browser pointer start/stop surface-aware.
5. Improve live utterance resume suppression for real surface commands.

### P1 - Next Layer Of Polish

These improve robustness and debugging.

1. Better active surface logs in WebSocket and browser state transitions.
2. Active surface diagnostics endpoint/status command.
3. Browser domain metadata cleanup and freshness rules.
4. Surface-aware command failure messages.
5. Surface-aware confirmation wording.

### P2 - Future Foundations

These prepare later phases without implementing them yet.

1. Site control profile entry point.
2. Motion/gesture active surface routing.
3. Spotify/music widget surface.
4. File browser surface.
5. External foreground app detection.

---

# Fix 1: Frontend Dashboard Focus And Surface Restore

## Problem

Browser Workspace can now set active surface to `BrowserWorkspace`, and closing the browser resets to `Dashboard` in the backend. However, the frontend does not yet explicitly tell the backend when the user has returned focus to the Merlin dashboard.

This leaves a gap:

```text
Browser opens
  -> surface becomes BrowserWorkspace

User manually returns to Merlin UI without closing browser
  -> backend may still think BrowserWorkspace is active
```

That can make ambiguous commands route incorrectly.

Example:

```text
User returns to Merlin dashboard
User says "pause"
Expected: pause Merlin speech
Possible stale result: pause browser media
```

## Desired Behavior

When the dashboard/main Merlin UI explicitly regains focus, frontend should send a surface update:

```text
Kind: Dashboard
SurfaceId: dashboard.main
DisplayName: Merlin Dashboard
Source: FrontendFocus
Confidence: 1.0
```

When Browser Workspace gains focus, it should remain or become:

```text
Kind: BrowserWorkspace
SurfaceId: browser.workspace.main
Source: BrowserWorkspace or FrontendFocus
```

## Implementation Notes

Add a small backend endpoint or WebSocket message:

```json
{
  "type": "active_surface.set",
  "kind": "dashboard",
  "source": "frontend_focus",
  "reason": "dashboard_focus"
}
```

Frontend should emit this when:

1. Main Merlin window receives focus.
2. Browser Workspace closes and frontend restores dashboard UI.
3. User exits browser mode via voice or UI.
4. Potentially when UI control mode starts on dashboard.

Do not persist this. It is runtime state only.

## Acceptance Criteria

```text
BrowserWorkspace active + dashboard focus event
  -> ActiveSurface = Dashboard

Dashboard focused + "pause"
  -> assistant playback control

Browser remains open in background + dashboard focused + "pause"
  -> assistant playback control
```

## Tests

Add tests around the backend message handler or WebSocket handler:

```text
active_surface.set dashboard updates ActiveSurfaceService
dashboard focus includes source FrontendFocus
invalid surface kind is rejected safely
browser active state is not required to set dashboard focus
```

---

# Fix 2: Fully Centralize Browser Media Phrases

## Problem

The new `BrowserMediaCommandNormalizer` exists, but browser media phrases historically lived in broader browser command lists too.

This can cause subtle duplication:

```text
WebDestinationParser knows about "click pause"
LiveUtteranceGate knows about browser commands
BrowserMediaCommandNormalizer also knows about "click pause"
```

That is better than before, but not clean enough long term.

## Desired Behavior

All browser media phrases should be owned by `BrowserMediaCommandNormalizer`.

Examples:

```text
pause video
pause the video
play video
resume video
fullscreen
go fullscreen
exit fullscreen
skip ad
overslaan
klik overslaan
click pause
click fullscreen
```

The parser should ask the normalizer first:

```text
normalizer.TryMatchExplicit(text)
normalizer.TryMatchAmbiguous(text, activeSurface)
```

Then map the result to:

```text
WebDestinationAction.CommonPageAction
CommonAction = pause_video / play_video / skip_ad / fullscreen / etc.
```

## Why This Matters

It stops routing behavior from being defined in several places.

When `pause` behaves incorrectly, there should be one place to inspect:

```text
BrowserMediaCommandNormalizer
```

Not:

```text
LiveUtteranceGate
WebDestinationParser
BrowserWorkspaceService
CommandRouter
Common page action scoring
```

## Implementation Notes

Keep non-media safe page actions in the browser parser:

```text
accept cookies
reject cookies
close popup
no thanks
```

Move media-only commands to normalizer:

```text
pause/play/mute/unmute/fullscreen/skip ad
```

Later, we can split `BrowserMediaCommandNormalizer` into a generic media normalizer and browser adapter, but do not do that yet.

## Acceptance Criteria

```text
"pause video" routes via BrowserMediaCommandNormalizer
"pause" on Dashboard does not become browser media
"pause" in BrowserWorkspace becomes browser media
"pause your answer" never becomes browser media
"skip ad" still works
"overslaan" still works
```

## Tests

Add or keep:

```text
BrowserMediaCommandNormalizerTests
WebDestinationParser explicit media tests
WebDestinationParser ambiguous media tests with Dashboard/BrowserWorkspace surfaces
LiveUtteranceGate tests for explicit assistant override
```

---

# Fix 3: Add Surface-Aware Media Commands Beyond Pause And Play

## Problem

Users naturally say:

```text
mute
unmute
next
previous
volume up
volume down
restart
rewind
fast forward
```

These are also surface-dependent.

Example:

```text
BrowserWorkspace active + "mute"
  -> mute browser video

Dashboard active + "mute"
  -> maybe no-op or assistant audio mute, depending product decision

Future Spotify active + "next"
  -> next song

BrowserWorkspace active + "next"
  -> maybe next video or next page, risky/ambiguous
```

## Desired Behavior

Add only the commands we can execute reliably in Browser Workspace V1:

```text
mute video
unmute video
click mute
click unmute
fullscreen
exit fullscreen
```

For ambiguous bare commands:

```text
mute
unmute
fullscreen
```

Resolve by active surface if BrowserWorkspace supports the capability.

Do not add `next` and `previous` yet unless browser host action support exists and tests are clear. Those can mean too many things.

## Implementation Notes

Add capabilities if needed:

```text
browser.media.mute
browser.media.unmute
browser.media.next
browser.media.previous
browser.media.volume_up
browser.media.volume_down
```

But for V1, it may be enough to reuse existing CommonPageAction values:

```text
mute_video
unmute_video
fullscreen
exit_fullscreen
```

Extend the normalizer with Dutch/STT variants where useful:

```text
geluid uit
mute
unmute
dempen
```

Only add Dutch phrases that are likely to show up in actual STT output.

## Acceptance Criteria

```text
BrowserWorkspace + "mute"
  -> browser media mute

Dashboard + "mute"
  -> not browser media

"mute the video"
  -> browser media mute from any surface

"fullscreen"
  -> browser fullscreen when BrowserWorkspace active

"exit fullscreen"
  -> browser exit fullscreen
```

## Tests

```text
BrowserMediaCommandNormalizer_MuteExplicit
BrowserMediaCommandNormalizer_MuteAmbiguousBrowserOnly
CommandRouter_MuteVideoRoutesToCommonAction
LiveUtteranceGate_BrowserWorkspaceMuteRoutesToCommandRouter
LiveUtteranceGate_DashboardMuteDoesNotRouteToBrowser
```

---

# Fix 4: Surface-Aware Browser Pointer Start/Stop

## Problem

Browser pointer commands currently depend on exact browser wording:

```text
start browser pointer
stop browser pointer
show browser pointer
hide browser pointer
```

But after active surface exists, the user should be able to use more natural commands when Browser Workspace is active:

```text
start pointer
stop pointer
turn on pointer
turn off pointer
hand control
stop hand control
```

These should not accidentally start browser pointer while dashboard UI control is active.

## Desired Behavior

```text
BrowserWorkspace active + "start pointer"
  -> start browser pointer

BrowserWorkspace active + "stop pointer"
  -> stop browser pointer

Dashboard active + "start pointer"
  -> no browser pointer, maybe dashboard UI pointer if supported

Dashboard active + "start browser pointer"
  -> explicit browser pointer command, allowed if browser open
```

## Implementation Notes

Create a small deterministic normalizer:

```text
BrowserPointerCommandNormalizer
```

Or add a scoped parser section in `WebDestinationParser` that accepts active surface.

Suggested capabilities:

```text
browser.pointer.start
browser.pointer.stop
```

Those are separate from page click/media capabilities.

## Acceptance Criteria

```text
BrowserWorkspace + "start pointer" starts browser pointer
BrowserWorkspace + "stop pointer" stops browser pointer
Dashboard + "start pointer" does not start browser pointer
Explicit "start browser pointer" still works from any surface if browser is open
```

## Tests

```text
CommandRouter_BrowserSurfaceStartPointer
CommandRouter_DashboardStartPointerIgnoredOrFallsThrough
LiveUtteranceGate_BrowserSurfaceStartPointerRoutesToCommandRouter
LiveUtteranceGate_StopPointerDoesNotBecomeGlobalStop
```

---

# Fix 5: Playback Resume Suppression For Real Surface Commands

## Problem

When Merlin is speaking and the user interrupts with a real command, the system must not treat it as a side comment and resume speech incorrectly.

Example:

```text
Merlin is speaking
User says "pause the video"
Browser video pauses
Merlin should not resume the previous spoken answer as if nothing happened
```

The active surface layer gives us a clean way to identify real surface commands.

## Desired Behavior

Surface commands should be decisive:

```text
browser.media.pause
browser.media.skip_ad
browser.page.click
browser.pointer.start
browser.pointer.stop
```

When routed, playback resume should be suppressed.

Assistant control commands should still control assistant playback:

```text
pause your answer
stop talking
continue your answer
```

## Implementation Notes

Current decisive gate behavior already helps this. Continue to route true surface commands as:

```text
LiveUtteranceGateDecisionKind.AcceptNewRequest
ShouldRouteToCommandRouter = true
```

Avoid classifying them as:

```text
AskClarification
SideComment
Resume
```

Add structured log:

```text
LiveUtteranceSurfaceDecision
```

Use it to confirm:

```text
DecisionKind = ActiveSurfaceCommand
RouteTarget = CommandRouter
CapabilityHint = browser.media.pause
```

## Acceptance Criteria

```text
Speaking + BrowserWorkspace + "pause"
  -> browser command routes, assistant does not auto-resume because it was a real command

Speaking + Dashboard + "pause"
  -> assistant playback pause/control

Speaking + BrowserWorkspace + "pause your answer"
  -> assistant playback pause/control
```

## Tests

```text
LiveUtteranceGate_BrowserSurfacePauseIsAcceptNewRequest
BargeInCoordinator_DecisiveAcceptNewRequestRaisesBackendVoiceRequest
BargeInCoordinator_SurfaceCommandSuppressesPlaybackResume
BargeInCoordinator_AssistantPauseStillClearsPlayback
```

---

# Fix 6: Active Surface Diagnostics Command

## Problem

When routing goes wrong, we need a quick answer:

```text
What surface does Merlin think is active right now?
```

Currently this requires reading logs.

## Desired Behavior

Add a command:

```text
what surface is active
what am i controlling
what do you think i am controlling
```

Merlin responds:

```text
You are controlling Browser Workspace.
```

Optionally include safe metadata:

```text
Domain: youtube.com
Title: Shakira - YouTube
```

Do not speak full URLs with sensitive queries.

## Implementation Notes

This can be a simple deterministic command in `CommandRouter`.

Do not call LLM.

Do not expose sensitive query strings.

Suggested response:

```text
Active surface is Browser Workspace, from browser workspace, confidence 1.0.
```

For user-facing speech, keep it shorter:

```text
You are controlling Browser Workspace.
```

## Acceptance Criteria

```text
Dashboard active + "what am I controlling"
  -> Merlin Dashboard

BrowserWorkspace active + "what am I controlling"
  -> Browser Workspace

Unknown active
  -> "I'm not sure what surface is active."
```

## Tests

```text
CommandRouter_SurfaceStatusDashboard
CommandRouter_SurfaceStatusBrowserWorkspace
CommandRouter_SurfaceStatusUnknown
SurfaceStatusDoesNotExposeQueryString
```

---

# Fix 7: Surface-Aware Failure Messages

## Problem

Failures are currently generic:

```text
I could not click that.
Browser is not open.
I could not do that.
```

With active surface, Merlin can be more precise.

## Desired Behavior

Examples:

```text
Dashboard active + "pause the video" + browser closed
  -> "The browser is not open."

BrowserWorkspace active + "pause" + no media control found
  -> "I could not find a video control on this page."

Dashboard active + "pause"
  -> assistant playback control, no browser error
```

## Implementation Notes

Do not over-explain.

Use active surface for choosing short error text, not for bypassing safety.

## Acceptance Criteria

```text
Browser media command failures mention page/video control
Dashboard assistant playback failures do not mention browser
Browser closed failures still say browser is not open
```

## Tests

```text
CommandRouter_BrowserMediaFailureMessage
CommandRouter_DashboardPauseNoBrowserFailure
CommandRouter_BrowserClosedExplicitVideoCommand
```

---

# Fix 8: Surface-Aware Confirmation Wording

## Problem

Confirmations can be generic or awkward:

```text
I need confirmation before clicking "that".
```

With active surface metadata, confirmations can be clearer:

```text
I need confirmation before clicking "Checkout" in the browser.
```

For future surfaces:

```text
I need confirmation before deleting "report.pdf" in File Browser.
```

## Desired Behavior

Use active surface display name and safe metadata in confirmation text.

Examples:

```text
BrowserWorkspace + risky click
  -> "I need confirmation before clicking Checkout in the browser."

Dashboard + dangerous app/tool action
  -> existing confirmation behavior
```

## Implementation Notes

Do not let surface context lower confirmation requirements.

Use it only to improve wording and logs.

`BrowserPageSafetyGuard` must remain the decision point for browser page actions.

## Acceptance Criteria

```text
Risky browser click still requires confirmation
Confirmation includes Browser Workspace context
Confirmed click still re-snapshots before action where applicable
```

## Tests

```text
BrowserPageSafetyGuardStillRequiresConfirmation
ConfirmationMessageIncludesSurface
ConfirmedBrowserClickStillRevalidatesPage
```

---

# Fix 9: Browser Domain Metadata As Entry Point For Site Profiles

## Problem

Generic browser control is limited. Sites like YouTube expose weird controls:

```html
data-title-no-tooltip="Pauzeren"
data-tooltip-title="Pauzeren (k)"
class="ytp-play-button"
```

We do not want generic browser control to become a pile of site-specific hacks.

Active surface metadata gives a clean future entry point:

```text
ActiveSurface = BrowserWorkspace
Metadata.domain = youtube.com
```

## Desired Future Behavior

When BrowserWorkspace is active and domain is known:

```text
domain = youtube.com
```

Merlin can load a site profile:

```text
youtube.profile.json
```

That profile can know:

```text
pause selector candidates
skip ad selector candidates
play selector candidates
fullscreen selector candidates
```

## Implementation Notes

Do not implement site profiles yet unless explicitly requested.

Prepare only by keeping metadata clean:

```text
domain
title
safe url without query string
```

Avoid storing user search queries in active surface state.

## Acceptance Criteria For Later

```text
BrowserWorkspace active on youtube.com
  -> site profile layer can activate

BrowserWorkspace active on generic site
  -> generic browser control only
```

## Future Tests

```text
SiteProfileResolver_UsesActiveSurfaceDomain
SiteProfileResolver_DoesNotUseQueryString
YouTubeProfile_PauseSelectorCandidates
```

---

# Fix 10: Motion And Gesture Routing By Active Surface

## Problem

Motion control currently has separate modes:

```text
UI motion control
Browser pointer control
```

Both use similar hand tracking and pinch logic, but they operate on different surfaces.

The active surface layer lets us formalize this:

```text
Dashboard active
  -> gestures target Merlin dashboard UI

BrowserWorkspace active
  -> gestures target Browser Workspace
```

## Desired Behavior

When hand tracking emits a pointer/motion event, the event router should ask:

```text
What surface is active?
What capabilities does it support?
```

Then:

```text
Dashboard + pinch
  -> dashboard UI action

BrowserWorkspace + pinch
  -> browser pointer click

BrowserWorkspace + pinch-hold vertical movement
  -> browser scroll
```

## Implementation Notes

Do not duplicate pinch thresholds.

The calibrated pinch logic should stay shared.

Surface routing should decide target, not gesture interpretation.

Suggested structure:

```text
VisionGestureEventRouter
  -> ActiveSurfaceService.Current
  -> Surface-specific gesture adapter
```

## Acceptance Criteria

```text
Same pinch threshold used for Dashboard and BrowserWorkspace
Active surface decides whether click is dashboard or browser
BrowserHost controls browser click location
Dashboard UI controls dashboard click location
```

## Tests

```text
VisionGestureRouter_DashboardSurfaceRoutesToUiControl
VisionGestureRouter_BrowserSurfaceRoutesToBrowserPointer
PinchCalibrationSharedAcrossSurfaces
BrowserPointerClickDoesNotUseBackendScreenCoordinates
```

---

# Fix 11: Cleaner Handling Of "Click That" And "Open This"

## Problem

Commands like:

```text
click that
open this
select that
```

need a target. Without pointing/motion context, they are ambiguous.

Active surface alone does not fully solve this, but it makes the future path clear.

## Desired Future Behavior

When BrowserWorkspace is active and pointer overlay has a current target:

```text
click that
  -> browser pointer click
```

When Dashboard is active and UI control has a current target:

```text
click that
  -> dashboard UI click
```

When no pointing context exists:

```text
click that
  -> ask clarification or say "I need you to point at something first."
```

## Implementation Notes

This should wait until motion control and target context are stable.

Do not route `click that` to random DOM matching.

Do not use OCR.

Add a future model:

```text
ActivePointingContext
SurfaceId
ScreenX/Y or surface-relative X/Y
LastTargetHint
UpdatedUtc
Confidence
```

## Acceptance Criteria For Later

```text
Browser pointer active + "click that"
  -> click current browser pointer location

No pointer context + "click that"
  -> clarification

Dashboard active + "click that"
  -> dashboard pointer click if dashboard pointer active
```

---

# Fix 12: External App Surfaces Later

## Problem

The plan intentionally does not implement external app detection yet. But active surface makes it possible later.

Future examples:

```text
Spotify surface
Steam surface
Discord surface
WhatsApp surface
FileBrowser surface
```

## Desired Future Behavior

Foreground/focused app or trusted Merlin widget can set:

```text
ActiveSurface = Spotify
Capabilities = media play/pause/next/previous
```

Then:

```text
Spotify active + "pause"
  -> Spotify pause

Discord active + "send"
  -> Discord send, probably confirmation-sensitive

FileBrowser active + "delete this"
  -> destructive confirmation
```

## Implementation Notes

Do not add this until:

1. BrowserWorkspace and Dashboard routing are stable.
2. Motion control target context exists.
3. Confirmation wording is surface-aware.

External apps should probably have lower confidence than explicit Merlin surfaces unless focus detection is very reliable.

## Safety Notes

Future external surfaces must not bypass:

```text
confirmation flow
destructive action checks
privacy/sensitive field checks
trusted app policies
```

---

# Fix 13: Command Router Context Object

## Problem

`AssistantRequest` now carries an optional `ActiveSurfaceSnapshot`, and `CommandRouter` falls back to `IActiveSurfaceService.Current`.

That works, but as routing grows, the request object may become overloaded.

## Desired Future Shape

Introduce:

```csharp
public sealed record CommandRoutingContext
{
    public required string UserText { get; init; }
    public required string CorrelationId { get; init; }
    public required ActiveSurfaceSnapshot ActiveSurface { get; init; }
    public string? CaptureId { get; init; }
    public string? InteractionSource { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
}
```

Keep `AssistantRequest` as the API/transport model.

Use `CommandRoutingContext` internally.

## Acceptance Criteria

```text
Existing WebSocket API remains compatible
CommandRouter receives active surface
Tools do not need direct mutable service access
Tests can inject surface snapshots easily
```

## Tests

```text
CommandRouterContext_FromAssistantRequest_UsesProvidedSurface
CommandRouterContext_NoProvidedSurface_UsesServiceCurrent
CommandRouterContext_NoService_UsesDashboardFallback
```

---

# Fix 14: Surface Freshness And Staleness Rules

## Problem

V1 does not implement surface timeout/staleness. That is fine for now, but stale surface state can eventually cause wrong routing.

Example:

```text
Browser crashes silently
ActiveSurface remains BrowserWorkspace
User says "pause"
Merlin tries browser pause
```

Some close/host-exit reset exists, but future surfaces will need freshness rules.

## Desired Behavior

Each surface update should have:

```text
UpdatedUtc
Source
Confidence
```

Later, routing can decide:

```text
If surface is stale and source was weak:
  fall back to Dashboard or Unknown
```

Do not add aggressive timeouts yet. Browser video control might remain active while the user watches for minutes.

## Implementation Notes

Potential future policy:

```text
Dashboard: never stale while app active
BrowserWorkspace: valid while host active
External foreground app: stale after 2-5 seconds without focus refresh
Pointer context: stale after 500-1500 ms
```

## Acceptance Criteria For Later

```text
BrowserWorkspace surface stays valid while host active
Host exit resets dashboard
External app surface expires if focus source stops reporting
```

---

# Fix 15: Surface-Aware Safety Auditing

## Problem

As surfaces grow, the risk level changes by surface.

Examples:

```text
BrowserWorkspace + "click buy now"
  -> confirmation

FileBrowser + "delete this"
  -> confirmation

Dashboard + "delete this memory"
  -> confirmation
```

Active surface can improve auditing, but should not be the safety decision alone.

## Desired Behavior

Safety logs should include:

```text
ActiveSurfaceKind
SurfaceId
Capability
Action
SafetyDecision
Risks
```

## Implementation Notes

Browser actions already use `BrowserPageSafetyGuard`.

Do not weaken it.

Future safety guards can be per surface:

```text
IBrowserPageSafetyGuard
IFileBrowserSafetyGuard
IExternalAppSafetyGuard
```

Common safety result shape may come later.

## Acceptance Criteria

```text
BrowserPageSafetyGuard still handles browser clicks
Surface metadata appears in safety logs
Confirmation still required for risky browser buttons
```

---

# Suggested Implementation Order

## Step 1

Add frontend dashboard focus event.

Reason:

```text
This closes the biggest stale-surface hole.
```

## Step 2

Fully centralize browser media phrases.

Reason:

```text
This prevents future phrase drift and makes debugging clean.
```

## Step 3

Add mute/unmute/fullscreen polish.

Reason:

```text
These are common browser media commands and fit the new model.
```

## Step 4

Make browser pointer start/stop surface-aware.

Reason:

```text
This makes motion control feel natural while avoiding accidental dashboard/browser confusion.
```

## Step 5

Add active surface diagnostics command.

Reason:

```text
This gives fast debugging during live testing.
```

## Step 6

Start site profile design only after motion control is stable.

Reason:

```text
Profiles need a reliable way for the user to correct wrong clicks by pointing.
```

---

# Non-Goals For This Fix Backlog

Do not implement these as part of the immediate cleanup:

```text
Steam surface
Discord surface
WhatsApp surface
Spotify widget surface
FileBrowser surface
external foreground app detection
Windows foreground app priority system
learned site profiles
learned app control profiles
OCR
new browser clicking behavior
new browser scroll behavior
new database tables
persistent memory of active surface
```

Those are future phases. The immediate goal is to stabilize the clean separation:

```text
surface -> normalizer -> router/tool -> safety
```

---

# Definition Of Done For The Cleanup Phase

The context layer cleanup is in good shape when these statements are true:

```text
Dashboard focus reliably sets ActiveSurface = Dashboard.
Browser open/focus reliably sets ActiveSurface = BrowserWorkspace.
Browser close/host-exit reliably resets ActiveSurface = Dashboard.
"pause" does the right thing on Dashboard vs BrowserWorkspace.
"pause your answer" always controls Merlin speech.
"pause the video" always controls browser media.
"stop" remains global interruption/control.
Browser media phrases live in BrowserMediaCommandNormalizer.
LiveUtteranceGate does not contain a giant browser phrase table.
CommandRouter logs active surface for routed commands.
Browser safety and confirmation remain unchanged.
Tests cover Dashboard, BrowserWorkspace, and Unknown for ambiguous commands.
```

Once those are true, Merlin has a solid base for browser motion control, site profiles, and later app-specific control without piling hacks into the live utterance layer.
