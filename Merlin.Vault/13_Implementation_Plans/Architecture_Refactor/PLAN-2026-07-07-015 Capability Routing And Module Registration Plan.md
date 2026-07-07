---
type: implementation-plan
plan_id: PLAN-2026-07-07-015
derived_work_id:
status: future
task_type: refactor
derived_work_type: refactor
origin_run:
origin_task: User requested full documentation for a major Merlin modular runtime refactor and feature-owned settings migration.
origin_evidence: Current backend architecture, current vault conventions, uploaded appsettings files, and discussion of Host/Kernel/Modules/Adapters target structure.
related_features:
  - Modular Runtime Refactor
  - Capability Routing
affected_systems:
  - backend
  - architecture
required_prompt_bundles:
  - PB-0010
required_prompt_extensions:
  - PE-0001
  - PE-0002
  - PE-0003
  - PE-0004
  - PE-0005
  - PE-0007
  - PE-0008
  - PE-0100
  - PE-0220
  - PE-0260
risk_level: high
ready_for_agent: false
created_prompt: PROMPT-2026-07-07-015
implemented_by:
superseded_by:
---

# PLAN-2026-07-07-015 Capability Routing And Module Registration Plan

## Plan Status

Status: future
Ready for agent use: false
Reason: Depends on earlier architecture migration phases; do not execute before prerequisites are implemented.
Related feature: Capability Routing
Related architecture:
- [[Modular Runtime Architecture]]
- [[Strangler Migration Architecture]]

## Goal

Add module-owned capability providers and explicit capability-id route decisions.

## Scope

Introduce `ICapabilityProvider`, module registration, descriptor validation, legacy intent-to-capability mapping, and dispatcher ownership.

## Non-Goals

1. Do not change unrelated feature behavior.
2. Do not bypass safety, confirmation, cancellation, or interruption ownership.
3. Do not implement future modules beyond this plan's scope.
4. Do not delete legacy paths until a capability-specific cutover is proven.
5. Do not mix feature expansion with this refactor.

## Dependencies

| Dependency | Required Status | Notes |
| --- | --- | --- |
| [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]] | implemented or not relevant | Settings ownership should be clear first. |
| [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] | implemented | Next skeleton and runtime mode flags required. |
| [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]] | implemented | Kernel contracts and shadow bridge required. |
| [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]] | implemented for most later plans | Proves vertical-slice migration. |

## Phases

### Phase 1 - Discovery / Verification

ID: PLAN-2026-07-07-015-P1

Goal:
Verify current code reality before runtime changes.

Steps:
1. Inspect current owners and tests.
2. Compare current code against vault notes.
3. Identify safety/cancellation/confirmation impacts.
4. Identify exact legacy path to preserve.
5. Produce Go/No-Go result before changing runtime code.

Validation:
- No runtime changes before Go.
- If No-Go, document blockers and create derived work if concrete.

Exit criteria:
- owner and dependency map is clear.

### Phase 2 - Contracts / Seams

ID: PLAN-2026-07-07-015-P2

Goal:
Add only the seam needed for this migration stage.

Steps:
1. Add interfaces/contracts.
2. Register with DI.
3. Wrap existing behavior where possible.
4. Keep legacy path active.
5. Add tests for inert/default behavior.

Exit criteria:
- code compiles;
- default runtime behavior unchanged.

### Phase 3 - Shadow / Hybrid Behavior

ID: PLAN-2026-07-07-015-P3

Goal:
Enable observation or limited execution only when config explicitly allows it.

Steps:
1. Add trace output.
2. Add feature flag / capability allowlist.
3. Ensure no double execution.
4. Preserve legacy fallback for non-owned capabilities.
5. Add tests for each mode.

Exit criteria:
- Legacy mode unchanged;
- Shadow is read-only;
- Hybrid has exact ownership.

### Phase 4 - Tests / Hardening

ID: PLAN-2026-07-07-015-P4

Goal:
Protect against regressions.

Steps:
1. Add unit tests around new seams.
2. Add regression tests for legacy behavior.
3. Add no-double-execution tests for side-effectful paths.
4. Add manual validation checklist if live systems are affected.

Exit criteria:
- build passes;
- relevant tests pass;
- known unrelated failures are documented.

### Phase 5 - Vault Writeback

ID: PLAN-2026-07-07-015-P5

Required updates:
- agent run;
- changelog;
- progress report;
- relevant architecture note;
- code atlas for new/moved ownership;
- master cutover table if capability ownership changes;
- derived plans/prompts for discovered follow-up work.

## Go / No-Go Preflight

Go only if:
- earlier required plans are implemented;
- current owner is verified;
- safety boundaries can be preserved;
- tests can cover the change;
- the plan can be executed without broad unrelated rewrites.

No-Go if:
- the implementation would create a second permanent subsystem;
- current code contradicts the plan;
- safety/confirmation would be bypassed;
- required test seams do not exist and cannot be added safely;
- live voice/browser timing would be altered without explicit approval.

Partial-Go allowed only for:
- documentation updates;
- inert contracts;
- tests;
- shadow-only read-only traces.

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

Task-type:
- [[PE-0220 Refactor Task Rules]]

## Validation Commands

Run focused validation first, then broader validation if runtime code changes:

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

If the change touches frontend, BrowserHost, or live-only systems, add the relevant manual validation checklist from the plan.
