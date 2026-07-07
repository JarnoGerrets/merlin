---
type: current-state
status: current
area: cross-cutting
tags:
  - merlin
---

# Current Known Limitations

| Limitation | Affected systems | Evidence | Practical impact |
| --- | --- | --- | --- |
| WebView2/native BrowserHost is not covered by automated E2E tests. | [[Browser Workspace]], [[Browser Control]] | BrowserHost atlas notes; backend tests use fakes. | Black windows, focus, z-order, and transparency need manual validation. |
| Real camera behavior is hardware-dependent. | [[Vision Sidecar]], [[Motion Control]] | `vision_worker.py` adaptive backend profile code. | FPS, lighting, camera angle, and pinch thresholds vary per device. |
| Dashboard gesture logic is centralized in `Main.gd`. | [[Dashboard UI Control]] | [[Main.gd]] owns many gesture state dictionaries/functions. | Changes can accidentally affect drag, resize, crumple, and browser hiding. |
| Raw browser motion click bypasses DOM safety. | [[Browser Pinch Click]], [[Safety and Confirmation]] | [[BrowserPinchClickController]] uses native click path; [[BrowserPageSafetyGuard]] guards DOM actions. | A pinch click can click unsafe page UI without confirmation. |
| Browser page-aware matching is snapshot based. | [[Browser Page-Aware Control]] | [[BrowserPageSnapshot]], [[BrowserWorkspaceService]]. | Dynamic pages can change between snapshot and click. |
| Live interruption/correction timing remains fragile. | [[Voice Interruption System]], [[Correction Layer]] | Current test coverage note. | Merlin may ignore or mishandle short confirmations/corrections. |
