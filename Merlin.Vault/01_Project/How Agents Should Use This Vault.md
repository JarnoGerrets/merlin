---
type: project
status: current
tags:
  - merlin
---

# How Agents Should Use This Vault

1. Read [[00_Index]] first.
2. Read [[Scope Rules]] so ignored side projects are not treated as missing coverage.
3. Read [[Status Rules]].
4. Read the relevant feature note.
5. Read the relevant architecture note.
6. Read linked code atlas notes.
7. Check `docs` when relevant for historical design notes, diagnostics, or support documentation.
8. Check status and readiness.
9. Verify actual code before trusting old plans.
10. Check dependencies and non-goals.
11. Check bugs and fragility.
12. Check [[08_Implementation_Prompts/Index|Implementation Prompts Index]] for obsolete prompts.
13. Do not implement future/blocked items unless explicitly asked.
14. Never bypass safety/confirmation systems.
15. Update the vault after implementation.

`docs` is in scope as supporting documentation. Use it as source material when relevant, but summarize and classify important findings in `Merlin.Vault` rather than forcing future agents to rediscover them.

## Implementation Plans vs Prompts

Large design/phase docs live in `13_Implementation_Plans`.

Short execution prompts live in `08_Implementation_Prompts`.

Raw imported material from the old `Merlin.ToDo` folder lives in `12_Source_Material/Imported_Merlin_ToDo`.

Before implementing a feature, agents should read:
1. the feature note,
2. the architecture note,
3. the code atlas notes,
4. the relevant implementation plan,
5. only then any source material or historical prompts.

## Prompt Extensions

Before implementing, agents must load the prompt extensions listed in:
- the implementation plan,
- the prompt extension selection guide,
- the task-specific prompt.

Prompt extensions are reusable rules. They prevent every prompt from needing to repeat all guardrails.

If required extensions conflict, follow the stricter safety/scoping rule and report the conflict.

## Agent Operating System Links

- [[Agent Writeback Rules]]
- [[Agent Run Index]]
- [[Progress Reports Index]]
- [[Change Log Index]]
- [[Prompt Extensions Index]]
- [[Prompt Extension Selection Guide]]
- [[Implementation Prompt Convention]]

## Operating Dashboards and Rules

- [[Current Work Dashboard]]
- [[Agent Run Naming Rules]]
- [[Bug Lifecycle Rules]]
- [[Vault Maintenance Checklist]]
- [[Implementation Plan Lifecycle]]
- [[Prompt Extension Selection Guide]]
