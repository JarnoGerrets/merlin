---
type: feature
status: partial
area: backend
tags:
  - merlin
  - feature
  - status/partial
  - layer/backend
---

# Correction Layer

## Summary

Voice correction/regeneration and correction request building.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- CorrectionRequestBuilder.cs exists.
- `InterruptionIntelligence contains correction/redirect handling.`
- CorrectionRegenerationTests.cs exists but currently has failing tests.

## What Exists Today

Correction classification/regeneration exists but has failing regression tests.

## Current Behavior

Corrections can cancel/replace turns in intended path, but token/correlation timing remains fragile.

## Planned Behavior

Stabilize correction regeneration before learning/correction-based profiles.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/CorrectionRequestBuilder.cs` | CorrectionRequestBuilder | Build correction request | Builds rewritten request text/context. |
| `Merlin.Backend/Services/InterruptionIntelligence/LiveInterruptionIntegrationService.cs` | LiveInterruptionIntegrationService | Correction handling | Handles correction/redirect outcomes. |

## Code Atlas

- [[CorrectionRequestBuilder]]
- [[Voice Command Flow]]

## Related Systems

- [[Control Profile DB]]
- [[Streaming Responses and TTS]]
- [[Voice Interruption System]]
- future learned correction behavior

## Dependencies

- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Dependents

- [[Control Profile DB]]
- future learned correction behavior

## Readiness

Ready for implementation: yes

Reason:
Bug fixing is ready and important.

Blocked by:
- Current failing CorrectionRegeneration tests.

Next safe action:
Fix the 5 CorrectionRegeneration test failures before adding learning.

## Non-Goals / Do Not Build Yet

- Do not implement learned site profiles until correction is stable.

## Known Bugs / Fragility

- Old cancelled correlation can suppress new correction; new corrected response may not enqueue speech according to failing tests.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/CorrectionRegenerationTests.cs` | Correction behavior | Currently failing. |

## Relevant Implementation Plans

- [[AskClarification Dead End Fix Plan]]
- [[Correction Classification And Semantic Rewrite Plan]]
- [[Correction Regeneration Token And Short Stop Fix Plan]]
- [[Live Turn Correction Regeneration Plan]]
- [[Site Control Profiles Learning Plan]]
- [[Voice Correction Learning Plan]]

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (6 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
