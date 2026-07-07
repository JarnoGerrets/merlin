---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
---

# External App Control

## Summary

Open/control external applications and future app surfaces.

## Status

partial

## What Exists Today

- OpenApplicationTool and trusted app registry exist.
- External app active surface/control profiles are not implemented.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Tools/OpenApplicationTool.cs` | App launch | Opens trusted apps. |
| `Merlin.Backend/Infrastructure/TrustedRegistry/*` | Trusted mappings | App/URL/command registries. |
| `Merlin.Backend.Tests/ApplicationResolverTests.cs` | Tests | App resolution. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Can open trusted apps; does not provide full app control surface.

## Planned Behavior

Future: external app detection, app-specific active surfaces, learned profiles.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- App control is unsafe without focus/surface/safety constraints.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Use Active Surface and Control Profile DB first.
