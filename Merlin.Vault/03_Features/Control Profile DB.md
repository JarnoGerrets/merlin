---
type: feature
status: future
area: cross-cutting
tags:
  - merlin
  - feature
  - status/future
---

# Control Profile DB

## Summary

Planned learned app/site/action controls with selectors, corrections, and surface-scoped behavior.

## Status

future

## What Exists Today

- Site control profile learning plan exists.
- No production control profile database confirmed.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.ToDo/site_control_profiles/merlin_site_control_profiles_learning_v1.md` | Plan | Learning/profile design. |
| `Merlin.Backend/Services/Context/ActiveSurface/*` | Dependency | Surface scoping. |
| `Merlin.Backend/Services/BrowserWorkspace/PageControl/*` | Dependency | Page-aware actions. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]

## Dependencies

- [[Active Surface Layer]]
- [[Browser Page-Aware Control]]
- [[Correction Layer]]
- [[Motion Control Profile Layer]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Not implemented.

## Planned Behavior

Store learned selectors/actions per surface/site/app; use corrections to improve mappings.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Dangerous if built before safety and correction are stable.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Wait until motion, page-aware control, correction, and safety are stable.
