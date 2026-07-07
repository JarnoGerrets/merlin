---
type: implementation-prompt
prompt_id: PROMPT-2026-07-07-022
derived_work_id: DW-2026-07-07-022
status: ready
related_plan: PLAN-2026-07-07-022
related_plan_path: Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan.md
origin_run: RUN-2026-07-07-011
task_mode: bugfix
tags:
  - merlin
  - agent/prompt
---

# PROMPT-2026-07-07-022 Fix BargeIn Idle Capture Test Failures

Copy/paste this into the agent:

```text
Use Merlin.Vault/AGENT.md.

Task mode: bugfix

Fix:
Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan.md

Scope:
Fix only the four known BargeIn idle-capture failures listed in the plan.

Non-goals:
- Do not change AskClarification PR10.4 ownership behavior.
- Do not change correction regeneration behavior.
- Do not broadly refactor BargeIn.

Validation:
- Run the four failing tests individually or with a focused filter.
- Run PR10.4 focused tests to prove no regression.
- Run the broad voice/BargeIn filter and classify remaining failures.

Vault writeback:
- Create an agent run report.
- Update Voice Interruption System, BargeInCoordinator atlas, Current Test Coverage, Current Work Dashboard, Voice Pipeline Progress, bug note, changelog, and derived work index.
```
