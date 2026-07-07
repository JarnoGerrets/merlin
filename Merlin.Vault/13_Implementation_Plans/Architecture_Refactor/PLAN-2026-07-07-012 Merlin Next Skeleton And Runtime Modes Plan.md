---
type: implementation-plan
plan_id: PLAN-2026-07-07-012
derived_work_id:
status: implemented
task_type: refactor
derived_work_type: refactor
origin_run:
origin_task: User requested full documentation for a major Merlin modular runtime refactor and feature-owned settings migration.
origin_evidence: Current backend architecture, current vault conventions, uploaded appsettings files, and discussion of Host/Kernel/Modules/Adapters target structure.
related_features:
  - Modular Runtime Refactor
  - Strangler Migration
affected_systems:
  - backend
  - host
  - configuration
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
risk_level: medium
ready_for_agent: false
created_prompt: PROMPT-2026-07-07-012
implemented_by: RUN-2026-07-07-014
superseded_by:
---

# PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan

## Plan Status

Status: implemented
Ready for agent use: false
Reason: Implemented in [[RUN-2026-07-07-014 Merlin Next Skeleton And Runtime Modes]].
Related architecture:
- [[Modular Runtime Architecture]]
- [[Strangler Migration Architecture]]
- [[ADR-0007 Modular Runtime Strangler Refactor]]

## Goal

Add a `Merlin.Backend/Next` skeleton and runtime mode configuration so the new Host/Kernel/Modules/Adapters architecture can be built beside the existing runtime.

No behavior should change.

## Scope

1. Add `Next/Host`, `Next/Kernel`, `Next/Modules`, `Next/Adapters` folders.
2. Add runtime mode options.
3. Add basic service registration extension methods.
4. Register Next services in `Program.cs` without taking over any request.
5. Add tests for option binding and mode parsing.
6. Add documentation comments to prevent accidental side effects.

## Non-Goals

1. Do not execute requests through Next.
2. Do not mirror traffic yet.
3. Do not implement capability routing yet.
4. Do not change `CommandRouter` behavior.
5. Do not move existing services.
6. Do not split C# projects.

## Proposed Files

```text
Merlin.Backend/Next/
  Host/
    MerlinNextRuntimeOptions.cs
    MerlinNextRuntimeMode.cs
    MerlinNextServiceCollectionExtensions.cs
  Kernel/
    README.md or placeholder contracts later
  Modules/
    README.md
  Adapters/
    README.md
```

Options section:

```json
{
  "MerlinNext": {
    "Enabled": false,
    "Mode": "Legacy",
    "ShadowEnabled": false,
    "HandledCapabilities": []
  }
}
```

Possible enum:

```csharp
public enum MerlinNextRuntimeMode
{
    Legacy,
    Shadow,
    Hybrid,
    NextFirst,
    NextOnly
}
```

## Phases

### Phase 1 - Add Runtime Options

ID: PLAN-2026-07-07-012-P1

Steps:
1. Add options class.
2. Add enum.
3. Bind from `MerlinNext` section.
4. Add validation:
   - mode must parse;
   - `HandledCapabilities` cannot be null;
   - `NextOnly` cannot be enabled by default.

Exit criteria:
- options bind in tests.

### Phase 2 - Add Skeleton Registration

ID: PLAN-2026-07-07-012-P2

Steps:
1. Add `AddMerlinNext(...)`.
2. Register options only.
3. Register placeholder marker service if needed.
4. Do not register handlers that execute behavior.

Exit criteria:
- backend build passes;
- DI resolves options.

### Phase 3 - Add Config Files

ID: PLAN-2026-07-07-012-P3

If settings split is already implemented:
- add `Settings/Kernel/merlin-next.settings.json`.

If not:
- add section to root `appsettings.json` with defaults.

Defaults:
```json
{
  "MerlinNext": {
    "Enabled": false,
    "Mode": "Legacy",
    "ShadowEnabled": false,
    "HandledCapabilities": []
  }
}
```

### Phase 4 - Tests

ID: PLAN-2026-07-07-012-P4

Add tests:
- default mode is Legacy;
- handled capabilities default empty;
- invalid mode fails validation or is handled deterministically;
- Next is inert when disabled.

### Phase 5 - Vault Writeback

ID: PLAN-2026-07-07-012-P5

Update:
- agent run;
- changelog;
- modular runtime progress;
- architecture note if skeleton paths differ.

## Go / No-Go Preflight

Go only if:
- settings migration has either been completed or the plan explicitly allows temporary root config;
- new skeleton can be registered without altering request handling;
- tests can prove inert behavior.

No-Go if:
- implementing this would require changing `CommandRouter` or WebSocket flow;
- feature execution is accidentally introduced.

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

Implemented in [[RUN-2026-07-07-014 Merlin Next Skeleton And Runtime Modes]].

Runtime behavior impact:

- `Merlin.Backend/Next` skeleton exists with Host, Kernel, Modules, and Adapters folders.
- `MerlinNext` runtime options bind from `Settings/Kernel/merlin-next.settings.json`.
- `Program.cs` registers `AddMerlinNext(builder.Configuration)`.
- `AddMerlinNext` only registers options and validation; it does not register request handlers, hosted services, shadow traffic, capability routing, or execution paths.
- Default runtime mode remains Legacy with `Enabled=false`, `ShadowEnabled=false`, and no handled capabilities.
- Focused tests verify default legacy mode, option binding, invalid mode failure, and enabled `NextOnly` rejection during the skeleton phase.
