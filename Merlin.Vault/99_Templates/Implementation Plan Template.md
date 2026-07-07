---
type: implementation-plan
plan_id: PLAN-YYYY-MM-DD-NNN
derived_work_id:
status: draft | ready | in_progress | implemented | superseded | obsolete | future | blocked
task_type: implementation | bugfix | refactor | investigation | documentation | test-only
derived_work_type:
origin_run:
origin_task:
origin_evidence:
related_features:
  - Feature Name
affected_systems:
  - system
required_prompt_bundles:
  - PB-0001
required_prompt_extensions:
  - PE-0001
  - PE-0002
  - PE-0003
  - PE-0004
  - PE-0005
  - PE-0007
  - PE-0008
  - PE-0260
risk_level: low | medium | high | critical
ready_for_agent: false
created_prompt:
implemented_by:
superseded_by:
---

# Implementation Plan

## Plan Status

Status:
Ready for agent use:
Reason:
Related feature:
Related architecture:
Related code atlas:
Original source:

## Required Prompt Extensions

List prompt extensions that any execution prompt must load before implementing this plan.

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

## Goal
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

## Scope
## Non-Goals
## Phases
## Validation
## Vault Writeback

## Required Prompt Bundles

List prompt bundles that execution prompts should load before implementing this plan.

- [[PB-0001 Standard Implementation Bundle]]

## Lifecycle

Use [[Implementation Plan Lifecycle]]. Set lifecycle status accurately and link `implemented_by` / `superseded_by` when applicable.

If this plan is derived from discovered follow-up work, set `derived_work_id`, `origin_run`, `origin_evidence`, and `created_prompt`, then list it in [[Derived Work Index]].
