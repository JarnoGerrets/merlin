---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
---

# Responsive Feedback

## Summary

Acknowledgements and lightweight feedback while longer work continues.

## Status

partial

## What Exists Today

- Acknowledgement policy/speech services exist.
- Responsive feedback orchestrator/options exist.
- Tests cover feedback selectors and integrations.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/Acknowledgement/*` | Acknowledgement | Immediate speech decisions. |
| `Merlin.Backend/Services/Feedback/*` | Feedback | Context/vector/orchestration. |
| `Merlin.Backend.Tests/ResponsiveFeedback*Tests.cs` | Tests | Feedback behavior. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Provides progress/acknowledgement for selected requests.

## Planned Behavior

Migrate carefully per responsive feedback roadmap.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Can conflict with interruption/playback if state is wrong.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Avoid chatty confirmations for terse commands.
