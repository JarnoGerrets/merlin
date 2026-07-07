---
type: feature
status: implemented
area: backend
tags:
  - merlin
  - feature
  - status/implemented
---

# Motion Control Profile Layer

## Summary

Uses `eyes open` to enable motion and select one active profile from Active Surface.

## Status

implemented

## What Exists Today

- Implemented backend profile service and registry.
- Profiles: Dashboard, BrowserWorkspace, Neutral.
- `eyes open` / `eyes closed` and existing aliases route through CommandRouter.
- VisionGestureEventRouter delegates to MotionControlModeService.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/Motion/MotionControlModeService.cs` | Mode service | Enables/disables and switches profile. |
| `Merlin.Backend/Services/Motion/MotionControlProfileRegistry.cs` | Registry | Resolves by surface or override. |
| `Merlin.Backend/Services/Motion/Profiles/DashboardMotionProfile.cs` | Dashboard profile | Starts dashboard UI control. |
| `Merlin.Backend/Services/Motion/Profiles/BrowserWorkspaceMotionProfile.cs` | Browser profile | Uses pointer, pinch click, scroll. |
| `Merlin.Backend/Services/Motion/Profiles/NeutralMotionProfile.cs` | Neutral profile | Safe no-op. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]

## Explicit Dependencies

- [[Active Surface Layer]]
- [[Vision Sidecar]]
- [[Dashboard UI Control]]
- [[Browser Workspace]]

## Explicit Non-Goals

- YouTube profile
- Spotify profile
- FileBrowser profile
- learned control DB


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Motion starts once, then active profile consumes gestures according to current active surface.

## Planned Behavior

Later profiles can represent app/site/widget controls after Control Profile DB and site learning exist.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Browser profile still relies on raw pointer click safety gap.
- Surface reset/lifecycle bugs can select wrong profile.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Live-test dashboard/browser switching and add report.
