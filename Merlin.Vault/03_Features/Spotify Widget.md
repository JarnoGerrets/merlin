---
type: feature
status: future
area: frontend
tags:
  - merlin
  - feature
  - status/future
  - layer/frontend
---

# Spotify Widget

## Summary

Future Spotify music widget.

## Status

future

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.ToDo/music_widget/merlin_spotify_music_widget_implementation_plan.md exists.`
- No Spotify widget production code found.

## What Exists Today

Not implemented.

## Current Behavior

No widget/API/auth runtime.

## Planned Behavior

Spotify auth/API + widget shell before any motion profile.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| Missing | Missing | Not implemented | Future/unknown. |

## Code Atlas

- None yet.

## Related Systems

- Future Spotify motion/site profile
- Spotify API/auth
- [[UI and Widgets Roadmap]]

## Dependencies

- Spotify API/auth
- [[UI and Widgets Roadmap]]

## Dependents

- Future Spotify motion/site profile

## Readiness

Ready for implementation: no

Reason:
Auth/API/widget foundation is missing.

Blocked by:
- Spotify auth/API decisions
- widget shell

Next safe action:
Implement non-motion widget foundation when explicitly requested.

## Non-Goals / Do Not Build Yet

- Do not build Spotify motion profile first.

## Known Bugs / Fragility

- N/A - future only.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Missing` | No implementation | All tests future. |

## Relevant Implementation Plans

- [[Spotify Music Widget Implementation Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
