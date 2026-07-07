---
type: feature
status: partial
area: frontend
tags:
  - merlin
  - feature
  - status/partial
---

# Dashboard UI Control

## Summary

Motion control for Merlin dashboard windows and UI.

## Status

partial

## What Exists Today

- UiControlModeController exists.
- Frontend receives dashboard gesture events.
- Godot window manager and window capabilities exist.
- Dashboard profile starts/stops controller.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/UiControlModeController.cs` | Backend mode | Command matching and state. |
| `Merlin.Backend/WebSocket/WebSocketHandler.cs` | Gesture forwarding | Sends dashboard gestures. |
| `Merlin.Frontend/Scripts/Main.gd` | Gesture consumer | Dashboard interactions. |
| `Merlin.Frontend/Scripts/UI/Windows/*` | Windows | Drag/resize/dismiss. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Dashboard control is active when dashboard motion profile is selected.

## Planned Behavior

Move more gesture semantics out of `Main.gd` later.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Gesture logic centralized in frontend.
- Sensitivity differs from browser pointer needs.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Use profile layer as boundary before frontend refactor.
