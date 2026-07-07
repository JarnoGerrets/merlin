---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/browser_control/merlin_browser_control_phases_2_to_5.md
classification: implementation-plan
related_features:
  - Browser Control
  - Browser Workspace
status: current
imported_to_vault: true
---

# Merlin Browser Control Roadmap: Phases 2–5

## Purpose

This document defines the next browser-control phases for Merlin after Phase 1 spoken browser navigation.

Phase 1 is intentionally excluded from this document because it is already being handled separately.

The goal of Phases 2–5 is to move Merlin from spoken browser navigation toward a rich multimodal browser-control experience:

```text
voice navigation
→ visual hand pointer overlay
→ pinch click
→ gesture scrolling
→ page-aware browser control
```

The browser surface is assumed to be Merlin’s internal WebView2 Browser Workspace, with Merlin’s real Godot orb/visual identity handled by the Godot frontend.

## Current Assumptions

Merlin currently has or is expected to have:

1. A working `Merlin.BrowserHost` using WebView2.
2. Browser URLs and web destinations routing to the internal Browser Workspace by default.
3. A Godot mini-orb overlay or browser-mode visual integration already working.
4. Spoken browser navigation commands from Phase 1 either implemented or in progress.
5. Existing hand hover/pinch logic already working inside Merlin UI.
6. Existing voice pipeline, command router, and browser workspace service.

## Explicit Non-Goals for This Roadmap

These phases do not cover:

1. Replacing WebView2 with a custom Godot browser.
2. Arbitrary native app embedding.
3. Global desktop app control outside the browser.
4. Building a full browser tab/bookmark/download manager.
5. Rewriting the entire orb/UI architecture.
6. Replacing current STT/TTS or interruption systems.

Those may be separate workstreams.

---

# Phase 2: Browser Motion Overlay

## Goal

Create a transparent Godot pointer overlay over the WebView2 BrowserHost that visually tracks the user’s hand position.

This phase is **visual only**.

No clicking yet. No pinch click yet. No mouse injection yet. No DOM automation yet.

The purpose is to prove that Merlin can project a reliable hand-controlled pointer over the browser surface before the pointer is allowed to interact with the page.

## Product Behavior

When Browser Workspace is active and browser motion mode is enabled:

```text
web page visible in BrowserHost
→ transparent Godot overlay appears above BrowserHost
→ hand movement moves a visible pointer/circle over the page
→ pointer follows hand smoothly
→ no clicks are sent
→ no page interactions happen yet
```

The user should feel that Merlin can “hover over” the web page with their hand, but it should not yet be able to click or modify anything.

## Desired Commands

Possible commands:

```text
start browser hand control
start browser pointer
show browser pointer
enable browser motion
stop browser hand control
hide browser pointer
stop browser pointer
```

The exact command names can follow existing Merlin command conventions.

## Architecture

Recommended structure:

```text
Existing hand tracking service
→ BrowserPointerMapper
→ BrowserMotionOverlayController
→ Godot transparent overlay window
```

Suggested components:

```text
BrowserMotionOverlayModeService
BrowserPointerOverlayWindow
BrowserPointerMapper
BrowserHostBoundsTracker
BrowserPointerRenderState
```

Responsibilities:

```text
BrowserMotionOverlayModeService
- owns active/inactive state
- starts/stops overlay mode
- receives browser workspace active/inactive events
- disables overlay when BrowserHost closes

BrowserHostBoundsTracker
- knows BrowserHost screen bounds
- updates when BrowserHost moves/resizes/minimizes

BrowserPointerMapper
- maps normalized hand coordinates to BrowserHost screen coordinates
- applies margins, smoothing, clamping, and deadzone

BrowserPointerOverlayWindow
- transparent/borderless Godot window
- follows BrowserHost bounds
- renders pointer circle/ring
- does not steal focus
- click-through if possible
```

## Overlay Window Requirements

The overlay should be:

```text
transparent
borderless
always above BrowserHost while browser mode is active
small/full-size enough to cover BrowserHost content area
mouse pass-through if possible
no focus stealing
hidden when BrowserHost is minimized
closed/hidden when Browser Workspace closes
```

It should render only browser-control visual elements, such as:

```text
hover circle
tracking confidence fade
optional small hand-state indicator
optional safe-zone boundary during calibration
```

It should not render:

```text
chat log
labels
debug text
fake orb
large Merlin UI panels
```

## Coordinate Mapping

Input:

```text
hand position in camera space
or existing Merlin UI hover coordinate
```

Output:

```text
screen-space coordinate over BrowserHost
```

Suggested mapping:

```text
normalized hand X/Y
→ active hand control area
→ BrowserHost client rectangle
→ overlay-local coordinate
→ screen coordinate if needed later
```

Important mapping concerns:

1. Mirrored camera input.
2. Deadzone around resting hand position.
3. Edge clamping.
4. DPI scaling.
5. BrowserHost position on screen.
6. BrowserHost resize/move.
7. Multi-monitor handling later.

For V1 of Phase 2, primary-monitor support is acceptable if documented.

## Smoothing and Stability

The pointer must not jitter wildly.

Use existing smoothing from internal Merlin UI control if possible.

Recommended behavior:

```text
low-confidence tracking → fade pointer / freeze pointer / hide pointer
stable tracking → normal pointer
hand leaves frame → pointer fades and no future click can be armed
```

Possible smoothing techniques:

```text
exponential smoothing
velocity damping
small deadzone
confidence threshold
max movement clamp per frame
```

Do not overbuild the math. Reuse existing Merlin hand-control smoothing if it already feels good.

## Visual Design

Suggested pointer states:

```text
normal tracking → thin circle/ring
low confidence → faded circle
hand lost → hidden or pulsing inactive marker
calibration active → larger guide area
```

No click animation yet, except maybe a disabled/inactive preview.

## Safety

Even though this phase is visual only, it should already prepare safety state for later phases.

Track:

```text
isTrackingReliable
isHandInFrame
lastReliablePointerPosition
canClickEventually
```

But do not send clicks.

## Acceptance Criteria

Phase 2 is complete when:

1. Browser pointer overlay can be enabled while Browser Workspace is active.
2. Overlay appears above BrowserHost and visually follows hand movement.
3. Overlay does not click or interact with the page.
4. Overlay hides when Browser Workspace closes.
5. Overlay follows BrowserHost move/resize at least reasonably.
6. Pointer is smoothed enough to be usable.
7. Low-confidence or lost-hand state does not produce unstable jumps.
8. Merlin voice/TTS/STT remains unaffected.
9. Existing normal Merlin UI hand control remains unaffected outside browser mode.
10. Build/tests pass.

## Manual Validation

Test:

```text
open browser
start browser pointer
move hand around
verify pointer follows over webpage
resize browser
verify pointer still aligns reasonably
move browser
verify pointer follows browser
remove hand from frame
verify pointer fades/freezes/hides
stop browser pointer
verify overlay disappears
close browser
verify overlay disappears and normal Merlin UI restores
```

## Do Not Implement in Phase 2

Do not implement:

```text
pinch click
SendInput
mouse movement
drag
scroll gestures
DOM element detection
click first result
page reading
page summarization
```

---

# Phase 3: Pinch Click

## Goal

Allow the visual browser pointer from Phase 2 to perform a real click on the WebView2 browser surface when the user performs a pinch gesture.

This is the first phase where hand gestures can interact with the actual webpage.

## Product Behavior

When browser motion overlay is active:

```text
hand controls pointer circle
→ user pinches intentionally
→ pointer gives click feedback
→ Merlin sends one left-click at the overlay coordinate
→ WebView2 receives the click
```

The click should feel like the pointer clicked the page.

## Recommended Implementation Strategy

For Phase 3, use Windows input injection rather than DOM automation.

Suggested flow:

```text
pointer overlay coordinate
→ convert to screen coordinate
→ SetCursorPos(x, y)
→ SendInput(left button down/up)
→ optional restore previous cursor position later
```

This should work because WebView2 is a real native browser surface.

DOM-level clicking can come later.

## Pinch State Machine

Do not send a click on every frame where pinch is detected.

Implement a state machine such as:

```text
OpenHand
→ PinchCandidate
→ PinchArmed
→ ClickSent
→ PinchHeld
→ Released
→ Cooldown
→ OpenHand
```

Minimum rules:

1. Pinch must be stable for a short duration before click is armed.
2. Click fires once per pinch gesture.
3. Holding pinch does not repeatedly click.
4. Release is required before another click can happen.
5. A cooldown prevents accidental double-clicks.

Suggested thresholds:

```text
pinch arm duration: 80–150 ms
click cooldown: 250–400 ms
minimum tracking confidence: reuse existing confidence threshold
```

Tune to feel natural.

## Visual Feedback

The overlay should communicate click state clearly.

Suggested visuals:

```text
normal hover → regular circle
pinch candidate → circle starts shrinking/filling
click sent → ripple/flash
cooldown → brief disabled/faded state
low confidence → no click arm, faded pointer
```

The user should be able to see whether Merlin is about to click.

## Safety Rules

Clicking must be gated.

Do not allow click if:

```text
Browser Workspace is not active
Browser pointer overlay is not active
hand tracking confidence is low
hand is not in frame
pointer is outside BrowserHost bounds
BrowserHost is minimized
BrowserHost is not ready
pinch state is unstable
cooldown is active
```

Optional but recommended:

```text
open palm for 1 second → disables browser pointer/click mode
voice “stop browser pointer” → disables immediately
voice “stop” / global stop → disables click mode immediately if applicable
```

## Click Injection Layer

Add a small native input abstraction rather than spreading Win32 calls throughout the code.

Possible interface:

```csharp
public interface IWindowsInputService
{
    Task LeftClickAsync(int screenX, int screenY, CancellationToken cancellationToken = default);
}
```

Implementation can use:

```text
SetCursorPos
SendInput
```

Keep it isolated for testing and future replacement.

## BrowserHost Focus

Before clicking, WebView2/BrowserHost may need focus.

Possible approaches:

1. Set cursor position and click directly.
2. Bring BrowserHost to foreground if necessary.
3. Avoid focus stealing unless required.

For V1, direct click is acceptable if it works reliably.

## Acceptance Criteria

Phase 3 is complete when:

1. Browser pointer overlay still works visually.
2. Pinch sends exactly one click per intentional pinch.
3. Click lands at the visible pointer coordinate.
4. Click works on normal webpage links/buttons inside WebView2.
5. Pinch holding does not repeatedly click.
6. Low confidence prevents clicking.
7. Pointer outside browser bounds prevents clicking.
8. Cooldown prevents accidental double clicks.
9. Visual click feedback is shown.
10. Browser mode can be safely disabled by voice.
11. Existing internal Merlin UI hand control remains unaffected outside browser mode.
12. Build/tests pass.

## Manual Validation

Test:

```text
open browser
open google
start browser pointer
hover over search box
pinch once
verify focus/click occurs
hover over a link/button
pinch once
verify link/button activates
hold pinch
verify only one click occurs
release and pinch again
verify second click occurs
move hand out of frame
pinch/noise should not click
stop browser pointer
verify pinch no longer clicks
```

## Do Not Implement in Phase 3

Do not implement:

```text
dragging
pinch-hold drag
scroll gestures
right click
double click gesture
DOM click by element name
page-aware click commands
```

---

# Phase 4: Scroll Gestures

## Goal

Allow the user to scroll the WebView2 page with hand gestures, while preserving spoken navigation and pinch-click control.

This phase introduces gesture-based scrolling and voice + gesture hybrid behavior.

## Product Behavior

The user should be able to:

```text
move pointer by hand
pinch click links/buttons
scroll page using a deliberate gesture
combine voice and gesture, e.g. “scroll down more” or “click there” later
```

For this phase, focus on scroll gestures only.

## Possible Gesture Designs

Choose the safest and most reliable gesture after inspecting existing hand tracking.

Recommended options:

## Option A: Pinch-Hold Drag to Scroll

Behavior:

```text
pinch and hold
→ move hand up/down
→ page scrolls opposite or same direction depending chosen natural mapping
→ release pinch to stop scrolling
```

Risk:

This conflicts with click if click also uses pinch.

Mitigation:

```text
short pinch release → click
pinch held longer than threshold + movement → scroll/drag mode
```

Example state split:

```text
pinch duration < 200 ms and release → click
pinch duration > 250 ms and vertical movement > threshold → scroll mode
```

## Option B: Open-Hand Vertical Swipe

Behavior:

```text
open hand swipe up/down
→ scroll page
```

Risk:

Can accidentally trigger while moving pointer.

Mitigation:

Require strong velocity and clear gesture start/end.

## Option C: Two-Finger / Palm Gesture

Behavior:

```text
specific hand pose enables scroll mode
vertical movement scrolls page
```

Risk:

Depends on whether current tracker recognizes enough landmarks/poses reliably.

## Recommended V1 Choice

Prefer one clear gesture only.

Recommended first implementation:

```text
pinch-hold + vertical movement = scroll mode
short pinch = click
```

Because it builds naturally on Phase 3.

## Gesture State Machine

Extend the Phase 3 pinch state machine.

Possible states:

```text
OpenHand
PinchCandidate
PinchClickArmed
PinchHeld
ScrollCandidate
Scrolling
Released
Cooldown
```

Rules:

```text
short stable pinch without movement → click
pinch held beyond threshold with vertical movement → enter scroll mode
while scrolling → send scroll commands based on vertical delta
release pinch → stop scrolling
cooldown → prevent accidental click after scroll release
```

Important:

After a scroll gesture, release should not also trigger a click.

## Scroll Implementation

Prefer BrowserHost/WebView2 command bridge over OS wheel injection.

Flow:

```text
hand vertical delta
→ BrowserGestureController computes scroll amount
→ BrowserWorkspaceService.ScrollByPixelsAsync(delta)
→ BrowserHost ExecuteScriptAsync(window.scrollBy(...))
```

Add a method like:

```csharp
Task ScrollByPixelsAsync(int deltaY, CancellationToken cancellationToken = default);
```

or reuse existing scroll command with continuous amounts.

Use throttling so scroll commands are not sent every frame if that creates too much traffic.

Suggested throttle:

```text
30–60 Hz maximum for smooth mode
or 10–20 Hz for simpler stable mode
```

If command bridge latency is high, batch scroll deltas.

## Direction Mapping

Choose and document the mapping.

Common browser/touch style:

```text
hand moves up → page content moves up / scroll down
hand moves down → page content moves down / scroll up
```

Mouse-wheel style may feel opposite.

Pick whichever feels natural in testing, then keep it consistent.

## Voice + Gesture Hybrid

Keep spoken scroll commands from Phase 1 working.

Phase 4 should allow both:

```text
voice: scroll down
hand: pinch-hold scroll
```

Do not make gestures replace voice.

Possible hybrid commands to support if easy:

```text
scroll mode
stop scroll mode
make scrolling slower
make scrolling faster
```

But do not overbuild.

## Visual Feedback

Overlay should show when scrolling is active.

Suggested visuals:

```text
scroll mode active → pointer changes shape or gains vertical arrows
scroll delta → subtle trail/arrow
release → return to normal hover pointer
```

Do not show text labels unless debug mode is enabled.

## Safety Rules

Do not scroll if:

```text
Browser Workspace inactive
pointer overlay inactive
tracking confidence low
hand not in frame
BrowserHost minimized/not ready
scroll gesture not clearly recognized
```

Scrolling must stop immediately when:

```text
hand lost
pinch released
voice stop command
browser closes
mode disabled
```

## Acceptance Criteria

Phase 4 is complete when:

1. Existing spoken browser navigation still works.
2. Existing browser pointer overlay still works.
3. Existing pinch click still works.
4. A deliberate scroll gesture scrolls the WebView page.
5. Scroll gesture does not accidentally click on release.
6. Short pinch still clicks reliably.
7. Gesture scrolling stops when hand is released/lost.
8. Low confidence prevents scrolling.
9. Visual feedback indicates scroll mode.
10. Voice scroll commands still work.
11. Build/tests pass.

## Manual Validation

Test:

```text
open browser
open long article or search results
start browser pointer
short pinch a link/search box
verify click still works
pinch-hold and move hand vertically
verify page scrolls
release pinch
verify scrolling stops
release after scrolling
verify no accidental click fires
use voice: scroll down
verify voice scroll still works
move hand out of frame while scrolling
verify scrolling stops
```

## Do Not Implement in Phase 4

Do not implement:

```text
page-aware element selection
click first result
click link named X
DOM extraction for page reading
summarization
full drag-and-drop
multi-touch gestures
right-click gestures
```

---

# Phase 5: Page-Aware Control

## Goal

Make Merlin aware of the current WebView2 page enough to perform semantic browser actions such as:

```text
click the first result
click the link named X
read current page
summarize current page
what is this page about
```

This is the first phase where Merlin moves beyond physical browser control and starts using page structure/content.

## Product Behavior

The user should be able to say:

```text
click the first result
click the YouTube link
click the login button
read this page
summarize this page
what page am I on
what does this say
find shipping information
```

Merlin should inspect the current page through WebView2/JavaScript where possible and then act or answer.

## Important Design Principle

Use page structure before OCR.

Preferred order:

```text
WebView2 DOM/JavaScript
→ accessibility tree if available later
→ visible text extraction
→ screenshot/OCR only as fallback later
```

Do not start with OCR if DOM access is available.

## BrowserHost Page Bridge

Add BrowserHost commands for page inspection.

Possible commands:

```json
{ "type": "get_page_snapshot" }
{ "type": "get_visible_text" }
{ "type": "get_links" }
{ "type": "get_buttons" }
{ "type": "click_element", "selectorOrId": "..." }
{ "type": "click_at_dom_rect", "x": 100, "y": 200 }
```

Keep the first version small.

## Page Snapshot V1

Create a safe page snapshot with:

```text
current URL
page title
visible text snippets
links with text + href + approximate bounding rect
buttons with text/aria-label + bounding rect
inputs with placeholder/name/aria-label + bounding rect
```

Do not dump the entire raw DOM into prompts.

Limit/cap content:

```text
max visible text characters
max links
max buttons
max inputs
exclude scripts/styles
exclude hidden elements
prefer viewport-visible elements first
```

Suggested V1 caps:

```text
visible text: 8,000–15,000 chars
links: 100 max
buttons: 100 max
inputs: 50 max
```

Tune as needed.

## JavaScript Extraction

Use WebView2 `ExecuteScriptAsync` to collect page info.

Possible extraction logic:

```javascript
Array.from(document.querySelectorAll('a, button, input, textarea, select, [role="button"], [role="link"]'))
  .map(element => {
    const rect = element.getBoundingClientRect();
    return {
      tag: element.tagName.toLowerCase(),
      text: element.innerText || element.value || element.ariaLabel || element.title || element.placeholder || '',
      href: element.href || null,
      role: element.getAttribute('role'),
      ariaLabel: element.getAttribute('aria-label'),
      rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
      visible: rect.width > 0 && rect.height > 0 && rect.bottom >= 0 && rect.top <= window.innerHeight
    };
  })
```

Sanitize and cap the result before sending it to the backend/LLM.

## Click First Result

Implement a deterministic first version for search-result-like pages.

Possible behavior:

```text
click the first result
→ get page links
→ filter visible links
→ filter out navigation/header/footer links if possible
→ choose first meaningful content/search result link
→ click it
```

For V1, this can be heuristic.

Safer alternative:

```text
click the first result
→ if confidence high, click
→ if confidence low, ask clarification or show/announce uncertainty
```

Avoid clicking ads if possible.

Possible filters:

```text
ignore empty text links
ignore javascript:void links
ignore same-page anchors
prefer main content area
avoid nav/menu links
avoid links with ad/sponsored indicators if detectable
```

## Click Link Named X

Command examples:

```text
click the login button
click the sign in link
click the YouTube link
open the second result
click contact
```

Flow:

```text
parse requested label/entity
→ get page snapshot elements
→ fuzzy match visible links/buttons/inputs
→ if one high-confidence match, click it
→ if multiple matches, ask clarification or choose nearest/most relevant
```

Start with simple matching:

```text
case-insensitive exact match
contains match
aria-label/title/placeholder match
```

Add fuzzy matching later if needed.

## How to Click Page Elements

Options:

1. JavaScript click:

```javascript
element.click()
```

2. Coordinate click:

```text
get element center rect
→ convert viewport coordinate to screen coordinate
→ SendInput click
```

Recommended V1:

```text
Use JavaScript click for simple links/buttons where safe.
Use coordinate click if JS click fails or element requires real pointer event.
```

Document behavior.

## Read Current Page

Command examples:

```text
read this page
read current page
what does this page say
```

Behavior:

```text
get page title/url/visible text
→ create concise spoken summary or read relevant page content
```

Do not read huge pages verbatim.

For long pages:

```text
summarize first
ask if user wants more detail only if necessary
```

## Summarize Current Page

Command examples:

```text
summarize this page
what is this page about
what are the main points
```

Behavior:

```text
get visible/main text
→ pass clean text to assistant model
→ answer with summary
```

Need source awareness:

```text
include current URL/title in internal context
avoid pretending to know content not present in snapshot
if page extraction fails, say so
```

## Privacy and Safety

Page-aware control can expose sensitive data.

Handle carefully:

```text
do not log full page text by default
do not send passwords or hidden fields
do not extract password input values
do not summarize private pages unless user requested it
avoid storing page contents in memory unless explicitly asked
```

For forms/payment/account actions, consider confirmation before clicking destructive or sensitive buttons.

Potential risky actions:

```text
buy
purchase
delete
submit
send
confirm
pay
transfer
logout maybe
```

If a clicked element appears destructive/high-impact, ask confirmation before action.

## Routing

Page-aware commands should route before generic LLM chat if Browser Workspace is active.

Examples:

```text
click the first result
click login
read this page
summarize current page
what page is this
```

If Browser Workspace is inactive:

```text
read this page
→ “The browser is not open.”
```

Do not open the browser automatically for page-aware commands that refer to “this page,” because there is no current page.

## Acceptance Criteria

Phase 5 is complete when:

1. Merlin can retrieve current page title and URL.
2. Merlin can retrieve a capped visible-text snapshot.
3. Merlin can retrieve visible links/buttons with labels and bounding rects.
4. `what page am I on` returns current title/URL.
5. `summarize this page` summarizes visible/current page content.
6. `read this page` provides useful page content without reading massive raw text.
7. `click the first result` works on common search result pages or safely declines/asks when uncertain.
8. `click link/button named X` works for simple visible elements.
9. Sensitive/destructive clicks are guarded by confirmation.
10. Page extraction does not log sensitive full content by default.
11. Existing spoken navigation still works.
12. Existing pointer overlay/pinch click/scroll gestures still work.
13. Build/tests pass.

## Manual Validation

Test:

```text
open google
search for weather tomorrow
click the first result
verify a result opens or Merlin asks if uncertain
what page am I on
verify title/url response
summarize this page
verify Merlin summarizes visible content
open wikipedia.org
search or navigate to an article
read this page
verify concise useful reading/summary
click a visible link by name
verify correct link opens
try a destructive-looking button on a safe test page
verify confirmation is required
```

## Do Not Implement in Phase 5 Unless Needed

Do not implement:

```text
full autonomous browsing agent
multi-step purchasing
password handling
banking/payment automation
OCR-first screen understanding
browser history UI
tabs/bookmarks/downloads
```

---

# Cross-Phase Design Rules

## Rule 1: BrowserHost Owns Browser Content Only

BrowserHost should focus on WebView2:

```text
navigate
back
forward
refresh
scroll
zoom
page snapshot
DOM interaction
close
```

It should not own Merlin visuals.

## Rule 2: Godot Owns Merlin Visuals

Godot should own:

```text
real orb
mini orb overlay
pointer overlay
gesture visuals
browser mode visuals
```

Avoid fake WinForms visuals.

## Rule 3: Backend Owns Routing and Coordination

Backend should own:

```text
command classification
browser workspace lifecycle
browser command routing
confirmation policy
safety gating
bridge messages
```

## Rule 4: Voice and Gesture Should Complement Each Other

Do not replace voice with gestures.

Good behavior:

```text
voice: open/search/go back/scroll
hand: point/click/gesture scroll
voice + hand: click there, scroll more, stop
```

## Rule 5: Safety Gating Comes Before Power

Especially for Phase 3 onward:

```text
no reliable tracking → no click
uncertain element → ask or decline
sensitive action → confirm
browser closed → no browser action
```

## Recommended Implementation Sequence

```text
Phase 2: Browser motion overlay
- visual pointer over BrowserHost
- hand follows pointer
- no click

Phase 3: Pinch click
- click state machine
- SendInput click
- visual feedback
- safety gates

Phase 4: Scroll gestures
- pinch-hold or chosen gesture scroll
- no accidental click after scroll
- voice + gesture coexist

Phase 5: Page-aware control
- page snapshot
- click first result
- click named link/button
- read/summarize page
```

## Later Possible Phase 6+

Potential future work after Phase 5:

```text
browser tabs
browser history
download handling
form filling with confirmation
visual element highlighting
DOM + pointer hybrid selection
learned website controls
external native app overlay
cross-app UI-control memory
```

These should not be mixed into Phases 2–5.
