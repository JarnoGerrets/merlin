---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Motion Roadmap

## Scope

Vision sidecar, profile selection, dashboard/browser motion, pointer/click/scroll, and calibration.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant feature notes | Relevant code atlas notes | Next safe action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Verify camera/profile baseline | implemented | vision worker and sidecar host | stable tracking UX | yes | Adaptive capture profile selection exists and is documented. | [[Vision Sidecar]], [[Motion Control]] | [[vision_worker.py]], [[VisionSidecarHost]], [[VisionSidecarClient]] | Keep logs when testing new webcams. |
| Profile-based dispatch | implemented | ActiveSurfaceService, profile registry | dashboard/browser separation | yes | Motion service delegates to selected profile. | [[Motion Control Profile Layer]], [[Active Surface Layer]] | [[MotionControlModeService]], [[MotionControlProfileRegistry]], [[DashboardMotionProfile]], [[BrowserWorkspaceMotionProfile]] | Preserve sidecar ownership in mode service. |
| Tune motion reach and sensitivity | partial | calibration/profile settings | usable UI/browser corners | yes | Motion-region calibration and pointer mapping exist. | [[Motion Control]], [[Browser Pointer Overlay]] | [[vision_worker.py]], [[BrowserPointerMapper]], [[Main.gd]] | Add diagnostics before more gain tweaks. |
| Add raw motion click safety | partial | browser pinch click, page safety | learned site control | yes | Native click currently bypasses DOM guard. | [[Browser Pinch Click]], [[Safety and Confirmation]] | [[BrowserPinchClickController]], [[BrowserPageSafetyGuard]] | Build safety boundary. |
| Learned profile controls | future | profile DB, correction stability, safety | site/app control | no | Storage/teaching layer not ready. | [[Control Profile DB]], [[Site Control Profiles]] | [[MotionControlModeService]], [[BrowserWorkspaceService]] | Defer. |

## Linked Implementation Plans

- [[Motion Control Profile Layer Plan]]
- [[Universal UI Control Layer Design Plan]]
