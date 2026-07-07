---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/site_control_profiles/merlin_site_control_profiles_learning_v1.md
classification: implementation-plan
related_features:
  - Browser Control
  - Browser Workspace
  - Browser Page-Aware Control
  - Site Control Profiles
  - Correction Layer
  - Control Profile DB
status: future
imported_to_vault: true
---

# Merlin Site Control Profiles + Correction Learning

**Status:** Future implementation design  
**Target project:** Merlin local desktop assistant  
**Suggested todo location:** `Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md`  
**Primary goal:** Make Merlin able to control websites reliably, learn from failed clicks, and build per-site action profiles over time instead of hard-patching every website manually.

---

## 1. Why this exists

Merlin already has the beginning of browser page-aware control: it can snapshot a page, inspect elements, and attempt actions such as page clicks or page search. The next step is to make those actions less brittle.

The current problem is that generic browser control can guess wrong:

- User says: `pause the video`
- Merlin sees multiple possible buttons or clickable elements.
- It clicks the wrong thing, maybe an ad, overlay, wrong player, wrong result, or unrelated page control.
- The user has no simple way to teach Merlin what the correct target was.
- The system must then be patched manually by code changes.

The desired behavior is different:

- Merlin tries the action using generic browser intelligence.
- If it fails, the user says: `No, that was wrong.`
- Merlin enters correction mode.
- Merlin asks the user to show the correct target.
- The user clicks manually, or later points with hand/motion control.
- Merlin records a learned per-site rule.
- Next time, Merlin uses the learned rule first.
- If a learned rule becomes bad, Merlin tracks that too and can relearn.

This creates a self-improving browser control layer.

---

## 2. Core concept

The browser control system should have two layers.

### 2.1 Generic browser intelligence

This is the default fallback that works everywhere.

It uses information from the current DOM/page snapshot:

- visible text
- aria labels
- role
- title
- tooltip text
- placeholder text
- button labels
- classes
- ids
- `data-*` attributes
- element type
- bounding box
- visibility
- enabled/disabled state
- z-index / overlay likelihood
- distance to relevant page text
- page URL/domain
- current browser state

This layer should be able to perform reasonable guesses on any website.

Example:

```text
User: pause the video
Generic browser intelligence looks for:
- button role
- aria-label containing pause/play
- title containing pause/play
- class/id suggesting player control
- visible button in video player area
```

### 2.2 Learned per-site profiles

This is the more reliable layer for known sites.

For a site like YouTube, Merlin can learn that:

```text
site: youtube.com
action: pause_video
preferred selector: .ytp-play-button
validation: after click, tooltip or aria label changes from pause to play / play to pause
```

For other apps/sites later:

```text
site: open.spotify.com
action: play_pause_music
preferred selector candidates:
- [data-testid="control-button-playpause"]
- button[aria-label*="Pause"]
- button[aria-label*="Play"]
```

The learned profile should not only store the final selector. It should store the learning history:

- which element Merlin clicked
- what it thought the action was
- whether the user accepted or corrected it
- selectors that worked
- selectors that failed
- page state before and after
- validation evidence
- confidence score
- last successful use
- last failed use

This lets Merlin avoid known bad matches.

---

## 3. Design principle

This feature must not become a dangerous “blind autoclicker.”

The rule is:

> Learned site profiles may improve target selection, but they must not bypass safety validation.

That means:

- Learned selectors still go through `BrowserPageSafetyGuard`.
- Destructive or risky actions still require confirmation.
- A learned rule can be disabled if it repeatedly fails.
- Merlin should prefer DOM-level stable signals over coordinates.
- Coordinates are allowed only as weak fallback evidence.
- Correction learning must be explainable and reversible.

---

## 4. Desired user experience

### 4.1 Normal successful flow

```text
User: pause the video
Merlin:
1. Detects browser context.
2. Resolves current site as youtube.com.
3. Finds learned profile for action pause_video.
4. Finds `.ytp-play-button`.
5. Runs safety guard.
6. Clicks.
7. Verifies expected state changed.
8. Responds briefly: Paused.
```

### 4.2 Generic first-time flow

```text
User: pause the video
Merlin:
1. No learned rule yet.
2. Uses generic browser intelligence.
3. Finds a likely video control button.
4. Clicks it.
5. Verifies the state changed.
6. Stores positive evidence if confidence is high enough.
```

Important: first-time generic success should not immediately create a permanent high-trust rule. It should create a low-confidence candidate rule that becomes stronger after repeated success.

### 4.3 Failed action correction flow

```text
User: pause the video
Merlin clicks wrong element.
User: No, that was wrong.
Merlin: Show me the correct control.
User manually clicks the correct pause button.
Merlin observes the click target.
Merlin records:
- previous action: pause_video
- previous clicked element: bad candidate
- user demonstrated element: good candidate
- domain: youtube.com
- validation result if available
Merlin: Got it. I'll use that control for pausing videos on YouTube.
```

### 4.4 Wrong ad / wrong result example

```text
User: play the video
Merlin accidentally opens an ad or wrong result.
User: No, that was wrong.
Merlin records:
- attempted action: play_video or click_video_result
- clicked element: bad selector/signature
- resulting navigation/state: undesired
- correction needed
Merlin asks for correct target.
```

If the wrong click caused navigation, Merlin should not blindly continue. It should first stabilize context:

- detect URL changed unexpectedly
- optionally ask whether to go back
- avoid saving the wrong target as successful

---

## 5. Vocabulary and canonical actions

Natural language should be normalized into canonical browser actions.

Examples:

| User phrase | Canonical action |
|---|---|
| pause the video | `video.pause` |
| stop the video | `video.pause` |
| play the video | `video.play` |
| resume the video | `video.play` |
| skip the ad | `video.skip_ad` |
| fullscreen | `video.fullscreen.toggle` |
| make it full screen | `video.fullscreen.enter` |
| exit fullscreen | `video.fullscreen.exit` |
| mute | `video.mute` |
| unmute | `video.unmute` |
| next song | `media.next` |
| previous song | `media.previous` |
| pause music | `media.pause` |
| play music | `media.play` |

For V1, keep the action vocabulary small and explicit.

Recommended V1 actions:

```text
video.play
video.pause
video.play_pause.toggle
video.skip_ad
video.fullscreen.toggle
browser.click_result
browser.click_named_control
```

Recommended V1.5/V2 actions:

```text
media.play
media.pause
media.next
media.previous
media.seek
media.volume_up
media.volume_down
media.mute
media.unmute
form.focus_field
form.submit
nav.open_menu
nav.close_modal
```

---

## 6. Site profile resolution

A site profile should be resolved by normalized domain, not full URL.

Examples:

```text
https://www.youtube.com/watch?v=abc -> youtube.com
https://m.youtube.com/watch?v=abc -> youtube.com
https://music.youtube.com/watch?v=abc -> music.youtube.com or youtube.com/music depending on strategy
https://open.spotify.com/track/abc -> open.spotify.com
```

Recommended domain matching levels:

1. Exact host match: `www.youtube.com`
2. Registered domain match: `youtube.com`
3. Optional path-aware profile: `youtube.com/watch`
4. Optional app/site mode: `youtube.com/video_player`

For V1, use:

```text
NormalizedDomain = registered domain, with selected known subdomain exceptions.
```

Suggested exceptions:

```text
www.youtube.com -> youtube.com
m.youtube.com -> youtube.com
music.youtube.com -> music.youtube.com
open.spotify.com -> open.spotify.com
```

---

## 7. Data model

The system needs more than a selector table. It needs a profile and learning history.

Recommended entities:

1. `BrowserSiteProfile`
2. `BrowserSiteActionProfile`
3. `BrowserSelectorCandidate`
4. `BrowserActionAttempt`
5. `BrowserActionCorrection`
6. `BrowserBadSelectorEvidence`
7. `BrowserValidationRule`

The names can change, but the separation matters.

---

## 8. Entity: BrowserSiteProfile

Represents a known website or web app.

```csharp
public sealed class BrowserSiteProfileEntity
{
    public Guid Id { get; set; }
    public string NormalizedDomain { get; set; } = default!;
    public string? DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

Example row:

```text
Id: ...
NormalizedDomain: youtube.com
DisplayName: YouTube
IsEnabled: true
Version: 1
```

---

## 9. Entity: BrowserSiteActionProfile

Represents one canonical action on one site.

```csharp
public sealed class BrowserSiteActionProfileEntity
{
    public Guid Id { get; set; }
    public Guid SiteProfileId { get; set; }
    public string CanonicalAction { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public double Confidence { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

Example:

```text
Site: youtube.com
CanonicalAction: video.skip_ad
Confidence: 0.92
SuccessCount: 17
FailureCount: 1
```

---

## 10. Entity: BrowserSelectorCandidate

Represents one possible way to find the element.

```csharp
public sealed class BrowserSelectorCandidateEntity
{
    public Guid Id { get; set; }
    public Guid SiteActionProfileId { get; set; }

    public string SelectorKind { get; set; } = default!;
    public string SelectorValue { get; set; } = default!;

    public double Confidence { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsUserTaught { get; set; }

    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }

    public string? ElementSignatureJson { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

Suggested `SelectorKind` values:

```text
css
aria_label
role_name
title
text
data_testid
xpath
signature
coordinate_fallback
```

Example selector candidates:

```text
SelectorKind: css
SelectorValue: .ytp-play-button
Confidence: 0.95
IsUserTaught: true
```

```text
SelectorKind: aria_label
SelectorValue: Pause|Play|Pauzeren|Afspelen
Confidence: 0.78
IsUserTaught: false
```

---

## 11. Entity: BrowserActionAttempt

Stores every important attempt, especially failed/corrected ones.

```csharp
public sealed class BrowserActionAttemptEntity
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string NormalizedDomain { get; set; } = default!;
    public string PageUrl { get; set; } = default!;
    public string CanonicalAction { get; set; } = default!;
    public string UserUtterance { get; set; } = default!;

    public Guid? SiteActionProfileId { get; set; }
    public Guid? SelectorCandidateId { get; set; }

    public string? ChosenElementSignatureJson { get; set; }
    public string? ChosenSelectorJson { get; set; }
    public string? PageSnapshotHashBefore { get; set; }
    public string? PageSnapshotHashAfter { get; set; }
    public string? UrlBefore { get; set; }
    public string? UrlAfter { get; set; }

    public string Outcome { get; set; } = default!;
    public string? OutcomeReason { get; set; }
    public double SelectionConfidence { get; set; }
    public double? ValidationConfidence { get; set; }

    public DateTime CreatedUtc { get; set; }
}
```

Suggested `Outcome` values:

```text
success
failed_validation
user_rejected
safety_blocked
confirmation_required
stale_element
not_found
wrong_navigation
unknown
```

This entity is crucial because when the user says `No, that was wrong`, Merlin must know what the previous browser action was.

---

## 12. Entity: BrowserActionCorrection

Stores a correction event linked to a previous attempt.

```csharp
public sealed class BrowserActionCorrectionEntity
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public string CorrectionUtterance { get; set; } = default!;
    public string CorrectionMode { get; set; } = default!;

    public string? CorrectedElementSignatureJson { get; set; }
    public string? CorrectedSelectorJson { get; set; }
    public string? CorrectedPageUrl { get; set; }
    public string? CorrectedPageSnapshotHash { get; set; }

    public bool CreatedPositiveRule { get; set; }
    public Guid? CreatedSelectorCandidateId { get; set; }
    public bool MarkedPreviousSelectorBad { get; set; }

    public DateTime CreatedUtc { get; set; }
}
```

Suggested `CorrectionMode` values:

```text
manual_click_observed
motion_point_observed
keyboard_focus_observed
user_selected_candidate
voice_description
```

V1 should support `manual_click_observed`. Motion pointing can come later.

---

## 13. Entity: BrowserBadSelectorEvidence

Stores negative selector evidence so Merlin can avoid repeated mistakes.

```csharp
public sealed class BrowserBadSelectorEvidenceEntity
{
    public Guid Id { get; set; }
    public string NormalizedDomain { get; set; } = default!;
    public string CanonicalAction { get; set; } = default!;
    public string SelectorKind { get; set; } = default!;
    public string SelectorValue { get; set; } = default!;
    public string? ElementSignatureJson { get; set; }
    public string Reason { get; set; } = default!;
    public int Count { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
```

Example:

```text
Domain: youtube.com
Action: video.play
SelectorKind: css
SelectorValue: .ytp-ad-overlay-close-button
Reason: user_rejected_after_click
Count: 2
```

---

## 14. Entity: BrowserValidationRule

Validation rules define how Merlin knows an action worked.

```csharp
public sealed class BrowserValidationRuleEntity
{
    public Guid Id { get; set; }
    public Guid SiteActionProfileId { get; set; }
    public string ValidationKind { get; set; } = default!;
    public string ValidationValueJson { get; set; } = default!;
    public double Confidence { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

Suggested validation kinds:

```text
tooltip_flip
aria_label_flip
class_change
url_change_expected
media_state_change
visible_text_appears
visible_text_disappears
element_appears
element_disappears
no_unexpected_navigation
```

Example for YouTube pause/play:

```json
{
  "kind": "aria_label_flip",
  "beforeAny": ["Pause", "Pauzeren"],
  "afterAny": ["Play", "Afspelen"]
}
```

Example for YouTube skip ad:

```json
{
  "kind": "element_disappears",
  "selectorAny": [".ytp-skip-ad-button", ".ytp-ad-skip-button"]
}
```

---

## 15. Element signature model

Selectors can break. Coordinates are weak. The system needs a stable element signature.

Suggested signature fields:

```json
{
  "tagName": "button",
  "role": "button",
  "ariaLabel": "Pauzeren (k)",
  "title": "Pauzeren (k)",
  "innerText": "",
  "id": "",
  "classes": ["ytp-play-button", "ytp-button"],
  "dataAttributes": {},
  "type": "button",
  "boundingBox": {
    "x": 24,
    "y": 742,
    "width": 48,
    "height": 48
  },
  "visible": true,
  "enabled": true,
  "nearbyText": [],
  "ancestorHints": [
    "div.html5-video-player",
    "div.ytp-chrome-controls"
  ]
}
```

Important:

- Store class lists, but do not assume all classes are stable.
- Prefer semantic attributes over generated class names.
- For YouTube, some classes are stable enough to be useful.
- For React/Vue apps, `data-testid`, aria labels, and role/name are often better.
- Bounding box should be supportive evidence, not the primary selector.

---

## 16. Selector candidate extraction

When the user manually clicks the correct target, Merlin should generate multiple selector candidates from the demonstrated element.

Example demonstrated element:

```html
<button class="ytp-play-button ytp-button" title="Pauzeren (k)" aria-label="Pauzeren (k)"></button>
```

Generated candidates:

```text
1. css: .ytp-play-button
2. css: button.ytp-play-button
3. aria_label: Pauzeren
4. title: Pauzeren
5. role_name: button/Pauzeren
6. signature: tag=button + class contains ytp-play-button + ancestor contains ytp-chrome-controls
```

Ranking rules:

1. Prefer unique selectors on current page.
2. Prefer selectors that survive after re-snapshot.
3. Prefer semantic selectors.
4. Prefer stable app-known selectors.
5. Penalize generated-looking classes.
6. Penalize selectors matching too many elements.
7. Penalize selectors previously marked bad for this action.
8. Penalize hidden/offscreen/disabled elements.

---

## 17. Action resolution pipeline

Recommended pipeline:

```text
User utterance
↓
Intent/action normalization
↓
Browser context available?
↓
Normalize current site/domain
↓
Try learned site profile candidates
↓
Run generic browser intelligence candidates
↓
Merge/rank candidates
↓
Remove known bad candidates
↓
Safety guard
↓
Confirmation if needed
↓
Execute click/action
↓
Re-snapshot
↓
Validate result
↓
Record attempt outcome
↓
If user later rejects: correction mode
```

Pseudo-code:

```csharp
public async Task<BrowserActionResult> ExecuteBrowserActionAsync(
    BrowserActionRequest request,
    CancellationToken ct)
{
    var context = await browserContextProvider.GetCurrentContextAsync(ct);
    var domain = domainNormalizer.Normalize(context.Url);
    var action = actionNormalizer.Normalize(request.UserUtterance, context);

    var learnedCandidates = await profileService.GetCandidatesAsync(domain, action, ct);
    var genericCandidates = await genericResolver.ResolveCandidatesAsync(context, action, ct);

    var ranked = candidateRanker.Rank(
        learnedCandidates,
        genericCandidates,
        knownBadEvidence: await profileService.GetBadEvidenceAsync(domain, action, ct));

    var selected = ranked.FirstOrDefault();
    if (selected is null)
    {
        return BrowserActionResult.NotFound(action);
    }

    var safetyDecision = await safetyGuard.EvaluateAsync(context, selected, action, ct);
    if (!safetyDecision.CanExecuteImmediately)
    {
        return BrowserActionResult.FromSafetyDecision(safetyDecision);
    }

    var attempt = await attemptStore.CreateStartedAttemptAsync(request, context, selected, ct);

    var clickResult = await pageController.ClickAsync(selected, ct);
    var afterContext = await browserContextProvider.GetCurrentContextAsync(ct);
    var validation = await validator.ValidateAsync(action, context, afterContext, selected, ct);

    await attemptStore.CompleteAttemptAsync(attempt.Id, clickResult, validation, afterContext, ct);
    await profileLearningService.UpdateFromAttemptAsync(attempt.Id, validation, ct);

    return BrowserActionResult.FromValidation(validation);
}
```

---

## 18. Correction mode

Correction mode is a short-lived state owned by the backend.

It should know:

- the last browser action attempt
- the action being corrected
- the element clicked by Merlin
- the page URL before/after
- the user’s correction phrase
- whether the page navigated unexpectedly
- whether manual click observation is currently active

### 18.1 Entering correction mode

Correction mode should trigger when the user says phrases like:

```text
No, that was wrong.
No, not that one.
That was not correct.
You clicked the wrong thing.
Wrong button.
No, go back.
No, I meant the other one.
```

But only if there was a recent browser action attempt.

Recommended time window:

```text
Last browser action attempt within 30 seconds.
```

Maybe extend to 90 seconds if the user is still on the same page and no other action happened.

### 18.2 Correction intent classifier

Add a small deterministic/semantic classifier:

```csharp
public enum BrowserCorrectionIntent
{
    None,
    RejectLastAction,
    RejectAndGoBack,
    DemonstrateCorrectTarget,
    CancelCorrection,
    ConfirmCorrection
}
```

Examples:

```text
No, that was wrong -> RejectLastAction
No, go back -> RejectAndGoBack
This one -> DemonstrateCorrectTarget, if pointing/manual click active
Never click that for pause -> RejectLastAction + negative rule
Cancel -> CancelCorrection
Yes, that's correct -> ConfirmCorrection
```

### 18.3 Manual click observation V1

V1 should support manual click observation before motion pointing.

Flow:

```text
1. User rejects last browser action.
2. Merlin enters correction mode.
3. Merlin asks: “Show me the correct control.”
4. Backend tells browser workspace to observe next user click.
5. User manually clicks the correct element in the browser.
6. Browser workspace reports clicked DOM element signature back to backend.
7. Backend generates selector candidates.
8. Backend stores positive rule.
9. Backend marks previous selector/evidence as bad.
```

Important implementation point:

The system needs a temporary browser-side click listener that captures the clicked element but does not prevent the normal click unless explicitly necessary.

Possible event payload:

```json
{
  "type": "browser.user_click_observed",
  "correlationId": "...",
  "url": "https://www.youtube.com/watch?v=...",
  "domain": "youtube.com",
  "elementSignature": {
    "tagName": "button",
    "role": "button",
    "ariaLabel": "Pauzeren (k)",
    "title": "Pauzeren (k)",
    "classes": ["ytp-play-button", "ytp-button"],
    "boundingBox": { "x": 24, "y": 742, "width": 48, "height": 48 }
  }
}
```

### 18.4 Motion pointing V2

Later, correction mode should support hand/motion control:

```text
Merlin: Show me the correct control.
User points/pinches at the control.
Motion system gives screen/browser coordinates.
Browser workspace maps coordinate to DOM element via document.elementFromPoint.
Backend records the demonstrated element.
```

Important:

- Motion coordinates should map to DOM element, not be stored as raw coordinates only.
- Raw coordinates are allowed as fallback if DOM mapping fails.
- The correction should still produce selector candidates where possible.

---

## 19. Attempt and correction lifecycle

### 19.1 Attempt start

When Merlin is about to click:

```text
Create BrowserActionAttempt with outcome = unknown/started.
Store selected element signature and selector.
```

### 19.2 Attempt completion

After click:

```text
Update attempt outcome:
- success
- failed_validation
- wrong_navigation
- safety_blocked
- not_found
```

### 19.3 User rejection

If user rejects:

```text
Update attempt outcome = user_rejected.
Add bad selector evidence.
Enter correction mode.
```

### 19.4 User demonstration

When user shows correct control:

```text
Create BrowserActionCorrection.
Extract selector candidates.
Create/update BrowserSiteProfile.
Create/update BrowserSiteActionProfile.
Create positive BrowserSelectorCandidate.
Mark previous selector candidate lower confidence or disabled if repeated.
```

### 19.5 Confidence updates

Recommended simple V1 confidence update:

```text
On success:
  confidence += 0.05, max 0.98
On validation failure:
  confidence -= 0.15
On user rejection:
  confidence -= 0.30
On user-taught correction:
  new candidate confidence = 0.85
After 3 confirmed successes:
  confidence = min(confidence, 0.95)
After 3 user rejections:
  disable candidate
```

Use conservative confidence changes. Do not allow one accidental success to permanently dominate.

---

## 20. Validation strategy

Validation is what prevents Merlin from believing every click succeeded.

### 20.1 Validation types

For browser control, validation can be:

1. **DOM state validation**
   - aria label changed
   - title changed
   - class changed
   - target disappeared
   - modal opened/closed

2. **URL validation**
   - URL changed expectedly
   - URL did not change unexpectedly

3. **Media validation**
   - video paused/playing state changed
   - current time started/stopped progressing
   - muted state changed

4. **Visual validation**
   - optional later
   - only if DOM is insufficient

5. **User validation**
   - user does not reject within short window
   - user explicitly says correct/yes

### 20.2 YouTube examples

Pause video:

```text
Before click:
- video.paused == false OR button tooltip/aria says Pause/Pauzeren
After click:
- video.paused == true OR button tooltip/aria says Play/Afspelen
```

Play video:

```text
Before click:
- video.paused == true OR button tooltip/aria says Play/Afspelen
After click:
- video.paused == false OR button tooltip/aria says Pause/Pauzeren
```

Skip ad:

```text
Before click:
- skip ad button visible
After click:
- skip ad button hidden OR ad overlay disappears OR main video resumes
```

Fullscreen toggle:

```text
Before click:
- document.fullscreenElement absent/present
After click:
- document.fullscreenElement changed
```

### 20.3 Localized UI language

This is especially important for Dutch.

YouTube may expose labels/tooltips such as:

```text
Overslaan
Advertentie overslaan
Pauzeren
Afspelen
Volledig scherm
Dempen
Geluid aanzetten
```

English examples:

```text
Skip
Skip ad
Pause
Play
Full screen
Mute
Unmute
```

Validation rules must support multiple languages. For V1, seed Dutch + English terms for YouTube media controls.

---

## 21. Safety requirements

This feature touches websites. Keep it safe.

### 21.1 Never bypass BrowserPageSafetyGuard

Learned profile rules are target selection hints only.

The final action must still pass:

```text
BrowserPageSafetyGuard
```

Risky actions still produce pending confirmation.

Examples that should require confirmation or be blocked:

- buying something
- submitting payment
- deleting account/data
- accepting legal terms
- sending messages/emails
- downloading suspicious files
- installing extensions/software
- entering passwords
- changing security settings
- clicking unknown ads
- gambling/age-restricted flows

### 21.2 Learned rules cannot create unlimited trust

Even if a selector was user-taught:

- It does not mean every future click is safe.
- It only means target selection is more likely correct for that action.
- Safety risk is determined by page context + action risk.

### 21.3 Confirmation wording

If a learned selector maps to a risky element, Merlin should say something like:

```text
I found the control, but this action looks sensitive. Do you want me to click it?
```

For blocked actions:

```text
I can't do that automatically.
```

Keep blocked-action responses short and safe.

---

## 22. Privacy and storage

The site control profile DB should avoid storing sensitive page content unnecessarily.

Do store:

- domain
- canonical action
- selector candidates
- element signatures
- success/failure counts
- validation metadata
- timestamps

Avoid storing:

- full page text dumps
- private form values
- passwords
- payment details
- personal messages
- full URLs with sensitive query tokens, unless sanitized

URL storage should be sanitized:

```text
https://example.com/account?token=secret -> https://example.com/account
```

For YouTube URLs, video IDs are probably acceptable but still not necessary for the profile.

---

## 23. Suggested backend services

Proposed services:

```text
IBrowserSiteProfileService
IBrowserActionAttemptStore
IBrowserSelectorCandidateExtractor
IBrowserSelectorRanker
IBrowserActionValidator
IBrowserCorrectionModeService
IBrowserDomainNormalizer
IBrowserActionNormalizer
IBrowserClickObservationService
```

Possible folder structure:

```text
Merlin.Backend/
  Services/
    BrowserWorkspace/
      SiteProfiles/
        BrowserSiteProfileService.cs
        BrowserActionAttemptStore.cs
        BrowserSelectorCandidateExtractor.cs
        BrowserSelectorRanker.cs
        BrowserActionValidator.cs
        BrowserCorrectionModeService.cs
        BrowserDomainNormalizer.cs
        BrowserActionNormalizer.cs
        Models/
          BrowserSiteProfile.cs
          BrowserSiteActionProfile.cs
          BrowserSelectorCandidate.cs
          BrowserActionAttempt.cs
          BrowserActionCorrection.cs
          BrowserValidationRule.cs
```

EF entities could live wherever the current project keeps DB entities.

---

## 24. Integration with current browser workspace

The feature should integrate with existing browser workspace action flow.

Existing relevant areas likely include:

```text
BrowserWorkspaceService.cs
BrowserPageSafetyGuard.cs
ConfirmationTool.cs
```

Expected integration points:

1. Before page click, resolve candidate through profile/generic resolver.
2. Before execution, run `BrowserPageSafetyGuard`.
3. During confirmation, preserve attempt metadata.
4. After execution, snapshot again and validate.
5. Store `BrowserActionAttempt`.
6. If user correction is detected, call `BrowserCorrectionModeService`.
7. In correction mode, observe the next manual click and store the rule.

Do not make the learned profile layer directly click the page. It should return candidates to the existing controlled/safe click pipeline.

---

## 25. Browser-side requirements

The backend needs enough data from the browser/page.

### 25.1 Snapshot enrichment

Current page snapshot should include per interactive element:

```text
element id inside snapshot
DOM path or stable handle
tag name
role
name/accessibility label
aria-label
title
text
placeholder
classes
id
data attributes
href/button type
bounding box
visible/enabled
ancestor hints
nearby text
```

### 25.2 Click observation

Browser workspace needs a temporary mode:

```text
ObserveNextUserClick(correlationId, timeout)
```

It returns:

```text
ObservedClickResult
- page URL
- normalized domain
- clicked element signature
- generated DOM path if available
- screen coordinate
- viewport coordinate
- timestamp
```

Timeout recommendation:

```text
30 seconds
```

If timeout:

```text
Merlin: I didn't catch a corrected click.
```

### 25.3 ElementFromPoint for motion V2

Motion control can later call:

```text
GetElementAtViewportPoint(x, y)
```

It should return the same element signature model as manual click observation.

---

## 26. Ranking algorithm

The ranking algorithm should combine learned and generic signals.

Suggested scoring:

```text
score = 0

+ learned candidate base confidence
+ semantic match score
+ selector uniqueness score
+ visibility score
+ enabled score
+ expected region score
+ validation availability score
+ previous success score
- known bad evidence penalty
- repeated failure penalty
- generated class penalty
- too many matches penalty
- hidden/offscreen penalty
- ad/overlay risk penalty
- destructive/risky action penalty only for execution mode, not target ranking
```

For media controls, expected region can help:

- YouTube player controls are often near bottom of video player.
- Skip ad button often appears in the video player area.
- But this should be weak evidence only.

Known bad evidence should have strong penalty.

Example:

```text
If selector was explicitly rejected by user for same domain/action:
  subtract 0.60
If rejected 3 times:
  disable candidate
```

---

## 27. YouTube V1 seed profile

It is useful to seed YouTube with known selectors, but still let the correction system improve them.

Suggested seed candidates:

### video.play_pause.toggle

```text
css: .ytp-play-button
aria/title terms:
- Play
- Pause
- Afspelen
- Pauzeren
```

### video.play

Same target as play/pause button, but validation expects playing state after click.

### video.pause

Same target as play/pause button, but validation expects paused state after click.

### video.skip_ad

```text
css: .ytp-skip-ad-button
css: .ytp-ad-skip-button
text/aria/title terms:
- Skip
- Skip ad
- Advertentie overslaan
- Overslaan
```

### video.fullscreen.toggle

```text
css: .ytp-fullscreen-button
aria/title terms:
- Full screen
- Exit full screen
- Volledig scherm
- Volledig scherm sluiten
```

Important:

- Seeded candidates start with confidence maybe `0.75`, not `1.0`.
- User-taught candidates can outrank seeded candidates.
- Failed seeded candidates can be demoted.

---

## 28. Correction phrases

V1 should recognize these phrases after a browser action attempt.

### Reject last action

```text
no that was wrong
no that is wrong
that was not correct
wrong one
wrong button
you clicked the wrong thing
not that one
no not that
nope wrong
```

### Reject and go back

```text
no go back
wrong go back
that opened the wrong page
go back that was wrong
```

### Demonstrate target

```text
this one
here
this button
this is the one
use this one
this control
```

### Save persistent rule

```text
always use this one
use this from now on
remember this control
next time use this
```

### One-time correction only

```text
just this time
only this time
for now use this
```

V1 can treat all demonstrated corrections as persistent, but V1.5 should distinguish temporary vs persistent. Since the user specifically wants learning, persistent should be the default after explicit correction unless the phrase says one-time only.

---

## 29. Interaction with wider correction layer

This feature should eventually connect with Merlin’s broader correction system.

There are two correction types:

1. **General assistant behavior correction**
   - “Answer shorter.”
   - “That was just a comment.”
   - “Don’t respond to that.”

2. **Browser/site control correction**
   - “No, that was the wrong button.”
   - “Click this one instead.”
   - “Use this control for pausing YouTube.”

The correction router should classify browser corrections separately when:

- there was a recent browser action attempt
- the utterance contains rejection/correction language
- current foreground context is browser workspace

This avoids polluting general memory with site-control details.

Site-control learning should go into its own DB tables, not user preference memory.

---

## 30. Handling ad mistakes

Ads are common on YouTube and similar sites.

The system should treat ads carefully.

### 30.1 Skip ad

`skip ad` is a specific action:

```text
video.skip_ad
```

Merlin should look for skip buttons using text/aria/class selectors.

Dutch examples:

```text
Overslaan
Advertentie overslaan
```

English examples:

```text
Skip
Skip ad
```

### 30.2 Accidental ad click

If Merlin clicks an ad or opens a new page after a media action:

- mark the clicked selector/signature as bad for that action
- classify result as `wrong_navigation`
- if the user says `no that was wrong`, optionally offer to go back
- do not save the clicked ad element as successful

### 30.3 Ad elements as negative evidence

Ad-related selectors/classes/text should often be penalized for normal video actions, except `video.skip_ad`.

Penalty hints:

```text
ad
ads
advertentie
sponsor
promoted
ytp-ad
```

For action `video.skip_ad`, these are not necessarily negative.

---

## 31. Handling localization and STT errors

The user is Dutch and YouTube buttons may be Dutch. STT can mishear Dutch UI words like `overslaan`.

The system should not rely only on transcribed exact words.

Examples:

```text
overslaan
overslan
over slaan
skip
skippen
skip ad
advertentie overslaan
```

For V1, add normalization aliases:

```text
overslaan -> video.skip_ad
overslan -> video.skip_ad
over slaan -> video.skip_ad
skip advertentie -> video.skip_ad
advertentie overslaan -> video.skip_ad
```

Also add English aliases:

```text
skip ad -> video.skip_ad
skip this ad -> video.skip_ad
skip the commercial -> video.skip_ad
```

This belongs in browser/media action normalization, not general memory.

---

## 32. Failure modes to handle

### 32.1 Selector not found

```text
Merlin: I couldn't find that control.
```

Optional next step:

```text
Show me the correct control.
```

### 32.2 Multiple candidates equal confidence

If confidence is too low or candidates are too close:

```text
Merlin can ask the user to point/click, or show candidate highlights later.
```

For V1, prefer asking for demonstration over blind clicking if confidence is below threshold.

Suggested thresholds:

```text
Execute automatically: >= 0.78 and safe
Ask/confirm target: 0.55 - 0.78
Ask user to demonstrate: < 0.55
```

### 32.3 Stale page

If the page changed between snapshot and click:

- re-snapshot
- re-resolve selector
- re-run safety
- only click if still valid

This matches the existing safety hardening direction.

### 32.4 Learned selector matches multiple elements

If a learned CSS selector matches multiple visible elements:

- apply signature similarity
- prefer expected region
- prefer active player/container
- if still ambiguous, do not blindly click

### 32.5 User correction comes too late

If no recent browser action attempt exists:

```text
Merlin should not assume what was wrong.
```

Response:

```text
What should I correct?
```

But if current page/action context is still obvious, it may offer:

```text
Show me the control you want me to use.
```

---

## 33. V1 implementation scope

V1 should be narrow and useful.

### Include in V1

1. DB tables for site profiles, action profiles, selector candidates, attempts, corrections, bad selector evidence.
2. Domain normalization.
3. Canonical action normalization for YouTube media actions.
4. YouTube seeded selectors for play/pause, skip ad, fullscreen.
5. Attempt recording for browser page clicks/actions.
6. User rejection detection after recent browser action.
7. Correction mode with manual click observation.
8. Selector candidate extraction from demonstrated element.
9. Store positive rule and negative evidence.
10. Learned candidates used before generic candidates.
11. Safety guard still required before click.
12. Basic validation for YouTube play/pause and skip ad.
13. Unit tests for normalization, ranking, confidence, correction creation.

### Exclude from V1

1. Motion pointing.
2. Visual candidate highlighting.
3. Full generic form filling.
4. Complex multi-step web workflows.
5. Cross-site profile sharing.
6. Automatic editing of sensitive pages.
7. LLM-only target selection without deterministic safety.
8. Background profile learning without user correction.

---

## 34. V2 scope

V2 can expand after V1 is stable.

### V2 ideas

1. Motion pointing correction.
2. Candidate highlighting overlay.
3. Profile management UI.
4. Per-profile enable/disable/reset.
5. Export/import site profiles.
6. More sites: Spotify, Netflix, Disney+, web mail, web apps.
7. Better validation using browser media APIs.
8. Generic app profiles for media players.
9. Action macros with safety boundaries.
10. Learned form field focusing.
11. User says “always use this” vs “just this time”.
12. Automatic low-confidence rule proposals after repeated generic success.
13. Sync with universal UI control memory DB if desired.

---

## 35. V3 / long-term direction

Long term, site profiles can become part of a broader “control intelligence” layer.

Possible future:

```text
User asks action
↓
Trusted app integration available? Use it.
↓
Known site profile available? Use it.
↓
Generic browser intelligence works? Use it.
↓
Generic UI/screen control profile available? Use it.
↓
Ask user to demonstrate.
↓
Learn new control profile.
```

This gives Merlin a graceful fallback ladder:

1. API/controller integration
2. browser DOM profile
3. generic browser intelligence
4. universal screen control profile
5. user demonstration

---

## 36. Acceptance criteria

### 36.1 YouTube pause learning

Given:

- User is on YouTube video page.
- No learned profile exists.

When:

- User says `pause the video`.

Then:

- Merlin resolves action `video.pause`.
- Merlin finds a candidate target.
- Merlin runs safety guard.
- Merlin clicks only if safe.
- Merlin records an attempt.
- Merlin validates whether the video paused.

### 36.2 Correction after wrong click

Given:

- Merlin clicked wrong element for `video.pause`.
- Attempt was recorded.

When:

- User says `No, that was wrong`.

Then:

- Merlin links the correction to the last attempt.
- Merlin marks previous selector/signature as bad evidence.
- Merlin enters correction mode.
- Merlin asks user to show correct control.

### 36.3 Manual click demonstration

Given:

- Merlin is in correction mode.

When:

- User manually clicks the correct button.

Then:

- Browser workspace captures clicked DOM element signature.
- Backend extracts selector candidates.
- Backend creates/updates site profile.
- Backend creates a positive selector candidate.
- Backend creates a correction record.
- Merlin confirms the correction briefly.

### 36.4 Learned rule used next time

Given:

- User previously taught YouTube pause button.

When:

- User says `pause the video` on YouTube again.

Then:

- Merlin uses learned candidate before generic candidate.
- Known bad candidates are penalized or ignored.
- Safety guard still runs.
- Attempt is recorded and validated.

### 36.5 Bad selector avoidance

Given:

- A selector was rejected by the user for `video.play` on YouTube.

When:

- Merlin ranks candidates for `video.play` again.

Then:

- That selector receives a strong penalty.
- If rejected repeatedly, it is disabled.

### 36.6 Safety preservation

Given:

- A learned selector points to a risky action.

When:

- User requests that action.

Then:

- `BrowserPageSafetyGuard` still blocks or requires confirmation.
- Learned profile does not bypass safety.

---

## 37. Unit test plan

### 37.1 Domain normalizer tests

Test:

```text
https://www.youtube.com/watch?v=abc -> youtube.com
https://m.youtube.com/watch?v=abc -> youtube.com
https://music.youtube.com/watch?v=abc -> music.youtube.com
https://open.spotify.com/track/abc -> open.spotify.com
```

### 37.2 Action normalizer tests

Test phrases:

```text
pause the video -> video.pause
stop the video -> video.pause
play the video -> video.play
resume video -> video.play
skip ad -> video.skip_ad
overslaan -> video.skip_ad
over slaan -> video.skip_ad
volledig scherm -> video.fullscreen.toggle
```

### 37.3 Selector extraction tests

Given element signature with:

```text
tag = button
class = ytp-play-button ytp-button
aria-label = Pauzeren (k)
title = Pauzeren (k)
```

Expect candidates:

```text
css .ytp-play-button
css button.ytp-play-button
aria_label Pauzeren
title Pauzeren
signature candidate
```

### 37.4 Ranking tests

Test:

- learned successful candidate outranks generic candidate
- user-rejected candidate is penalized
- disabled candidate is ignored
- hidden candidate is ignored
- selector matching many elements is penalized

### 37.5 Correction mode tests

Test:

- rejection phrase enters correction mode after recent attempt
- rejection phrase does not enter correction mode without recent attempt
- manual click creates correction record
- previous selector becomes bad evidence
- positive candidate is created

### 37.6 Validation tests

Test YouTube-like snapshots:

- `Pauzeren` before and `Afspelen` after -> pause success
- `Afspelen` before and `Pauzeren` after -> play success
- skip button disappears -> skip ad success
- URL changes unexpectedly after play/pause -> wrong_navigation

---

## 38. Integration test plan

### 38.1 Fake browser page test

Create a deterministic fake page snapshot:

```html
<div class="html5-video-player">
  <button class="ytp-play-button ytp-button" aria-label="Pauzeren (k)"></button>
  <button class="ytp-fullscreen-button ytp-button" aria-label="Volledig scherm"></button>
</div>
```

Run:

```text
pause the video
```

Assert:

- target is `.ytp-play-button`
- safety is called
- click is called
- attempt is stored
- validation is successful when after snapshot flips to `Afspelen`

### 38.2 Correction test

Fake wrong click:

```text
Merlin clicked .ad-overlay-button
User says: No, that was wrong
User manually clicks .ytp-play-button
```

Assert:

- `.ad-overlay-button` bad evidence created
- `.ytp-play-button` positive candidate created
- next action selects `.ytp-play-button`

---

## 39. Logging requirements

Add structured logs for debugging.

Suggested events:

```text
BrowserSiteProfileResolved
BrowserActionNormalized
BrowserSelectorCandidatesGenerated
BrowserSelectorRanked
BrowserKnownBadSelectorPenalized
BrowserActionAttemptStarted
BrowserActionAttemptCompleted
BrowserActionValidationCompleted
BrowserCorrectionModeEntered
BrowserCorrectionClickObserved
BrowserSelectorCandidateLearned
BrowserBadSelectorEvidenceRecorded
BrowserSiteProfileRuleDisabled
```

Important log fields:

```text
CorrelationId
Domain
CanonicalAction
CandidateCount
SelectedSelectorKind
SelectedSelectorValueHash or safe value
SelectionConfidence
SafetyDecision
ValidationOutcome
AttemptId
CorrectionId
```

Avoid logging sensitive page content.

---

## 40. Minimal implementation sequence

### PR 1: Data model and stores

Implement:

- EF entities
- migrations
- repository/store interfaces
- domain normalizer
- basic tests

No behavior change yet.

### PR 2: Action normalization and YouTube seed profile

Implement:

- canonical browser/media action normalizer
- YouTube seed profile loader
- Dutch/English aliases
- tests

Still no correction mode yet.

### PR 3: Learned selector ranking integration

Implement:

- selector candidate model
- learned candidate retrieval
- generic candidate merge
- bad evidence penalty
- integrate before BrowserWorkspaceService click resolution
- safety guard remains mandatory
- attempt recording

### PR 4: Validation layer

Implement:

- `IBrowserActionValidator`
- YouTube play/pause validation
- skip ad validation
- wrong navigation detection
- update attempt outcomes

### PR 5: Correction mode V1

Implement:

- rejection phrase detection after recent attempt
- correction mode state
- backend asks user to show correct control
- manual click observation start/stop
- timeout handling

### PR 6: Manual click learning

Implement:

- capture clicked element signature
- selector candidate extraction
- create positive candidate
- create bad evidence for previous selector
- confidence updates
- tests

### PR 7: Hardening and profile management

Implement:

- disable bad candidates after repeated failures
- profile reset commands
- profile debug command
- better logs
- privacy sanitization

### PR 8: Motion pointing correction

Implement later:

- point/pinch -> browser viewport coordinate
- DOM element from point
- reuse correction learning flow

---

## 41. Example agent prompt for PR 1

```text
You are working in the Merlin repo.

Implement PR 1 for Site Control Profiles + Correction Learning.

Goal:
Add the persistent data model and stores for browser site profiles, site action profiles, selector candidates, browser action attempts, browser action corrections, bad selector evidence, and validation rules. This PR must not change runtime browser behavior yet.

Reference design:
Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md

Requirements:
1. Add EF entities and migrations for:
   - BrowserSiteProfile
   - BrowserSiteActionProfile
   - BrowserSelectorCandidate
   - BrowserActionAttempt
   - BrowserActionCorrection
   - BrowserBadSelectorEvidence
   - BrowserValidationRule
2. Add store/service interfaces and implementations for basic CRUD/query operations.
3. Add a BrowserDomainNormalizer service.
4. Sanitize URLs before storing full page URLs.
5. Add unit tests for entity/store behavior and domain normalization.
6. Do not wire this into live clicking yet.
7. Do not bypass or modify BrowserPageSafetyGuard.
8. Keep logs privacy-safe.

Deliverable:
A focused implementation with tests and a short report listing changed files, migration name, and test results.
```

---

## 42. Example agent prompt for PR 2

```text
You are working in the Merlin repo.

Implement PR 2 for Site Control Profiles + Correction Learning.

Goal:
Add browser/media action normalization and seed a conservative YouTube profile for media controls.

Reference design:
Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md

Requirements:
1. Add canonical action normalization for:
   - video.play
   - video.pause
   - video.play_pause.toggle
   - video.skip_ad
   - video.fullscreen.toggle
2. Include Dutch and English aliases, especially:
   - overslaan / over slaan / overslan -> video.skip_ad
   - pauzeren -> video.pause
   - afspelen -> video.play
   - volledig scherm -> video.fullscreen.toggle
3. Add YouTube seed selector candidates for:
   - .ytp-play-button
   - .ytp-skip-ad-button
   - .ytp-ad-skip-button
   - .ytp-fullscreen-button
4. Seed confidence must be conservative, not 1.0.
5. Add tests for action normalization and seed profile creation.
6. Do not execute browser clicks yet based on the seed profile.
7. Do not bypass BrowserPageSafetyGuard.

Deliverable:
Changed files, test results, and examples of normalized phrases.
```

---

## 43. Example agent prompt for PR 3

```text
You are working in the Merlin repo.

Implement PR 3 for Site Control Profiles + Correction Learning.

Goal:
Use learned/seeded site profile selector candidates to improve browser action target selection, while preserving all existing browser safety behavior.

Reference design:
Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md

Requirements:
1. Add a BrowserSelectorRanker.
2. Retrieve learned/seeded candidates for current normalized domain + canonical action.
3. Merge learned candidates with generic browser intelligence candidates.
4. Penalize known bad selector evidence.
5. Ignore disabled candidates.
6. Preserve/reuse the existing BrowserPageSafetyGuard flow before any click.
7. Record BrowserActionAttempt for each action attempt.
8. Do not add correction mode yet.
9. Add unit tests for ranking and bad-selector penalties.
10. Add integration tests with a fake YouTube page snapshot.

Critical safety rule:
A learned selector is only a target selection hint. It must never bypass BrowserPageSafetyGuard or pending confirmation.

Deliverable:
Changed files, test results, and a short explanation of where profile ranking is integrated.
```

---

## 44. Example agent prompt for PR 4

```text
You are working in the Merlin repo.

Implement PR 4 for Site Control Profiles + Correction Learning.

Goal:
Add browser action validation so Merlin can tell whether a site control action actually worked.

Reference design:
Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md

Requirements:
1. Add IBrowserActionValidator.
2. Implement validation for:
   - video.pause
   - video.play
   - video.play_pause.toggle
   - video.skip_ad
   - video.fullscreen.toggle if feasible
3. Use DOM/page snapshots before and after the click.
4. Support Dutch and English YouTube labels:
   - Pauzeren / Afspelen
   - Pause / Play
   - Overslaan / Advertentie overslaan / Skip ad
   - Volledig scherm / Full screen
5. Detect unexpected URL navigation for media control actions.
6. Update BrowserActionAttempt outcome with validation results.
7. Add tests for success, failed validation, and wrong navigation.

Deliverable:
Changed files, test results, and validation examples.
```

---

## 45. Example agent prompt for PR 5

```text
You are working in the Merlin repo.

Implement PR 5 for Site Control Profiles + Correction Learning.

Goal:
Add correction mode V1 for browser actions. When the user rejects the last browser action, Merlin should enter a short-lived correction mode and ask the user to show the correct control.

Reference design:
Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md

Requirements:
1. Add BrowserCorrectionModeService.
2. Detect rejection phrases after a recent BrowserActionAttempt, including:
   - no that was wrong
   - wrong button
   - not that one
   - you clicked the wrong thing
   - no go back
3. Link correction mode to the last relevant browser action attempt.
4. Mark the previous attempt outcome as user_rejected when appropriate.
5. Record bad selector evidence for the previous selected element/candidate.
6. Ask the user: “Show me the correct control.”
7. Add timeout handling.
8. Do not implement manual click learning yet if too large; just enter mode and start observation hook if available.
9. Add tests for correction intent detection and recent-attempt matching.

Deliverable:
Changed files, test results, and a short explanation of how correction mode is entered and exited.
```

---

## 46. Example agent prompt for PR 6

```text
You are working in the Merlin repo.

Implement PR 6 for Site Control Profiles + Correction Learning.

Goal:
Complete manual click correction learning. When Merlin is in correction mode and the user manually clicks the correct element, Merlin should learn a selector candidate for the current site/action.

Reference design:
Merlin.ToDo/browser_workspace/site_control_profiles/merlin_site_control_profiles_learning_v1.md

Requirements:
1. Add browser-side ObserveNextUserClick support if not already present.
2. Capture clicked element signature:
   - tag
   - role/name
   - aria-label
   - title
   - text
   - id/classes
   - data attributes
   - bounding box
   - ancestor hints if available
3. Add BrowserSelectorCandidateExtractor.
4. Generate multiple selector candidates from the demonstrated element.
5. Store positive candidate(s) under the current domain/action profile.
6. Create BrowserActionCorrection record.
7. Mark previous selected element/candidate as bad evidence.
8. Update confidence values conservatively.
9. Add tests for selector extraction, correction storage, and next-time selection.
10. Preserve BrowserPageSafetyGuard for all future clicks.

Deliverable:
Changed files, test results, and a demonstration scenario for YouTube pause correction.
```

---

## 47. Commands Merlin should eventually support

Useful voice commands after V1/V2:

```text
Merlin, pause the video.
Merlin, play the video.
Merlin, skip the ad.
Merlin, fullscreen.
Merlin, no, that was wrong.
Merlin, use this one.
Merlin, always use this button for pausing YouTube.
Merlin, forget what you learned for YouTube pause.
Merlin, reset YouTube controls.
Merlin, show what you learned for this site.
Merlin, stop correction mode.
```

Profile management commands should be safe and explicit.

---

## 48. Profile reset and debugging

Add commands later:

```text
reset controls for this site
forget the YouTube pause button
forget what you learned for YouTube
show learned controls for this site
```

Debug output example:

```text
YouTube learned controls:
- video.pause: .ytp-play-button, confidence 0.92, 12 successes, 1 failure
- video.skip_ad: .ytp-skip-ad-button, confidence 0.81, 3 successes, 0 failures
Known bad for video.pause:
- .ytp-ad-overlay-close-button, rejected 2 times
```

Do not expose raw sensitive URLs or page content in normal debug output.

---

## 49. Important non-goals

This feature should not become:

- an unrestricted browser automation agent
- a payment/checkout bot
- a password/login automation system
- a blind coordinate click recorder
- a replacement for safety confirmation
- a system that stores private page data casually
- a system that trusts one failed/successful action forever

Its job is narrower:

> Learn safe, reusable target selection rules for common site controls, especially media/player controls, with correction history.

---

## 50. Final implementation stance

The correct mental model is:

```text
Generic browser intelligence = works anywhere, but may be uncertain.
Learned site profiles = improve reliability on known sites.
Attempt history = remembers what Merlin tried.
Correction history = remembers what the user fixed.
Bad selector evidence = prevents repeated wrong clicks.
Validation = prevents false success.
Safety guard = remains final authority before execution.
```

This should make Merlin feel much less brittle. Instead of manually patching every website forever, Merlin gradually learns reliable controls per site and can recover from mistakes through user-guided correction.
