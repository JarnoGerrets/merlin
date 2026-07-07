---
type: feature
status: implemented
area: backend
tags:
  - merlin
  - feature
  - status/implemented
---

# Active Surface Layer

## Summary

Tracks which interactive surface is active so routing and motion can be context-aware.

## Status

implemented

## What Exists Today

- `IActiveSurfaceService` exists.
- Current surfaces include Dashboard, BrowserWorkspace, Unknown.
- Capabilities include assistant playback controls and browser/media/page controls.
- BrowserWorkspace can update active surface.
- CommandRouter and LiveUtteranceGate read active surface context.
- MotionControlModeService subscribes to active surface changes.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/Context/ActiveSurface/IActiveSurfaceService.cs` | Interface | Current snapshot and event. |
| `Merlin.Backend/Services/Context/ActiveSurface/ActiveSurfaceService.cs` | Implementation | Stores current surface. |
| `Merlin.Backend/Services/Context/ActiveSurface/KnownSurfaces.cs` | Definitions | Dashboard, BrowserWorkspace, Unknown. |
| `Merlin.Backend/Services/CommandRouter.cs` | Consumer | Uses active surface. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Active surface can shift routing away from brittle phrase-only decisions.

## Planned Behavior

Add external app detection, widget/file browser surfaces, gesture target updates, learned profile integration.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Browser close/reset lifecycle can leave stale state.
- External app focus not implemented.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Keep surface changes explicit and logged.
