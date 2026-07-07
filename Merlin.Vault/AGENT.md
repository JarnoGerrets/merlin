---
type: agent-operating-manual
status: active
scope: merlin-vault-root
tags:
  - merlin
  - agent
  - operating-system
  - project-brain
---

# Merlin Agent Operating Manual

This file is the single bootloader for agents working in the Merlin repository.

Future prompts should not need to list every feature note, architecture note, code atlas note, prompt extension, bundle, writeback file, or validation rule manually.

A normal future prompt should be as small as:

```text
Use Merlin.Vault/AGENT.md.

Task mode: implementation

Implement:
Merlin.Vault/13_Implementation_Plans/<Area>/<Plan>.md
```

or:

```text
Use Merlin.Vault/AGENT.md.

Task mode: investigation

Investigate prerequisites for:
Merlin.Vault/13_Implementation_Plans/<Area>/<Plan>.md
```

This file tells the agent how to resolve everything else.

---

# 1. Prime Directive

Merlin.Vault is the project brain.

Before meaningful work, the agent reads from the vault.

After meaningful work, the agent writes back to the vault.

## Core rule

```text
If runtime code changes but the vault does not, the task is incomplete.
```

## Derived work rule

```text
Concrete follow-up work discovered during a task must become a durable plan and prompt, not just a chat note or Remaining Work bullet.
```

Derived work generation is writeback, not execution. The agent may document discovered follow-up work, but it must not implement that follow-up unless the current user prompt explicitly requested it.

## Go / No-Go rule

```text
Go = implement.

No-Go = stop before runtime changes and report blockers.

Partial-Go = only allowed if explicitly approved by the user prompt or by the implementation plan.
```

No-Go never means “do a smaller fallback anyway.”

---

# 2. Main Repository Scope

The main vault scope is defined by [[Scope Rules]].

In normal Merlin work, the agent should cover:

- `Merlin.Backend`
- `Merlin.Backend.Tests`
- `Merlin.BrowserHost`
- `Merlin.Frontend`
- `Merlin.ToDo`
- `Merlin.Vault`
- `docs`

The following are intentionally ignored unless the user explicitly targets them:

- `Merlin.OrbLab`
- `Merlin.UiCanvas`
- `Merlin.VoiceTest`
- `STT_FAILURE_DIAGNOSTICS`
- `mnt`
- `scripts`
- `tools`
- `tools/chatterbox-bench`

Do not report ignored folders as missing coverage.

---

# 3. Minimal Prompt Contract

A user prompt may provide only:

```text
Use Merlin.Vault/AGENT.md.

Task mode: <implementation | bugfix | investigation | documentation | refactor | test-only>

Target:
<path to implementation plan, feature note, bug note, or investigation request>
```

The agent must then:

1. Read this `AGENT.md`.
2. Resolve the task target.
3. Determine task type.
4. Load required prompt bundles/extensions.
5. Read relevant feature, architecture, roadmap, bug, code atlas, source, and plan notes.
6. Perform Go/No-Go preflight.
7. Execute only if Go.
8. Write back to the vault.
9. Return a final report.

---

# 4. Always Read First

For any implementation, bugfix, investigation, refactor, test-only, or meaningful documentation task, read these first:

- [[00_Index]]
- [[Scope Rules]]
- [[Status Rules]]
- [[Agent Preflight Checklist]]
- [[Agent Writeback Rules]]
- [[Implementation Prompt Convention]]
- [[Prompt Extension Selection Guide]]
- [[PE-0008 Go No-Go Rules]]
- [[PE-0260 Derived Work Planning Rules]]
- [[Current Work Dashboard]]

For documentation-only vault organization tasks, also read:

- [[Vault Maintenance Checklist]]
- [[Implementation Plan Lifecycle]]
- [[Agent Run Naming Rules]]

---

# 5. Task Modes

The agent must classify the requested work into exactly one primary task mode.

## implementation

Use when adding a requested feature, phase, or planned behavior.

Default bundle:

- [[PB-0001 Standard Implementation Bundle]]

Also add area bundles/extensions based on affected systems.

## bugfix

Use when fixing a defect, failing behavior, regression, runtime wedge, exception, or wrong output.

Default bundle:

- [[PB-0009 Bugfix Bundle]]

Also add area bundles/extensions based on affected systems.

## investigation

Use when the task is to inspect, report, decide prerequisites, compare logs, or perform Go/No-Go analysis.

Default bundle:

- [[PB-0008 Investigation Bundle]]

No runtime code changes are allowed unless the user explicitly converts the task into implementation.

## documentation

Use when editing vault/docs only.

Default bundle:

- [[PB-0007 Documentation Bundle]]

No runtime code changes are allowed.

## refactor

Use when changing structure while preserving behavior.

Default bundle:

- [[PB-0010 Refactor Bundle]]

Refactors must not sneak in feature expansion.

## test-only

Use when adding or updating tests for existing intended behavior.

Required extension:

- [[PE-0250 Test-Only Task Rules]]

Production behavior must not change.

---

# 6. Bundle and Extension Resolution

The user should not have to list every bundle.

The agent must infer required bundles/extensions from:

1. Task mode.
2. Target plan frontmatter.
3. Target plan sections.
4. Related feature notes.
5. Affected code atlas notes.
6. Prompt Extension Selection Guide.
7. Area/system keywords in the task.

## Always include for implementation, bugfix, refactor, and substantial investigation

- [[PE-0001 Agent Preflight]]
- [[PE-0002 Scope and Status Rules]]
- [[PE-0003 Implementation Guardrails]]
- [[PE-0004 Testing and Validation]]
- [[PE-0005 Vault Writeback Rules]]
- [[PE-0007 Final Report Format]]
- [[PE-0008 Go No-Go Rules]]
- [[PE-0260 Derived Work Planning Rules]]

For investigation-only tasks, use [[PB-0008 Investigation Bundle]] and do not add implementation guardrails that imply coding unless needed for preflight.

## Area mapping

| Affected system | Add bundle / extension |
| --- | --- |
| Backend services, DI, command routing, tests | [[PE-0100 Backend Change Rules]] |
| Frontend/Godot/UI/windows/gesture UI | [[PE-0110 Frontend Godot Change Rules]] |
| BrowserHost/WebView2/native overlay/native input | [[PE-0120 BrowserHost Change Rules]] |
| Vision sidecar/camera/MediaPipe/gesture protocol | [[PE-0130 Vision Sidecar Change Rules]] |
| Memory/user facts/prompt blocks/memory lifecycle | [[PE-0140 Memory Change Rules]] |
| STT/TTS/live utterance/interruption/playback | [[PE-0150 Voice Pipeline Change Rules]] |
| Motion profiles/hand control/pinch/scroll/browser pointer | [[PE-0160 Motion Control Change Rules]] |
| BrowserWorkspace/page control/page snapshot/browser actions | [[PE-0170 Browser Workspace Change Rules]] |
| ActiveSurface/context routing/surface capabilities | [[PE-0180 Active Surface Change Rules]] |
| Risky/destructive/privacy/native input/browser/page/file/message actions | [[PE-0006 Safety and Confirmation Rules]] |

## Bundle mapping

| Work type / system | Preferred bundle |
| --- | --- |
| Standard implementation | [[PB-0001 Standard Implementation Bundle]] |
| Backend feature | [[PB-0002 Backend Feature Bundle]] |
| BrowserWorkspace / BrowserHost / page actions | [[PB-0003 Browser Workspace Bundle]] |
| Motion / gesture / vision / pointer | [[PB-0004 Motion Control Bundle]] |
| Voice pipeline / interruption / TTS / STT | [[PB-0005 Voice Pipeline Bundle]] |
| Memory | [[PB-0006 Memory Bundle]] |
| Documentation-only | [[PB-0007 Documentation Bundle]] |
| Investigation-only | [[PB-0008 Investigation Bundle]] |
| Bugfix | [[PB-0009 Bugfix Bundle]] |
| Refactor | [[PB-0010 Refactor Bundle]] |

If a task touches multiple systems, load all relevant bundles/extensions.

If bundle rules conflict, follow the stricter safety/scoping rule.

---

# 7. Target Resolution

The target may be:

- an implementation plan in `13_Implementation_Plans`,
- a feature note in `03_Features`,
- a bug note in `09_Bugs`,
- an agent report request,
- a direct user-described task,
- or an imported source material file.

## If the target is an implementation plan

Read:

1. The plan itself.
2. Its frontmatter.
3. `related_features`.
4. `required_prompt_bundles`.
5. `required_prompt_extensions`.
6. `source_path` / source material if present.
7. linked feature notes.
8. linked architecture notes.
9. linked code atlas notes.
10. relevant bug notes and progress reports.

## If the target is a feature note

Read:

1. feature note,
2. related architecture notes,
3. code atlas links,
4. roadmap entries,
5. readiness section,
6. implementation plans,
7. bugs,
8. prompt bundles/extensions if listed.

If the feature is `future` or `blocked`, default to No-Go unless the user explicitly asks for planning/investigation.

## If the target is a bug note

Read:

1. bug note,
2. related feature,
3. related architecture,
4. related code atlas,
5. related agent runs,
6. relevant tests,
7. bug lifecycle status.

If the bug lacks reproduction or owner evidence, default to investigation before implementation.

## If the target is a direct user request with no plan

Do not guess blindly.

Find the closest matching feature/architecture/roadmap/code atlas notes.

If a safe implementation plan already exists, use it.

If no plan exists and the work is non-trivial, create or request an investigation/planning step first.

---

# 8. Required Implementation Plan Metadata

Every durable implementation plan should have frontmatter like:

```yaml
---
type: implementation-plan
plan_id: PLAN-YYYY-MM-DD-NNN
derived_work_id:
status: draft | ready | in_progress | implemented | superseded | obsolete | future | blocked
ready_for_agent: true | false
task_type: implementation | bugfix | refactor | investigation | documentation | test-only
derived_work_type:
origin_run:
origin_task:
origin_evidence:
related_features:
  - Feature Name
affected_systems:
  - voice
  - browser
required_prompt_bundles:
  - PB-0001
required_prompt_extensions:
  - PE-0008
  - PE-0260
risk_level: low | medium | high | critical
created_prompt:
implemented_by:
superseded_by:
source_origin:
source_path:
---
```

If metadata is missing:

1. Try to infer from feature and roadmap notes.
2. Enrich the plan metadata if the answer is obvious and documentation-only change is safe.
3. If still unclear, stop with No-Go metadata report.
4. Do not implement runtime code from an under-specified plan.

---

# 9. Go / No-Go Preflight

Before runtime changes, perform Go/No-Go.

## Go

Proceed only if:

- required owners/services exist,
- dependencies are present,
- architecture boundaries are clear,
- safety/cancellation/confirmation rules can be preserved,
- test seams exist or can be added safely,
- requested scope can be implemented without inventing a parallel subsystem,
- non-goals are clear,
- feature is not blocked/future unless explicitly overridden.

## No-Go

Stop before runtime code changes if:

- prerequisite system is missing,
- correct owner/service does not exist,
- implementation would require broader architecture first,
- task would create a parallel subsystem,
- task would bypass safety/confirmation/cancellation/interruption rules,
- test seams are absent and cannot be safely added,
- current code contradicts the plan,
- user prompt does not explicitly approve fallback/partial behavior.

## Partial-Go

Partial-Go is allowed only if the user prompt or plan explicitly says so.

Accepted wording:

```text
If full implementation is blocked, implement this approved fallback scope: ...
```

or:

```text
Partial implementation is approved only for: ...
```

Otherwise, No-Go means no runtime changes.

## Required No-Go response

A No-Go report must include:

1. requested task,
2. Go/No-Go result,
3. exact blockers,
4. missing prerequisites,
5. evidence from code/vault,
6. why implementation would be unsafe/premature,
7. required prerequisite work,
8. proposed implementation sequence,
9. suggested next prompt,
10. vault notes updated.

Allowed No-Go changes are documentation/vault-only.

---

# 10. Implementation Rules

When Go is reached:

1. Implement only the requested phase/scope.
2. Do not sneak in future phases.
3. Preserve existing behavior unless explicitly changing it.
4. Keep changes narrow.
5. Respect ownership boundaries from architecture/code atlas.
6. Do not move logic into broad routers/gates if a dedicated service should own it.
7. Do not bypass safety/confirmation/cancellation/interruption.
8. Add or update tests for behavior changes.
9. Update code atlas for changed files/classes.
10. Update feature/architecture/roadmap/progress status.

---

# 11. Safety Rules

Routing context may decide where a command goes.

Safety decides whether it may execute.

Always preserve:

- BrowserPageSafetyGuard,
- confirmation flow,
- global stop/cancel,
- assistant playback controls,
- cancellation tokens,
- interruption ownership,
- state cleanup,
- memory privacy,
- no destructive external/file/message actions without confirmation.

Special caution is required for:

- native input,
- browser page actions,
- motion clicks,
- file operations,
- messaging,
- purchases/payments,
- deletion,
- upload/send/submit,
- credentials,
- sensitive page contents,
- memory writes.

If uncertain, stop and produce No-Go or safety report.

---

# 12. Validation Rules

Use [[PE-0004 Testing and Validation]].

Default backend validation:

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Run focused tests first when appropriate, then full backend tests before completion if runtime code changed.

Do not ignore failing tests.

If full tests fail:

1. classify each failure,
2. determine pre-existing vs introduced,
3. fix introduced failures,
4. document unrelated/pre-existing failures in agent run and bug/progress notes.

For documentation-only tasks, build/test is not required unless project files or runtime code changed.

---

# 13. Vault Writeback

Use [[Agent Writeback Rules]] and [[PE-0005 Vault Writeback Rules]].

## After implementation

Update:

1. `14_Agent_Runs`,
2. `16_Change_Log`,
3. relevant `15_Progress_Reports`,
4. affected `03_Features`,
5. affected `02_Architecture`,
6. affected `11_Code_Atlas`,
7. relevant `05_Roadmaps`,
8. `04_Current_State` if status changed,
9. `09_Bugs` if bugs/fragility found or fixed,
10. implementation plan status.

## After investigation

Update:

1. `07_Agent_Reports` or `14_Agent_Runs`,
2. affected feature/architecture/code atlas notes if understanding changed,
3. bug notes if issues found,
4. progress report and current work dashboard if blockers found,
5. suggested next plan/prompt if useful.

## After documentation-only tasks

Update:

1. indexes,
2. changelog if structure changed,
3. agent run report if meaningful.

## Derived work writeback

If a task discovers concrete follow-up work, required prerequisites, No-Go blockers, approved Partial-Go remainders, bugs or fragility outside current scope, missing test seams, architecture owner gaps, separable next phases, or stale/under-specified plans, create or update:

1. a derived implementation plan under `13_Implementation_Plans/<Area>/`,
2. a matching short implementation prompt under `08_Implementation_Prompts/`,
3. relevant indexes and backlinks,
4. links from the current agent run report,
5. dashboard/progress entries if the item affects active, blocked, or next-safe work.

Use:

- `DW-YYYY-MM-DD-NNN`
- `PLAN-YYYY-MM-DD-NNN`
- `PROMPT-YYYY-MM-DD-NNN`

Do not create derived work artifacts for vague ideas, speculation, out-of-scope side projects, tiny cleanup notes, or more than three derived items in one run unless the user explicitly requests a full planning sweep.

---

# 14. Agent Run Reports

Every meaningful task should create an agent run note.

Use [[Agent Run Naming Rules]].

Format:

```text
RUN-YYYY-MM-DD-NNN Short Task Title.md
```

Location:

```text
Merlin.Vault/14_Agent_Runs/YYYY/
```

Required frontmatter:

```yaml
---
type: agent-run
run_id: RUN-YYYY-MM-DD-NNN
date: YYYY-MM-DD
run_type: implementation | bugfix | investigation | documentation | refactor | test-only
related_features:
  - Feature Name
status: completed | partial | failed | blocked
branch:
commit_before:
commit_after:
agent:
---
```

Run reports must include:

- task,
- prompt/source,
- vault notes read,
- scope,
- non-goals,
- files changed,
- behavior changed,
- tests/validation,
- bugs found,
- vault updates,
- status changes,
- derived work created or considered,
- remaining work,
- risks/follow-up.

---

# 15. Bug Reporting

Use [[Bug Lifecycle Rules]].

If a bug is found:

1. Create or update a bug note under `09_Bugs`.
2. Assign status:
   - `open`
   - `investigating`
   - `fixed`
   - `verified`
   - `wont-fix`
3. Link affected feature, code atlas, agent run, and tests.
4. Add reproduction/evidence if available.
5. Do not leave bugs only in chat.

A fixed bug is not verified until validation is documented.

---

# 16. Progress Reports and Current Work Dashboard

Meaningful changes should update:

- [[Current Work Dashboard]]
- relevant progress report in `15_Progress_Reports`
- relevant roadmap note in `05_Roadmaps`

Current Work Dashboard should show:

- active work,
- next safe tasks,
- blocked work,
- recently completed work,
- high-risk areas,
- required prompt bundles.

---

# 17. Implementation Plans vs Prompts vs Source Material

Use this distinction:

| Type | Location | Purpose |
| --- | --- | --- |
| Curated implementation plan | `13_Implementation_Plans` | Durable implementation design / phase plan |
| Short execution prompt | `08_Implementation_Prompts` | Concrete one-off task prompt |
| Raw imported ToDo/source | `12_Source_Material` | Historical/source material |
| Agent report | `07_Agent_Reports` | Inspection/status/current-structure reports |
| Agent run | `14_Agent_Runs` | What an agent actually did |

Implementation prompts should be short and point to `AGENT.md` plus one plan.

Large design should live in implementation plans, not one giant prompt.

---

# 18. Handling Missing or Stale Vault Information

If vault notes disagree with code:

1. code reality wins,
2. update the vault,
3. report the mismatch.

If the target plan is obsolete/superseded:

1. do not implement it,
2. follow superseded_by if present,
3. otherwise No-Go and report.

If feature status appears wrong:

1. verify against code/tests,
2. update status if documentation-only change is in scope,
3. otherwise report required vault correction.

---

# 19. Final Response Format

Every final response must include enough for the user to trust what happened.

## Implementation / bugfix / refactor

Report:

```text
Summary:
Files changed:
Behavior changed:
Tests run:
Vault notes updated:
Bugs found/updated:
Go/No-Go result:
Remaining work:
Known limitations:
Derived work created:
```

## Investigation

Report:

```text
Runtime code changed: no
Go/No-Go result:
Findings:
Missing prerequisites:
Evidence:
Recommended next PR:
Vault notes updated:
Suggested next prompt:
Derived work created:
```

## Documentation-only

Report:

```text
Runtime code changed: no
Vault notes changed:
Indexes updated:
Links checked:
Remaining documentation gaps:
Derived work created:
```

---

# 20. Standard Future Prompt Examples

## Implementation

```text
Use Merlin.Vault/AGENT.md.

Task mode: implementation

Implement:
Merlin.Vault/13_Implementation_Plans/Motion/Motion Control Profile Layer Plan.md

Scope:
Phase 2 only.
```

## Bugfix

```text
Use Merlin.Vault/AGENT.md.

Task mode: bugfix

Fix:
Merlin.Vault/09_Bugs/AskClarification Live Dead-End.md
```

## Investigation

```text
Use Merlin.Vault/AGENT.md.

Task mode: investigation

Investigate prerequisites for:
Merlin.Vault/13_Implementation_Plans/Voice/AskClarification Live Dead-End Recovery Plan.md

Runtime code changes are not allowed.
```

## Documentation

```text
Use Merlin.Vault/AGENT.md.

Task mode: documentation

Update:
Merlin.Vault/13_Implementation_Plans/Browser/Browser Control Phases 2-5 Plan.md
```

---

# 21. Quick Decision Matrix

| Situation | Action |
| --- | --- |
| Plan is ready and dependencies exist | Go, implement |
| Plan is blocked/future | No-Go unless user explicitly overrides |
| Prerequisite owner missing | No-Go, report prerequisite |
| User asks for investigation | No runtime code changes |
| User asks for implementation but plan lacks metadata | Enrich plan if obvious; otherwise No-Go |
| Tests fail before changes | Document baseline; do not hide |
| Tests fail after changes | Fix or report as failed/partial |
| Feature touches safety/destructive action | Load safety rules and require confirmation path |
| Feature touches motion/browser/voice | Load relevant area bundle |
| Runtime code changed | Vault writeback required |
| Bug found | Bug note required |

---

# 22. Absolute Non-Negotiables

1. No-Go means no runtime code changes.
2. Partial implementation requires explicit approval.
3. Do not bypass safety/confirmation/cancellation/interruption.
4. Do not build future phases unless explicitly requested.
5. Do not leave code changes without vault updates.
6. Do not leave bugs only in chat.
7. Do not trust old plans over current code.
8. Do not treat ignored side-projects as missing coverage.
9. Do not turn LiveUtteranceGate or CommandRouter into giant app-specific switchboards.
10. Do not create parallel subsystems when an owner should exist.

---

# 23. Related Operating Notes

- [[00_Index]]
- [[Scope Rules]]
- [[Status Rules]]
- [[Agent Preflight Checklist]]
- [[Agent Writeback Rules]]
- [[Agent Run Naming Rules]]
- [[Bug Lifecycle Rules]]
- [[Implementation Plan Lifecycle]]
- [[Implementation Prompt Convention]]
- [[Prompt Extension Selection Guide]]
- [[Prompt Extensions Index]]
- [[Current Work Dashboard]]
- [[Vault Maintenance Checklist]]
