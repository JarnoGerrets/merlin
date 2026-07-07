---
type: flow
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Dashboard Motion Profile Flow

## Summary

Dashboard profile starts UI control mode and frontend Main.gd interprets dashboard gestures.

## Current Flow

1. DashboardMotionProfile.ActivateAsync
2. UiControlModeController.Start
3. UI_CONTROL_MODE_STARTED
4. Main.gd _handle_ui_control_visual_event
5. GESTURE_* event
6. Main.gd _gesture_* handlers

## Mermaid Diagram

```mermaid
flowchart LR
    N0[DashboardMotionProfile.ActivateAsync] --> N1[UiControlModeController.Start]
    N1[UiControlModeController.Start] --> N2[UI_CONTROL_MODE_STARTED]
    N2[UI_CONTROL_MODE_STARTED] --> N3[Main.gd _handle_ui_control_visual_event]
    N3[Main.gd _handle_ui_control_visual_event] --> N4[GESTURE_* event]
    N4[GESTURE_* event] --> N5[Main.gd _gesture_* handlers]
```

## Related Feature And Architecture Notes

- [[Dashboard UI Control]]
- [[Main.gd]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
