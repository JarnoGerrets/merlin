---
type: current-state
status: current
area: cross-cutting
tags:
  - merlin
---

# Current Implemented Features

These are implemented enough to be current runtime systems, though several still have partial UX around them.

| Feature | Status evidence | Key code atlas notes | Tests / validation |
| --- | --- | --- | --- |
| [[Active Surface Layer]] | `ActiveSurfaceService` owns current dashboard/browser surface and publishes changes. | [[ActiveSurfaceService]], [[Active Surface Flow]], [[Active Surface State Ownership]] | `ActiveSurfaceServiceTests.cs` |
| [[Motion Control Profile Layer]] | `MotionControlModeService` resolves dashboard/browser/neutral profiles through `MotionControlProfileRegistry`. | [[MotionControlModeService]], [[MotionControlProfileRegistry]], [[DashboardMotionProfile]], [[BrowserWorkspaceMotionProfile]], [[NeutralMotionProfile]] | `MotionControlModeServiceTests.cs`, `MotionControlProfileRegistryTests.cs` |
| [[Vision Sidecar]] | `VisionSidecarHost` starts Python worker and routes gesture/calibration messages. | [[VisionSidecarHost]], [[VisionSidecarClient]], [[vision_worker.py]], [[Vision Sidecar Protocol]] | `VisionSidecarClientTests.cs`, `VisionGestureEventRouterTests.cs` |
| [[Browser Workspace]] | Backend starts BrowserHost and sends stdin commands; BrowserHost hosts WebView2 and emits stdout events. | [[BrowserWorkspaceService]], [[BrowserWorkspaceForm]], [[Backend BrowserHost Commands]] | Backend protocol/scoring tests; live WebView2 mostly manual |
| [[Browser Pointer Overlay]] | Native BrowserHost overlay renders pointer state and returns click coordinate. | [[BrowserMotionOverlayModeService]], [[BrowserPointerMapper]], [[NativeBrowserPointerOverlayWindow]] | `BrowserMotionOverlayModeServiceTests.cs`; visual manual |
| [[Memory System]] | Memory orchestrator, prompt compiler, stores, and memory tests exist. | [[MemoryOrchestrator]], [[PromptCompiler]], [[Memory Prompt Context Flow]] | `BrainLikeMemoryLayerTests.cs` and memory store/service tests |

## Evidence Boundaries

Implemented does not mean finished UX. BrowserHost/Godot/camera behavior still needs live validation after changes because CI does not exercise WebView2, real camera capture, native overlay drawing, or Godot gesture scenes.
