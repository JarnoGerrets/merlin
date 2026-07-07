---
type: implementation-prompt
prompt_id: PROMPT-2026-07-07-001
derived_work_id:
status: used
related_plan: PLAN-2026-07-07-001
related_plan_path: Merlin.Vault/13_Implementation_Plans/General/PLAN-2026-07-07-001 Derived Work Planning Layer Plan.md
origin_run: RUN-2026-07-07-007
task_mode: documentation
tags:
  - merlin
  - agent/prompt
---

# PROMPT-2026-07-07-001 Implement Derived Work Planning Layer

Copy/paste this into the agent:

```text
Use Merlin.Vault/AGENT.md.

Task mode: documentation

Implement:
Merlin.Vault/13_Implementation_Plans/General/PLAN-2026-07-07-001 Derived Work Planning Layer Plan.md

Scope:
Full documentation-only plan.

Goal:
Add a Derived Work Planning Layer to the vault so future agents must create concrete implementation plans and short copy/paste prompts when they discover actionable prerequisites, No-Go blockers, bugs, missing test seams, architecture gaps, or separable next phases.

Required behavior:
- Do not change runtime code.
- Update vault operating rules only.
- Add derived work IDs:
  - DW-YYYY-MM-DD-NNN
  - PLAN-YYYY-MM-DD-NNN
  - PROMPT-YYYY-MM-DD-NNN
- Add derived work trigger and non-trigger rules.
- Add `PE-0260 Derived Work Planning Rules`.
- Update prompt extension indexes and selection guide.
- Add `PE-0260` to relevant prompt bundles.
- Add derived implementation plan and prompt templates.
- Update agent run report template.
- Add derived work index.
- Update current work dashboard.
- Add changelog and agent run report.

Non-goals:
- Do not implement any runtime code.
- Do not automatically execute derived work.
- Do not create plans for vague speculation.
- Do not create more than the required vault/process files for this task.

Validation:
- Verify all new IDs are unique.
- Verify all new paths are linked from indexes.
- Verify final report format includes `Derived work created`.
- Verify `PE-0260` is discoverable from the prompt extension index and selection guide.
- Documentation-only task; build/test is not required unless project/runtime files are changed.

Vault writeback:
- Create an agent run report.
- Update changelog.
- Update indexes and dashboard.
- Report every vault file changed.

Final response:
Use the documentation-only final response format from AGENT.md and include:

Derived work created:
- none, unless this documentation task discovers a concrete additional follow-up that is not already covered by the plan.
```
