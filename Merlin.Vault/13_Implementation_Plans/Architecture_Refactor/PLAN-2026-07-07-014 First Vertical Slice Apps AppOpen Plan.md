---
type: implementation-plan
plan_id: PLAN-2026-07-07-014
derived_work_id:
status: ready
task_type: refactor
derived_work_type: refactor
origin_run:
origin_task: User requested full documentation for a major Merlin modular runtime refactor and feature-owned settings migration.
origin_evidence: Current backend architecture, current vault conventions, uploaded appsettings files, and discussion of Host/Kernel/Modules/Adapters target structure.
related_features:
  - Modular Runtime Refactor
  - Apps Module
affected_systems:
  - backend
  - apps
  - routing
  - safety
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
ready_for_agent: true
created_prompt: PROMPT-2026-07-07-014
implemented_by:
superseded_by:
---

# PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan

## Plan Status

Status: ready
Ready for agent use: true
Reason: `app.open` is a safe first vertical slice if guarded against double execution.
Related architecture:
- [[Modular Runtime Architecture]]
- [[Module Boundary Architecture]]
- [[Strangler Migration Architecture]]

## Goal

Prove the full new architecture path with one safe capability:

```text
request
→ Next runtime
→ kernel turn
→ route/capability id
→ Apps module
→ AppOpen handler
→ presentation result
→ legacy-compatible response
```

Only `app.open` may be handled by Next in Hybrid mode.

Everything else remains legacy.

## Scope

1. Add Apps module skeleton.
2. Register `app.open` capability descriptor.
3. Add `AppOpenCapabilityHandler` wrapping current app open behavior.
4. Add hybrid capability ownership check.
5. Ensure legacy does not also execute `app.open` when Next owns it.
6. Add tests for both legacy fallback and Next execution.
7. Keep app safety behavior equivalent or stricter.

## Non-Goals

1. Do not migrate `app.close`.
2. Do not migrate `app.focus`.
3. Do not implement arbitrary shell commands.
4. Do not modify voice/barge-in/browser behavior.
5. Do not change app trust policy unless needed to preserve current behavior.
6. Do not make Next default owner for all commands.

## Proposed Files

```text
Next/Modules/Apps/
  AppsModule.cs
  AppCapabilityIds.cs
  AppOpenCapabilityHandler.cs
  AppOpenRequest.cs
  AppOpenResult.cs

Next/Kernel/Capabilities/
  ICapabilityHandler.cs
  ICapabilityDispatcher.cs
  CapabilityExecutionContext.cs
```

If contracts do not exist yet, this plan depends on Plan 013.

## Hybrid Ownership Rule

Pseudo-flow:

```csharp
if (_nextMode.IsHybrid &&
    _nextOptions.HandledCapabilities.Contains(route.CapabilityId))
{
    return await _nextRuntime.ExecuteAsync(request, cancellationToken);
}

return await _legacyCommandRouter.RouteAsync(request, cancellationToken);
```

Guard:
- if Next already executed the action, do not fall back to legacy;
- if Next fails before side effect, fallback may be allowed only for idempotent/safe failures;
- log execution ownership.

## Safety

`app.open` can be safe for trusted configured apps.

If the existing app open path requires confirmation for untrusted apps, the Next handler must preserve that.

Do not reduce confirmation requirements.

## Phases

### Phase 1 - Verify Existing App Open Path

ID: PLAN-2026-07-07-014-P1

Steps:
1. Inspect current app open tool/service.
2. Identify confirmation/trusted app behavior.
3. Identify tests.
4. Record legacy response shape.

Exit criteria:
- old behavior understood.

### Phase 2 - Apps Module Skeleton

ID: PLAN-2026-07-07-014-P2

Add Apps module registration with descriptor:

```text
CapabilityId: app.open
ModuleId: apps
Risk: safe/confirmation_required depending target trust
```

Exit criteria:
- descriptor can be listed in tests.

### Phase 3 - Handler Wrapper

ID: PLAN-2026-07-07-014-P3

Implement handler by wrapping/reusing existing app open service/tool.

Do not duplicate process-launch logic if existing service is stable.

Exit criteria:
- direct handler tests pass.

### Phase 4 - Hybrid Execution

ID: PLAN-2026-07-07-014-P4

Enable Next execution only when:

```json
"MerlinNext": {
  "Mode": "Hybrid",
  "HandledCapabilities": ["app.open"]
}
```

Exit criteria:
- `app.open` handled by Next only in Hybrid allowlist;
- all other commands use legacy.

### Phase 5 - Tests

ID: PLAN-2026-07-07-014-P5

Tests:
- Legacy mode still uses legacy path.
- Hybrid without `app.open` uses legacy path.
- Hybrid with `app.open` uses Next path.
- Next path does not double execute legacy path.
- untrusted/unknown app behavior matches existing confirmation/missing behavior.
- response text remains compatible.

### Phase 6 - Vault Writeback

ID: PLAN-2026-07-07-014-P6

Update:
- agent run;
- changelog;
- modular runtime progress;
- code atlas for Apps module/handler;
- cutover table in master/progress note.

## Go / No-Go Preflight

Go only if:
- Plan 012 and Plan 013 are implemented;
- current app open behavior is testable;
- no double execution path exists;
- confirmation/trusted behavior can be preserved.

No-Go if:
- app open path is too tangled with `CommandRouter` to wrap safely;
- current tests cannot detect double execution;
- Next would bypass confirmation.

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
