---
type: feature
status: partial
area: cross-cutting
tags:
  - merlin
  - feature
  - status/partial
---

# Motion Control

## Summary

Camera-based hand tracking for dashboard UI and browser workspace.

## Status

partial

## What Exists Today

- Vision sidecar uses OpenCV/MediaPipe style worker and calibration.
- Gesture events route through backend.
- Dashboard UI control exists.
- Browser pointer overlay, pinch click, and scroll gestures exist.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/VisionScripts/vision_worker.py` | Vision worker | Camera capture, gestures, calibration. |
| `Merlin.Backend/Services/Vision/VisionSidecarHost.cs` | Backend host | Starts/stops worker and parses events. |
| `Merlin.Backend/Services/Vision/VisionGestureEventRouter.cs` | Router | Delegates to motion service. |
| `Merlin.Backend/Services/BrowserWorkspace/Motion/BrowserPointerMapper.cs` | Mapping | Normalized hand to overlay. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Commands include `eyes open`, `eyes closed`, `open your eyes`, `close your eyes`, `start ui control`, `stop ui control`, `start browser pointer`, `stop browser pointer`, `enable browser motion`, `disable browser motion`.

## Planned Behavior

Add learned target profiles only after motion profiles, page-aware browser control, and active surface are stable.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Camera angle can make lower corners hard to reach.
- Pinch calibration can be too sensitive/insensitive.
- Browser raw click path can bypass safety.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Keep calibration and profile dispatch separate.
