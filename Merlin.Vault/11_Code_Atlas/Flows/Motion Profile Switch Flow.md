---
type: flow
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Motion Profile Switch Flow

## Summary

ActiveSurfaceChanged while motion is enabled switches profiles.

## Current Flow

1. ActiveSurfaceService.SetActiveSurfaceAsync
2. ActiveSurfaceChanged event
3. MotionControlModeService.OnActiveSurfaceChangedAsync
4. SwitchProfileLockedAsync
5. Deactivate old profile
6. Activate new profile

## Mermaid Diagram

```mermaid
flowchart LR
    N0[ActiveSurfaceService.SetActiveSurfaceAsync] --> N1[ActiveSurfaceChanged event]
    N1[ActiveSurfaceChanged event] --> N2[MotionControlModeService.OnActiveSurfaceChangedAsync]
    N2[MotionControlModeService.OnActiveSurfaceChangedAsync] --> N3[SwitchProfileLockedAsync]
    N3[SwitchProfileLockedAsync] --> N4[Deactivate old profile]
    N4[Deactivate old profile] --> N5[Activate new profile]
```

## Related Feature And Architecture Notes

- [[Active Surface Architecture]]
- [[MotionControlModeService]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
