---
type: project
status: current
tags:
  - merlin
---

# Agent Preflight Checklist

Before implementation:

1. Read [[00_Index]].
2. Read [[Scope Rules]].
3. Read [[Status Rules]].
4. Read the relevant feature note.
5. Read the relevant architecture note.
6. Read linked code atlas notes.
7. If the requested work references historical design notes or diagnostics, check `docs` as an in-scope supporting source.
8. Check status and readiness.
9. Verify actual code before trusting old plans.
10. Check dependencies and non-goals.
11. Check [[Current Bugs and Fragility]].
12. Check [[Implementation Prompts Index]] for obsolete prompts.
13. Do not implement future/blocked items unless explicitly asked.
14. Never bypass safety/confirmation systems.
15. Update the vault after implementation.

## Prompt Extension Steps

- Select required prompt extensions using [[Prompt Extension Selection Guide]].
- Read every selected prompt extension before implementing.
- Confirm selected extensions in the final agent run report.

## Go / No-Go Check

Before runtime changes:

- [ ] Required owners/services exist.
- [ ] Required architecture boundaries are clear.
- [ ] Required dependencies exist.
- [ ] Test seam exists or can be safely added.
- [ ] Task can be implemented without parallel subsystem.
- [ ] Safety/confirmation/cancellation/interruption rules are preserved.
- [ ] If any item fails, stop and produce No-Go report.
- [ ] Do not implement fallback scope unless explicitly approved.

## Writeback Steps

- Read [[Agent Writeback Rules]] before making code changes.
- Create an agent run report for meaningful work.
- Update progress reports and changelog when the vault or runtime meaningfully changes.

## Operating Dashboards and Rules

- [[Current Work Dashboard]]
- [[Agent Run Naming Rules]]
- [[Bug Lifecycle Rules]]
- [[Vault Maintenance Checklist]]
- [[Implementation Plan Lifecycle]]
- [[Prompt Extension Selection Guide]]
