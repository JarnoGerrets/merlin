---
type: implementation-prompt
prompt_id: PROMPT-YYYY-MM-DD-NNN
derived_work_id:
status: draft | ready | used | obsolete
related_plan:
related_plan_path:
origin_run:
task_mode: implementation | bugfix | investigation | documentation | refactor | test-only
tags:
  - merlin
  - agent/prompt
---

# Implementation Prompt

## Required Prompt Extensions

Always:
- [[PE-0001 Agent Preflight]]
- [[PE-0002 Scope and Status Rules]]
- [[PE-0003 Implementation Guardrails]]
- [[PE-0004 Testing and Validation]]
- [[PE-0005 Vault Writeback Rules]]
- [[PE-0007 Final Report Format]]
- [[PE-0008 Go No-Go Rules]]
- [[PE-0260 Derived Work Planning Rules]]

Area-specific:
- ...

Task-type:
- ...

## Objective
## Context
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

## Requirements
## Non-Goals
## Validation
## Vault Updates Required

## Required Prompt Bundles

- [[PB-0001 Standard Implementation Bundle]]

## Additional Prompt Extensions

- ...

## Derived Work

If this prompt was created for derived work, fill `prompt_id`, `derived_work_id`, `related_plan`, `related_plan_path`, and `origin_run`.
