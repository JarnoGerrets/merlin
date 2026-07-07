---
type: implementation-prompt
prompt_id: PROMPT-2026-07-07-010
derived_work_id: DW-2026-07-07-010
status: implemented
related_plan: PLAN-2026-07-07-010
related_plan_path: Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan.md
origin_run: RUN-2026-07-07-009
task_mode: implementation
tags:
  - merlin
  - agent/prompt
implemented_by: RUN-2026-07-07-010
---

# PROMPT-2026-07-07-010 Implement AskClarification Full Recomposition Ownership

Status: implemented by [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]].

Copy/paste this into the agent:

```text
Use Merlin.Vault/AGENT.md.

Task mode: implementation

Implement:
Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan.md

Scope:
PR10.4e only: full pending AskClarification clarification/recomposition ownership.

Before runtime changes:
- Perform Go / No-Go preflight from AGENT.md.
- Verify PR10.4a, PR10.4b, PR10.4c, and PR10.4d are still present.
- Re-run or inspect known BargeIn idle-capture failures and classify them; do not hide them.

Required behavior:
- Bind consumed pending clarification responses to the live clarification/recomposition owner.
- Prevent generic command routing and legacy cleanup from stealing pending clarification answers.
- Reuse existing playback/model/speech output ports.
- Preserve pending timeout/cancel cleanup and stale handling watchdog behavior.
- Preserve global stop/cancel behavior.
- Keep PR10.4a terminal fallback for unsupported or failed ownership paths.

Non-goals:
- Do not refactor BargeIn broadly.
- Do not move pending clarification ownership into playback, LiveUtteranceGate, or responsive feedback.
- Do not change unrelated correction regeneration behavior.

Validation:
- Run:
  dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
  dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification|BargeIn" -p:UseSharedCompilation=false
- If broad BargeIn tests still fail, classify known idle-capture failures separately from PR10.4e results.

Vault writeback:
- Create an agent run report.
- Update AskClarification recovery plan, Voice Interruption System, AskClarification bug note, affected code atlas notes, dashboard, progress, and changelog.

Final response:
- Summary
- Go/No-Go result
- Files changed
- Behavior changed
- Tests run and results
- Vault notes updated
- Whether full PR10.4 is complete
- Remaining work
```
