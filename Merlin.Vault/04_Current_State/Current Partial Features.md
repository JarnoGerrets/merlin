---
type: current-state
status: current
area: cross-cutting
tags:
  - merlin
---

# Current Partial Features

| Feature | Why partial | Evidence | Next safe action |
| --- | --- | --- | --- |
| [[Motion Control]] | End-to-end camera/motion works, but tuning and raw click safety remain active issues. | [[MotionControlModeService]], [[vision_worker.py]], [[Main.gd]] | Improve diagnostics/profile config before learned controls. |
| [[Dashboard UI Control]] | Gesture drag/resize/crumple exists in `Main.gd`, but frontend logic is centralized and manual-tested. | [[DashboardMotionProfile]], [[Main.gd]], [[MerlinWindow.gd]] | Split only after behavior stabilizes. |
| [[Browser Control]] | Navigation, pointer, pinch, scroll, page-aware primitives exist; site-specific controls and close/reset UX remain fragile. | [[BrowserWorkspaceService]], [[BrowserWorkspaceForm]], [[CommandRouter]] | Harden lifecycle and avoid generic site hacks. |
| [[Browser Pinch Click]] | Pinch click works but raw native click bypasses DOM safety. | [[BrowserPinchClickController]], [[BrowserPinchClickStateMachine]] | Add safety adapter or contextual confirmation around raw clicks. |
| [[Browser Scroll Gestures]] | Scroll-hold exists through pinch movement but tuning is manual. | [[BrowserScrollCommandService]], [[Browser Pinch Click Flow]] | Add diagnostics/tuning config. |
| [[Browser Page-Aware Control]] | Snapshot/click/search/read primitives exist; stale/dynamic page matching can fail. | [[BrowserPageSafetyGuard]], [[PageSnapshotScript]], [[ClickElementScript]] | Improve stale snapshot/site-profile separation. |
| [[Voice Interruption System]] | Live gate/playback holds exist; current tests still expose barge-in/correction fragility. | [[LiveUtteranceGate]], [[AssistantSpeechPlaybackService]] | Stabilize failing tests first. |
| [[Responsive Feedback]] | Acknowledgement/progress speech exists; timing interactions with final speech remain delicate. | [[CommandRouter]], [[AssistantSpeechPlaybackService]] | Keep changes isolated and test voice timing. |
| [[Streaming Responses and TTS]] | Streaming/queued playback works, but timing/GPU/audio behavior remains sensitive. | [[AssistantSpeechPlaybackService]], [[Streaming TTS Flow]] | Preserve generation and hold invariants. |
| [[Correction Layer]] | Correction request builder and regeneration flow exist but tests are failing. | [[CorrectionRequestBuilder]], [[Correction Flow]] | Fix regeneration tests before profile learning. |
| [[External App Control]] | Basic trusted app/URL control exists; deeper app control is not mature. | [[CommandRouter]], [[Safety and Confirmation]] | Keep safety confirmations explicit. |
| [[Safety and Confirmation]] | Confirmation service and browser page guard exist, but raw motion click safety gap remains. | [[BrowserPageSafetyGuard]], [[Confirmation State Ownership]] | Add raw motion click safety boundary. |
