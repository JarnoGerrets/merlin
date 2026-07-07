---
type: feature
status: future
area: cross-cutting
tags:
  - merlin
  - feature
  - status/future
---

# File Browser

## Summary

Future file browsing/control surface.

## Status

future

## What Exists Today

- Capability specs mention file access.
- No production FileBrowser surface confirmed.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.ToDo/merlin_capability_specs/05_FileAccessCapability.md` | Capability plan | File access scope. |
| `Merlin.ToDo/merlin_capability_specs/08_DestructiveFileActionsCapability.md` | Safety plan | Destructive action constraints. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Not implemented as a dedicated surface.

## Planned Behavior

Build after active surface, safety, and confirmation policies are strong enough.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- High-risk destructive actions.
- Needs explicit confirmation and path boundaries.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Do not build until requested and safety design is accepted.
