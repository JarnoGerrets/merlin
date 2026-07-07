---
type: feature
status: future
area: cross-cutting
tags:
  - merlin
  - feature
  - status/future
---

# Spotify Widget

## Summary

Planned music widget and Spotify control surface.

## Status

future

## What Exists Today

- Implementation plan exists in `Merlin.ToDo/music_widget/merlin_spotify_music_widget_implementation_plan.md`.
- No confirmed production Spotify API/auth widget implementation found.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.ToDo/music_widget/merlin_spotify_music_widget_implementation_plan.md` | Plan | Source planning doc. |
| `Merlin.Frontend/Scripts/UI/Windows/*` | Future UI substrate | Window/widget base. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]

## Dependencies

- Spotify API/auth/control
- future widget UI
- [[Motion Control Profile Layer]] for future gesture profile


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Not built as a production feature yet.

## Planned Behavior

Requires Spotify API/auth/control, widget UI, and future motion profile.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Should not be built until base widget/control exists.
- Needs safety around account actions.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Keep future/blocked until explicitly requested.
