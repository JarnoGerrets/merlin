---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
---

# Correction Layer

## Summary

Handles user corrections and regeneration of answers.

## Status

partial

## What Exists Today

- Correction regeneration tests and services exist.
- LiveUtteranceGate recognizes correction phrases.
- Answer recomposer exists.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend.Tests/CorrectionRegenerationTests.cs` | Tests | Correction flow. |
| `Merlin.Backend/Services/InterruptionIntelligence/AnswerRecomposer.cs` | Recomposition | Corrected answers. |
| `Merlin.ToDo/Merlin_Correction_*` | Plans | Implementation notes. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Corrections can cancel old turn and regenerate new response.

## Planned Behavior

Integrate with learned control profiles later.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Full test suite currently has correction regeneration failures.
- Confirmation/correction can dead-end if gate state is wrong.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Fix existing correction failures before correction-driven site profiles.
