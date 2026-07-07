---
type: bug-index
status: current
area: cross-cutting
tags:
  - merlin
  - bug
---

# Bug Index

## AskClarification live dead-end

Status: fixed, pending live validation
Affected system: voice/interruption
Evidence:
- `LiveInterruptionIntegrationService` previously deferred live `AskUserToClarifyInterruption` with the stale PR7 reason.

Impact:
Short unclear interruptions such as `in the pool` could suppress resume and leave Merlin handling without an executable owner.

Fix direction:
PR10.4a-e implementation is complete. Run manual live UX validation before marking verified.

Related notes:
[[AskClarification Live Dead-End]], [[LiveInterruptionIntegrationService]]


## BargeIn idle-capture test failures

Status: open
Affected system: voice/barge-in
Evidence:
- Broad voice/BargeIn validation fails four known BargeIn idle-capture tests.

Impact:
Broad voice regression validation remains red and idle speech routing may be unreliable.

Fix direction:
Use [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]].

Related notes:
[[BargeIn Idle Capture Test Failures]], [[Voice Interruption System]]


## Correction regeneration test failures

Status: open
Affected system: correction/voice
Evidence:
- Full backend validation has known `CorrectionRegenerationDispatcherTests` failures.

Impact:
Correction regeneration cannot be treated as stable until the failures are fixed.

Fix direction:
Use [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]].

Related notes:
[[Correction Regeneration Test Failures]], [[Correction Layer]]


## Legacy motion paths after profile layer

Status: mitigated/open
Affected system: motion
Evidence:
- `VisionGestureEventRouter.cs` keeps compatibility fallback while `MotionControlModeService.cs` exists.

Impact:
Can create ambiguity if new consumers bypass profiles.

Fix direction:
Keep new consumers behind profiles only.

Related notes:
[[Motion Architecture]], [[Motion Control Profile Layer]]


## Browser overlay z-order/lifecycle fragility

Status: open
Affected system: browser/motion
Evidence:
- `NativeBrowserPointerOverlayWindow.cs` and `BrowserWorkspaceForm.cs` own overlay visibility over WebView2.

Impact:
Pointer can appear behind/wrong window or survive stale host state.

Fix direction:
BrowserHost should own overlay cleanup and report lifecycle robustly.

Related notes:
[[BrowserHost Architecture]], [[Browser Pointer Overlay]]


## DPI/multi-monitor uncertainty

Status: open
Affected system: browser/motion
Evidence:
- `TryGetCurrentScreenClickPoint` converts overlay-local to screen coordinates.

Impact:
Clicks can be offset.

Fix direction:
Add DPI diagnostics and coordinate comparison logs.

Related notes:
[[Browser Pointer Flow]]


## Dashboard gesture logic centralized in Main.gd

Status: open
Affected system: frontend/motion
Evidence:
- `Main.gd` owns many `_gesture_*` functions.

Impact:
Hard to change safely.

Fix direction:
Extract only after behavior is documented and covered.

Related notes:
[[Dashboard UI Control]]


## Raw motion clicks bypass BrowserPageSafetyGuard

Status: open
Affected system: safety/browser
Evidence:
- `BrowserPinchClickController` fires native pointer click; `BrowserPageSafetyGuard` protects page actions.

Impact:
Risky page controls can be clicked by motion.

Fix direction:
Add safety-aware raw click policy.

Related notes:
[[Safety and Confirmation]], [[Browser Pinch Click]]


## Pause/play/stop routing ambiguity

Status: partial
Affected system: voice/browser
Evidence:
- `BrowserMediaCommandNormalizer.cs`, `LiveUtteranceGate.cs`, and `CommandRouter.cs` share responsibility.

Impact:
Plain commands can target wrong system if surface is stale.

Fix direction:
Keep active surface accurate and phrase matching centralized.

Related notes:
[[Command Routing Architecture]]


## Dutch STT variants like overslaan

Status: partial
Affected system: browser/media
Evidence:
- `BrowserMediaCommandNormalizerTests.cs` covers overslaan/overslan/over slaan.

Impact:
Variants can still be missed in DOM/action path.

Fix direction:
Add variants centrally only.

Related notes:
[[Browser Page-Aware Control]]


## Correction/barge-in timing fragility

Status: open
Affected system: voice/correction
Evidence:
- Full test run fails `CorrectionRegenerationTests.cs` and `BargeInTests.cs` cases.

Impact:
Corrections and idle speech may route unreliably.

Fix direction:
Fix test failures before learning features.

Related notes:
[[Correction Layer]], [[Voice Interruption System]]


## Speech presence file log lock

Status: open
Affected system: speech presence/tests
Evidence:
- Latest full backend test run failed `SpeechPresenceDetectorTests.OfficialDecision_WhenFileLoggingEnabled_WritesDedicatedJsonLine` because the temp log file was still being used by another process.

Impact:
The test can fail even when the logging behavior may be correct, making the suite less trustworthy.

Fix direction:
Inspect file writer lifetime/share mode and ensure tests wait for or dispose logging before reading.

Related notes:
[[Voice Pipeline Architecture]], [[Current Test Coverage]]


## Browser close/reset stale state

Status: open
Affected system: browser/frontend
Evidence:
- Browser close can leave Merlin UI visually in browser mode.

Impact:
User sees stale UI after host closes.

Fix direction:
Synchronize BrowserWorkspace close, ActiveSurface reset, and frontend restore.

Related notes:
[[Browser Workspace]]


## Sidecar lifecycle leaks

Status: open
Affected system: vision/motion
Evidence:
- `VisionSidecarHost` owns process and camera start/stop.

Impact:
Camera may stay active or fail to release.

Fix direction:
Ensure stop/shutdown paths are idempotent and logged.

Related notes:
[[Vision Sidecar]]


## Duplicated smoothing thresholds

Status: open
Affected system: motion
Evidence:
- `vision_worker.py`, `BrowserPointerMapper.cs`, and `Main.gd` have separate smoothing/sensitivity constants.

Impact:
Hard to tune consistently.

Fix direction:
Move profile-specific config into documented profile layer.

Related notes:
[[Motion Control]]

## Imported ToDo Bug Reports and Diagnostics

| Source | Imported Copy | Related System | Status | Notes |
| --- | --- | --- | --- | --- |
| Merlin Playback Reference Ring Buffer And Correlation Availability Debugging | `Merlin.Vault/12_Source_Material/Imported_Merlin_ToDo/Merlin_Playback_Reference_Ring_Buffer_Correlation_Debugging.md` | Voice Interruption System | still-useful | Imported historical troubleshooting/source material. |
| Merlin Self-Speech Gate Diagnostics And Stricter Echo Policy | `Merlin.Vault/12_Source_Material/Imported_Merlin_ToDo/Merlin_Self_Speech_Gate_Diagnostics_And_Stricter_Echo_Policy.md` | Voice Interruption System | still-useful | Imported historical troubleshooting/source material. |

## Bug Lifecycle

Use [[Bug Lifecycle Rules]] and [[Bug Report Template]] for new bug notes. Bugs should link related agent runs, affected feature notes, and relevant code atlas notes.
