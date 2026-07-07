---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Browser Roadmap

## Scope

BrowserWorkspace, BrowserHost, page-aware control, browser motion, and site-profile sequencing.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant feature notes | Relevant code atlas notes | Next safe action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Stabilize correction/barge-in tests | partial | current failing tests | Control Profile DB, learned site profiles, interruption UX | yes | Runtime code exists, but failures are isolated enough to investigate. | [[Correction Layer]], [[Voice Interruption System]] | [[LiveUtteranceGate]], [[AssistantSpeechPlaybackService]], [[CorrectionRequestBuilder]] | Fix failing tests without touching browser/motion. |
| Harden browser close/reset | partial | BrowserWorkspaceService, ActiveSurfaceService, frontend restore | reliable browser UX, site profiles | yes | Known stale state after close affects active surface and UI restore. | [[Browser Workspace]], [[Active Surface Layer]], [[Browser Control]] | [[BrowserWorkspaceService]], [[ActiveSurfaceService]], [[Main.gd]] | Sync close/reset/front-end restore path. |
| Add raw motion click safety | partial | BrowserPinchClickController, BrowserPageSafetyGuard | safe motion browser control, learned profiles | yes | Raw native click works but bypasses page-action safety. | [[Browser Pinch Click]], [[Safety and Confirmation]] | [[BrowserPinchClickController]], [[BrowserPageSafetyGuard]], [[NativeBrowserInputService]] | Design safety adapter for motion clicks. |
| Tune motion profile config/diagnostics | partial | Motion profile layer and vision worker | better dashboard/browser motion UX | yes | Profiles exist; tuning needs diagnostics not architecture change. | [[Motion Control]], [[Motion Control Profile Layer]], [[Vision Sidecar]] | [[MotionControlModeService]], [[MotionControlProfileRegistry]], [[vision_worker.py]] | Add/read diagnostics before changing thresholds. |
| Seeded YouTube media shortcut profile | planned | BrowserWorkspace active surface and BrowserHost command protocol | first site-control example | yes | Small scoped exception, not learned profile DB. | [[Site Control Profiles]], [[Browser Control]], [[YouTube Site Control Profile Media Commands]] | [[BrowserMediaCommandNormalizer]], [[CommandRouter]], [[BrowserWorkspaceService]], [[BrowserWorkspaceForm]] | Implement fullscreen confirmations and J/L seek mapping. |
| Control Profile DB | future | correction stability, raw click safety, page-aware control | learned site/app profiles | no | Foundations still fragile. | [[Control Profile DB]], [[Site Control Profiles]] | [[BrowserWorkspaceService]], [[BrowserPageSafetyGuard]], [[MotionControlModeService]] | Do not build until blockers pass. |
| Improve snapshot freshness for dynamic pages | partial | BrowserPageSnapshot, BrowserWorkspaceService | reliable page click/read | yes | Snapshot model exists but dynamic pages can change under it. | [[Browser Page-Aware Control]] | [[BrowserPageSnapshot]], [[BrowserSnapshotElement]], [[BrowserWorkspaceService]] | Add stale-page diagnostics and retry policy before site hacks. |
| BrowserHost E2E validation harness | future | BrowserHost process protocol | safe BrowserHost refactors | maybe | Backend tests fake host; native/WebView behavior is manual. | [[Browser Workspace]] | [[BrowserWorkspaceForm]], [[Backend BrowserHost Commands]] | Create smoke harness after lifecycle stabilizes. |

## Linked Implementation Plans

- [[Browser Control Phases 2-5 Plan]]
- [[Site Control Profiles Learning Plan]]
