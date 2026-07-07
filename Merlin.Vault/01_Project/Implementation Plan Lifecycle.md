---
type: project
status: current
---

# Implementation Plan Lifecycle

## Purpose

Implementation plans are durable design/execution docs. They are not the same as short prompts.

## Statuses

- draft
- ready
- in_progress
- implemented
- superseded
- obsolete
- future
- blocked

## Rules

1. Only `ready` plans should be used directly by agents.
2. `implemented` plans must link to the agent run that implemented them.
3. `superseded` plans must link to the replacement plan.
4. `obsolete` plans must explain why they should not be used.
5. `future` plans must explain what dependencies are missing.
6. `blocked` plans must list blockers.
7. Every implementation plan should list required prompt bundles/extensions.

## Required Frontmatter

```yaml
---
type: implementation-plan
plan_id: PLAN-YYYY-MM-DD-NNN
derived_work_id: DW-YYYY-MM-DD-NNN
status: draft | ready | in_progress | implemented | superseded | obsolete | future | blocked
ready_for_agent: true | false
task_type: implementation | bugfix | refactor | investigation | documentation | test-only
derived_work_type: prerequisite | bugfix | hardening | phase-split | investigation | documentation | test-seam | refactor | future-feature
origin_run: RUN-YYYY-MM-DD-NNN
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
risk_level: low | medium | high | critical
created_prompt: PROMPT-YYYY-MM-DD-NNN
implemented_by:
superseded_by:
---
```

## Derived Plans

Derived plans are implementation plans created from evidence discovered during another task.

They must include:

- `plan_id`
- `derived_work_id`
- `origin_run`
- `origin_evidence`
- `created_prompt`

A derived plan must not be marked `ready` unless:

1. scope is clear;
2. owner/components are identified;
3. dependencies are known;
4. non-goals are listed;
5. validation is defined;
6. safety/cancellation/confirmation/interruption implications are addressed.

Use [[Derived Work Index]] to make cross-area derived work discoverable.
