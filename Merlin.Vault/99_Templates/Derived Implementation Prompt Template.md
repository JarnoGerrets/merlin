---
type: implementation-prompt
prompt_id: PROMPT-YYYY-MM-DD-NNN
derived_work_id: DW-YYYY-MM-DD-NNN
status: ready
related_plan: PLAN-YYYY-MM-DD-NNN
related_plan_path: Merlin.Vault/13_Implementation_Plans/<Area>/PLAN-YYYY-MM-DD-NNN Short Title Plan.md
origin_run: RUN-YYYY-MM-DD-NNN
task_mode: implementation | bugfix | investigation | documentation | refactor | test-only
tags:
  - merlin
  - agent/prompt
---

# PROMPT-YYYY-MM-DD-NNN Implement Short Title

Copy/paste this into the agent:

```text
Use Merlin.Vault/AGENT.md.

Task mode: <implementation | bugfix | investigation | documentation | refactor | test-only>

Implement:
Merlin.Vault/13_Implementation_Plans/<Area>/PLAN-YYYY-MM-DD-NNN Short Title Plan.md

Scope:
<Exact phase or full plan>

Required behavior:
- Follow the implementation plan.
- Perform Go/No-Go preflight before runtime changes.
- If Go, implement only the requested scope.
- If No-Go, stop before runtime changes and create/update the required derived prerequisite plan and prompt.
- Preserve safety, confirmation, cancellation, interruption, memory, and routing boundaries.
- Do not create fallback/minimal behavior unless the plan explicitly allows Partial-Go.

Validation:
- Run focused tests for the changed area.
- Run required build/test commands from the plan.
- Document pre-existing failures separately from introduced failures.

Vault writeback:
- Create an agent run report.
- Update feature, architecture, code atlas, roadmap, progress, current-state, bug, and changelog notes as required.
- If new concrete follow-up work is discovered, create a derived implementation plan and matching implementation prompt.

Final response:
Use the final report format from Merlin.Vault/AGENT.md.
```
