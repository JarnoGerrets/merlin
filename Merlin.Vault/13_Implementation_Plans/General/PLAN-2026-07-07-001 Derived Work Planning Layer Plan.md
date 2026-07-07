---
type: implementation-plan
plan_id: PLAN-2026-07-07-001
derived_work_id:
status: implemented
ready_for_agent: false
task_type: documentation
derived_work_type: documentation
origin_run: RUN-2026-07-07-007
origin_task: User requested agents to plan ahead and generate plan/prompt artifacts for discovered follow-up work.
origin_evidence: Existing vault has run reports, implementation plans, prompt conventions, and writeback rules, but no mandatory derived work materialization rule.
related_features:
  - Vault operating system
affected_systems:
  - vault
  - agent-process
required_prompt_bundles:
  - PB-0007
required_prompt_extensions:
  - PE-0001
  - PE-0002
  - PE-0005
  - PE-0007
  - PE-0008
  - PE-0230
  - PE-0260
risk_level: low
created_prompt: PROMPT-2026-07-07-001
implemented_by: RUN-2026-07-07-007
superseded_by:
---

# PLAN-2026-07-07-001 Derived Work Planning Layer Plan

## Plan Status

Status: implemented
Ready for agent use: false
Reason: Documentation-only vault process change has been applied.
Related feature: Vault operating system
Related architecture: None
Related code atlas: None

## Goal

Add a formal Derived Work Planning Layer to the Merlin vault so agents create concrete follow-up implementation plans and copy/paste prompts whenever they discover actionable prerequisite work, blockers, bugs, missing test seams, architecture gaps, or separable next phases.

## Scope

1. Add derived work rules to `AGENT.md`.
2. Add derived writeback rules to `Agent Writeback Rules.md`.
3. Add derived plan metadata to `Implementation Plan Lifecycle.md`.
4. Add derived prompt rules to `Implementation Prompt Convention.md`.
5. Create `PE-0260 Derived Work Planning Rules.md`.
6. Add `PE-0260` to prompt extension indexes and selection guide.
7. Update relevant prompt bundles to include `PE-0260`.
8. Create derived implementation plan and prompt templates.
9. Update agent run report template with derived work sections.
10. Add `Derived Work Index.md`.
11. Update current work dashboard with a newly derived work section.
12. Add changelog and agent run report for this documentation change.

## Non-Goals

1. Do not change runtime code.
2. Do not create an automated scheduler.
3. Do not let agents implement discovered follow-up work automatically.
4. Do not create infinite planning chains.
5. Do not create derived artifacts for vague ideas or speculation.

## Phases

### Phase 1 - Rules and Metadata

ID: PLAN-2026-07-07-001-P1

Update:
- `AGENT.md`
- `Agent Writeback Rules.md`
- `Implementation Plan Lifecycle.md`
- `Implementation Prompt Convention.md`

Exit criteria:
- derived work is defined;
- triggers and non-triggers are documented;
- IDs are documented;
- final report requirements include derived work.

### Phase 2 - Prompt Extension

ID: PLAN-2026-07-07-001-P2

Create:
- `PE-0260 Derived Work Planning Rules.md`

Update:
- `Prompt Extensions Index.md`
- `Prompt Extension Selection Guide.md`
- relevant prompt bundles.

Exit criteria:
- agents can load derived work planning as a reusable prompt extension.

### Phase 3 - Templates

ID: PLAN-2026-07-07-001-P3

Create:
- `Derived Implementation Plan Template.md`
- `Derived Implementation Prompt Template.md`

Update:
- `Agent Run Report Template.md`

Exit criteria:
- derived plans/prompts have stable structures;
- run reports capture created and considered derived work.

### Phase 4 - Indexes and Dashboard

ID: PLAN-2026-07-07-001-P4

Create/update:
- `Derived Work Index.md`
- `Current Work Dashboard.md`
- implementation prompt index;
- implementation plan indexes.

Exit criteria:
- derived work is discoverable from indexes and dashboards.

### Phase 5 - Validation and Writeback

ID: PLAN-2026-07-07-001-P5

Create:
- agent run report;
- changelog entry.

Validation:
- documentation-only review;
- links/paths are internally consistent;
- no runtime code changed.

## Validation

Documentation-only task. Build/test is not required unless runtime/project files are changed.

Manual validation:
- confirm all new IDs are unique;
- confirm indexes link to new files;
- confirm `AGENT.md` final response format includes derived work;
- confirm prompt bundles/extensions reference `PE-0260`.

## Vault Writeback

Completed:
- agent run report in `14_Agent_Runs/2026/`;
- changelog entry in `16_Change_Log/2026/2026 Change Log.md`;
- current work dashboard update;
- affected index updates.
