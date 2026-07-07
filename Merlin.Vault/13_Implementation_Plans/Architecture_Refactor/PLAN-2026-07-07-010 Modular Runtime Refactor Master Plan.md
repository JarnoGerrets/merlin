---
type: implementation-plan
plan_id: PLAN-2026-07-07-010
derived_work_id:
status: ready
task_type: refactor
derived_work_type: refactor
origin_run:
origin_task: User requested full documentation for a major Merlin modular runtime refactor and feature-owned settings migration.
origin_evidence: Current backend architecture, current vault conventions, uploaded appsettings files, and discussion of Host/Kernel/Modules/Adapters target structure.
related_features:
  - Modular Runtime Refactor
affected_systems:
  - backend
  - configuration
  - voice
  - browser
  - memory
  - routing
  - active-surface
  - safety
  - frontend-bridge
required_prompt_bundles:
  - PB-0010
required_prompt_extensions:
  - PE-0001
  - PE-0002
  - PE-0003
  - PE-0004
  - PE-0005
  - PE-0006
  - PE-0007
  - PE-0008
  - PE-0100
  - PE-0150
  - PE-0170
  - PE-0180
  - PE-0220
  - PE-0260
risk_level: critical
ready_for_agent: false
created_prompt: PROMPT-2026-07-07-010
implemented_by:
superseded_by:
---

# PLAN-2026-07-07-010 Modular Runtime Refactor Master Plan

## Plan Status

Status: ready
Ready for agent use: false
Reason: This is the master governance plan. Agents should implement one child plan at a time.
Related feature: Modular Runtime Refactor
Related architecture:
- [[Modular Runtime Architecture]]
- [[Kernel Brainstem Architecture]]
- [[Module Boundary Architecture]]
- [[Adapter Boundary Architecture]]
- [[Feature-Owned Settings Architecture]]
- [[Strangler Migration Architecture]]

## Goal

Move Merlin from the current feature-accumulated backend toward a modular monolith with Host, Kernel, Modules, and Adapters.

The migration must preserve current working behavior while reducing the cost of adding new capabilities such as Discord, Spotify widget, file browser, browser media controls, learned control profiles, and future surface-specific modules.

## Why This Exists

Merlin has reached the point where new feature work often requires editing broad central services and overlapping route paths.

The refactor must address:

1. global settings sprawl;
2. broad `Program.cs` startup composition;
3. central `CommandRouter` responsibility overload;
4. overlapping intent/tool/capability routing;
5. static or narrow active surface modeling;
6. browser and voice subsystems acting as behemoths;
7. safety/confirmation logic that must not be bypassed;
8. need for feature-owned module boundaries;
9. future multi-input/multi-output operation.

## Target Architecture

```text
Merlin.Host
Merlin.Kernel
Merlin.Modules.Browser
Merlin.Modules.Voice
Merlin.Modules.Memory
Merlin.Modules.Apps
Merlin.Modules.Web
Merlin.Modules.Conversation
Merlin.Adapters.*
```

During migration, implement this first under the existing backend:

```text
Merlin.Backend/Next/
  Host/
  Kernel/
  Modules/
  Adapters/
```

Only split into separate C# projects after the boundaries are proven.

## Non-Negotiable Migration Rule

Do not rewrite the backend in place.

Use:

```text
Legacy active
→ Settings split
→ Next skeleton
→ Shadow mode
→ Hybrid per capability
→ NextFirst
→ NextOnly
```

## Scope

This master plan covers the full refactor sequence.

Child plans implement the work.

This plan itself is not a direct implementation prompt.

## Non-Goals

1. Do not immediately delete legacy services.
2. Do not migrate voice first.
3. Do not migrate browser page actions first.
4. Do not split into many C# projects before boundaries are proven.
5. Do not introduce feature expansion during structural refactors.
6. Do not disable current confirmation, safety, cancellation, or interruption behavior.
7. Do not make `Merlin.Kernel` a new god class.

## Dependencies

| Dependency | Status | Evidence |
| --- | --- | --- |
| Vault operating manual | exists | `AGENT.md` defines refactor, writeback, Go/No-Go, derived work. |
| Prompt bundles | exists | `PB-0010 Refactor Bundle`, area extensions. |
| Existing tests | partial | Backend tests exist; live BrowserHost/Godot/audio require manual validation. |
| Current settings files | exists | root appsettings currently contains many unrelated feature settings. |
| Active surface layer | exists/partial | Current state notes identify active surface as implemented but limited. |
| Browser/Voice tests | fragile | Current-state notes identify failing/fragile areas. |

## Master Phases

### Phase 1 - Feature-Owned Settings

Implement [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]].

Goal:
- reduce config sprawl;
- establish module ownership language;
- keep behavior unchanged.

Exit criteria:
- root `appsettings` is smaller;
- feature settings load through a loader extension;
- section names remain compatible;
- settings README exists;
- tests pass.

### Phase 2 - Next Skeleton and Runtime Modes

Implement [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]].

Goal:
- introduce `Next/Host`, `Next/Kernel`, `Next/Modules`, `Next/Adapters`;
- add runtime mode config;
- no behavior changes.

Exit criteria:
- runtime can run in `Legacy`;
- `Shadow` mode config exists but has no side effects;
- tests pass.

### Phase 3 - Kernel Contracts and Shadow Bridge

Implement [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]].

Goal:
- define kernel request/turn/capability/surface/safety/response contracts;
- mirror legacy requests into read-only shadow traces.

Exit criteria:
- legacy behavior unchanged;
- shadow trace logs what Next would do;
- no Next side effects.

### Phase 4 - First Vertical Slice

Implement [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]].

Goal:
- prove Host → Kernel → Module → Handler → Presentation → Response path.
- use a safe capability.

Exit criteria:
- `app.open` can be handled by Next in Hybrid mode;
- legacy handles all other capabilities;
- no double execution.

### Phase 5 - Capability Registry and Module Registration

Implement [[PLAN-2026-07-07-015 Capability Routing And Module Registration Plan]].

Goal:
- modules register capability descriptors;
- route decisions use capability IDs.

Exit criteria:
- central capability list is shrinking;
- modules own descriptors.

### Phase 6 - Dynamic Surface Registry

Implement [[PLAN-2026-07-07-016 Dynamic Surface Registry Plan]].

Goal:
- replace static surface thinking with module-provided surface descriptors.

Exit criteria:
- browser/dashboard surfaces are descriptors;
- future Discord/Spotify/file browser surfaces can register without enum growth.

### Phase 7 - Router Strangler

Implement [[PLAN-2026-07-07-017 CommandRouter Strangler Pipeline Plan]].

Goal:
- keep `CommandRouter` as facade;
- move responsibilities into pipeline steps.

Exit criteria:
- external callers still work;
- internal ownership is clearer.

### Phase 8 - Adapter Boundaries

Implement [[PLAN-2026-07-07-018 Adapter Boundary Migration Plan]].

Goal:
- wrap external integrations behind ports.

Exit criteria:
- DeepInfra/Ollama/BrowserHost/Godot/audio boundaries are clean enough for modules.

### Phase 9 - Browser Module

Implement [[PLAN-2026-07-07-019 Browser Module Migration Plan]].

Goal:
- migrate BrowserWorkspace into a module without bypassing safety.

Exit criteria:
- browser capabilities are module-owned;
- page actions still go through safety/confirmation.

### Phase 10 - Voice Module

Implement [[PLAN-2026-07-07-020 Voice Module Migration Plan]].

Goal:
- migrate voice playback/STT/TTS/barge-in/interruption ownership late and safely.

Exit criteria:
- voice state and interruption ownership are module-owned;
- current live behavior remains stable.

### Phase 11 - Validation Harness

Implement and maintain [[PLAN-2026-07-07-021 Validation Regression Harness Plan]] throughout.

Goal:
- trace old/new decisions;
- protect fragile live behavior.

## Cutover Tracking Table

Update this table after each child plan.

| Feature / Capability | Legacy Path | Next Path | Mode | Status |
| --- | --- | --- | --- | --- |
| settings load | active | feature-owned files | Legacy | Plan 011 implemented. |
| runtime mode config | active | inert options | Legacy | Plan 012 implemented; default Legacy/disabled. |
| shadow request trace | active | read-only trace | Shadow | Plan 013 implemented; disabled unless `Enabled=true`, `ShadowEnabled=true`, `Mode=Shadow`. |
| `app.open` | active | planned | Legacy | First hybrid candidate. |
| `app.focus` | active | planned | Legacy | After app.open. |
| `url.open` | active | planned | Legacy | Safe early candidate. |
| `web.search` | active/partial | planned | Legacy | Needs Web module. |
| `browser.media.pause` | active/partial | future | Legacy | Needs surface registry. |
| `browser.page.click` | active | future | Legacy | Safety-critical. |
| `voice.stop_speaking` | active | future | Legacy | Voice migration late. |
| interruption clarification | active | future | Legacy | High risk. |

## Required Prompt Extensions

Always:
- [[PE-0001 Agent Preflight]]
- [[PE-0002 Scope and Status Rules]]
- [[PE-0003 Implementation Guardrails]]
- [[PE-0004 Testing and Validation]]
- [[PE-0005 Vault Writeback Rules]]
- [[PE-0007 Final Report Format]]
- [[PE-0008 Go No-Go Rules]]
- [[PE-0260 Derived Work Planning Rules]]

Area-specific:
- [[PE-0100 Backend Change Rules]]
- [[PE-0150 Voice Pipeline Change Rules]]
- [[PE-0170 Browser Workspace Change Rules]]
- [[PE-0180 Active Surface Change Rules]]
- [[PE-0006 Safety and Confirmation Rules]]
Task-type:
- [[PE-0220 Refactor Task Rules]]

## Required Prompt Bundles

- [[PB-0010 Refactor Bundle]]
- [[PB-0002 Backend Feature Bundle]]
- [[PB-0003 Browser Workspace Bundle]]
- [[PB-0005 Voice Pipeline Bundle]]

## Validation

This master plan is documentation/governance. Runtime validation happens in child plans.

## Vault Writeback

Each child plan must update:

- agent run;
- changelog;
- relevant architecture notes;
- relevant progress reports;
- code atlas if ownership changes;
- current-state notes if status changes;
- derived work index if new prerequisites are found.

## Final Agent Report Must Include

For child plans:

- Go/No-Go result;
- files changed;
- behavior changed;
- runtime mode impact;
- capability ownership changes;
- tests run;
- vault notes updated;
- derived work created/considered.
