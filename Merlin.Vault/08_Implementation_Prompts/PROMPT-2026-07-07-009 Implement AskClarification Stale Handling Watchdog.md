---
type: implementation-prompt
prompt_id: PROMPT-2026-07-07-009
derived_work_id: DW-2026-07-07-009
status: implemented
related_plan: PLAN-2026-07-07-009
related_plan_path: Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan.md
origin_run: RUN-2026-07-07-008
task_mode: implementation
tags:
  - merlin
  - agent/prompt
---

# PROMPT-2026-07-07-009 Implement AskClarification Stale Handling Watchdog

Implemented by: [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]]

Copy/paste this into the agent:

```text
Use Merlin.Vault/AGENT.md.

Task mode: implementation

Implement:
Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan.md

Scope:
PR10.4d only: stale `InterruptionState=handling` watchdog.

Required behavior:
- Perform Go/No-Go preflight before runtime changes.
- Implement only an owner-aware stale handling watchdog.
- Clear `InterruptionState=handling` to `none` only when no active owner remains.
- Preserve pending clarification, playback hold, cancellation, and interruption ownership boundaries.
- Do not implement full PR10.4 recomposition.
- Do not refactor BargeIn broadly.
- Do not change unrelated correction regeneration behavior.

Validation:
- Run:
  dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
  dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "BargeIn|ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification" -p:UseSharedCompilation=false
- Classify known BargeIn/correction failures separately from introduced failures.

Vault writeback:
- Create an agent run report.
- Update AskClarification recovery plan, Voice Interruption System, AskClarification bug note, affected code atlas notes, dashboard, progress, and changelog.
- If new concrete follow-up work is discovered, create a derived implementation plan and matching implementation prompt.

Final response:
- Summary
- Go/No-Go result
- Files changed
- Behavior changed
- Tests run and results
- Vault notes updated
- Whether PR10.4d is complete
- Whether full PR10.4 remains blocked
- Remaining work
- Derived work created, if any
```
