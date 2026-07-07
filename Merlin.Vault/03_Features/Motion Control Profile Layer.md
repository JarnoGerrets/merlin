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

# Motion Control Profile Layer

## Summary

Profile dispatcher that chooses Dashboard, BrowserWorkspace, or Neutral motion behavior from ActiveSurface.

## Status

implemented

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.Backend/Services/Motion/MotionControlModeService.cs exists and owns enable/disable/profile switching.`
- `Merlin.Backend/Services/Motion/MotionControlProfileRegistry.cs resolves profiles.`
- DashboardMotionProfile.cs, BrowserWorkspaceMotionProfile.cs, and NeutralMotionProfile.cs exist.
- `Merlin.Backend.Tests/MotionControlModeServiceTests.cs and MotionControlProfileRegistryTests.cs cover profile behavior.`

## What Exists Today

`eyes open` enables motion through MotionControlModeService. The service resolves a profile from ActiveSurface and starts the vision sidecar unless the Neutral profile is selected.

## Current Behavior

Dashboard gestures are forwarded to Godot. BrowserWorkspace profile delegates pointer, pinch click, and scroll to browser motion services. ActiveSurface changes can switch profiles while enabled.

## Planned Behavior

Add profile-specific sensitivity, safety-aware pointer click policy, and better observability before app/site profiles.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/Motion/MotionControlModeService.cs` | MotionControlModeService | Mode/profile state owner | EnableAsync, DisableAsync, HandleGestureAsync, OnActiveSurfaceChangedAsync. |
| `Merlin.Backend/Services/Motion/MotionControlProfileRegistry.cs` | MotionControlProfileRegistry | Profile resolver | Resolves explicit overrides and active surface. |
| `Merlin.Backend/Services/Motion/Profiles/BrowserWorkspaceMotionProfile.cs` | BrowserWorkspaceMotionProfile | Browser motion adapter | Pointer/click/scroll delegation. |

## Code Atlas

- [[MotionControlModeService]]
- [[MotionControlProfileRegistry]]
- [[DashboardMotionProfile]]
- [[BrowserWorkspaceMotionProfile]]
- [[Motion Profile Selection Flow]]
- [[Motion Profile Switch Flow]]

## Related Systems

- [[Active Surface Layer]]
- [[Browser Workspace]]
- [[Control Profile DB]]
- [[Dashboard UI Control]]
- [[File Browser]]
- [[Site Control Profiles]]
- [[Spotify Widget]]
- [[Vision Sidecar]]

## Dependencies

- [[Active Surface Layer]]
- [[Vision Sidecar]]
- [[Dashboard UI Control]]
- [[Browser Workspace]]

## Dependents

- [[Control Profile DB]]
- [[Site Control Profiles]]
- [[Spotify Widget]]
- [[File Browser]]

## Readiness

Ready for implementation: yes

Reason:
V1 exists; next work can harden safety and diagnostics.

Blocked by:
- App/site profiles are blocked by [[Control Profile DB]] and safety policy.

Next safe action:
Add a safety-aware browser pointer click adapter before any learned profile.

## Non-Goals / Do Not Build Yet

- Do not create YouTube/Spotify/site-specific profiles yet.
- Do not duplicate pinch logic per profile.

## Known Bugs / Fragility

- Legacy fallback paths still exist in VisionGestureEventRouter for compatibility.
- Profile correctness depends on accurate ActiveSurface state.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/MotionControlModeServiceTests.cs` | Enable/disable/profile switch/forwarding | Live camera behavior is manual. |
| `Merlin.Backend.Tests/MotionControlProfileRegistryTests.cs` | Resolution rules | No app/site profile coverage yet. |

## Relevant Implementation Plans

- [[Motion Control Profile Layer Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
