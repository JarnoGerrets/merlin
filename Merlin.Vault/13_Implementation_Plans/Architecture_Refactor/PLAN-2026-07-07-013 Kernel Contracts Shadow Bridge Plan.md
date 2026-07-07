---
type: implementation-plan
plan_id: PLAN-2026-07-07-013
derived_work_id:
status: implemented
task_type: refactor
derived_work_type: refactor
origin_run:
origin_task: User requested full documentation for a major Merlin modular runtime refactor and feature-owned settings migration.
origin_evidence: Current backend architecture, current vault conventions, uploaded appsettings files, and discussion of Host/Kernel/Modules/Adapters target structure.
related_features:
  - Modular Runtime Refactor
  - Kernel Brainstem
  - Strangler Migration
affected_systems:
  - backend
  - routing
  - kernel
  - logging
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
  - PE-0260
  - PE-0100
risk_level: high
ready_for_agent: false
created_prompt: PROMPT-2026-07-07-013
implemented_by: RUN-2026-07-07-015
superseded_by:
---

# PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan

## Plan Status

Status: implemented
Ready for agent use: false
Reason: Implemented in [[RUN-2026-07-07-015 Kernel Contracts Shadow Bridge]].
Related architecture:
- [[Kernel Brainstem Architecture]]
- [[Strangler Migration Architecture]]

## Goal

Introduce the first real `Merlin.Kernel` contracts and a read-only shadow bridge from legacy requests into the Next runtime.

Legacy remains the only executor.

## Scope

1. Add kernel request/turn/result contracts.
2. Add a `IMerlinNextRuntime` interface.
3. Add `MerlinNextShadowRuntime` implementation.
4. Add a bridge from current request entry point to shadow runtime.
5. Add shadow trace logging.
6. Ensure shadow mode is non-blocking or bounded.
7. Add tests proving no side effects.

## Non-Goals

1. Do not execute capabilities.
2. Do not replace `CommandRouter`.
3. Do not change user-facing responses.
4. Do not publish real UI events from shadow path.
5. Do not create pending operations in real runtime state.
6. Do not open apps, browser, memory writes, or speech output.

## Proposed Contracts

```text
Next/Kernel/Requests/MerlinRequest.cs
Next/Kernel/Turns/MerlinTurnContext.cs
Next/Kernel/Routing/RouteDecision.cs
Next/Kernel/Capabilities/CapabilityDescriptor.cs
Next/Kernel/Capabilities/CapabilityResult.cs
Next/Kernel/Surfaces/SurfaceSnapshot.cs
Next/Kernel/Safety/SafetyDecision.cs
Next/Kernel/Presentation/MerlinResponse.cs
Next/Kernel/Events/MerlinEvent.cs
```

## Shadow Trace Shape

Trace fields:

| Field | Required |
| --- | --- |
| request id | yes |
| source | yes |
| normalized input text | yes |
| active surface if available | yes |
| pending operation prediction | optional |
| route prediction | optional |
| capability id | optional |
| confidence | optional |
| safety prediction | optional |
| execution disabled reason | yes |
| elapsed ms | yes |
| exception if any | yes |

Example log:

```text
MerlinNextShadowTrace request=... source=backend_idle_voice text="open notepad" capability=app.open execution=disabled_shadow_mode elapsedMs=4
```

## Bridge Placement

Preferred bridge location:
- after current request normalization but before or around legacy routing execution;
- must not delay voice-critical paths;
- must be disabled by config.

If bridge placement is unclear, No-Go and create a small investigation plan.

## Phases

### Phase 1 - Contracts

ID: PLAN-2026-07-07-013-P1

Add minimal immutable contracts.

Exit criteria:
- contracts compile;
- no runtime path uses them yet.

### Phase 2 - Shadow Runtime

ID: PLAN-2026-07-07-013-P2

Add:
- `IMerlinNextRuntime`;
- `MerlinNextShadowRuntime`;
- no-op route predictor returning `NoDecision` or simple diagnostics.

Exit criteria:
- direct unit tests can call shadow runtime.

### Phase 3 - Legacy Request Adapter

ID: PLAN-2026-07-07-013-P3

Add adapter:
- legacy request → `MerlinRequest`.

Include source, text, metadata, and request ID if available.

Exit criteria:
- adapter unit tests cover voice/text basics.

### Phase 4 - Bridge Hook

ID: PLAN-2026-07-07-013-P4

Wire bridge only when:
- `MerlinNext.Enabled == true`;
- mode is `Shadow`, `Hybrid`, `NextFirst`, or `NextOnly`;
- bridge is explicitly allowed.

In this plan, only `Shadow` should do meaningful trace work.

Exit criteria:
- default legacy behavior unchanged;
- when enabled, shadow logs appear;
- when shadow throws, legacy still works.

### Phase 5 - Tests

ID: PLAN-2026-07-07-013-P5

Tests:
- shadow disabled by default;
- shadow receives request when enabled;
- shadow exception does not break legacy request;
- shadow path does not invoke tool/app/browser/memory/speech services;
- trace includes request source and text.

### Phase 6 - Vault Writeback

ID: PLAN-2026-07-07-013-P6

Update:
- agent run;
- changelog;
- [[Kernel Brainstem Architecture]];
- code atlas for new kernel contracts;
- progress report.

## Go / No-Go Preflight

Go only if:
- `MerlinNext` skeleton exists;
- request bridge can be inserted without altering behavior;
- shadow mode can be read-only;
- tests can assert no side effects.

No-Go if:
- the only possible bridge placement would alter voice/interruption timing;
- shadow route requires executing tools;
- current request shape is unclear.

Partial-Go allowed:
- contracts and direct shadow runtime only, without bridge hook.

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

## Implementation Result

Implemented in [[RUN-2026-07-07-015 Kernel Contracts Shadow Bridge]].

Runtime behavior impact:

- Added minimal kernel contracts under `Merlin.Backend/Next/Kernel`.
- Added `IMerlinNextRuntime` and `MerlinNextShadowRuntime`.
- Added `LegacyMerlinRequestAdapter` for `AssistantRequest` to `MerlinRequest`.
- Added `MerlinNextShadowBridge`.
- `CommandRouter` starts the optional bridge after normalization, but catches bridge failures and continues legacy routing.
- Production shadow work only runs when `MerlinNext.Enabled=true`, `MerlinNext.ShadowEnabled=true`, and `MerlinNext.Mode=Shadow`.
- Shadow runtime only logs a `NoDecision` trace with `disabled_shadow_mode`; it does not execute capabilities, mutate pending state, publish UI events, speak, open apps/browser, write memory, or call tools.
