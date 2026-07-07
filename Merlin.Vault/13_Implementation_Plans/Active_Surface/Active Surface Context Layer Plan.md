---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/surface_context_layer/merlin_active_surface_context_layer_plan.md
related_features:
  - Active Surface Layer
status: current
ready_for_agent: true
---

## Plan Status

Status: current
Ready for agent use: yes
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: [[Active Surface Layer]]
Related architecture: [[Active Surface Architecture]]
Related code atlas: [[ActiveSurfaceService]]
Original source: `Merlin.ToDo/surface_context_layer/merlin_active_surface_context_layer_plan.md`

# Active Surface Context Layer Plan

## Purpose

Merlin currently has a routing ambiguity problem.

A phrase like:

```text
pause
```

can mean different things depending on what the user is currently doing:

```text
Dashboard active        → pause Merlin speech / assistant playback
Browser Workspace      → pause YouTube / browser media
Spotify widget active  → pause Spotify
Game active            → pause game/media, or possibly do nothing
File browser active    → maybe irrelevant, maybe pause file preview
Discord active         → maybe pause media attachment, not assistant speech
```

The Live Utterance Gate should not be forced to infer all of this from wording alone. It needs a separate layer that tells Merlin **which surface the user is currently operating in**, what capabilities that surface supports, and how ambiguous commands should be resolved.

This document proposes a dedicated **Active Surface / Context Layer** for Merlin.

The goal is to create a clean foundation for:

1. Browser Workspace commands.
2. Dashboard / Merlin UI commands.
3. App-specific control such as Steam, Discord, WhatsApp, Spotify, File Explorer, etc.
4. Future gesture and pointing workflows.
5. Future learned per-app and per-site control profiles.
6. Safer command routing with fewer phrase-specific hacks.

---

## Current Problem

The immediate issue came from commands such as:

```text
pause
click pause
pause the video
skip ad
fullscreen
click overslaan
```

Without knowing whether Browser Workspace is active, Merlin may route a browser/media phrase to assistant playback control.

The temporary patch would be:

```text
If phrase explicitly mentions browser/media/video/ad/fullscreen:
    route to CommandRouter / Browser Workspace
Else if phrase is plain "pause":
    treat as assistant playback pause
```

That is safer than the current behavior, but it is not the correct long-term architecture. It would lead to an expanding set of fragile phrase checks:

```text
video
ad
fullscreen
browser
YouTube
pause button
skip button
Spotify
Discord
Steam
file
folder
message
send
upload
```

Every new app would create more routing exceptions.

The deeper missing concept is:

```text
Where is the user operating right now?
```

That should be represented explicitly.

---

## Proposed Concept

Introduce an **Active Surface Layer**.

Recommended core service name:

```csharp
IActiveSurfaceService
```

Alternative names:

```text
IMerlinContextModeService
IInteractionSurfaceService
IActiveWorkspaceService
```

Recommended term: **Active Surface**.

Reason: it is broader than “app”, “mode”, or “window”. A surface can be:

1. Merlin Dashboard.
2. Browser Workspace.
3. Spotify widget inside Merlin.
4. External foreground app.
5. File browser.
6. A floating overlay.
7. A learned control context.
8. A temporary pointing/correction target.

A surface is the current interactive area Merlin should interpret short commands against.

---

## Core Rule

The Live Utterance Gate should not execute surface-specific commands.

Instead, it should classify the utterance into a broad interaction class, then consult the Active Surface layer and CommandRouter.

Simplified routing:

```text
User utterance
    ↓
Live Utterance Gate
    ↓
Broad classification:
    - global assistant interruption
    - assistant playback control
    - active surface command
    - explicit app/browser command
    - correction
    - normal assistant request
    - side comment / garbage
    ↓
Active Surface Service
    ↓
Surface-aware routing decision
    ↓
CommandRouter / AssistantSpeechPlaybackService / ignore / clarification
```

---

## Default Surface

The default active surface should be:

```text
Dashboard
```

This means:

1. The user is interacting with Merlin itself.
2. Short commands like `pause` most likely refer to Merlin playback.
3. General questions and normal requests go through the assistant pipeline.
4. UI commands refer to Merlin's own UI unless explicitly targeted elsewhere.

The system should start in Dashboard mode unless another stronger signal is available.

---

## Surface Kinds

Initial enum proposal:

```csharp
public enum ActiveSurfaceKind
{
    Dashboard = 0,
    BrowserWorkspace = 1,
    SpotifyWidget = 2,
    FileBrowser = 3,
    Discord = 4,
    WhatsApp = 5,
    Steam = 6,
    Desktop = 7,
    ExternalApp = 8,
    Unknown = 99
}
```

This should probably start small in implementation:

```csharp
public enum ActiveSurfaceKind
{
    Dashboard,
    BrowserWorkspace,
    ExternalApp,
    Unknown
}
```

Then add additional kinds later.

Recommended approach:

1. Implement the layer with extensibility from day one.
2. Only wire Dashboard and Browser Workspace in the first PR.
3. Do not overbuild every app immediately.

---

## Surface Identity Model

A surface should not only have a kind. It should also include metadata.

Example model:

```csharp
public sealed record ActiveSurfaceSnapshot(
    ActiveSurfaceKind Kind,
    string SurfaceId,
    string DisplayName,
    double Confidence,
    ActiveSurfaceSource Source,
    DateTimeOffset UpdatedUtc,
    IReadOnlySet<string> Capabilities,
    IReadOnlyDictionary<string, string> Metadata
);
```

Example snapshots:

```json
{
  "kind": "Dashboard",
  "surfaceId": "dashboard.main",
  "displayName": "Merlin Dashboard",
  "confidence": 1.0,
  "source": "FrontendState",
  "capabilities": [
    "assistant.playback.pause",
    "assistant.playback.resume",
    "dashboard.ui.control",
    "assistant.chat"
  ]
}
```

```json
{
  "kind": "BrowserWorkspace",
  "surfaceId": "browser.workspace.main",
  "displayName": "Browser Workspace",
  "confidence": 1.0,
  "source": "FrontendFocus",
  "capabilities": [
    "browser.navigate",
    "browser.page.click",
    "browser.page.search",
    "browser.media.play",
    "browser.media.pause",
    "browser.media.fullscreen",
    "browser.media.skip_ad"
  ],
  "metadata": {
    "url": "https://youtube.com/...",
    "domain": "youtube.com",
    "pageTitle": "Example video - YouTube"
  }
}
```

---

## Active Surface Source

The active surface can be updated by different sources.

Suggested enum:

```csharp
public enum ActiveSurfaceSource
{
    StartupDefault,
    FrontendState,
    FrontendFocus,
    BrowserWorkspace,
    UserCommand,
    GestureFocus,
    PointerTarget,
    WindowsForegroundApp,
    CorrectionFlow,
    TimeoutFallback
}
```

Source matters because not every signal should have equal authority.

For example:

```text
Frontend says Browser Workspace is focused
→ high authority

Windows foreground app says Godot is focused
→ less useful because Merlin itself may be top-level

User says "control the browser"
→ high authority

User briefly alt-tabs to Discord
→ medium authority

No updates for 10 minutes
→ fallback to Dashboard or Unknown depending on state
```

---

## Surface Authority / Priority

Recommended priority order for choosing active surface:

```text
1. Explicit user command
   Example: "control the browser", "back to Merlin", "use Discord"

2. Merlin-owned focused workspace
   Example: Browser Workspace, Spotify widget, Dashboard panel

3. Gesture / pointer target
   Example: user points inside browser overlay or app region

4. Browser Workspace internal state
   Example: Browser Workspace is open and active, page has media

5. Windows foreground app
   Example: Discord, Steam, Explorer, Chrome

6. Default fallback
   Dashboard
```

This avoids making Windows foreground app the only source of truth.

Important: Merlin UI may be an always-on-top overlay, so Windows foreground focus alone can be misleading.

---

## Surface Capabilities

A surface should declare capabilities. This allows the router to ask:

```text
Can the current surface handle this kind of command?
```

Recommended capability names:

### Assistant / Dashboard

```text
assistant.chat
assistant.playback.pause
assistant.playback.resume
assistant.playback.stop
assistant.playback.cancel
assistant.memory.save
assistant.memory.search
dashboard.ui.control
dashboard.widget.move
dashboard.widget.resize
```

### Browser Workspace

```text
browser.navigate
browser.back
browser.forward
browser.refresh
browser.page.click
browser.page.search
browser.page.type
browser.page.scroll
browser.media.play
browser.media.pause
browser.media.stop
browser.media.seek
browser.media.fullscreen
browser.media.skip_ad
browser.tab.open
browser.tab.close
browser.tab.switch
```

### Spotify Widget

```text
music.play
music.pause
music.stop
music.next
music.previous
music.seek
music.volume.set
music.volume.up
music.volume.down
music.track.like
music.track.info
```

### File Browser

```text
file.open
file.select
file.rename
file.delete
file.copy
file.move
file.search
file.preview
folder.open
folder.create
```

### Discord / WhatsApp / Messaging Apps

```text
message.read
message.reply
message.send
message.search
message.open_conversation
message.attach_file
message.call.start
message.call.end
```

### Steam / Game Surface

```text
game.open
game.launch
game.close
game.overlay.open
game.media.pause
game.media.resume
game.input.forward
```

---

## Surface-Aware Routing Examples

### Example 1: Plain pause while Dashboard is active

```text
ActiveSurface = Dashboard
User: "pause"
```

Decision:

```text
assistant.playback.pause
```

Reason:

Dashboard owns assistant playback controls.

---

### Example 2: Plain pause while Browser Workspace is active

```text
ActiveSurface = BrowserWorkspace
User: "pause"
```

Decision:

```text
browser.media.pause
```

Reason:

Browser Workspace is active and supports browser media pause.

---

### Example 3: Explicit video phrase while active surface is uncertain

```text
ActiveSurface = Unknown
User: "pause the video"
```

Decision:

```text
CommandRouter → browser/media or active app media
```

Reason:

The phrase explicitly targets media/video, so it should not pause Merlin playback.

---

### Example 4: Explicit assistant phrase while Browser Workspace is active

```text
ActiveSurface = BrowserWorkspace
User: "pause your answer"
```

Decision:

```text
assistant.playback.pause
```

Reason:

Explicit assistant target overrides active surface.

---

### Example 5: Hard global stop

```text
ActiveSurface = BrowserWorkspace
User: "stop"
```

Decision:

```text
assistant/global interruption or cancel current Merlin operation
```

Reason:

Hard interruption commands must remain globally available.

Potential nuance:

```text
"stop the video" → browser.media.pause/stop
"stop talking"   → assistant.playback.stop
"stop"           → global interruption
```

---

### Example 6: Browser skip ad

```text
ActiveSurface = BrowserWorkspace
User: "skip ad"
```

Decision:

```text
browser.media.skip_ad
```

Dutch variants should also work:

```text
overslaan
sla advertentie over
skip advertentie
advertentie overslaan
klik overslaan
```

Reason:

Browser Workspace can handle page-aware media/ad controls.

---

### Example 7: File browser open command

```text
ActiveSurface = FileBrowser
User: "open that"
```

Decision:

```text
file.open selected/highlighted item
```

If no selected item exists:

```text
ask clarification or request pointing
```

---

## Live Utterance Gate Changes

The Live Utterance Gate should gain access to a snapshot of the active surface.

It should not become a huge app-command parser. Its job should remain lightweight and fast.

Recommended responsibilities:

1. Detect hard global interruptions.
2. Detect assistant-specific playback commands.
3. Detect explicit surface/app/browser/media commands.
4. Detect ambiguous short commands.
5. Detect correction/clarification utterances.
6. Detect side comments / garbage.
7. Produce a route recommendation, not execute complex tools.

Suggested model:

```csharp
public sealed record LiveUtteranceDecision(
    LiveUtteranceDecisionKind Kind,
    string? RouteTarget,
    string? CapabilityHint,
    bool SuppressPlaybackResume,
    string Reason
);
```

Possible decision kinds:

```csharp
public enum LiveUtteranceDecisionKind
{
    Ignore,
    GlobalInterrupt,
    AssistantPlaybackControl,
    ActiveSurfaceCommand,
    ExplicitSurfaceCommand,
    NormalAssistantRequest,
    Correction,
    AskClarification
}
```

The gate may return:

```json
{
  "kind": "ActiveSurfaceCommand",
  "routeTarget": "CommandRouter",
  "capabilityHint": "browser.media.pause",
  "suppressPlaybackResume": true,
  "reason": "Ambiguous pause resolved by active BrowserWorkspace surface."
}
```

---

## Routing Priority

Recommended voice routing priority:

```text
1. Hard global interruption / cancellation
   Examples:
   - stop
   - shut up
   - cancel
   - never mind
   - no no no

2. Explicit assistant playback control
   Examples:
   - pause yourself
   - stop talking
   - resume your answer
   - continue your answer

3. Explicit surface/app command
   Examples:
   - pause the video
   - skip the ad
   - click fullscreen
   - search in the browser
   - open Discord
   - send this in WhatsApp

4. Ambiguous command resolved by active surface
   Examples:
   - pause
   - play
   - click that
   - open this
   - close it

5. Normal assistant request
   Examples:
   - what does this mean?
   - explain this
   - write a prompt

6. Side comment / garbage
   Examples:
   - yeah
   - okay then
   - in the pool
   - random overheard TV speech
```

---

## Important Distinction: Global Stop vs Plain Pause

`pause` should not be treated the same as `stop`.

`stop` is often a safety-critical interruption.

`pause` is usually contextual.

Recommended behavior:

```text
"stop"
→ global Merlin interruption by default

"stop talking"
→ assistant playback stop

"stop the video"
→ active media/browser stop or pause

"pause"
→ active surface decides

"pause your answer"
→ assistant playback pause

"pause the video"
→ browser/media pause
```

---

## Browser Workspace Integration

Browser Workspace should become a first-class surface provider.

When Browser Workspace is opened, focused, clicked, or controlled by gesture, it should update ActiveSurfaceService.

Example:

```csharp
await activeSurfaceService.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
{
    Kind = ActiveSurfaceKind.BrowserWorkspace,
    SurfaceId = "browser.workspace.main",
    DisplayName = "Browser Workspace",
    Source = ActiveSurfaceSource.BrowserWorkspace,
    Confidence = 1.0,
    Capabilities = BrowserWorkspaceCapabilities.All,
    Metadata = new Dictionary<string, string>
    {
        ["url"] = currentUrl,
        ["domain"] = currentDomain,
        ["title"] = pageTitle
    }
});
```

When Browser Workspace closes:

```csharp
await activeSurfaceService.SetActiveSurfaceAsync(DashboardSurface.Default);
```

When Browser Workspace is open but not focused:

Options:

1. Keep it active until another Merlin-owned surface becomes active.
2. Downgrade confidence.
3. Keep active only if last user action was inside browser.

Recommended V1:

```text
If Browser Workspace is explicitly opened/focused, set active surface to BrowserWorkspace.
If user says "back to Merlin" or dashboard is focused, set Dashboard.
```

Avoid overcomplicating V1.

---

## Frontend Events

The Godot frontend should notify backend when major surfaces become active.

Suggested WebSocket event:

```json
{
  "type": "active_surface.changed",
  "surface": {
    "kind": "BrowserWorkspace",
    "surfaceId": "browser.workspace.main",
    "displayName": "Browser Workspace",
    "source": "FrontendFocus",
    "confidence": 1.0,
    "metadata": {
      "reason": "browser_window_focused"
    }
  }
}
```

Other examples:

```json
{
  "type": "active_surface.changed",
  "surface": {
    "kind": "Dashboard",
    "surfaceId": "dashboard.main",
    "displayName": "Merlin Dashboard",
    "source": "FrontendFocus",
    "confidence": 1.0
  }
}
```

```json
{
  "type": "active_surface.changed",
  "surface": {
    "kind": "SpotifyWidget",
    "surfaceId": "widget.spotify.main",
    "displayName": "Spotify Widget",
    "source": "FrontendFocus",
    "confidence": 1.0
  }
}
```

---

## Backend API

Suggested interface:

```csharp
public interface IActiveSurfaceService
{
    ActiveSurfaceSnapshot Current { get; }

    Task<ActiveSurfaceSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task SetActiveSurfaceAsync(
        ActiveSurfaceUpdate update,
        CancellationToken cancellationToken = default);

    Task ResetToDashboardAsync(
        string reason,
        CancellationToken cancellationToken = default);

    bool CurrentSupports(string capability);
}
```

Suggested update model:

```csharp
public sealed record ActiveSurfaceUpdate
{
    public required ActiveSurfaceKind Kind { get; init; }
    public required string SurfaceId { get; init; }
    public required string DisplayName { get; init; }
    public required ActiveSurfaceSource Source { get; init; }
    public required double Confidence { get; init; }
    public IReadOnlySet<string> Capabilities { get; init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
```

Thread safety:

```text
The service should be safe to read from voice routing and update from frontend/browser events concurrently.
```

A simple lock or immutable snapshot replacement is enough for V1.

---

## Persistence

V1 does not need long-term persistence for the active surface itself.

The active surface is runtime state.

However, future learned behavior should be persisted separately.

Do not store the current active surface as memory.

Recommended split:

```text
Runtime state:
- ActiveSurfaceService
- current surface snapshot
- current capabilities
- current metadata

Persistent learned control:
- control profile DB
- app/site/action mappings
- learned selectors
- learned corrections
- user preferences
```

---

## Future Control Profile DB Integration

This layer connects naturally to the previously discussed `memory_control_db` / control profile system.

For example:

```text
ActiveSurface = BrowserWorkspace
Domain = youtube.com
User says: "skip ad"
```

The router can query:

```text
Control profile for:
- surface kind: BrowserWorkspace
- app/site: youtube.com
- action: browser.media.skip_ad
```

Stored profile entry example:

```json
{
  "surfaceKind": "BrowserWorkspace",
  "scope": "site",
  "scopeKey": "youtube.com",
  "action": "browser.media.skip_ad",
  "selectorCandidates": [
    ".ytp-skip-ad-button",
    "button[aria-label*='Skip']",
    "button[aria-label*='Overslaan']"
  ],
  "textCandidates": [
    "Skip",
    "Skip ads",
    "Overslaan",
    "Advertentie overslaan"
  ],
  "lastConfirmedUtc": "2026-07-06T00:00:00Z",
  "confidence": 0.91
}
```

The active surface tells Merlin where to look. The control profile tells Merlin how to act there.

---

## Surface-Aware Corrections

Corrections should be contextual.

Example:

```text
User: "pause"
Merlin pauses its own speech.
User: "No, I meant pause the video."
```

The correction layer should be able to store:

```text
When ActiveSurface = BrowserWorkspace
and utterance = "pause"
prefer capability = browser.media.pause
```

But it should not globally rewrite `pause` to always mean browser pause.

Contextual correction entry example:

```json
{
  "utterancePattern": "pause",
  "surfaceKind": "BrowserWorkspace",
  "preferredCapability": "browser.media.pause",
  "scope": "surface",
  "confidence": 0.8,
  "source": "user_correction",
  "createdUtc": "2026-07-06T00:00:00Z"
}
```

For Dashboard:

```text
When ActiveSurface = Dashboard
"pause" still means assistant.playback.pause
```

This prevents corrections from becoming destructive global behavior.

---

## Dutch / Multilingual Considerations

The user is Dutch, but current STT may not reliably transcribe Dutch words such as:

```text
overslaan
advertentie overslaan
sla over
```

The Active Surface Layer does not solve STT accuracy directly, but it reduces the burden on phrase matching.

When Browser Workspace is active, even imperfect transcripts may be easier to interpret.

Examples:

```text
"overslan"
"over slaan"
"overslaan"
"skip ad"
"skip at"
"skip add"
"klik overslaan"
```

Browser Workspace active + ad/button context can make these route to:

```text
browser.media.skip_ad
```

Recommended V1 Dutch/media phrase aliases:

```text
overslaan
slaan over
sla over
advertentie overslaan
skip advertentie
skip ad
skip add
klik overslaan
click skip
```

But these aliases should be part of a browser/media command normalizer, not hardcoded everywhere.

---

## Safety Model

Active surface routing must integrate with the existing BrowserPageSafetyGuard and confirmation system.

Important principle:

```text
Active surface determines routing. It does not bypass safety.
```

Examples:

```text
"click pause" on YouTube
→ low risk, may execute directly

"click buy now"
→ risky, require confirmation

"delete this file"
→ risky, require confirmation

"send it" in Discord/WhatsApp
→ risky if message content is not explicit, require confirmation
```

Surface-aware routing should produce a capability/action request. Existing safety layers should still decide whether it can execute.

---

## Confirmation Behavior

For ambiguous commands, active surface may reduce ambiguity but should not remove all confirmations.

Examples:

```text
ActiveSurface = BrowserWorkspace
User: "pause"
→ safe direct browser.media.pause

ActiveSurface = FileBrowser
User: "delete this"
→ confirmation required

ActiveSurface = WhatsApp
User: "send it"
→ confirmation required unless message content and recipient are explicit and trusted behavior allows it

ActiveSurface = BrowserWorkspace
User: "click that"
→ if target is known and low risk, execute; otherwise ask user to point/confirm
```

---

## Timeout and Fallback

The active surface should not become stale forever.

Possible V1 behavior:

```text
Dashboard is default and never stale.
BrowserWorkspace remains active while open/focused.
If BrowserWorkspace closes, reset to Dashboard.
If no surface update for long time, keep current Merlin-owned surface if still open.
```

Possible V2 behavior:

```text
If surface is external app and no update for 10 minutes, downgrade confidence.
If confidence drops below threshold, route ambiguous commands more conservatively.
```

Recommended V1: keep it simple.

---

## Observability / Logging

Every routing decision should log the active surface snapshot.

Recommended log fields:

```text
Event: LiveUtteranceSurfaceDecision
UtteranceNormalized
DecisionKind
RouteTarget
CapabilityHint
ActiveSurfaceKind
ActiveSurfaceId
ActiveSurfaceSource
ActiveSurfaceConfidence
Reason
CorrelationId
TurnId
```

Example:

```json
{
  "event": "LiveUtteranceSurfaceDecision",
  "utterance": "pause",
  "decisionKind": "ActiveSurfaceCommand",
  "routeTarget": "CommandRouter",
  "capabilityHint": "browser.media.pause",
  "activeSurfaceKind": "BrowserWorkspace",
  "activeSurfaceId": "browser.workspace.main",
  "activeSurfaceSource": "FrontendFocus",
  "activeSurfaceConfidence": 1.0,
  "reason": "Ambiguous pause resolved by active surface capability."
}
```

This is essential for debugging.

---

## Testing Strategy

### Unit Tests: ActiveSurfaceService

Test cases:

```text
Default surface is Dashboard.
Set BrowserWorkspace updates current snapshot.
ResetToDashboard sets Dashboard.
Capabilities are exposed correctly.
Concurrent reads do not throw.
Invalid confidence is clamped or rejected.
Unknown surface is handled safely.
```

### Unit Tests: Live Utterance Gate

Test cases:

```text
Dashboard + "pause" → AssistantPlaybackControl
BrowserWorkspace + "pause" → ActiveSurfaceCommand browser.media.pause
Unknown + "pause" → AssistantPlaybackControl or AskClarification depending chosen policy
BrowserWorkspace + "pause your answer" → AssistantPlaybackControl
Dashboard + "pause the video" → ExplicitSurfaceCommand browser.media.pause
BrowserWorkspace + "skip ad" → ActiveSurfaceCommand browser.media.skip_ad
Dashboard + "stop" → GlobalInterrupt
BrowserWorkspace + "stop the video" → browser.media.pause/stop
BrowserWorkspace + "stop talking" → AssistantPlaybackControl
BrowserWorkspace + "yeah" → Ignore
```

### Integration Tests: Browser Workspace

Test cases:

```text
Opening Browser Workspace sets ActiveSurface to BrowserWorkspace.
Focusing Dashboard sets ActiveSurface to Dashboard.
Closing Browser Workspace resets to Dashboard.
Browser media phrase routes to CommandRouter.
Browser media action still passes BrowserPageSafetyGuard.
Confirmed browser click still re-snapshots page before action.
```

### Regression Tests

Protect existing behavior:

```text
"pause" still pauses Merlin when Dashboard is active.
"stop" still interrupts Merlin globally.
Side comments are still ignored.
Correction phrases still route to correction/clarification flow.
Playback resume suppression still works when a real command is detected.
```

---

## Suggested Implementation Phases

## Phase 1: Runtime Active Surface Foundation

Goal:

Create the runtime service and models. Default to Dashboard. No major routing changes yet.

Deliverables:

1. Add `ActiveSurfaceKind` enum.
2. Add `ActiveSurfaceSource` enum.
3. Add `ActiveSurfaceSnapshot` model.
4. Add `ActiveSurfaceUpdate` model.
5. Add `IActiveSurfaceService`.
6. Add in-memory thread-safe implementation.
7. Register service in DI.
8. Add unit tests.
9. Add minimal logging for surface changes.

Acceptance criteria:

```text
Backend starts with Dashboard as current surface.
Surface can be updated to BrowserWorkspace.
Surface can reset to Dashboard.
Current snapshot can be read from voice routing code.
Unit tests pass.
```

Do not yet change all utterance routing in this phase.

---

## Phase 2: Browser Workspace Surface Updates

Goal:

Make Browser Workspace update ActiveSurfaceService.

Deliverables:

1. When Browser Workspace opens/focuses, set active surface to BrowserWorkspace.
2. When Dashboard regains focus, set active surface to Dashboard.
3. When Browser Workspace closes, reset to Dashboard.
4. Include browser metadata when available:
   - current URL
   - domain
   - page title
5. Include Browser Workspace capabilities.
6. Add integration tests or service-level tests.

Acceptance criteria:

```text
Opening Browser Workspace changes active surface to BrowserWorkspace.
Closing it resets to Dashboard.
Logs show surface change events.
No browser command behavior changes yet except state tracking.
```

---

## Phase 3: Surface-Aware Live Utterance Routing

Goal:

Use active surface to resolve ambiguous commands such as `pause`.

Deliverables:

1. Inject `IActiveSurfaceService` into Live Utterance Gate or the routing layer immediately after the gate.
2. Add broad command classification for:
   - global interruption
   - assistant playback
   - explicit browser/media command
   - ambiguous active surface command
   - ignore
   - normal assistant request
3. Add routing logic:
   - Dashboard + `pause` → assistant playback pause
   - BrowserWorkspace + `pause` → browser media pause
   - explicit `pause the video` → browser/media route
   - explicit `pause your answer` → assistant playback
4. Ensure playback resume suppression is set correctly for surface commands.
5. Add unit tests.

Acceptance criteria:

```text
Plain pause still pauses Merlin on Dashboard.
Plain pause routes to browser media when Browser Workspace is active.
Video/ad/fullscreen phrases route to CommandRouter even when active surface is uncertain.
Global interruption behavior remains unchanged.
```

---

## Phase 4: Browser Media Command Normalizer

Goal:

Create a clean browser/media command normalization layer instead of scattering phrase checks.

Deliverables:

1. Add browser/media command normalizer.
2. Normalize phrases to capabilities:
   - `pause video` → `browser.media.pause`
   - `play video` → `browser.media.play`
   - `fullscreen` → `browser.media.fullscreen`
   - `skip ad` / `overslaan` → `browser.media.skip_ad`
3. Include English and Dutch variants.
4. Handle likely STT variants:
   - `skip add`
   - `overslan`
   - `over slaan`
5. Route normalized commands through CommandRouter.
6. Keep safety guard intact.

Acceptance criteria:

```text
Browser/media aliases are centralized.
Tests cover English and Dutch media phrases.
No duplicated phrase lists across gate/router/browser services.
```

---

## Phase 5: Active Surface CommandRouter Context

Goal:

Pass active surface context into CommandRouter so tools can make better decisions.

Deliverables:

1. Extend command routing context with active surface snapshot.
2. Browser tools can read current surface metadata.
3. Future app tools can read active surface context.
4. Logs include surface context for routed commands.

Potential model:

```csharp
public sealed record CommandRoutingContext(
    string UserText,
    string CorrelationId,
    ActiveSurfaceSnapshot ActiveSurface,
    // existing fields...
);
```

Acceptance criteria:

```text
CommandRouter receives active surface snapshot.
Browser actions know whether they were invoked from BrowserWorkspace.
No existing tools break.
```

---

## Phase 6: External App Surface Detection

Goal:

Add optional support for external foreground apps such as Discord, Steam, WhatsApp, File Explorer.

This should come after BrowserWorkspace is stable.

Deliverables:

1. Add Windows foreground app detector.
2. Map known process/window names to surface kinds:
   - Discord
   - Steam
   - WhatsApp
   - Explorer / FileBrowser
   - Browser external Chrome/Edge if relevant
3. Use lower confidence than Merlin-owned surface focus.
4. Do not let external app detection override explicit Merlin-owned focus too aggressively.
5. Add logging.

Acceptance criteria:

```text
Foreground app can be detected.
Merlin-owned workspace state has higher priority.
Unknown apps become ExternalApp or Unknown.
Ambiguous commands are conservative when confidence is low.
```

---

## Phase 7: Correction and Learned Control Profile Integration

Goal:

Make user corrections surface-aware.

Deliverables:

1. Correction layer records active surface in correction context.
2. Corrections can be scoped to surface kind, app, site, or global.
3. `No, I meant pause the video` while BrowserWorkspace is active teaches surface-specific routing.
4. Learned profile does not globally override Dashboard behavior.
5. Add tests.

Acceptance criteria:

```text
Surface-scoped corrections are stored separately from global preferences.
Dashboard behavior is not broken by browser-specific correction.
Browser-specific corrections improve future browser commands.
```

---

## Phase 8: Gesture / Pointing Integration

Goal:

When the user points to something, the target surface becomes active.

Deliverables:

1. Gesture system emits target surface or screen region.
2. ActiveSurfaceService updates from pointer target.
3. If pointing inside Browser Workspace, active surface becomes BrowserWorkspace.
4. If pointing inside Dashboard, active surface becomes Dashboard.
5. If pointing inside external app, active surface becomes ExternalApp with metadata.
6. Later, learned control DB can bind pointed targets to actions.

Acceptance criteria:

```text
Pointing at Browser Workspace makes browser the active surface.
Pointing at Dashboard makes Dashboard active.
Subsequent short commands are interpreted against the pointed surface.
```

---

## Phase 9: UI Debug Panel

Goal:

Make active surface visible during development.

Deliverables:

1. Add optional debug overlay showing:
   - active surface kind
   - source
   - confidence
   - capabilities
   - URL/domain if browser
2. Add backend log command or endpoint to inspect current surface.
3. Optional: expose current active surface in frontend developer panel.

Acceptance criteria:

```text
Developer can see why "pause" routed to assistant or browser.
Debug info can be disabled for normal use.
```

---

## Minimal V1 Scope

For the first actual implementation, keep scope tight.

V1 should include only:

```text
Dashboard
BrowserWorkspace
Unknown
```

V1 should solve:

```text
Dashboard + pause → Merlin playback
BrowserWorkspace + pause → browser media pause
Explicit video/ad/fullscreen phrases → browser route
Explicit assistant playback phrases → assistant playback
Global stop/cancel still works
```

Do not implement Steam, Discord, WhatsApp, FileBrowser, Spotify, or gesture learning in V1. The architecture should allow them later, but they should not be built immediately.

---

## Recommended File / Namespace Layout

Possible backend structure:

```text
Merlin.Backend/
  Services/
    Context/
      ActiveSurface/
        IActiveSurfaceService.cs
        ActiveSurfaceService.cs
        ActiveSurfaceKind.cs
        ActiveSurfaceSource.cs
        ActiveSurfaceSnapshot.cs
        ActiveSurfaceUpdate.cs
        ActiveSurfaceCapabilities.cs
        KnownSurfaces.cs
```

Alternative if browser workspace already has a context folder:

```text
Merlin.Backend/
  Services/
    InteractionContext/
      ActiveSurface...
```

Recommended: keep it separate from BrowserWorkspace.

Reason:

```text
Browser Workspace is one surface provider.
It should not own the generic context layer.
```

---

## Capability Constants

Avoid raw strings scattered everywhere.

Suggested constants class:

```csharp
public static class ActiveSurfaceCapabilities
{
    public const string AssistantChat = "assistant.chat";
    public const string AssistantPlaybackPause = "assistant.playback.pause";
    public const string AssistantPlaybackResume = "assistant.playback.resume";
    public const string AssistantPlaybackStop = "assistant.playback.stop";

    public const string BrowserNavigate = "browser.navigate";
    public const string BrowserPageClick = "browser.page.click";
    public const string BrowserPageSearch = "browser.page.search";
    public const string BrowserMediaPlay = "browser.media.play";
    public const string BrowserMediaPause = "browser.media.pause";
    public const string BrowserMediaFullscreen = "browser.media.fullscreen";
    public const string BrowserMediaSkipAd = "browser.media.skip_ad";
}
```

Known surface defaults:

```csharp
public static class KnownSurfaces
{
    public static ActiveSurfaceSnapshot Dashboard(DateTimeOffset now) => new(
        Kind: ActiveSurfaceKind.Dashboard,
        SurfaceId: "dashboard.main",
        DisplayName: "Merlin Dashboard",
        Confidence: 1.0,
        Source: ActiveSurfaceSource.StartupDefault,
        UpdatedUtc: now,
        Capabilities: new HashSet<string>
        {
            ActiveSurfaceCapabilities.AssistantChat,
            ActiveSurfaceCapabilities.AssistantPlaybackPause,
            ActiveSurfaceCapabilities.AssistantPlaybackResume,
            ActiveSurfaceCapabilities.AssistantPlaybackStop
        },
        Metadata: new Dictionary<string, string>());
}
```

---

## Browser Command Normalizer Sketch

Potential interface:

```csharp
public interface IBrowserMediaCommandNormalizer
{
    BrowserMediaCommandMatch? TryMatch(string normalizedText);
}

public sealed record BrowserMediaCommandMatch(
    string Capability,
    double Confidence,
    string Reason
);
```

Example matching:

```csharp
if (text.Contains("pause the video") || text.Contains("pause video"))
{
    return new BrowserMediaCommandMatch(
        ActiveSurfaceCapabilities.BrowserMediaPause,
        0.95,
        "Explicit video pause phrase.");
}

if (text is "pause" && activeSurface.Kind == ActiveSurfaceKind.BrowserWorkspace)
{
    return new BrowserMediaCommandMatch(
        ActiveSurfaceCapabilities.BrowserMediaPause,
        0.8,
        "Plain pause resolved by BrowserWorkspace active surface.");
}
```

Important: this normalizer should be deterministic and fast.

---

## Suggested Routing Pseudocode

```csharp
var surface = await activeSurfaceService.GetCurrentAsync(ct);
var normalized = utteranceNormalizer.Normalize(userText);

if (globalInterruptMatcher.IsMatch(normalized))
{
    return Route.GlobalInterrupt(reason: "Global interruption phrase.");
}

if (assistantPlaybackMatcher.TryMatchExplicit(normalized, out var assistantAction))
{
    return Route.AssistantPlayback(assistantAction);
}

if (browserMediaNormalizer.TryMatchExplicit(normalized, out var browserAction))
{
    return Route.CommandRouter(browserAction.Capability);
}

if (ambiguousCommandMatcher.TryMatch(normalized, out var ambiguous))
{
    if (surface.Kind == ActiveSurfaceKind.BrowserWorkspace &&
        surface.Capabilities.Contains(ActiveSurfaceCapabilities.BrowserMediaPause) &&
        ambiguous.Kind == AmbiguousCommandKind.Pause)
    {
        return Route.CommandRouter(ActiveSurfaceCapabilities.BrowserMediaPause);
    }

    if (surface.Kind == ActiveSurfaceKind.Dashboard &&
        ambiguous.Kind == AmbiguousCommandKind.Pause)
    {
        return Route.AssistantPlayback(AssistantPlaybackAction.Pause);
    }
}

return existingRouting.Route(userText);
```

---

## Avoid These Mistakes

### Mistake 1: Making the Gate app-specific

Bad:

```text
LiveUtteranceGate knows all browser, YouTube, Spotify, Discord, Steam behavior.
```

Better:

```text
Gate performs broad classification.
Specialized normalizers / CommandRouter / surface providers handle details.
```

### Mistake 2: Letting foreground app override everything

Bad:

```text
Windows foreground app says Discord, so all commands route to Discord.
```

Better:

```text
Merlin-owned active workspace and explicit user commands have higher priority.
Foreground app is a useful signal, not absolute truth.
```

### Mistake 3: Global correction from contextual mistake

Bad:

```text
User corrects "pause" once in browser.
Now "pause" always means browser pause forever.
```

Better:

```text
Correction is scoped to ActiveSurface = BrowserWorkspace.
```

### Mistake 4: Bypassing safety because active surface is known

Bad:

```text
ActiveSurface = FileBrowser
User: delete this
→ delete immediately
```

Better:

```text
ActiveSurface routes the command.
Safety/confirmation still decides execution.
```

### Mistake 5: Building every app in V1

Bad:

```text
Implement Browser, Steam, Discord, WhatsApp, FileBrowser, Spotify, games, and gestures all at once.
```

Better:

```text
V1 only Dashboard + BrowserWorkspace.
Architecture supports the rest later.
```

---

## Agent Prompt: Phase 1

```text
You are working in the Merlin repo. Implement Phase 1 of the Active Surface / Context Layer.

Goal:
Create a runtime ActiveSurfaceService that tracks which interaction surface Merlin is currently operating in. Do not change command routing behavior yet except where needed to compile and expose the service.

Requirements:
1. Add models/enums for ActiveSurfaceKind, ActiveSurfaceSource, ActiveSurfaceSnapshot, ActiveSurfaceUpdate.
2. Add IActiveSurfaceService.
3. Add an in-memory thread-safe ActiveSurfaceService implementation.
4. Default startup surface must be Dashboard.
5. Add capability constants for assistant playback and browser media basics.
6. Register the service in DI.
7. Add unit tests for default state, updates, reset to Dashboard, capability checks, and safe concurrent reads.
8. Add structured logging when the active surface changes.

Constraints:
- Do not implement Steam, Discord, WhatsApp, Spotify, FileBrowser, or gesture support yet.
- Do not store active surface in long-term memory.
- Do not bypass any safety or confirmation behavior.
- Do not make the LiveUtteranceGate app-specific in this PR.

Acceptance criteria:
- Backend starts with Dashboard active.
- Tests can set BrowserWorkspace active and reset Dashboard.
- Current surface can be read from services via IActiveSurfaceService.
- Existing tests continue to pass.
```

---

## Agent Prompt: Phase 2

```text
You are working in the Merlin repo. Implement Phase 2 of the Active Surface / Context Layer.

Goal:
Wire Browser Workspace and Dashboard focus/open/close events into IActiveSurfaceService.

Requirements:
1. When Browser Workspace opens or receives focus, set ActiveSurfaceKind.BrowserWorkspace.
2. When Browser Workspace closes, reset to Dashboard.
3. When Dashboard explicitly receives focus or user returns to Merlin UI, set ActiveSurfaceKind.Dashboard.
4. BrowserWorkspace surface should include browser capabilities:
   - browser.navigate
   - browser.page.click
   - browser.page.search
   - browser.media.play
   - browser.media.pause
   - browser.media.fullscreen
   - browser.media.skip_ad
5. Include metadata when available:
   - url
   - domain
   - title
6. Add logging for each surface transition.
7. Add tests for BrowserWorkspace state transitions.

Constraints:
- Do not change phrase routing yet.
- Do not bypass BrowserPageSafetyGuard.
- Do not implement external app detection yet.

Acceptance criteria:
- Opening/focusing Browser Workspace updates ActiveSurfaceService.
- Closing Browser Workspace resets to Dashboard.
- Logs show the active surface kind/source/confidence.
- Existing browser behavior is otherwise unchanged.
```

---

## Agent Prompt: Phase 3

```text
You are working in the Merlin repo. Implement Phase 3 of the Active Surface / Context Layer.

Goal:
Make live voice routing surface-aware for ambiguous media/playback commands, especially "pause".

Requirements:
1. Give the LiveUtteranceGate or immediate voice routing layer access to IActiveSurfaceService.
2. Preserve existing global interruption behavior for commands like "stop", "cancel", "no no no", etc.
3. Explicit assistant playback phrases must route to AssistantSpeechPlaybackService:
   - pause yourself
   - stop talking
   - pause your answer
   - resume your answer
   - continue your answer
4. Explicit browser/media phrases must route to CommandRouter/browser capabilities:
   - pause the video
   - play the video
   - skip ad
   - skip advertentie
   - overslaan
   - fullscreen
   - click pause
   - click fullscreen
5. Ambiguous plain "pause" behavior:
   - Dashboard active → assistant playback pause
   - BrowserWorkspace active → browser.media.pause through CommandRouter
6. Ensure playback resume suppression is set correctly when a surface command is routed.
7. Add tests for Dashboard and BrowserWorkspace cases.

Constraints:
- Do not put every app-specific phrase directly in the gate.
- Keep browser/media phrase matching centralized or clearly isolated.
- Do not bypass BrowserPageSafetyGuard.
- Do not break existing interruption/correction behavior.

Acceptance criteria:
- "pause" pauses Merlin when Dashboard is active.
- "pause" pauses browser media when BrowserWorkspace is active.
- "pause the video" routes to browser/media even when Dashboard or Unknown is active.
- "pause your answer" always routes to assistant playback.
- "stop" still acts as a global interruption.
- Tests pass.
```

---

## Agent Prompt: Phase 4

```text
You are working in the Merlin repo. Implement Phase 4 of the Active Surface / Context Layer.

Goal:
Create a centralized BrowserMediaCommandNormalizer so browser/media command phrases are not scattered through the LiveUtteranceGate or CommandRouter.

Requirements:
1. Add IBrowserMediaCommandNormalizer and implementation.
2. It should map normalized utterances to browser media capabilities:
   - browser.media.pause
   - browser.media.play
   - browser.media.fullscreen
   - browser.media.skip_ad
3. Include English and Dutch aliases:
   - pause video
   - pause the video
   - play video
   - fullscreen
   - go fullscreen
   - skip ad
   - skip add
   - skip advertentie
   - overslaan
   - advertentie overslaan
   - klik overslaan
4. Include likely STT variants where reasonable.
5. Use active surface context only where needed for ambiguous short commands.
6. Add unit tests for all aliases.

Constraints:
- Do not make this an LLM call.
- Keep it deterministic and fast.
- Do not bypass safety.
- Do not duplicate phrase lists elsewhere.

Acceptance criteria:
- Browser/media phrases normalize consistently.
- Gate/routing code calls the normalizer instead of owning large browser phrase lists.
- Tests cover Dutch and English variants.
```

---

## Agent Prompt: Phase 5

```text
You are working in the Merlin repo. Implement Phase 5 of the Active Surface / Context Layer.

Goal:
Pass ActiveSurfaceSnapshot into CommandRouter routing context so tools and future app controllers can make context-aware decisions.

Requirements:
1. Extend command routing context with ActiveSurfaceSnapshot.
2. Ensure CommandRouter receives the current surface for voice commands.
3. Browser Workspace tools should be able to inspect active surface metadata, such as url/domain/title when available.
4. Add structured logs showing the active surface for routed commands.
5. Add tests proving routing context includes active surface.

Constraints:
- Do not make tools depend directly on mutable ActiveSurfaceService if the routing context can provide the snapshot.
- Do not bypass safety or confirmation.
- Keep backwards compatibility where possible.

Acceptance criteria:
- CommandRouter receives active surface snapshot.
- Browser commands can see BrowserWorkspace metadata.
- Existing tools still work.
- Tests pass.
```

---

## Open Questions

These should be answered during implementation, not necessarily before Phase 1.

1. Should active surface be updated from Godot frontend events only, or can backend BrowserWorkspaceService update it directly too?
2. Where exactly should surface-aware routing live: inside LiveUtteranceGate, immediately after it, or in CommandRouter pre-routing?
3. Should `pause` in BrowserWorkspace map to `browser.media.pause` or a more generic `media.pause` capability that BrowserWorkspace handles?
4. Should Dashboard always win when Merlin is speaking, or should BrowserWorkspace active surface still interpret `pause` as video pause?
5. How should the user switch explicitly?
   - `back to Merlin`
   - `control the browser`
   - `use the dashboard`
6. Should external app detection be opt-in only at first?
7. How much Dutch aliasing should be deterministic versus learned through correction?

Recommended answers for V1:

```text
1. Both frontend and backend may update, but use one central service.
2. Minimal surface-aware decision can be immediately after the gate if cleaner.
3. Use explicit browser capabilities for now; consider generic media later.
4. Explicit target wins. Plain pause follows active surface.
5. Add simple explicit switch commands later or in Phase 2/3 if easy.
6. External detection should wait.
7. Add a small deterministic Dutch media alias set now, correction later.
```

---

## Final Target Behavior

When this layer is implemented, Merlin should behave like this:

```text
Default state:
ActiveSurface = Dashboard

User opens Browser Workspace:
ActiveSurface = BrowserWorkspace

User says "pause":
Browser video pauses.

User says "pause your answer":
Merlin speech pauses.

User says "skip ad" or "overslaan":
Browser Workspace attempts skip-ad behavior through safe browser page control.

User says "back to Merlin":
ActiveSurface = Dashboard

User says "pause":
Merlin speech pauses again.
```

This creates the foundation for controlling many different apps without turning the Live Utterance Gate into a giant brittle phrase switch.
