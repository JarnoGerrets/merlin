---
type: current-state
status: current
area: cross-cutting
tags:
  - merlin
---

# Current Test Coverage

| Area | Covered by | What is protected | Gaps |
| --- | --- | --- | --- |
| Active surface | `ActiveSurfaceServiceTests.cs` | Default dashboard, browser update/reset, confidence clamp. | No multi-subscriber stress. |
| Motion profiles | `MotionControlModeServiceTests.cs`, `MotionControlProfileRegistryTests.cs` | Enable/disable, profile switch, sidecar start/stop, dashboard/browser/neutral resolution. | No real camera. |
| Vision protocol | `VisionSidecarClientTests.cs`, `VisionGestureEventRouterTests.cs` | JSON parsing, worker source invariants, gesture routing. | No live OpenCV/MediaPipe camera test. |
| Browser motion | `BrowserMotionOverlayModeServiceTests.cs`, `BrowserPinchClickControllerTests.cs` | Overlay state, pinch click/scroll phases, inactive/bounds guards. | No native overlay/SendInput E2E. |
| Browser page safety | `BrowserPageSafetyGuardTests.cs`, `BrowserWorkspaceScoringTests.cs` | Safety decisions and candidate scoring. | No live WebView DOM test. |
| Command routing | `CommandRouterTests.cs`, `WebSocketHandlerTests.cs` | Browser, motion, tools, confirmations, response routing. | Live STT/TTS timing manual. |
| Playback/interruption | `AssistantSpeechPlaybackServiceTests.cs`, `LiveUtteranceGateTests.cs`, barge-in tests | Playback generation/hold/gate decisions. | Some correction/barge-in tests currently fail. |
| Memory | `BrainLikeMemoryLayerTests.cs`, memory store/service tests | Retrieval, prompt compilation, stores, persistence. | Not all long-run quality behavior covered. |

## Current Validation Status

- `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` passed on 2026-07-07.
- `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false` ran on 2026-07-07: 1701 passed, 9 failed, 0 skipped, 1710 total.
- The failing tests are 5 correction regeneration tests and 4 barge-in idle/capture tests. This documentation pass did not change runtime code.
- AskClarification PR10.4 focused validation passed in prior runs; manual live UX validation is tracked in [[AskClarification PR10.4 Live UX Validation Checklist]].
- Separate bugfix plans now track the known red families: [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]] and [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]].
