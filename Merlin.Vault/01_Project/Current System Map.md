---
type: project
status: current
tags:
  - merlin
  - system-map
---

# Current System Map

## Voice Runtime

```mermaid
flowchart TD
    A[User voice] --> B[Voice capture / STT]
    B --> C[LiveUtteranceGate]
    C --> D[BargeInCoordinator]
    D --> E[CommandRouter]
    E --> F[Tools and services]
    F --> G[Backend state]
    G --> H[WebSocketHandler]
    H --> I[Godot frontend]
    E --> J[TTS / speech playback]
    J --> K[AssistantUiStateBroadcaster]
    K --> H
```

## Motion Runtime

```mermaid
flowchart TD
    A[Camera] --> B[Python vision sidecar]
    B --> C[VisionGestureEvent]
    C --> D[VisionGestureEventRouter]
    D --> E[MotionControlModeService]
    E --> F{Active surface}
    F --> G[DashboardMotionProfile]
    F --> H[BrowserWorkspaceMotionProfile]
    F --> I[NeutralMotionProfile]
    G --> J[Godot dashboard UI]
    H --> K[BrowserHost pointer overlay]
```

## Browser Runtime

```mermaid
flowchart TD
    A[Voice command] --> B[WebDestinationParser]
    B --> C[CommandRouter]
    C --> D[BrowserWorkspaceService]
    D --> E[Merlin.BrowserHost process]
    E --> F[WebView2]
    E --> G[Native pointer overlay]
    F --> H[Page snapshot / DOM action scripts]
```

## Notes

- Active Surface is implemented and currently covers dashboard, browser workspace, and unknown surfaces.
- Motion profiles are implemented in backend services and selected by active surface.
- BrowserHost controls final screen click location for the browser pointer.
