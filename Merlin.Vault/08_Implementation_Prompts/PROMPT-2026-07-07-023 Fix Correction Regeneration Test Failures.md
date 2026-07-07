---
type: implementation-prompt
prompt_id: PROMPT-2026-07-07-023
derived_work_id: DW-2026-07-07-023
status: ready
related_plan: PLAN-2026-07-07-023
related_plan_path: Merlin.Vault/13_Implementation_Plans/Correction/PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan.md
origin_run: RUN-2026-07-07-011
task_mode: bugfix
tags:
  - merlin
  - agent/prompt
---

# PROMPT-2026-07-07-023 Fix Correction Regeneration Test Failures

Copy/paste this into the agent:

```text
Use Merlin.Vault/AGENT.md.

Task mode: bugfix

Fix:
Merlin.Vault/13_Implementation_Plans/Correction/PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan.md

Scope:
Fix only the known CorrectionRegenerationDispatcherTests failures listed in the plan.

Non-goals:
- Do not change AskClarification PR10.4 ownership behavior.
- Do not change BargeIn idle-capture behavior.
- Do not implement correction learning or control profile learning.

Validation:
- Run the correction regeneration focused tests.
- Run PR10.4 focused tests to prove no regression.
- Classify any remaining backend failures with evidence.

Vault writeback:
- Create an agent run report.
- Update Correction Layer, Correction Flow/code atlas notes, Current Test Coverage, Current Work Dashboard, Correction Layer Progress, Voice Pipeline Progress if affected, bug note, changelog, and derived work index.
```
