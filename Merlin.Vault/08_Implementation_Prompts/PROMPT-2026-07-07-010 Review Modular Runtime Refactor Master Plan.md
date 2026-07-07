---
type: implementation-prompt
prompt_id: PROMPT-2026-07-07-010
derived_work_id:
status: ready
related_plan: PLAN-2026-07-07-010
related_plan_path: Merlin.Vault/13_Implementation_Plans/Architecture_Refactor/PLAN-2026-07-07-010 Modular Runtime Refactor Master Plan.md
origin_run:
task_mode: investigation
tags:
  - merlin
  - agent/prompt
  - modular-runtime
---

# PROMPT-2026-07-07-010 Review Modular Runtime Refactor Master Plan

## Prompt

```text
Use Merlin.Vault/AGENT.md.

Task mode: investigation

Implement:
Merlin.Vault/13_Implementation_Plans/Architecture_Refactor/PLAN-2026-07-07-010 Modular Runtime Refactor Master Plan.md

Scope:
Review only. Do not change runtime code.

Required behavior:
- Perform Go/No-Go preflight before runtime changes.
- If Go, implement only the requested scope.
- If No-Go, stop before runtime changes and report blockers.
- Preserve existing externally visible behavior unless the plan explicitly changes it.
- Do not implement future phases.
- Do not bypass safety, confirmation, cancellation, or interruption behavior.
- Update the vault according to AGENT.md after meaningful work.
- Create derived work plan + prompt if concrete follow-up work is discovered.

Non-goals:
- Do not do a big-bang rewrite.
- Do not migrate browser or voice unless this exact plan requests it.
- Do not split into separate C# projects unless this exact plan requests it.
- Do not delete legacy paths unless this exact plan explicitly approves cutover.

Validation:
- Run the validation commands listed in the plan.
- If full validation cannot run, explain why and run the closest focused validation.
- Document pre-existing failures separately from introduced failures.

Final report:
- Go/No-Go result
- Files changed
- Behavior changed
- Runtime mode impact
- Tests run
- Vault notes updated
- Bugs found/updated
- Derived work created/considered
- Remaining work
```

## Notes

Use this prompt to have an agent review the master sequencing plan and report readiness. Do not implement the full refactor from the master plan.
