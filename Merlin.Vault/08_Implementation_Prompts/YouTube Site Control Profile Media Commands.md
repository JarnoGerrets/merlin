---
type: implementation-prompt
status: ready
area: cross-cutting
tags:
  - merlin
  - agent/prompt
  - status/ready
  - system/browser
  - system/site-control
  - system/active-surface
---

# YouTube Site Control Profile Media Commands

## Objective

Implement two related browser media improvements:

1. Correct voice confirmations for fullscreen commands:
   - `go fullscreen`
   - `go full screen`
   - `exit fullscreen`
   - `exit full screen`
2. Add a small seeded YouTube site control profile for 10-second seek commands:
   - `skip ahead 10 seconds`
   - `jump ahead 10 seconds`
   - `forward 10 seconds`
   - `jump back 10 seconds`
   - `go back 10 seconds`
   - `rewind 10 seconds`

The YouTube seek actions should map to YouTube's keyboard shortcuts:

| Spoken action | YouTube key | Confirmation |
| --- | --- | --- |
| Skip ahead 10 seconds | `L` | `Skipping ahead 10 seconds.` |
| Jump back 10 seconds | `J` | `Jumping back 10 seconds.` |

## Why This Is One Prompt

This does not need to be split into two implementation prompts yet.

Fullscreen confirmation and YouTube 10-second seek both live on the browser media command surface. They should be implemented as separate phases inside one task:

1. A generic browser media intent/confirmation fix.
2. A YouTube-specific seeded site control profile.

The important boundary is architectural, not document-level: do not put YouTube-specific behavior directly into generic browser control routing.

## Context

Current code already recognizes several fullscreen phrases, but the route collapses both "go fullscreen" and "exit fullscreen" into the same common action key.

Relevant current files:

| File | Role |
| --- | --- |
| `Merlin.Backend/Services/Context/ActiveSurface/BrowserMediaCommandNormalizer.cs` | Normalizes explicit and ambiguous browser media phrases. |
| `Merlin.Backend/Services/CommandRouter.cs` | Routes browser media matches into BrowserWorkspace commands and spoken confirmations. |
| `Merlin.Backend/Services/Web/WebDestinationParser.cs` | Also maps browser common action phrases used by web destination routing. |
| `Merlin.Backend/Services/BrowserWorkspace/BrowserWorkspaceService.cs` | Executes common actions and page-aware browser actions. |
| `Merlin.BrowserHost/CommonActionScript.cs` | Finds/clicks common media controls such as fullscreen and skip-ad. |
| `Merlin.BrowserHost/BrowserWorkspaceCommand.cs` | BrowserHost command contract. |
| `Merlin.BrowserHost/BrowserWorkspaceForm.cs` | BrowserHost command dispatcher. |

Relevant vault notes:

- [[Browser Control]]
- [[Site Control Profiles]]
- [[Active Surface Layer]]
- [[BrowserMediaCommandNormalizer]]
- [[CommandRouter]]
- [[BrowserWorkspaceService]]
- [[CommonActionScript]]
- [[Backend BrowserHost Commands]]
- [[Browser Workspace Flow]]
- [[Browser Page Action Safety Flow]]
- [[Command Router Flow]]
- [[Active Surface Flow]]

## Current Behavior To Preserve

- Generic browser controls still work outside YouTube where appropriate.
- Existing fullscreen execution can continue using the current BrowserHost common action path.
- Existing pause/play/skip-ad behavior should not be rewritten as part of this task.
- Polite wrappers such as `please` and `Merlin` should keep working through the existing normalization path.

## Current Behavior To Fix

### Fullscreen Confirmation

`BrowserMediaCommandNormalizer` currently maps both entering and exiting fullscreen to `ActiveSurfaceCapabilities.BrowserMediaFullscreen`.

`CommonActionForCapability` then maps that capability to the common action string:

```text
fullscreen
```

That means the execution can work while the spoken confirmation is too generic, for example:

```text
Fullscreen.
```

Target behavior:

| User says | Execution action | Spoken confirmation |
| --- | --- | --- |
| `go fullscreen` | fullscreen control | `Going fullscreen.` |
| `go full screen` | fullscreen control | `Going fullscreen.` |
| `make it fullscreen` | fullscreen control | `Going fullscreen.` |
| `exit fullscreen` | fullscreen control or exit-fullscreen control | `Exiting fullscreen.` |
| `exit full screen` | fullscreen control or exit-fullscreen control | `Exiting fullscreen.` |
| `fullscreen` | fullscreen control | `Toggling fullscreen.` or existing generic response if preferred. |

The key change is preserving user intent separately from the lower-level action key.

### YouTube Seek Commands

There is no current seeded YouTube site control profile for media seek commands.

Target behavior:

| User says | Active surface requirement | BrowserHost action | Spoken confirmation |
| --- | --- | --- | --- |
| `skip ahead 10 seconds` | BrowserWorkspace on YouTube | send `L` to WebView | `Skipping ahead 10 seconds.` |
| `jump ahead 10 seconds` | BrowserWorkspace on YouTube | send `L` to WebView | `Skipping ahead 10 seconds.` |
| `forward 10 seconds` | BrowserWorkspace on YouTube | send `L` to WebView | `Skipping ahead 10 seconds.` |
| `jump back 10 seconds` | BrowserWorkspace on YouTube | send `J` to WebView | `Jumping back 10 seconds.` |
| `go back 10 seconds` | BrowserWorkspace on YouTube | send `J` to WebView | `Jumping back 10 seconds.` |
| `rewind 10 seconds` | BrowserWorkspace on YouTube | send `J` to WebView | `Jumping back 10 seconds.` |

## Requirements

### Phase A: Preserve Browser Media Intent

Add an intent-level distinction to browser media command matches.

Recommended shape:

```csharp
public sealed record BrowserMediaCommandMatch(
    string Capability,
    string CommonAction,
    string Intent,
    string ConfirmationText,
    double Confidence,
    string Reason);
```

Names can differ, but the behavior must be:

- execution action remains the BrowserHost-compatible action key;
- confirmation text is based on semantic intent;
- tests can assert both execution and confirmation behavior.

Suggested intents:

| Intent | Common action | Confirmation |
| --- | --- | --- |
| `enter_fullscreen` | `fullscreen` | `Going fullscreen.` |
| `exit_fullscreen` | `exit_fullscreen` if supported, otherwise `fullscreen` | `Exiting fullscreen.` |
| `toggle_fullscreen` | `fullscreen` | `Toggling fullscreen.` |
| `skip_ad` | `skip_ad` | Existing skip confirmation. |
| `pause` | `pause` | Existing pause confirmation. |
| `play` | `play` | Existing play confirmation. |

Implementation notes:

- Keep explicit fullscreen phrases in `BrowserMediaCommandNormalizer`.
- Do not rely only on `ActiveSurfaceCapabilities.BrowserMediaFullscreen` to decide response text.
- Update `CommandRouter` so the final response uses match intent/confirmation, not only the common action string.
- Keep `CommonActionForCapability` only if still useful, but avoid making it the only source of meaning.
- If `CommonActionScript` already supports `exit_fullscreen`, prefer using it for explicit exit phrases. If not reliable, execute the existing fullscreen toggle but keep the confirmation as `Exiting fullscreen.`

### Phase B: Add Seeded YouTube Site Control Profile

Create the smallest useful site profile layer for YouTube media seek commands.

This should be a seeded profile, not the full future learning system described in [[Control Profile DB]].

Recommended backend shape:

```text
Services/BrowserWorkspace/SiteControl/
  BrowserSiteControlProfile.cs
  BrowserSiteControlProfileResolver.cs
  YoutubeSiteControlProfile.cs
```

The exact names can change to match local conventions.

The profile resolver should:

- receive the current browser active surface or BrowserWorkspace state;
- check current URL/domain;
- resolve `youtube.com` and `youtu.be` as YouTube;
- return YouTube profile actions only while the active surface is BrowserWorkspace and the current page is YouTube.

Suggested profile actions:

| Profile action | Spoken phrases | BrowserHost command |
| --- | --- | --- |
| `youtube_seek_forward_10` | `skip ahead 10 seconds`, `jump ahead 10 seconds`, `forward 10 seconds`, `go forward 10 seconds` | key press `L` |
| `youtube_seek_back_10` | `jump back 10 seconds`, `go back 10 seconds`, `rewind 10 seconds`, `back 10 seconds` | key press `J` |

### Phase C: Add BrowserHost-Scoped Keyboard Command

Do not send global OS keyboard input from the backend.

Add a BrowserHost-scoped command that presses a key inside the active WebView2/browser surface.

Suggested command:

```json
{
  "type": "browser_key_press",
  "key": "L",
  "reason": "youtube_seek_forward_10"
}
```

or:

```json
{
  "type": "browser_keyboard_shortcut",
  "keys": ["L"],
  "reason": "youtube_seek_forward_10"
}
```

Requirements:

- BrowserHost must execute the key only against its WebView/browser surface.
- The command must not require the BrowserHost window to be the globally focused foreground app if WebView2 provides a safer scoped path.
- If WebView2 does require focus, BrowserHost should make that local and explicit, not a backend global keyboard hack.
- Log command success/failure with profile action and target domain.

## Safety And Routing Rules

- YouTube seek commands are low-risk and reversible; they should not require a confirmation prompt.
- They must be domain-gated to YouTube.
- On non-YouTube pages, do not send `J` or `L`.
- On non-YouTube pages, respond with a short failure or clarification, for example:
  - `That shortcut is only available on YouTube.`
- Do not route YouTube-specific phrases through generic `BrowserPageSafetyGuard` click-by-text behavior.
- Do not add YouTube-specific selectors into generic page click code.
- Do not add broad "press any key" voice control as part of this task.

## Non-Goals

- Do not implement the full learned [[Control Profile DB]].
- Do not implement user-taught controls in this task.
- Do not add site-specific commands directly into generic browser click/page-action code.
- Do not change pause/play/skip-ad logic unless required to keep tests passing.
- Do not implement variable seek durations yet.
- Do not send global OS keyboard input.
- Do not make YouTube commands work on arbitrary video sites in this pass.

## Implementation Steps

1. Read the relevant current code:
   - `BrowserMediaCommandNormalizer.cs`
   - `CommandRouter.cs`
   - `WebDestinationParser.cs`
   - `BrowserWorkspaceService.cs`
   - `BrowserWorkspaceCommand.cs`
   - `BrowserWorkspaceForm.cs`
   - `CommonActionScript.cs`
2. Add intent-preserving fullscreen command matches.
3. Update command routing so fullscreen confirmations are:
   - `Going fullscreen.`
   - `Exiting fullscreen.`
   - optional `Toggling fullscreen.`
4. Add unit tests for fullscreen variants and confirmation text.
5. Add a minimal seeded YouTube site control resolver.
6. Add YouTube seek phrase recognition.
7. Add BrowserWorkspace service method for site profile action execution.
8. Add BrowserHost command support for scoped key press/shortcut.
9. Wire YouTube seek actions to `L` and `J`.
10. Add tests that prove:
    - YouTube domain resolves the profile.
    - non-YouTube pages do not resolve the profile.
    - seek forward sends `L`.
    - seek back sends `J`.
    - confirmations are exactly correct.
11. Update vault/code atlas notes if files or message contracts change.

## Validation

Run targeted tests first:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --filter "BrowserMediaCommandNormalizer|CommandRouter|BrowserWorkspace"
```

Then run broader backend tests if the routing changes touch shared command flow:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

Manual validation:

1. Open YouTube in BrowserWorkspace.
2. Start a video.
3. Say `go fullscreen`.
4. Verify the video enters fullscreen and Merlin says `Going fullscreen.`
5. Say `exit fullscreen`.
6. Verify fullscreen exits and Merlin says `Exiting fullscreen.`
7. Say `skip ahead 10 seconds`.
8. Verify the video jumps forward by roughly 10 seconds and Merlin says `Skipping ahead 10 seconds.`
9. Say `jump back 10 seconds`.
10. Verify the video jumps back by roughly 10 seconds and Merlin says `Jumping back 10 seconds.`
11. Navigate to a non-YouTube page.
12. Say `skip ahead 10 seconds`.
13. Verify no key is sent and Merlin does not accidentally type `L` into the page.

## Acceptance Criteria

- `go fullscreen` and equivalent phrases no longer produce the bare confirmation `Fullscreen.`
- `exit fullscreen` and equivalent phrases no longer produce the bare confirmation `Fullscreen.`
- Fullscreen execution still works.
- YouTube seek forward maps to one scoped `L` key press.
- YouTube seek backward maps to one scoped `J` key press.
- YouTube seek commands only run when BrowserWorkspace is active on YouTube.
- Non-YouTube pages do not receive `J` or `L`.
- Existing browser control tests still pass.

## Vault Updates Required

If implemented, update:

- [[Browser Control]]
- [[Site Control Profiles]]
- [[BrowserMediaCommandNormalizer]]
- [[CommandRouter]]
- [[BrowserWorkspaceService]]
- [[Backend BrowserHost Commands]]
- [[CommonActionScript]]
- [[Browser Roadmap]]

## Open Questions

- Should the generic one-word `fullscreen` confirmation be `Toggling fullscreen.` or remain `Fullscreen.`?
- Should explicit `exit fullscreen` execute `exit_fullscreen` all the way down to BrowserHost, or is the existing fullscreen toggle enough when paired with correct confirmation?
- Should YouTube seek commands be routed through Active Surface capability flags or through a separate site profile resolver first?
- Should `go forward 10 seconds` be allowed, given `go forward` already means browser history forward?

