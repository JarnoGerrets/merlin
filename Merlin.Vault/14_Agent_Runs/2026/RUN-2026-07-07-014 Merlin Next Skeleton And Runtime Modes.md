---
type: agent-run
run_id: RUN-2026-07-07-014
date: 2026-07-07
run_type: refactor
related_features:
  - Modular Runtime Refactor
  - Strangler Migration
status: completed
branch: main
commit_before: 5314f441ea1a3b2b0d9928cab589cef57372635d
commit_after: 5314f441ea1a3b2b0d9928cab589cef57372635d
agent: Codex
---

# Agent Run: Merlin Next Skeleton And Runtime Modes

## Task

Implement the full [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]].

## Prompt / Source

User prompt:

- Use `Merlin.Vault/AGENT.md`.
- Task mode: refactor.
- Implement Plan 012, full plan.
- Preserve externally visible behavior.
- Do not implement future phases.
- Do not migrate browser or voice.
- Runtime behavior must remain legacy by default.

## Go / No-Go Result

Go.

Evidence:

- [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] is `ready_for_agent: true`.
- [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]] is implemented.
- `Merlin.Backend/Settings` and `UseMerlinConfiguration` exist.
- `Merlin.Backend/Next` did not exist before this run.
- The requested scope can be implemented without changing `CommandRouter`, WebSocket routing, browser, voice, safety, confirmation, cancellation, or interruption behavior.
- Tests can prove options bind and defaults remain inert.

## Scope

- Add `Merlin.Backend/Next` Host/Kernel/Modules/Adapters skeleton.
- Add `MerlinNext` runtime mode options and enum.
- Add `AddMerlinNext` service registration extension.
- Register Next options from `Program.cs`.
- Add feature-owned `Settings/Kernel/merlin-next.settings.json`.
- Add focused tests for default mode, binding, invalid mode, and enabled `NextOnly` rejection.
- Update vault documentation.

## Non-Goals

- No request execution through Next.
- No shadow traffic.
- No capability routing.
- No `CommandRouter` behavior change.
- No browser or voice migration.
- No C# project split.

## Files Changed

Runtime/config:

- `Merlin.Backend/Program.cs`
- `Merlin.Backend/Configuration/MerlinConfigurationBuilderExtensions.cs`
- `Merlin.Backend/Next/Host/MerlinNextRuntimeMode.cs`
- `Merlin.Backend/Next/Host/MerlinNextRuntimeOptions.cs`
- `Merlin.Backend/Next/Host/MerlinNextServiceCollectionExtensions.cs`
- `Merlin.Backend/Next/Kernel/README.md`
- `Merlin.Backend/Next/Modules/README.md`
- `Merlin.Backend/Next/Adapters/README.md`
- `Merlin.Backend/Settings/Kernel/merlin-next.settings.json`
- `Merlin.Backend/Settings/README.md`
- `Merlin.Backend.Tests/MerlinNextRuntimeOptionsTests.cs`
- `Merlin.Backend.Tests/MerlinSettingsConfigurationTests.cs`

Vault:

- [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]]
- [[PROMPT-2026-07-07-012 Implement Merlin Next Skeleton And Runtime Modes]]
- [[Modular Runtime Architecture]]
- [[Strangler Migration Architecture]]
- [[MerlinNextRuntimeOptions]]
- [[Modular Runtime Refactor Progress]]
- [[Current Refactor Readiness]]
- [[Current Work Dashboard]]
- [[2026 Change Log]]
- implementation plan, prompt, code atlas, and run indexes

## Behavior Changed

No externally visible behavior changed.

Configuration now includes a `MerlinNext` section, but default values keep Next disabled and Legacy mode active.

## Runtime Mode Impact

Legacy remains the only executing runtime.

Added defaults:

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

`AddMerlinNext` registers options and validation only. It does not register handlers, hosted services, shadow bridge, hybrid routing, or any side-effectful path.

## Validation

| Command / Check | Result | Notes |
| --- | --- | --- |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` before changes | passed | Baseline build; one pre-existing `FloorYieldController` warning appeared before edits. |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter "MerlinNextRuntimeOptionsTests\|MerlinSettingsConfigurationTests"` | passed, 6/6 | Focused Plan 012 and settings tests. |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` | passed | 0 warnings / 0 errors. |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false` | failed, 1728 passed / 9 failed | Failures are known pre-existing correction and BargeIn failures. |

Validation note:

- An early parallel build/test attempt caused a transient compiler file-lock error while both commands targeted the same backend output. Sequential reruns passed the backend build and focused tests.

## Full Test Failure Classification

Known pre-existing/separate failures:

- five `CorrectionRegenerationDispatcherTests` failures tracked by [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]];
- four `BargeInCoordinatorTests` idle-capture failures tracked by [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]].

No MerlinNext-related failure was observed.

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
| Next skeleton is now available for shadow bridge work. | No new derived work. Existing [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]] already covers the next phase. |
| Full backend validation remains red on known BargeIn/correction tests. | No new derived work. Existing Plans 022 and 023 cover those bugfixes. |

## Vault Updates Made

| Vault Note | Update |
| --- | --- |
| [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] | Marked implemented. |
| [[PROMPT-2026-07-07-012 Implement Merlin Next Skeleton And Runtime Modes]] | Marked implemented. |
| [[Modular Runtime Architecture]] | Added implementation status. |
| [[Strangler Migration Architecture]] | Added implementation status. |
| [[MerlinNextRuntimeOptions]] | Added code atlas note. |
| [[Modular Runtime Refactor Progress]] | Marked Next skeleton implemented and next task Plan 013. |
| [[Current Refactor Readiness]] | Marked runtime skeleton implemented and shadow tracing ready. |
| [[Current Work Dashboard]] | Updated modular runtime active task and recent completion. |
| [[2026 Change Log]] | Added implementation entry. |

## Remaining Work

- Implement [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]] next.
- Fix known BargeIn idle-capture and correction regeneration failures in their scoped bugfix passes.

## Risks / Follow-Up

- Future shadow work must stay read-only and must not mutate voice/browser/memory/safety state.
- Future hybrid work must prevent double execution per capability.
- Enabled `NextOnly` is intentionally rejected during this skeleton phase.
