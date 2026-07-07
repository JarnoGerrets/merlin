---
type: current-state
status: current
area: cross-cutting
tags:
  - merlin
---

# Current Bugs and Fragility

See [[09_Bugs/Index|Bug Index]] for detailed entries.

| Fragility | Severity | Evidence | Next action |
| --- | --- | --- | --- |
| Correction/barge-in tests failing | high | `CorrectionRegenerationTests.cs` and `BargeInTests.cs` failures in current coverage note. | Use [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]] and [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]]. |
| Browser close/reset stale state | high | BrowserWorkspace/ActiveSurface/frontend restore bugs observed live. | Sync close, ActiveSurface reset, and frontend restore. |
| Raw motion click safety gap | high | [[BrowserPinchClickController]] native click bypasses [[BrowserPageSafetyGuard]]. | Add contextual safety adapter. |
| Browser page click target mismatch | medium | Snapshot scoring can select wrong YouTube/sidebar elements. | Site profile layer and stale snapshot handling. |
| Camera/profile/pinch calibration sensitivity | medium | [[vision_worker.py]] thresholds and profile selection depend on hardware. | Keep calibration and diagnostics visible. |
| Frontend gesture state centralization | medium | [[Main.gd]] owns gesture dictionaries and window state. | Refactor only with behavior tests/manual plan. |
