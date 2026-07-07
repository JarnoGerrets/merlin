---
type: implementation-plan
plan_id: PLAN-YYYY-MM-DD-NNN
derived_work_id: DW-YYYY-MM-DD-NNN
status: draft | ready | in_progress | implemented | superseded | obsolete | future | blocked
ready_for_agent: false
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
  - PE-0002
  - PE-0003
  - PE-0004
  - PE-0005
  - PE-0007
  - PE-0008
  - PE-0260
risk_level: low | medium | high | critical
created_prompt: PROMPT-YYYY-MM-DD-NNN
implemented_by:
superseded_by:
---

# PLAN-YYYY-MM-DD-NNN Title

## Plan Status

Status:
Ready for agent use:
Reason:
Derived work type:
Origin run:
Origin evidence:
Related feature:
Related architecture:
Related code atlas:
Related bug notes:
Created prompt:

## Goal

## Why This Exists

Explain what the previous agent discovered and why this needs its own plan.

## Go / No-Go Preflight

Before runtime changes, perform a Go/No-Go preflight.

Go only if:
- required owners/services exist,
- required dependencies exist,
- safety/cancellation/confirmation/interruption rules can be preserved,
- test seams exist or can be added safely,
- scope can be implemented without creating a parallel subsystem.

No-Go if:
- prerequisite ownership is unclear,
- current code contradicts this plan,
- implementation would bypass safety,
- scope is too broad,
- required tests cannot be created safely.

## Scope

## Non-Goals

## Dependencies

| Dependency | Status | Evidence |
| --- | --- | --- |

## Affected Systems

## Owning Components

| Component / File | Expected Role | Must Verify? |
| --- | --- | --- |

## Phases

### Phase 1 - Discovery / Verification

ID: PLAN-YYYY-MM-DD-NNN-P1

Goal:

Steps:

Validation:

Exit criteria:

### Phase 2 - Implementation

ID: PLAN-YYYY-MM-DD-NNN-P2

Goal:

Steps:

Validation:

Exit criteria:

### Phase 3 - Tests / Hardening

ID: PLAN-YYYY-MM-DD-NNN-P3

Goal:

Steps:

Validation:

Exit criteria:

### Phase 4 - Vault Writeback

ID: PLAN-YYYY-MM-DD-NNN-P4

Goal:

Required updates:
- agent run
- changelog
- progress report
- feature note
- architecture note if changed
- code atlas if changed
- roadmap/current-state if status changed
- bug note if relevant

Exit criteria:

## Validation Commands

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Adjust validation if the task is frontend, BrowserHost, documentation-only, or test-only.

## Required Prompt Bundles

## Required Prompt Extensions

## Final Agent Report Must Include

- Go/No-Go result
- files changed
- behavior changed
- tests run
- vault notes updated
- bugs found/updated
- derived work created, if any
- remaining work
