---
type: feature
status: implemented
area: backend
tags:
  - merlin
  - feature
  - status/implemented
  - layer/backend
---

# Active Surface Layer

## Summary

Runtime context layer that says which surface currently owns ambiguous commands.

## Status

implemented

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.Backend/Services/Context/ActiveSurface/IActiveSurfaceService.cs exposes Current and ActiveSurfaceChanged.`
- `Merlin.Backend/Services/Context/ActiveSurface/ActiveSurfaceService.cs owns the snapshot.`
- `Merlin.Backend/Services/Context/ActiveSurface/KnownSurfaces.cs defines Dashboard and BrowserWorkspace snapshots.`
- `Merlin.Backend.Tests/ActiveSurfaceServiceTests.cs verifies defaults, browser updates, reset, and confidence clamp.`

## What Exists Today

Dashboard is the default surface. BrowserWorkspace sets active surface while open and reset returns to Dashboard. CommandRouter, LiveUtteranceGate, BrowserMediaCommandNormalizer, and MotionControlProfileRegistry consume this context.

## Current Behavior

Plain commands such as media pause can be interpreted differently when BrowserWorkspace is active. Motion control profile selection also uses the active surface.

## Planned Behavior

External app detection, richer surface metadata, and learned surface profiles remain future.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/Context/ActiveSurface/ActiveSurfaceService.cs` | ActiveSurfaceService | Owns surface snapshot | Current implemented state owner. |
| `Merlin.Backend/Services/Context/ActiveSurface/KnownSurfaces.cs` | KnownSurfaces | Factory for known surfaces | Dashboard and BrowserWorkspace. |
| `Merlin.Backend/Services/Context/ActiveSurface/BrowserMediaCommandNormalizer.cs` | BrowserMediaCommandNormalizer | Surface-aware media phrase matching | Handles pause/play/skip/fullscreen variants. |

## Code Atlas

- [[ActiveSurfaceService]]
- [[BrowserMediaCommandNormalizer]]
- [[Active Surface Flow]]

## Related Systems

- [[Browser Control]]
- [[Browser Workspace]]
- [[Command Routing Architecture]]
- [[Control Profile DB]]
- [[Motion Control Profile Layer]]

## Dependencies

- [[Command Routing Architecture]]
- [[Browser Workspace]]

## Dependents

- [[Motion Control Profile Layer]]
- [[Browser Control]]
- [[Control Profile DB]]

## Readiness

Ready for implementation: yes

Reason:
Base service and tests exist; improvements can be scoped safely.

Blocked by:
- Future external app surfaces need app detection and profile policy.

Next safe action:
Add diagnostics for stale surface transitions before expanding to external apps.

## Non-Goals / Do Not Build Yet

- Do not turn ActiveSurface into memory.
- Do not bypass safety decisions based on surface alone.

## Known Bugs / Fragility

- Browser close/reset stale state can leave UI visually in browser mode even when backend surface resets.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/ActiveSurfaceServiceTests.cs` | Core state transitions | Does not validate every runtime producer. |

## Relevant Implementation Plans

- [[Fixes Enabled By Active Surface Context Layer Plan]]
- [[Active Surface Context Layer Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (2 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
