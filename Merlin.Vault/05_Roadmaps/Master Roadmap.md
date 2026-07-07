---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Master Roadmap

## Scope

Dependency-ordered source of truth across all Merlin systems.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant feature notes | Relevant code atlas notes | Next safe action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Stabilize correction/barge-in tests | partial | current failing tests | Control Profile DB, learned site profiles, interruption UX | yes | Runtime code exists, but failures are isolated enough to investigate. | [[Correction Layer]], [[Voice Interruption System]] | [[LiveUtteranceGate]], [[AssistantSpeechPlaybackService]], [[CorrectionRequestBuilder]] | Fix failing tests without touching browser/motion. |
| Harden browser close/reset | partial | BrowserWorkspaceService, ActiveSurfaceService, frontend restore | reliable browser UX, site profiles | yes | Known stale state after close affects active surface and UI restore. | [[Browser Workspace]], [[Active Surface Layer]], [[Browser Control]] | [[BrowserWorkspaceService]], [[ActiveSurfaceService]], [[Main.gd]] | Sync close/reset/front-end restore path. |
| Add raw motion click safety | partial | BrowserPinchClickController, BrowserPageSafetyGuard | safe motion browser control, learned profiles | yes | Raw native click works but bypasses page-action safety. | [[Browser Pinch Click]], [[Safety and Confirmation]] | [[BrowserPinchClickController]], [[BrowserPageSafetyGuard]], [[NativeBrowserInputService]] | Design safety adapter for motion clicks. |
| Tune motion profile config/diagnostics | partial | Motion profile layer and vision worker | better dashboard/browser motion UX | yes | Profiles exist; tuning needs diagnostics not architecture change. | [[Motion Control]], [[Motion Control Profile Layer]], [[Vision Sidecar]] | [[MotionControlModeService]], [[MotionControlProfileRegistry]], [[vision_worker.py]] | Add/read diagnostics before changing thresholds. |
| Seeded YouTube media shortcut profile | planned | BrowserWorkspace active surface and BrowserHost command protocol | first site-control example | yes | Small scoped exception, not learned profile DB. | [[Site Control Profiles]], [[Browser Control]], [[YouTube Site Control Profile Media Commands]] | [[BrowserMediaCommandNormalizer]], [[CommandRouter]], [[BrowserWorkspaceService]], [[BrowserWorkspaceForm]] | Implement fullscreen confirmations and J/L seek mapping. |
| Control Profile DB | future | correction stability, raw click safety, page-aware control | learned site/app profiles | no | Foundations still fragile. | [[Control Profile DB]], [[Site Control Profiles]] | [[BrowserWorkspaceService]], [[BrowserPageSafetyGuard]], [[MotionControlModeService]] | Do not build until blockers pass. |

## Linked Implementation Plans

- [[Always-On Interruption And Live Utterance Routing Plan]]
- [[AskClarification Dead End Fix Plan]]
- [[Browser Control Phases 2-5 Plan]]
- [[Codex Mission Control Plan]]
- [[External Open Overlay And Animation Plan]]
- [[Fixes Enabled By Active Surface Context Layer Plan]]
- [[Conversational Interruption Redesign V2 Plan]]
- [[Responsive Feedback Migration V2 Plan]]
- [[Conversational Interruption Redesign Original Plan]]
- [[Responsive Feedback Migration Original Plan]]
- [[Correction Classification And Semantic Rewrite Plan]]
- [[Correction Regeneration Token And Short Stop Fix Plan]]
- [[Echo Aware Self Speech Suppression Plan]]
- [[Fast Near-End Ducking Path Plan]]
- [[Instant Ducking And Natural Hard Stop Plan]]
- [[Live Turn Correction Regeneration Plan]]
- [[Playback Clock Aligned Reference Tap Plan]]
- [[Playback Mic Correlation Self Echo Suppression Plan]]
- [[Motion Control Profile Layer Plan]]
- [[Spotify Music Widget Implementation Plan]]
- [[Site Control Profiles Learning Plan]]
- [[Active Surface Context Layer Plan]]
- [[Universal UI Control Layer Design Plan]]
- [[Voice Correction Learning Plan]]
