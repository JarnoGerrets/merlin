---
type: project
status: current
tags:
  - merlin
---

# Current System Map

## User Voice

User voice -> STT -> [[LiveUtteranceGate]] / interruption routing -> [[ActiveSurfaceService]] when surface context matters -> [[CommandRouter]] -> tools/services -> assistant response / TTS -> [[AssistantSpeechPlaybackService]] -> frontend visual events handled by [[MerlinWebSocketClient.gd]] and [[Main.gd]].

## Camera / Vision Sidecar

Camera -> [[vision_worker.py]] -> `VisionGestureEvent` JSON -> [[VisionSidecarHost]] -> [[VisionGestureEventRouter]] -> [[MotionControlModeService]] profile layer -> [[DashboardMotionProfile]] or [[BrowserWorkspaceMotionProfile]] -> Godot dashboard or BrowserHost overlay/click/scroll.

## Browser Workspace

[[BrowserWorkspaceService]] -> `Merlin.BrowserHost` -> [[BrowserWorkspaceForm]] / WebView2 -> page snapshot, pointer overlay, click, scroll -> BrowserHost stdout events -> backend state and [[ActiveSurfaceService]] updates.

## Not Current

[[Control Profile DB]], [[Site Control Profiles]], [[Spotify Widget]], and [[File Browser]] are not current runtime systems.
