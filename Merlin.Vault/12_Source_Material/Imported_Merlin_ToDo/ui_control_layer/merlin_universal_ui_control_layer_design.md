---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/ui_control_layer/merlin_universal_ui_control_layer_design.md
classification: implementation-plan
related_features:
  - Dashboard UI Control
status: current
imported_to_vault: true
---

# Merlin Universal UI Control Layer

**Document status:** Draft implementation design  
**Project:** Merlin Local Desktop AI Assistant  
**Primary goal:** Give Merlin reliable, extensible control over UI elements outside its own UI without hardcoding every app in existence.  
**Core idea:** Use a layered control system: app-specific integrations where valuable, learned local control memory for repeated user workflows, generic app-type profiles where possible, universal screen control as fallback, and visual disambiguation when Merlin is uncertain.

---

## 0. Executive Summary

Merlin should be able to act on desktop applications and websites that are not part of Merlin’s own frontend. The user should be able to say things like:

```text
Open Discord.
Open chat with John.
Open Facebook.
Search for John Doe.
Click the export button.
Upload this file.
Type this into the search field.
Open the settings page.
```

The wrong approach would be to hardcode every app and website separately. That does not scale, becomes fragile, and turns Merlin into a pile of app-specific scripts.

The better architecture is a layered UI-control stack:

```text
User command
↓
Command normalization / intent parsing
↓
Active app/site/window context detection
↓
Can this be handled by a trusted app integration?
  yes → use app controller
  no
↓
Can this be handled by learned UI control memory?
  yes → use learned target recipe
  no
↓
Can this be handled by a generic app-type profile?
  yes → use profile
  no
↓
Use universal screen control
↓
If confidence is low, ask the user to point/select visually
↓
After successful user correction, save/update learned UI control memory
```

The key principle is:

```text
Merlin should not just fail less.
Merlin should learn from every corrected failure.
```

If Merlin cannot find a search bar, button, chat, input field, or menu item, it should not simply give up. It should ask the user to point at it, select it from a numbered overlay, hover the mouse over it, or eventually use webcam/gesture pointing. Once the user identifies the target and the action succeeds, Merlin stores a reusable target recipe in a new database called **UiControlMemoryDb**.

This control memory must be separate from normal conversational memory. Normal memory stores user facts and preferences. UI control memory stores local interaction patterns such as:

```text
On facebook.com, the search field is usually the input near the top-left with nearby text "Search Facebook".
For the action "search", focus that field, clear it, type the query, and press Enter.
```

Over time, Merlin becomes better at controlling the user’s actual apps, actual websites, actual layouts, and actual workflows.

---

## 1. Background and Motivation

Merlin currently aims to be a local Windows desktop assistant with a .NET backend and Godot frontend. It already has command routing, local/cloud intent parsing, audio input/output, and tool/capability concepts. The next large capability area is external UI control.

The user does not want only Merlin’s own UI to be controllable. The user wants Merlin to interact with the broader desktop environment.

Examples:

```text
"Open Discord."
"Open chat with John."
"Open Facebook."
"Search for John Doe."
"Click the export button."
"Fill that field."
"Open the conversation with Sarah."
"Upload this file."
```

Some apps will be used frequently. For those, tailored app controllers make sense. But many apps and websites will only be used rarely. Creating a full app-specific integration for every possible program is not practical.

The system therefore needs a generic UI-control engine, but not a naive one. It should combine several strategies:

1. **Trusted app integrations** for high-use apps.
2. **Learned UI control memory** for repeated actions learned through user correction.
3. **Generic app-type profiles** for common categories like browsers, chat apps, file managers, forms, and media players.
4. **Universal screen control** using Windows UI Automation, screenshots, OCR, visual detection, keyboard, and mouse primitives.
5. **Visual disambiguation** when the system is uncertain.
6. **Verification and safety gates** before considering actions successful or allowing risky actions.

This makes Merlin feel like a practical assistant instead of a brittle command macro system.

---

## 2. Core Product Principle

The system should be designed around this UX principle:

```text
Low confidence should not mean failure.
Low confidence should mean collaboration.
Collaboration should produce learning.
Learning should reduce future friction.
```

When Merlin does not know where something is, it should ask the user to help:

```text
"I can’t find the search field. Point at it."
"I found multiple Johns. Which one?"
"I’m not sure which export button you mean. Say a number or point at it."
```

After the user points or selects the correct target, Merlin should:

1. Capture the active context.
2. Capture the UI element under the target.
3. Capture structural signals around the target.
4. Execute the intended action.
5. Verify that the action succeeded.
6. Save/update a reusable learned target recipe.

This turns user correction into local training data.

---

## 3. Non-Goals

This system should **not** attempt to:

```text
hardcode every app in existence
silently activate the webcam
store screenshots by default
store private chat/email/page contents in memory
let an LLM directly click arbitrary screen coordinates without deterministic checks
automatically execute destructive/high-risk actions without confirmation
automate banking/payment/security flows casually
replace official APIs where strong APIs are available
assume learned coordinates remain valid forever
```

The system should be powerful, but controlled.

---

## 4. High-Level Architecture

Final architecture:

```text
Voice Command
↓
Speech-to-text
↓
Command normalization
↓
Intent + parameter extraction
↓
Active context detection
↓
Control tier router
   ├── Trusted app controller
   ├── Learned UI control memory
   ├── Generic app-type profile
   ├── Universal screen control
   └── Visual disambiguation fallback
↓
Action execution
↓
Verification
↓
Execution logging
↓
Learning/confidence update
↓
Response to user
```

Merlin’s UI control engine should be implemented as a capability family, not as random logic scattered through app opening, speech handling, or intent parsing.

Proposed capability family:

```text
ui.control
ui.observe
ui.find
ui.focus
ui.click
ui.type
ui.invoke
ui.scroll
ui.verify
ui.disambiguate
ui.learned_target.search
ui.learned_target.save
ui.learned_target.update
```

Most user-facing commands should go through the high-level capability:

```text
ui.control
```

Low-level capabilities should be available internally for controllers, profiles, tests, and debugging.

---

## 5. Control Tiers

## 5.1 Tier 1 — Trusted App Integrations

Trusted app integrations are app-specific controllers for apps the user uses often.

Examples:

```text
Discord
WhatsApp Desktop / WhatsApp Web
Chrome / Edge / Firefox
Spotify
File Explorer
VS Code / Visual Studio / Rider
Windows Settings
Email client
Calendar
```

These are allowed to know app-specific behavior because the return on reliability is worth it.

Example Discord flow:

```text
User: "Open chat with John."

Active context: Discord
Controller: DiscordController
Strategy:
  1. Press Ctrl+K.
  2. Type "John".
  3. Wait for quick switcher results.
  4. Select best match.
  5. Press Enter.
  6. Verify chat header contains "John".
```

Important rule:

```text
App controllers should be thin strategy layers over shared UI primitives.
```

They should not implement their own separate mouse/keyboard/screenshot stack.

They should use shared services:

```text
UiObservationService
UiTargetResolver
UiActionExecutor
UiVerificationService
UiControlMemoryService
VisualDisambiguationService
```

### Benefits

```text
high reliability
fast execution
better verification
fewer ambiguous clicks
better app-specific shortcuts
safer for high-use workflows
```

### Risks

```text
integration drift when app updates
spaghetti if every controller uses custom logic
large maintenance load if overused
```

### Rule of thumb

Build trusted app integrations only for apps the user actually uses often.

---

## 5.2 Tier 2 — Learned UI Control Memory

Learned UI control memory is the most important addition from this design.

When Merlin fails and the user points/selects the correct UI target, Merlin should store a reusable control recipe. This should happen in a separate local database called **UiControlMemoryDb**.

Example:

```text
User: "Search for John Doe."

Merlin cannot confidently find the Facebook search bar.

Merlin: "I can’t find the search field. Point at it."

User points to the Facebook search field.
User: "Here it is."

Merlin:
  - captures active context: Chrome + facebook.com
  - inspects UIA element under coordinate
  - captures nearby OCR words
  - stores normalized bounds relative to active window
  - infers target kind: search_input
  - infers recipe: focus, clear, type query, press Enter
  - executes recipe
  - verifies results changed
  - stores learned target candidate
```

Next time:

```text
User: "Search for Jane Doe."

Merlin:
  - active context = Chrome + facebook.com
  - command intent = search
  - learned target exists
  - evidence still matches
  - focus search field
  - type "Jane Doe"
  - press Enter
  - verify success
```

No pointing needed.

This tier should be checked before broad generic fallback once the target has enough confidence.

Recommended routing order:

```text
1. Trusted app integration
2. Learned UI control memory
3. Generic app-type profile
4. Universal screen control
5. Visual disambiguation fallback
```

Alternative order during early MVP:

```text
1. Trusted app integration
2. Generic app-type profile
3. Learned UI control memory
4. Universal screen control
5. Visual disambiguation fallback
```

But long-term, learned memory should likely come before generic profiles when confidence is high, because it reflects the user’s actual local setup.

---

## 5.3 Tier 3 — Generic App-Type Profiles

Generic profiles handle common interaction patterns without being tied to a specific app.

Examples:

```text
BrowserProfile
ChatAppProfile
FileManagerProfile
MediaAppProfile
EditorProfile
SettingsProfile
FormProfile
InstallerProfile
```

These profiles understand categories of UI behavior.

### BrowserProfile

Capabilities:

```text
open URL
focus address bar
search web
search current website/page
find page search input
click link/button
fill field
submit form
switch tab
refresh/go back
```

Example:

```text
User: "Search for John Doe."
Active context: browser on facebook.com
BrowserProfile:
  - find search input by UIA/OCR/DOM-like signals if available
  - focus input
  - type query
  - press Enter
  - verify page changed or results visible
```

### ChatAppProfile

Capabilities:

```text
find contact/chat
open conversation
read visible message snippets
focus message box
draft message
send only after confirmation
```

Example:

```text
User: "Open chat with John."
ChatAppProfile:
  - search visible conversation list for John
  - if not visible, find chat search field
  - type John
  - select best result
  - verify header contains John
```

### FileManagerProfile

Capabilities:

```text
find file/folder
open item
search folder
rename
copy/move
open context menu
```

### FormProfile

Capabilities:

```text
find label/input pairs
fill known field
move through fields
select dropdown
submit only after confirmation when needed
```

Generic profiles should still use shared primitives and learned memory.

---

## 5.4 Tier 4 — Universal Screen Control

Universal screen control is the lowest generic automation layer.

It does not know the app. It only sees:

```text
active window
process name
window title
UI Automation tree
screenshot
OCR text
visual candidates
mouse position
keyboard focus
```

It supports primitives:

```text
find visible text
find clickable item
find input field
find button
find list item
click
focus
type
clear
press key
scroll
wait for UI change
verify
```

Example:

```text
User: "Click export."

UniversalScreenControl:
  1. Search UIA tree for Button/ListItem/MenuItem named Export.
  2. If found, invoke/click.
  3. If not found, OCR screenshot for "Export".
  4. If found, click detected region.
  5. Verify menu/dialog/download appeared.
  6. If ambiguous, ask user to choose.
```

Universal screen control should always prefer semantic actions over blind coordinates.

Preferred action order:

```text
1. UI Automation patterns: Invoke, Value, Selection, ExpandCollapse, Scroll, etc.
2. Focus element and type.
3. Keyboard shortcuts.
4. Click element bounds.
5. Raw coordinate click as last resort.
```

---

## 5.5 Tier 5 — Visual Disambiguation / Point-To-Target Fallback

Visual disambiguation is used when Merlin cannot confidently resolve a target.

Example prompts:

```text
"I can’t find the search field. Point at it."
"I found three possible matches. Which one?"
"I’m not sure which John you mean. Say a number or point at the right one."
```

Supported selection methods over time:

```text
numbered overlay
mouse-hover confirmation
manual mouse position capture
webcam hand pointing
gesture-controlled cursor/circle
```

Recommended MVP order:

```text
1. Numbered overlay
2. Mouse-hover confirmation
3. Webcam / gesture pointer later
```

The visual disambiguation service should be generic and app-independent. It answers only one question:

```text
Which visible target does the user mean?
```

After that, the rest of the UI engine executes the original command and learns from the correction.

---

## 6. UI Control Memory DB

## 6.1 Purpose

Create a new local database or database area called:

```text
UiControlMemoryDb
```

Alternative names:

```text
Merlin.ControlMemory
Merlin.UiControlMemory
LearnedControlTargetDb
UiAffordanceMemoryDb
```

Recommended name:

```text
UiControlMemoryDb
```

This database is separate from normal memory.

Normal memory stores facts/preferences/conversation context:

```text
Jarno prefers medium-long technical answers.
Jarno is building Merlin with .NET and Godot.
```

UI control memory stores learned UI affordances and action recipes:

```text
On facebook.com, the search input is usually near the top-left and has nearby text "Search Facebook".
For the action "search", focus it, clear it, type the query, and press Enter.
```

### Why separate?

Because UI control memory has a different lifecycle, risk profile, schema, privacy model, and usage pattern.

Normal memory answers:

```text
What does the user prefer?
What facts should Merlin remember?
What context should be included in prompts?
```

UI control memory answers:

```text
In this app/site/window, where is the target for this action?
What evidence identifies it?
How should Merlin interact with it?
How confident are we that this still works?
When did it last succeed/fail?
```

---

## 6.2 Do Not Store Only Coordinates

Bad memory:

```json
{
  "app": "Facebook",
  "action": "search",
  "x": 142,
  "y": 96
}
```

This breaks when:

```text
window size changes
browser zoom changes
DPI changes
monitor changes
window moves
sidebar opens/closes
website layout changes
language changes
user switches browser
app updates
```

Better memory stores a target recipe with multiple signals:

```json
{
  "context": {
    "kind": "website",
    "host": "facebook.com",
    "browser": "chrome",
    "path_pattern": "*"
  },
  "intent": "search",
  "target_label": "search field",
  "target_kind": "search_input",
  "preferred_strategy": "uia_then_ocr_then_relative_position",
  "uia_signature": {
    "control_type": "Edit",
    "name_contains": ["Search", "Facebook"],
    "supports_value_pattern": true
  },
  "visual_signature": {
    "nearby_text": ["Search Facebook"],
    "relative_region": "top_left",
    "approx_bounds_normalized": {
      "x": 0.05,
      "y": 0.04,
      "w": 0.22,
      "h": 0.04
    }
  },
  "action_recipe": {
    "steps": [
      "focus_target",
      "clear_existing_text",
      "type_parameter:query",
      "press_enter"
    ]
  },
  "verification": {
    "expected_change": "url_or_results_change"
  },
  "confidence": 0.82,
  "success_count": 3,
  "failure_count": 0
}
```

Coordinates can be stored, but only as one weak signal among multiple stronger signals.

---

## 6.3 What Should Be Stored

Store structural/actionable data:

```text
app/site/window context
intent/action name
target label
target kind
UI Automation signature
OCR/nearby text signature
relative location
normalized bounds
action recipe
verification recipe
confidence
success/failure counters
last success/failure timestamps
corrections
```

Do not store by default:

```text
full screenshots
full OCR page dumps
private message contents
email bodies
passwords
payment details
2FA codes
sensitive form values
```

Screenshots should be temporary for observation/debugging unless explicit debug mode is enabled.

---

## 7. Proposed Database Schema

For MVP, start simple. For long-term, model context, targets, signals, recipes, executions, corrections, and confidence events separately.

## 7.1 MVP Tables

MVP can use:

```text
UiControlTargets
UiControlExecutions
```

### UiControlTargets MVP Fields

```text
Id
ContextKind
AppProcessName
AppDisplayName
WindowTitlePattern
BrowserName
WebsiteHost
WebsitePathPattern
IntentName
TargetLabel
TargetKind
PreferredStrategy
RiskLevel
UiaSignatureJson
VisualSignatureJson
RelativeBoundsJson
ActionRecipeJson
VerificationJson
Confidence
SuccessCount
FailureCount
IsEnabled
CreatedAtUtc
UpdatedAtUtc
LastSuccessAtUtc
LastFailureAtUtc
```

### UiControlExecutions MVP Fields

```text
Id
TargetId nullable
CommandText
ResolvedIntent
ContextJson
ParametersJson
ExecutionStatus
UsedStrategy
ConfidenceBefore
ConfidenceAfter
FailureReason
StartedAtUtc
CompletedAtUtc
```

This is enough to prove the feature.

---

## 7.2 Full Schema

Long-term schema:

```text
UiControlContexts
UiControlTargets
UiControlTargetSignals
UiControlRecipes
UiControlExecutions
UiControlCorrections
UiControlConfidenceEvents
```

---

## 7.3 UiControlContexts

Represents where a learned target applies.

Examples:

```text
Discord desktop app
Chrome on facebook.com
Chrome on gmail.com
File Explorer
Unknown app with window title pattern
```

Fields:

```text
Id
ContextKind
AppProcessName
AppDisplayName
WindowTitlePattern
BrowserName
WebsiteHost
WebsitePathPattern
UrlPattern
DeviceProfileId
MonitorProfileId
DpiScale
CreatedAtUtc
UpdatedAtUtc
```

Context kinds:

```text
desktop_app
website
browser
windows_shell
unknown_window
```

Example:

```json
{
  "contextKind": "website",
  "appProcessName": "chrome.exe",
  "appDisplayName": "Chrome",
  "browserName": "Chrome",
  "websiteHost": "facebook.com",
  "websitePathPattern": "*"
}
```

---

## 7.4 UiControlTargets

Represents a learned UI target.

Examples:

```text
Facebook search bar
Discord quick switcher
WhatsApp message box
Upload button on a specific website
Export button in a specific tool
```

Fields:

```text
Id
ContextId
IntentName
TargetLabel
TargetKind
PreferredStrategy
RiskLevel
IsEnabled
Confidence
SuccessCount
FailureCount
LastSuccessAtUtc
LastFailureAtUtc
CreatedAtUtc
UpdatedAtUtc
```

Target kinds:

```text
input
search_input
button
link
list_item
menu_item
tab
chat_item
message_box
file_picker
unknown_click_target
```

Example:

```json
{
  "intentName": "search",
  "targetLabel": "search field",
  "targetKind": "search_input",
  "preferredStrategy": "uia_then_ocr_then_relative_position",
  "riskLevel": "low",
  "confidence": 0.82,
  "successCount": 4,
  "failureCount": 1
}
```

---

## 7.5 UiControlTargetSignals

Stores evidence used to relocate the target.

A target should have multiple signals, because a single signal can fail.

Signal types:

```text
uia
ocr
visual_region
relative_bounds
nearby_text
keyboard_shortcut
dom_hint
historical_coordinate
```

Fields:

```text
Id
TargetId
SignalType
SignalJson
Weight
CreatedAtUtc
LastMatchedAtUtc
MatchSuccessCount
MatchFailureCount
```

Example UIA signal:

```json
{
  "signalType": "uia",
  "weight": 0.9,
  "signalJson": {
    "controlType": "Edit",
    "nameContains": ["Search", "Facebook"],
    "automationId": null,
    "className": null,
    "supportsValuePattern": true
  }
}
```

Example OCR signal:

```json
{
  "signalType": "ocr",
  "weight": 0.65,
  "signalJson": {
    "nearbyText": ["Search Facebook"],
    "expectedRegion": "top_left"
  }
}
```

Example relative bounds signal:

```json
{
  "signalType": "relative_bounds",
  "weight": 0.4,
  "signalJson": {
    "x": 0.052,
    "y": 0.041,
    "w": 0.220,
    "h": 0.038,
    "relativeTo": "active_window"
  }
}
```

---

## 7.6 UiControlRecipes

Represents how to act once the target is found.

Fields:

```text
Id
TargetId
RecipeName
ParametersJson
StepsJson
VerificationJson
RequiresConfirmation
CreatedAtUtc
UpdatedAtUtc
```

Example search recipe:

```json
{
  "recipeName": "search_with_query",
  "parametersJson": {
    "query": "string"
  },
  "stepsJson": [
    { "type": "focus_target" },
    { "type": "clear_existing_text" },
    { "type": "type_parameter", "parameter": "query" },
    { "type": "press_key", "key": "Enter" }
  ],
  "verificationJson": {
    "type": "expected_ui_change",
    "signals": [
      "url_changed",
      "results_visible",
      "input_contains_query"
    ]
  },
  "requiresConfirmation": false
}
```

---

## 7.7 UiControlExecutions

Stores actual attempts.

Useful for debugging, confidence updates, analytics, and regression testing.

Fields:

```text
Id
TargetId nullable
CommandText
ResolvedIntent
ContextJson
ParametersJson
ExecutionStatus
UsedStrategy
ConfidenceBefore
ConfidenceAfter
FailureReason
StartedAtUtc
CompletedAtUtc
```

Execution statuses:

```text
success
failed_target_not_found
failed_verification
cancelled_by_user
blocked_by_safety
requires_confirmation
```

---

## 7.8 UiControlCorrections

Stores user corrections.

Example:

```text
Merlin clicked the wrong "John".
User says: "No, the lower one."
User points at correct target.
Merlin records correction.
```

Fields:

```text
Id
OriginalExecutionId
CorrectionType
UserCorrectionText
PointedCoordinateJson
CorrectedTargetId
CreatedAtUtc
```

Correction types:

```text
pointed_target
selected_overlay_number
voice_correction
manual_mouse_position
cancelled_wrong_target
```

---

## 7.9 UiControlConfidenceEvents

Stores confidence changes over time.

Fields:

```text
Id
TargetId
EventType
OldConfidence
NewConfidence
Reason
CreatedAtUtc
```

Event types:

```text
created_candidate
promoted_after_success
increased_after_repeated_success
decreased_after_failure
disabled_after_repeated_failure
manually_confirmed
manually_rejected
```

---

## 8. Learning Lifecycle

A learned target should not become trusted immediately.

Lifecycle:

```text
candidate
↓
confirmed
↓
trusted
↓
stale
↓
disabled / relearned
```

---

## 8.1 Candidate

Created when the user points to or selects a target.

Initial confidence:

```text
0.40 - 0.55
```

Candidate means:

```text
Merlin has seen this target once.
Merlin may try it again, but should verify carefully.
```

---

## 8.2 Confirmed

After one or two successful uses.

Confidence:

```text
0.65 - 0.80
```

Confirmed means:

```text
Merlin can use it automatically for low-risk actions when evidence matches.
```

---

## 8.3 Trusted

After repeated success.

Confidence:

```text
0.85 - 0.95
```

Trusted means:

```text
Merlin should prefer this target before generic fallback.
```

Even trusted targets still require verification.

---

## 8.4 Stale

A target becomes stale if:

```text
it has not been used for a long time
it recently failed
context changed
UIA/OCR evidence no longer matches
app/site layout changed
```

Stale targets should require stronger matching or user confirmation.

---

## 8.5 Disabled / Relearned

After repeated failure, disable or relearn.

Example response:

```text
"I used to know where the Facebook search field was, but the layout seems to have changed. Point at it once and I’ll relearn it."
```

---

## 9. Confidence Model

Merlin should use confidence to decide whether to act, ask, or fall back.

Suggested thresholds:

```text
>= 0.85
  act automatically for low-risk actions

0.65 - 0.85
  act if verification is strong
  otherwise ask quick confirmation

0.40 - 0.65
  show overlay or ask user to confirm target

< 0.40
  do not act
  use generic control or visual disambiguation
```

Confidence factors:

```text
context match
intent match
UIA signature match
OCR nearby text match
relative position match
historical coordinate match
success history
recent failure history
risk level
number of similar candidates
current window size/DPI match
```

Example weighted heuristic:

```text
context match: +0.25
intent match: +0.20
UIA signature match: +0.25
OCR nearby text match: +0.15
relative region match: +0.10
success history: +0.10
duplicate ambiguous candidates: -0.20
recent failure: -0.25
context mismatch: hard reject
```

MVP does not need machine learning. A transparent weighted heuristic is better at first.

---

## 10. Visual Disambiguation Mode

## 10.1 Purpose

Visual disambiguation lets the user help Merlin when Merlin cannot confidently resolve a target.

Instead of failing:

```text
"I cannot find it."
```

Merlin should say:

```text
"I can’t find it. Point at it."
"I found multiple options. Which one?"
```

This keeps the interaction flowing.

---

## 10.2 Numbered Overlay

The most reliable MVP selection mechanism.

Merlin identifies possible targets and overlays numbers:

```text
1 Search field
2 Browser address bar
3 Search this page
```

User says:

```text
"Number 1."
"The first one."
"Use two."
```

Acceptance criteria:

```text
Merlin can show overlay boxes/numbers on top of candidate UI elements.
User can select by voice.
Merlin maps selection back to target element.
Merlin can cancel overlay.
```

---

## 10.3 Mouse-Hover Confirmation

Simple and useful.

Flow:

```text
Merlin: "Move your mouse over it and say 'here'."
User hovers over target.
User: "Here it is."
Merlin captures current mouse coordinate.
Merlin inspects element under cursor.
```

This avoids webcam complexity and should probably be implemented before gesture pointing.

---

## 10.4 Webcam Pointing

Longer-term.

Merlin can activate webcam pointer mode only after explicit user-facing indication.

Good UX:

```text
Merlin: "I’m opening pointer mode. Point at the target. Say cancel to stop."
UI shows: camera active / pointer mode active
```

Bad UX:

```text
Merlin silently starts webcam.
```

Do not silently activate camera.

---

## 10.5 Gesture-Controlled Cursor

Instead of trying to calculate exact physical finger raycasts, use a hand-controlled on-screen cursor/circle.

Flow:

```text
User moves hand.
Merlin moves circle/crosshair on screen.
Circle reaches target.
User says: "Here it is."
Merlin captures target.
```

This is likely more reliable than pure finger-to-screen raycasting.

---

## 10.6 Pointing Calibration

If physical pointing is used, Merlin may need calibration:

```text
Point to top-left.
Point to top-right.
Point to bottom-left.
Point to bottom-right.
```

This maps camera coordinates to screen coordinates.

But calibration should not be required for MVP.

---

## 11. Target Capture After User Points

When the user points/selects a target, Merlin should capture a rich target snapshot.

Capture process:

```text
1. Get active app/window/site context.
2. Get selected coordinate or selected overlay target.
3. Inspect UI Automation element at coordinate.
4. Capture element bounds, role, name, value, automation id, class name.
5. Capture supported patterns/actions.
6. OCR nearby region.
7. Store normalized bounds relative to active window.
8. Infer target kind.
9. Infer action recipe from original command.
10. Execute requested action.
11. Verify success.
12. Save or update learned target.
```

Example:

```text
Original command:
  "Search for John Doe."

Pointed target:
  Facebook search field.

Inferred target:
  search_input

Inferred recipe:
  focus target
  clear text
  type query
  press Enter
```

---

## 12. Action Recipes

A target answers:

```text
Where is the thing?
```

A recipe answers:

```text
What should Merlin do with it?
```

Merlin should learn and execute recipes, not just click locations.

---

## 12.1 Search Recipe

```json
{
  "name": "search_with_query",
  "parameters": ["query"],
  "steps": [
    { "type": "focus_target" },
    { "type": "clear_existing_text" },
    { "type": "type_parameter", "parameter": "query" },
    { "type": "press_key", "key": "Enter" }
  ],
  "verification": [
    "input_contains_query",
    "url_changed_or_results_visible"
  ],
  "requiresConfirmation": false
}
```

---

## 12.2 Open Chat Recipe

```json
{
  "name": "open_chat",
  "parameters": ["contactName"],
  "steps": [
    { "type": "focus_search_or_chat_list" },
    { "type": "type_parameter", "parameter": "contactName" },
    { "type": "select_best_matching_result" }
  ],
  "verification": [
    "conversation_header_contains:contactName"
  ],
  "requiresConfirmation": false
}
```

---

## 12.3 Draft Message Recipe

```json
{
  "name": "draft_message",
  "parameters": ["message"],
  "steps": [
    { "type": "focus_message_box" },
    { "type": "type_parameter", "parameter": "message" }
  ],
  "verification": [
    "message_box_contains:message"
  ],
  "requiresConfirmation": false
}
```

---

## 12.4 Send Message Recipe

```json
{
  "name": "send_message",
  "parameters": [],
  "steps": [
    { "type": "press_enter_or_click_send" }
  ],
  "verification": [
    "message_appeared_in_conversation"
  ],
  "requiresConfirmation": true
}
```

Important:

```text
Drafting a message and sending a message are separate actions.
```

---

## 12.5 Upload File Recipe

```json
{
  "name": "upload_file",
  "parameters": ["filePath"],
  "steps": [
    { "type": "click_upload_button" },
    { "type": "wait_for_file_picker" },
    { "type": "enter_file_path", "parameter": "filePath" },
    { "type": "confirm_file_picker" }
  ],
  "verification": [
    "file_name_visible",
    "upload_started_or_completed"
  ],
  "requiresConfirmation": true
}
```

---

## 12.6 Click Button Recipe

```json
{
  "name": "click_button",
  "parameters": [],
  "steps": [
    { "type": "invoke_or_click_target" }
  ],
  "verification": [
    "expected_ui_change"
  ],
  "requiresConfirmation": false
}
```

Confirmation depends on button risk.

---

## 13. Safety Model

Merlin must classify UI actions by risk.

---

## 13.1 Low-Risk Actions

Usually safe to perform automatically when confidence is high:

```text
open app
focus app
open search
search current site
click navigation item
open chat
open menu
scroll
go back
switch tab
focus field
type into search box
```

---

## 13.2 Medium-Risk Actions

May require confirmation depending on context:

```text
fill form field
upload file
change filter
open external link
download file
join meeting
start call
change visible UI setting
```

---

## 13.3 High-Risk Actions

Always require confirmation:

```text
send message
post publicly
submit form
purchase
checkout
transfer money
delete
archive
install software
change password
grant permission
accept invite
decline invite
modify account settings
share private file
```

Example:

```text
Merlin may type a WhatsApp message.
Merlin must ask before sending it.
```

---

## 13.4 Safety Gates

Before action execution:

```text
classify risk
check confidence
check target ambiguity
check whether recipe requires confirmation
check whether active context is sensitive
```

After action execution:

```text
verify result
log execution
if high-risk and not confirmed, do not finalize irreversible action
```

---

## 13.5 Sensitive Contexts

Merlin should be extra careful in:

```text
banking websites
payment pages
password managers
login pages
2FA pages
medical portals
government websites
work admin panels
cloud consoles
```

For these contexts:

```text
require explicit user instruction
require confirmation before submission
avoid storing detailed OCR
avoid storing values
prefer user-guided interaction
```

---

## 14. Privacy Model

## 14.1 Screenshot Policy

Default:

```text
Screenshots are temporary.
Do not store screenshots in UiControlMemoryDb.
```

Store structural signals only:

```text
app/site
window title pattern
element type
element name
nearby OCR keywords
relative region
normalized bounds
action recipe
success/failure counters
```

Debug mode may store screenshots only if explicitly enabled.

---

## 14.2 OCR Redaction

OCR should be minimized.

Avoid storing:

```text
full page text
private messages
email bodies
names from unrelated contexts
passwords
payment information
medical/government data
```

Store only small nearby target labels when useful:

```text
"Search Facebook"
"Upload"
"Export"
"Message"
```

---

## 14.3 Webcam Privacy

Webcam should never activate silently.

Required UX:

```text
spoken notification
visible UI indicator
cancel command
clear pointer mode state
```

Example:

```text
"I’m opening pointer mode. Say cancel to stop."
```

---

## 15. Service Architecture

Proposed services:

```text
UniversalUiControlService
  ├── UiCommandRouter
  ├── ActiveContextService
  ├── AppControllerRegistry
  ├── GenericProfileRegistry
  ├── UiControlMemoryService
  ├── UiObservationService
  ├── UiTargetResolver
  ├── UiActionExecutor
  ├── UiVerificationService
  ├── VisualDisambiguationService
  └── UiControlLearningService
```

---

## 15.1 UniversalUiControlService

Main orchestration service.

Responsibilities:

```text
receive normalized UI command
determine active context
choose control tier
execute action
verify result
fallback if needed
record execution
trigger learning after correction/success
```

Pseudo-code:

```csharp
public async Task<UiControlResult> HandleAsync(
    UiCommand command,
    CancellationToken cancellationToken)
{
    var context = await activeContext.GetCurrentAsync(cancellationToken);

    var appController = appControllers.TryResolve(context, command);
    if (appController is not null)
    {
        var result = await appController.ExecuteAsync(command, context, cancellationToken);
        if (result.Success || result.BlockedBySafety)
            return result;
    }

    var learned = await controlMemory.TryResolveAsync(command, context, cancellationToken);
    if (learned.HasUsableCandidate)
    {
        var result = await learnedExecutor.ExecuteAsync(learned, command, context, cancellationToken);
        if (result.Success)
            return result;
    }

    var profile = profiles.TryResolve(context, command);
    if (profile is not null)
    {
        var result = await profile.ExecuteAsync(command, context, cancellationToken);
        if (result.Success || result.BlockedBySafety)
            return result;
    }

    var genericResult = await genericScreenControl.ExecuteAsync(command, context, cancellationToken);
    if (genericResult.Success || genericResult.BlockedBySafety)
        return genericResult;

    return await visualDisambiguation.ResolveWithUserAsync(command, context, cancellationToken);
}
```

---

## 15.2 ActiveContextService

Determines the current app/site/window context.

Data sources:

```text
active window handle
process name
window title
browser URL if available
monitor bounds
DPI scaling
focused element
foreground window state
```

Example output:

```json
{
  "contextKind": "website",
  "processName": "chrome.exe",
  "appDisplayName": "Chrome",
  "windowTitle": "Facebook - Google Chrome",
  "browserName": "Chrome",
  "websiteHost": "facebook.com",
  "url": "https://www.facebook.com/"
}
```

Browser URL detection can initially be limited or optional. If not available, use window title and UI observation.

---

## 15.3 UiObservationService

Creates a snapshot of visible/interactable UI elements.

Observation sources:

```text
Windows UI Automation
screenshot capture
OCR
focused element
mouse position
known learned targets
```

Output:

```json
{
  "activeWindow": {
    "title": "Facebook - Google Chrome",
    "bounds": {
      "x": 0,
      "y": 0,
      "w": 1920,
      "h": 1080
    }
  },
  "elements": [
    {
      "id": "uia:123",
      "kind": "input",
      "name": "Search Facebook",
      "bounds": {
        "x": 82,
        "y": 48,
        "w": 240,
        "h": 42
      },
      "source": "uia",
      "confidence": 0.92
    }
  ]
}
```

---

## 15.4 UiTargetResolver

Converts command + context + observation into a target.

Responsibilities:

```text
map command to target kind
match target label
rank candidates
use learned target signals
handle ambiguity
return target + confidence
```

Example:

```text
Command: "Search for John Doe."
Intent: search
Target kind: search_input
Parameter: query = "John Doe"
```

---

## 15.5 UiActionExecutor

Executes actions using safest available method.

Priority:

```text
1. UIA InvokePattern / ValuePattern / SelectionPattern
2. Focus element and type
3. Keyboard shortcuts
4. Mouse click on element bounds
5. Raw coordinate click fallback
```

Supported primitives:

```text
focus_target
invoke_target
click_target
clear_existing_text
type_text
type_parameter
press_key
scroll
wait_for_change
```

---

## 15.6 UiVerificationService

Verifies result after action.

Rule:

```text
Clicking is not success.
Verified result is success.
```

Verification examples:

```text
active window changed
URL changed
input contains typed text
expected text became visible
dialog opened
selected item changed
chat header contains contact name
download started
file picker opened
message appeared in draft box
```

---

## 15.7 VisualDisambiguationService

Handles user-guided target selection.

Responsibilities:

```text
show numbered overlay
show target circle
listen for selection phrases
capture mouse coordinate
capture webcam/gesture pointer later
support cancel
return selected target
```

Phrases:

```text
here
here it is
that one
this one
use this
number one
number two
cancel
never mind
```

---

## 15.8 UiControlLearningService

Turns corrections into learned control memory.

Responsibilities:

```text
create candidate target
update existing target if similar
store target signals
store action recipe
record verification result
increase/decrease confidence
disable stale/broken targets
```

Learning should happen only after successful verification, or as a low-confidence candidate if the user explicitly identifies the target but action has not yet been verified.

---

## 16. C# Models and Interfaces

## 16.1 UiCommand

```csharp
public sealed record UiCommand
{
    public required string RawText { get; init; }
    public required string IntentName { get; init; }
    public string? TargetLabel { get; init; }
    public string? TargetKind { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
    public UiRiskLevel RiskLevel { get; init; } = UiRiskLevel.Low;
}
```

---

## 16.2 UiContext

```csharp
public sealed record UiContext
{
    public required string ContextKind { get; init; }
    public string? ProcessName { get; init; }
    public string? AppDisplayName { get; init; }
    public string? WindowTitle { get; init; }
    public string? BrowserName { get; init; }
    public string? WebsiteHost { get; init; }
    public string? Url { get; init; }
    public Rect ActiveWindowBounds { get; init; }
    public double DpiScale { get; init; } = 1.0;
}
```

---

## 16.3 UiElementSnapshot

```csharp
public sealed record UiElementSnapshot
{
    public required string Id { get; init; }
    public required string Source { get; init; } // uia, ocr, vision, learned
    public required string Kind { get; init; }
    public string? Name { get; init; }
    public string? Value { get; init; }
    public Rect Bounds { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<string> SupportedActions { get; init; } = [];
}
```

---

## 16.4 UiResolvedTarget

```csharp
public sealed record UiResolvedTarget
{
    public required UiElementSnapshot Element { get; init; }
    public required string ResolutionSource { get; init; }
    public double Confidence { get; init; }
    public string? LearnedTargetId { get; init; }
    public bool RequiresUserConfirmation { get; init; }
}
```

---

## 16.5 UiControlResult

```csharp
public sealed record UiControlResult
{
    public bool Success { get; init; }
    public bool BlockedBySafety { get; init; }
    public bool RequiresUserConfirmation { get; init; }
    public string? MessageForUser { get; init; }
    public string? FailureReason { get; init; }
    public UiResolvedTarget? Target { get; init; }
    public double Confidence { get; init; }
}
```

---

## 16.6 IUiControlService

```csharp
public interface IUiControlService
{
    Task<UiControlResult> ExecuteAsync(
        UiCommand command,
        CancellationToken cancellationToken);
}
```

---

## 16.7 IUiObservationService

```csharp
public interface IUiObservationService
{
    Task<UiObservationSnapshot> ObserveAsync(
        UiContext context,
        CancellationToken cancellationToken);
}
```

---

## 16.8 IUiControlMemoryService

```csharp
public interface IUiControlMemoryService
{
    Task<IReadOnlyList<LearnedUiTarget>> FindCandidatesAsync(
        UiCommand command,
        UiContext context,
        CancellationToken cancellationToken);

    Task SaveCandidateAsync(
        UiCommand command,
        UiContext context,
        UiResolvedTarget target,
        UiControlResult result,
        CancellationToken cancellationToken);

    Task RecordExecutionAsync(
        LearnedUiTarget? target,
        UiControlResult result,
        CancellationToken cancellationToken);

    Task AdjustConfidenceAsync(
        string targetId,
        UiConfidenceEvent confidenceEvent,
        CancellationToken cancellationToken);
}
```

---

## 17. LLM Boundary

The model should help interpret commands, but it should not directly control pixels unless absolutely necessary.

Good model output:

```json
{
  "intent": "search",
  "targetKind": "search_input",
  "parameters": {
    "query": "John Doe"
  },
  "riskLevel": "low"
}
```

Bad model output:

```json
{
  "clickX": 153,
  "clickY": 88
}
```

The deterministic UI layer should own coordinate resolution.

The LLM may help with:

```text
intent classification
target label extraction
parameter extraction
ambiguity explanation
candidate ranking when deterministic scoring is inconclusive
natural-language clarification
```

Deterministic code should own:

```text
active context
UI snapshot
target matching
coordinate calculation
action execution
verification
safety enforcement
memory updates
```

---

## 18. Integration with Existing Merlin Flow

Current simplified Merlin backend flow:

```text
WebSocketHandler
→ CommandRouter
→ HybridIntentParser
→ CapabilityRegistry
→ ITool
→ AssistantResponse
```

UI control should plug in as a capability/tool family.

Suggested flow:

```text
Speech transcript
↓
Command normalization
↓
HybridIntentParser
↓
CapabilityClassifier returns ui.control
↓
UiControlTool receives structured command
↓
UniversalUiControlService executes
↓
AssistantResponse / UI state update
```

Important: UI control should integrate with interruption/cancellation.

If the user says:

```text
stop
cancel
no not that one
wait
wrong one
the lower one
```

Merlin should stop or enter correction/disambiguation mode.

---

## 19. Example User Flows

## 19.1 Discord Open Chat

```text
User:
"Open chat with John."

Merlin:
1. Active app/context = Discord.
2. DiscordController exists.
3. Use Ctrl+K quick switcher.
4. Type John.
5. Select best result.
6. Verify chat header contains John.
```

If DiscordController fails:

```text
1. Check learned control memory.
2. Try ChatAppProfile.
3. Try universal screen control.
4. Ask user to point at John or the search field.
5. Learn corrected target.
```

---

## 19.2 Facebook Search

```text
User:
"Open Facebook."

Merlin:
1. BrowserController opens facebook.com.

User:
"Search for John Doe."

Merlin:
1. Active context = Chrome + facebook.com.
2. Check learned target for context=facebook.com + intent=search.
3. Found learned search field recipe.
4. Match UIA/OCR/position evidence.
5. Focus target.
6. Type John Doe.
7. Press Enter.
8. Verify URL/results changed.
```

If no learned memory exists:

```text
1. Try BrowserProfile.
2. Find search input by UIA/OCR.
3. If ambiguous, show overlay.
4. User points/selects.
5. Merlin learns Facebook search target.
```

---

## 19.3 Unknown Website Upload Button

```text
User:
"Upload this file."

Merlin:
1. No app integration.
2. No learned target.
3. BrowserProfile tries to find upload button.
4. OCR finds "Attach" and "Upload".
5. Confidence low because multiple candidates.
6. Merlin asks: "Which upload button?"
7. Overlay shows numbers.
8. User says: "Number 2."
9. Merlin clicks it.
10. File picker opens.
11. Merlin verifies picker opened.
12. Merlin stores learned upload target for this website.
```

---

## 19.4 Wrong Target Correction

```text
User:
"Open chat with John."

Merlin clicks wrong John.

User:
"No, the lower one."

Merlin:
1. Pauses.
2. Enters correction mode.
3. User points/selects correct John.
4. Merlin opens correct chat.
5. Verifies header.
6. Records correction.
7. Decreases confidence for wrong target.
8. Creates/updates correct target.
```

---

## 19.5 Message Draft and Send

```text
User:
"Send Sarah: I’ll be five minutes late."

Merlin:
1. Opens Sarah chat.
2. Focuses message box.
3. Types message.
4. Stops before sending.
5. Asks: "Ready to send?"

User:
"Yes."

Merlin sends.
```

Reason:

```text
Drafting is lower risk.
Sending is high risk.
```

---

## 20. Failure Modes and Mitigations

## 20.1 Wrong Target

Risk:

```text
Merlin clicks the wrong item.
```

Mitigation:

```text
use confidence thresholds
verify result
ask user when ambiguous
allow immediate correction
record failure
decrease confidence
```

---

## 20.2 Layout Changed

Risk:

```text
Stored target no longer exists.
```

Mitigation:

```text
match multiple signals
decay confidence after failed match
fall back to generic resolver
ask user to point again
update learned target
```

---

## 20.3 Duplicate Targets

Risk:

```text
Multiple Johns, multiple search fields, multiple export buttons.
```

Mitigation:

```text
rank by context
rank by role
rank by region
show numbered overlay
ask user to choose
learn selected target
```

---

## 20.4 Unsafe Action

Risk:

```text
Merlin sends, posts, deletes, purchases, or submits accidentally.
```

Mitigation:

```text
risk-level classification
confirmation policy
block high-risk learned auto-actions
separate draft and send actions
```

---

## 20.5 Private Data Capture

Risk:

```text
Control memory stores sensitive text.
```

Mitigation:

```text
do not store screenshots by default
store structural signals only
redact OCR
ignore password/payment/2FA fields
limit logs
```

---

## 21. MVP Implementation Plan

## Phase 1 — UI Observation MVP

Goal:

```text
Merlin can inspect the active window and list visible UI targets.
```

Implement:

```text
ActiveContextService
UiAutomationSnapshotProvider
Basic screenshot provider
UiElementSnapshot model
UiObservationService
Debug endpoint/tool: ui.observe
```

Acceptance criteria:

```text
Merlin can report active process and window title.
Merlin can list visible UIA controls.
Merlin can return element bounds, names, control types.
Merlin can distinguish common controls: buttons, edits, list items, menus.
```

Manual tests:

```text
Windows Settings
File Explorer
Chrome
Discord
Notepad
```

---

## Phase 2 — Basic Action Execution

Goal:

```text
Merlin can focus/click/type into visible UI elements.
```

Implement:

```text
UiActionExecutor
focus element
invoke element
click bounds
type text
press key
clear field
```

Acceptance criteria:

```text
Merlin can focus a visible input.
Merlin can type text.
Merlin can click a visible button.
Merlin prefers UIA action over coordinate click.
Merlin can cancel before/while executing.
```

---

## Phase 3 — Generic Target Resolver

Goal:

```text
Merlin can map commands to generic targets.
```

Implement intents:

```text
search
click
open_item
type_into_field
focus_field
scroll
go_back
```

Acceptance criteria:

```text
"search for John Doe" resolves to search_input + query.
"click export" resolves to button/text target "export".
"open John" resolves to visible list item/text "John".
```

---

## Phase 4 — Visual Disambiguation MVP

Goal:

```text
When confidence is low, Merlin can ask the user to choose a target.
```

Implement first:

```text
numbered overlay
mouse-hover confirmation
voice confirmation phrases
cancel handling
```

Do not start with webcam pointing yet.

Acceptance criteria:

```text
Merlin shows numbered target candidates.
User can say "number 2".
Merlin executes against selected target.
User can cancel disambiguation.
Merlin can capture mouse-hover target after "here".
```

---

## Phase 5 — UI Control Memory DB MVP

Goal:

```text
Merlin stores learned target recipes after user correction.
```

Start with tables:

```text
UiControlTargets
UiControlExecutions
```

Acceptance criteria:

```text
After user points to a search field, Merlin stores a candidate target.
On next similar command, Merlin tries the learned target.
After success, confidence increases.
After failure, confidence decreases.
After repeated failure, learned target is disabled or marked stale.
```

---

## Phase 6 — Generic App-Type Profiles

Goal:

```text
Add reusable profiles for common app categories.
```

Initial profiles:

```text
BrowserProfile
ChatAppProfile
FileManagerProfile
```

Acceptance criteria:

```text
BrowserProfile can search current website.
ChatAppProfile can open visible chat/contact.
FileManagerProfile can open visible file/folder.
Profiles use shared primitives.
Profiles fall back to disambiguation when uncertain.
```

---

## Phase 7 — Trusted App Controllers

Goal:

```text
Add high-confidence integrations for frequent apps.
```

First candidates:

```text
DiscordController
BrowserController
WhatsAppController
FileExplorerController
VSCodeController
```

Acceptance criteria:

```text
App controller is preferred over generic control.
App controller uses shared primitives.
App controller can fall back to learned/generic/disambiguation.
App controller does not bypass safety/verification.
```

---

## Phase 8 — Webcam / Gesture Pointer Mode

Goal:

```text
Add natural pointing for target selection.
```

This comes after overlay/mouse selection works.

Acceptance criteria:

```text
Webcam activation is explicit.
UI clearly shows pointer mode active.
User can cancel.
User can move visual cursor/circle.
User can say "here it is".
Merlin captures target and learns from it.
```

---

## 22. Testing Strategy

## 22.1 Unit Tests

Test:

```text
intent to target-kind mapping
context matching
learned target scoring
confidence updates
risk classification
recipe generation
verification result handling
candidate promotion/staling/disabling
```

Example tests:

```text
SearchCommand_ExtractsQueryAndSearchInputTarget
LearnedTarget_ContextMismatch_IsRejected
LearnedTarget_UiaAndOcrMatch_GetsHighConfidence
FailedExecution_DecreasesConfidence
RepeatedSuccess_PromotesTargetToTrusted
HighRiskAction_RequiresConfirmation
DuplicateCandidates_RequiresDisambiguation
Correction_CreatesCandidateTarget
```

---

## 22.2 Integration Tests

Use controlled fake windows/pages:

```text
fake UIA tree
test WinForms/WPF app
test browser page
duplicate target scenarios
layout changed scenarios
missing target scenarios
```

---

## 22.3 Manual Tests

Scenarios:

```text
Facebook search bar
Discord open chat
WhatsApp message box
File Explorer search
random website upload button
installer next button
settings search
wrong target correction
numbered overlay selection
mouse-hover "here it is"
```

---

## 22.4 Regression Tests

Store anonymized synthetic snapshots, not private screenshots.

Regression cases:

```text
known target found by UIA
known target found by OCR
learned target matched after window resize
learned target rejected after context mismatch
ambiguous duplicate target requires user selection
failed verification decreases confidence
```

---

## 23. Logging and Debugging

UI control will be hard to debug without good logs.

Log at a high level:

```text
command
resolved intent
active context
control tier selected
candidate count
candidate confidence
selected target source
execution method
verification result
memory update
```

Avoid logging private content.

Example log:

```text
[UiControl] Command='search for John Doe' Intent=search Context=website:facebook.com
[UiControl] Tier=LearnedMemory CandidateCount=1 Confidence=0.87
[UiControl] Target=search_input Source=uia+ocr BoundsNorm=(0.05,0.04,0.22,0.04)
[UiControl] Action=focus,type,enter Verification=url_changed Success=true
[UiControl] Confidence 0.87 -> 0.90 Reason=repeated_success
```

Do not log:

```text
full screenshots
full OCR page text
message bodies
passwords
payment data
private document contents
```

---

## 24. Frontend / Overlay Requirements

Merlin frontend should support a transparent/topmost overlay layer for disambiguation.

Features:

```text
highlight candidate bounds
number candidates
show crosshair/circle
show pointer mode active
show cancel hint
optionally show confidence/label in debug mode
```

Overlay should not steal focus unless needed.

For numbered selection:

```text
show number badges near candidates
accept voice command number
optionally accept keyboard number
remove overlay after selection/cancel
```

For pointer mode:

```text
show circle/crosshair
track mouse or gesture position
confirm with voice phrase
remove overlay after confirmation/cancel
```

---

## 25. Interaction with Voice, TTS, and Interruption

UI control is interactive and may need multi-turn correction.

Merlin should be able to say:

```text
"I found multiple options. Which one?"
"Point at it."
"Move your mouse over it and say here."
"Ready to send?"
```

During this, Merlin must keep listening for:

```text
cancel
stop
wrong one
no, the lower one
number two
here it is
that one
yes
no
```

Important:

```text
Visual disambiguation mode should be a clear conversational state.
```

The system should avoid treating these correction phrases as normal unrelated chat.

---

## 26. Suggested Folder / Project Structure

Possible backend structure:

```text
Merlin.Backend/
  Services/
    UiControl/
      UniversalUiControlService.cs
      ActiveContextService.cs
      Observation/
        UiObservationService.cs
        UiAutomationSnapshotProvider.cs
        ScreenshotProvider.cs
        OcrProvider.cs
      Resolution/
        UiTargetResolver.cs
        LearnedTargetResolver.cs
        CandidateScorer.cs
      Execution/
        UiActionExecutor.cs
        KeyboardMouseExecutor.cs
        UiAutomationActionExecutor.cs
      Verification/
        UiVerificationService.cs
      Learning/
        UiControlLearningService.cs
        UiControlMemoryService.cs
      Disambiguation/
        VisualDisambiguationService.cs
        OverlayCandidateService.cs
      Profiles/
        BrowserProfile.cs
        ChatAppProfile.cs
        FileManagerProfile.cs
      Controllers/
        DiscordController.cs
        BrowserController.cs
        WhatsAppController.cs
  Data/
    UiControl/
      UiControlDbContext.cs
      Entities/
        UiControlTargetEntity.cs
        UiControlExecutionEntity.cs
        ...
```

Frontend:

```text
Merlin.Frontend/
  UiControlOverlay/
    TargetOverlay.gd/cs
    PointerCircle.gd/cs
    CandidateBadge.gd/cs
```

---

## 27. Agent Implementation Guidance

When giving this to an implementation agent, require the agent to:

```text
read current Merlin architecture first
identify existing services/capability registration patterns
avoid bypassing current command routing
avoid bypassing cancellation/interruption state
implement in small phases
add tests per phase
keep UI control memory separate from conversational memory
avoid storing screenshots/private text by default
```

Do not let the agent immediately implement webcam pointing. Start with observation, action execution, overlay/mouse disambiguation, and memory DB.

---

## 28. First Agent Prompt Suggestion

```text
SYSTEM:
You are working in the Merlin repository. Implement the first phase of the Universal UI Control Layer described in Merlin.ToDo/ui_control/merlin_universal_ui_control_layer_design.md.

GOAL:
Implement Phase 1 only: UI Observation MVP.

SCOPE:
- Add ActiveContextService for foreground window/process/title detection.
- Add UiElementSnapshot and UiObservationSnapshot models.
- Add UiAutomationSnapshotProvider that reads visible/interactable controls from the active window using Windows UI Automation.
- Add UiObservationService that combines active context + UIA snapshot.
- Add a debug/internal capability or endpoint named ui.observe that returns the active context and visible element summaries.
- Add unit tests where practical and at least one integration-style test using a fake/provider abstraction if direct UIA testing is brittle.

CONSTRAINTS:
- Do not implement clicking/typing yet.
- Do not implement memory DB yet.
- Do not implement webcam/gesture pointing yet.
- Do not store screenshots.
- Do not bypass Merlin’s existing command/capability architecture.
- Keep code cancellation-aware.
- Keep logs privacy-safe.

ACCEPTANCE CRITERIA:
- Merlin can report active process and window title.
- Merlin can list visible UIA controls with name, control type, bounds, and supported action hints when available.
- The code compiles.
- Existing tests still pass.
- New tests cover context detection abstraction and observation mapping.
```

---

## 29. Final Summary

The UI Control Layer should make Merlin capable of controlling external apps/sites without requiring a custom integration for every possible program.

The correct architecture is layered:

```text
Trusted app integrations
↓
Learned UI control memory
↓
Generic app-type profiles
↓
Universal screen control
↓
Visual disambiguation fallback
```

The most important new concept is **UiControlMemoryDb**.

Every time Merlin cannot find something and the user points/selects the correct target, Merlin should treat that as an opportunity to learn:

```text
context + intent + target signals + recipe + verification + confidence
```

This makes Merlin more useful over time without relying on global app hardcoding.

The final UX target:

```text
Merlin uses direct integrations where they make sense.
Merlin uses learned local memory for repeated personal workflows.
Merlin uses generic screen control for unknown apps.
Merlin asks the user to point when uncertain.
Merlin learns from the correction.
Merlin verifies actions.
Merlin asks before risky actions.
```

This is the foundation for a truly personal desktop assistant.
