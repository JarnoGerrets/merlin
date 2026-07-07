---
type: prompt-index
status: current
area: cross-cutting
tags:
  - merlin
  - agent/prompt
---

# Implementation Prompts Index

Use [[Status Rules]] before trusting any prompt. A prompt is not implementation evidence. Extensive plans live in [[Implementation Plans Index]]. Raw imported material lives in [[Imported Merlin.ToDo Index]].

| Prompt | Path | Related Feature | Status | Can Use Now? | Notes |
| --- | --- | --- | --- | --- | --- |
| Derived Work Planning Layer | `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-001 Implement Derived Work Planning Layer.md` | Vault operating system | used | no | Documentation-only prompt used by [[RUN-2026-07-07-007 Derived Work Planning Layer]]. |
| AskClarification Stale Handling Watchdog | `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-009 Implement AskClarification Stale Handling Watchdog.md` | Voice Interruption System | implemented | no | Used by [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]]. |
| AskClarification Full Recomposition Ownership | `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-010 Implement AskClarification Full Recomposition Ownership.md` | Voice Interruption System | implemented | no | Used by [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]]. |
| BargeIn Idle Capture Test Failures | `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-022 Fix BargeIn Idle Capture Test Failures.md` | Voice Interruption System | ready | yes | Scoped bugfix prompt for the four known BargeIn idle-capture failures. |
| Correction Regeneration Test Failures | `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-023 Fix Correction Regeneration Test Failures.md` | Correction Layer | ready | yes | Scoped bugfix prompt for known correction regeneration dispatcher failures. |
| Feature-Owned Settings Migration | `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-011 Implement Feature-Owned Settings Migration.md` | Modular Runtime Refactor | implemented | no | Used by [[RUN-2026-07-07-012 Feature-Owned Settings Migration]]. |
| YouTube Site Control Profile Media Commands | `Merlin.Vault/08_Implementation_Prompts/YouTube Site Control Profile Media Commands.md` | Site Control Profiles / Browser Control | ready | yes | Short executable agent prompt for seeded YouTube media controls. Verify current code before implementing. |

## Prompt Extension Requirement

Short execution prompts should list required prompt extensions instead of repeating all operating rules.

Default source: [[Prompt Extension Selection Guide]].
