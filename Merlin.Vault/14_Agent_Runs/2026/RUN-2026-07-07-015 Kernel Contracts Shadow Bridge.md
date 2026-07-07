---
type: agent-run
run_id: RUN-2026-07-07-015
date: 2026-07-07
run_type: refactor
related_features:
  - Modular Runtime Refactor
  - Kernel Brainstem
  - Strangler Migration
status: completed
branch: main
commit_before: 364357ebd46d2448fa63508069dadbf1a7a3a3ef
commit_after: 364357ebd46d2448fa63508069dadbf1a7a3a3ef
agent: Codex
---

# Agent Run: Kernel Contracts Shadow Bridge

## Task

Implement the full [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]].

## Prompt / Source

User prompt:

- Use `Merlin.Vault/AGENT.md`.
- Task mode: refactor.
- Implement Plan 013, full plan.
- Preserve externally visible behavior.
- Do not implement future phases.
- Do not migrate browser or voice.
- Shadow mode must be read-only and side-effect free.

## Go / No-Go Result

Go.

Evidence:

- [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]] is `ready_for_agent: true`.
- [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] is implemented.
- `Merlin.Backend/Next` exists.
- `MerlinNext` options exist.
- `CommandRouter.RouteAsync(AssistantRequest)` has a safe bridge point after current normalization and active-surface selection.
- The bridge can be optional, config-gated, bounded, exception-safe, and read-only.

## Scope

- Add minimal kernel contracts.
- Add shadow runtime interface and implementation.
- Add legacy request adapter.
- Add optional shadow bridge.
- Hook `CommandRouter` to start the bridge without changing routing results.
- Add focused tests proving read-only/default/exception-safe behavior.
- Update vault documentation.

## Non-Goals

- No capability execution.
- No `CommandRouter` replacement.
- No user-facing response change.
- No real UI events from shadow.
- No pending operation creation.
- No app, browser, memory, or speech side effects.
- No browser or voice migration.
- No C# project split.

## Files Changed

Runtime/config:

- `Merlin.Backend/Next/Kernel/Requests/MerlinRequest.cs`
- `Merlin.Backend/Next/Kernel/Turns/MerlinTurnContext.cs`
- `Merlin.Backend/Next/Kernel/Routing/RouteDecision.cs`
- `Merlin.Backend/Next/Kernel/Routing/RouteDecisionKind.cs`
- `Merlin.Backend/Next/Kernel/Capabilities/CapabilityDescriptor.cs`
- `Merlin.Backend/Next/Kernel/Capabilities/CapabilityRiskLevel.cs`
- `Merlin.Backend/Next/Kernel/Capabilities/CapabilityResult.cs`
- `Merlin.Backend/Next/Kernel/Capabilities/CapabilityResultKind.cs`
- `Merlin.Backend/Next/Kernel/Surfaces/SurfaceSnapshot.cs`
- `Merlin.Backend/Next/Kernel/Safety/SafetyDecision.cs`
- `Merlin.Backend/Next/Kernel/Safety/SafetyDecisionKind.cs`
- `Merlin.Backend/Next/Kernel/Presentation/MerlinResponse.cs`
- `Merlin.Backend/Next/Kernel/Presentation/MerlinResponseKind.cs`
- `Merlin.Backend/Next/Kernel/Events/MerlinEvent.cs`
- `Merlin.Backend/Next/Kernel/Runtime/IMerlinNextRuntime.cs`
- `Merlin.Backend/Next/Kernel/Runtime/MerlinNextShadowTrace.cs`
- `Merlin.Backend/Next/Kernel/Runtime/MerlinNextShadowRuntime.cs`
- `Merlin.Backend/Next/Host/ILegacyMerlinRequestAdapter.cs`
- `Merlin.Backend/Next/Host/LegacyMerlinRequestAdapter.cs`
- `Merlin.Backend/Next/Host/IMerlinNextShadowBridge.cs`
- `Merlin.Backend/Next/Host/MerlinNextShadowBridge.cs`
- `Merlin.Backend/Next/Host/MerlinNextServiceCollectionExtensions.cs`
- `Merlin.Backend/Services/CommandRouter.cs`
- `Merlin.Backend.Tests/MerlinNextShadowBridgeTests.cs`
- `Merlin.Backend.Tests/CommandRouterTests.cs`

Vault:

- [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]]
- [[PROMPT-2026-07-07-013 Implement Kernel Contracts Shadow Bridge]]
- [[Kernel Brainstem Architecture]]
- [[Strangler Migration Architecture]]
- [[MerlinNextShadowBridge]]
- [[MerlinNextRuntimeOptions]]
- [[Modular Runtime Refactor Progress]]
- [[Current Refactor Readiness]]
- [[Current Work Dashboard]]
- [[2026 Change Log]]
- plan, prompt, code atlas, and run indexes

## Behavior Changed

No externally visible behavior changed.

`CommandRouter` now snapshots a normalized request for an optional shadow bridge, but legacy routing remains the sole executor and exceptions from the bridge are caught.

## Runtime Mode Impact

Default runtime remains Legacy.

Shadow trace work can run only when all are true:

- `MerlinNext.Enabled == true`
- `MerlinNext.ShadowEnabled == true`
- `MerlinNext.Mode == Shadow`

Shadow runtime returns/logs `NoDecision` and `disabled_shadow_mode`. It does not execute capabilities.

## Validation

| Command / Check | Result | Notes |
| --- | --- | --- |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter "MerlinNextShadowBridgeTests\|RouteAsync_WhenShadowBridge"` | passed, 5/5 | Focused Plan 013 tests. |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter "MerlinNextRuntimeOptionsTests\|MerlinSettingsConfigurationTests\|MerlinNextShadowBridgeTests\|RouteAsync_WhenShadowBridge"` | passed, 11/11 | Combined Next/settings focused tests. |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` | passed | 0 warnings / 0 errors. |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false` | failed, 1733 passed / 9 failed | Failures are known pre-existing correction and BargeIn failures. |

## Full Test Failure Classification

Known pre-existing/separate failures:

- five `CorrectionRegenerationDispatcherTests` failures tracked by [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]];
- four `BargeInCoordinatorTests` idle-capture failures tracked by [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]].

No MerlinNext shadow bridge failure was observed.

## Bugs Found / Updated

No new bug family found.

Existing known failures remain:

- [[BargeIn Idle Capture Test Failures]]
- [[Correction Regeneration Test Failures]]

## Derived Work Created

None.

## Derived Work Considered

| Finding | Decision |
| --- | --- |
| Shadow bridge is now available for the first controlled vertical slice. | No new derived work. Existing [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]] already covers the next phase. |
| Full backend validation remains red on known BargeIn/correction tests. | No new derived work. Existing Plans 022 and 023 cover those bugfixes. |

## Vault Updates Made

| Vault Note | Update |
| --- | --- |
| [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]] | Marked implemented. |
| [[PLAN-2026-07-07-010 Modular Runtime Refactor Master Plan]] | Updated cutover table for settings, runtime mode config, and shadow trace. |
| [[PROMPT-2026-07-07-013 Implement Kernel Contracts Shadow Bridge]] | Marked implemented. |
| [[Kernel Brainstem Architecture]] | Added implementation status. |
| [[Strangler Migration Architecture]] | Added implementation status. |
| [[MerlinNextShadowBridge]] | Added code atlas note. |
| [[MerlinNextRuntimeOptions]] | Updated for shadow bridge registration. |
| [[Modular Runtime Refactor Progress]] | Marked shadow bridge implemented and next task Plan 014. |
| [[Current Refactor Readiness]] | Marked shadow tracing implemented and first vertical slice ready. |
| [[Current Work Dashboard]] | Updated modular runtime active task and recent completion. |
| [[2026 Change Log]] | Added implementation entry. |

## Remaining Work

- Implement [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]] next.
- Fix known BargeIn idle-capture and correction regeneration failures in their scoped bugfix passes.

## Risks / Follow-Up

- Future vertical-slice work must prevent double execution between legacy and Next.
- Shadow mode must remain read-only until a later plan explicitly approves hybrid execution.
- Browser and voice migrations remain later high-risk phases.
