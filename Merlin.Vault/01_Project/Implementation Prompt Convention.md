---
type: project
status: current
tags:
  - merlin
  - agent/prompt
---

# Implementation Prompt Convention

Future implementation prompts should be short.

They should include:

1. Task goal.
2. Exact implementation plan or feature note to read.
3. Required prompt extensions.
4. Requested phase/scope.
5. Explicit non-goals.
6. Validation requirements.
7. Vault writeback requirement.

They should not duplicate the entire architecture if that architecture already lives in the vault.

## Standard Prompt Header

```text
Before implementing, read:

Core:
- Merlin.Vault/00_Index.md
- Merlin.Vault/01_Project/Scope Rules.md
- Merlin.Vault/01_Project/Status Rules.md
- Merlin.Vault/17_Prompt_Extensions/Prompt Extension Selection Guide.md

Required prompt extensions:
- PE-0001 Agent Preflight
- PE-0002 Scope and Status Rules
- PE-0003 Implementation Guardrails
- PE-0004 Testing and Validation
- PE-0005 Vault Writeback Rules
- PE-0007 Final Report Format
- PE-0008 Go / No-Go Rules
- PE-0260 Derived Work Planning Rules
- <area-specific extensions>
- <task-type extensions>

Task plan:
- <path to implementation plan>

Implement only:
- <phase/scope>
```

## Go / No-Go Preflight

Before runtime changes, perform a go/no-go preflight.

If Go:
- implement the requested scope.

If No-Go:
- stop before runtime changes,
- report blockers,
- update vault status/bug/progress notes,
- propose prerequisite work.

If Partial-Go is allowed:
- the prompt must explicitly list the approved reduced scope.

Do not implement fallback/minimal behavior after No-Go unless this prompt explicitly approves that fallback scope.

## Preferred Prompt Style

Future implementation prompts should reference bundles first.

Example:

Use prompt bundles:
- [[PB-0004 Motion Control Bundle]]

Additional extensions:
- [[PE-0110 Frontend Godot Change Rules]] if frontend files are touched.

## Derived Prompt Rule

When an agent creates a derived implementation plan, it must also create a short implementation prompt.

The prompt must:

1. reference `Merlin.Vault/AGENT.md`;
2. declare task mode;
3. point to exactly one implementation plan;
4. specify phase/scope;
5. include explicit non-goals;
6. require Go/No-Go preflight;
7. require validation;
8. require vault writeback;
9. require derived work planning if new follow-up work is discovered.
