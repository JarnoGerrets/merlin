---
type: bug-index
status: current
tags:
  - merlin
  - bug
---

# Bug Index

| Bug title | Status | Affected system | Evidence / source file | Related notes | Possible fix direction |
| --- | --- | --- | --- | --- | --- |
| Motion modes can be split/duplicated | mitigated | motion | `MotionControlModeService`, legacy router fallback | [[Motion Control Profile Layer]] | Continue moving gesture consumers behind profiles. |
| Browser overlay z-order/lifecycle fragility | open | browser/motion | `NativeBrowserPointerOverlayWindow.cs`, BrowserHost lifecycle logs | [[Browser Workspace]] | Ensure host-owned overlay lifecycle and reset on close. |
| DPI/multi-monitor uncertainty | open | browser/motion | native overlay and screen click path | [[BrowserHost Architecture]] | Add DPI-aware tests/logging and monitor bounds diagnostics. |
| Dashboard gesture logic too centralized in Main.gd | open | frontend/motion | `Merlin.Frontend/Scripts/Main.gd` | [[Dashboard UI Control]] | Extract reusable dashboard control layer later. |
| Raw motion clicks bypass BrowserPageSafetyGuard | open | browser safety | `BrowserPinchClickController` -> `FireBrowserPointerClickAsync` | [[Safety and Confirmation Architecture]] | Route click intent through a safety-aware browser action adapter. |
| Routing ambiguity around pause/play/stop | partial | voice/browser | `LiveUtteranceGate`, `CommandRouter`, `WebDestinationParser` | [[Command Routing Architecture]] | Use Active Surface before parser fallback. |
| STT Dutch variants like overslaan | partial | browser media | `CommonActionScript.cs` includes `overslaan` lookup | [[Browser Page-Aware Control]] | Keep multilingual labels in common actions and future site profiles. |
| Browser close does not always restore Merlin UI | open | browser/frontend | user-observed lifecycle issue, BrowserWorkspace state | [[Browser Workspace]] | Reset active surface and frontend browser sleep/restore state on host exit. |
| Correction/barge-in timing tests failing | open | voice/correction | `CorrectionRegenerationTests`, `BargeInTests` full-suite failures | [[Correction Layer]] | Investigate timing/cancellation state separately. |
