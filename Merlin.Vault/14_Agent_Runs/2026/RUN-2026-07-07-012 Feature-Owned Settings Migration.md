---
type: agent-run
run_id: RUN-2026-07-07-012
date: 2026-07-07
run_type: refactor
related_features:
  - Modular Runtime Refactor
  - Feature-Owned Settings
status: completed
branch:
commit_before:
commit_after:
agent: Codex
---

# Agent Run: Feature-Owned Settings Migration

## Task

Implement the full [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]].

## Prompt / Source

User prompt:

- Use `Merlin.Vault/AGENT.md`.
- Task mode: refactor.
- Implement [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]].
- Preserve externally visible behavior.
- Do not do a big-bang rewrite.
- Do not split into separate C# projects.
- Do not delete legacy paths unless the plan approves cutover.

## Go / No-Go Result

Go.

Evidence:

- Runtime code binds options by configuration section names, not by raw root appsettings file paths.
- Existing section names could be preserved.
- A loader could preserve the old effective load order while inserting feature-owned settings files.
- Settings files could be optional and copied to output.
- Baseline backend build passed before runtime changes.

## Scope

- Add feature-owned settings files under `Merlin.Backend/Settings`.
- Reduce root appsettings files to host/global settings.
- Add a single configuration loader extension.
- Preserve section names and Development override behavior.
- Add settings README/index.
- Add focused configuration tests.
- Update vault documentation.

## Non-Goals

- No module runtime implementation.
- No new C# projects.
- No browser/voice module migration.
- No option renames.
- No strict validation that could break development config.

## Files Changed

Runtime/config:

- `Merlin.Backend/Configuration/MerlinConfigurationBuilderExtensions.cs`
- `Merlin.Backend/Program.cs`
- `Merlin.Backend/Merlin.Backend.csproj`
- `Merlin.Backend/appsettings.json`
- `Merlin.Backend/appsettings.Development.json`
- `Merlin.Backend/Settings/**`
- `Merlin.Backend.Tests/MerlinSettingsConfigurationTests.cs`

Vault:

- [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]]
- [[PROMPT-2026-07-07-011 Implement Feature-Owned Settings Migration]]
- [[Feature-Owned Settings Architecture]]
- [[ADR-0008 Feature-Owned Settings Files]]
- [[MerlinConfigurationBuilderExtensions]]
- [[Modular Runtime Refactor Progress]]
- [[Current Work Dashboard]]
- [[2026 Change Log]]
- architecture-refactor and prompt indexes

## Behavior Changed

Externally visible runtime behavior should be unchanged.

Configuration ownership changed:

- root `appsettings.json` contains only `Logging` and `AllowedHosts`;
- root `appsettings.Development.json` contains only `Logging`;
- feature settings now live under `Merlin.Backend/Settings`;
- section names such as `BargeIn`, `Vision`, `Tts`, `ApplicationLaunch`, `CapabilityDomains`, and `WebDestinations` are unchanged;
- Development feature settings preserve previous Development values.

## Runtime Mode Impact

No runtime mode change.

The backend remains in legacy runtime mode. This refactor prepares for future modular runtime work but does not add `Merlin.Next`, module registration, shadow routing, or project splits.

## Validation

| Command / Check | Result | Notes |
| --- | --- | --- |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` before changes | passed | Baseline before runtime changes. |
| Mechanical JSON equivalence check against `HEAD` root appsettings | passed | Reconstructed base and Development documents matched old appsettings files exactly. |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` | passed | 0 warnings / 0 errors. |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "MerlinSettingsConfigurationTests" -p:UseSharedCompilation=false` | passed, 2/2 | Verifies feature-owned settings load and Development overrides win. |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false` | failed, 1724 passed / 9 failed | The 9 failures are the known correction regeneration and BargeIn idle-capture failures. |

## Full Test Failure Classification

Known pre-existing/separate failures:

- five `CorrectionRegenerationDispatcherTests` failures tracked by [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]];
- four `BargeInCoordinatorTests` idle-capture failures tracked by [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]].

No new settings-related test failure was observed.

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
| Development feature override files still preserve full legacy sections in some areas. | No new derived work. This is documented in `Settings/README.md`; shrinking overrides should happen owner-by-owner later if needed. |
| The architecture refactor can now proceed to Plan 012. | No new derived work. Existing [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] already covers it. |

## Vault Updates Made

| Vault Note | Update |
| --- | --- |
| [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]] | Marked implemented. |
| [[PROMPT-2026-07-07-011 Implement Feature-Owned Settings Migration]] | Marked implemented. |
| [[Feature-Owned Settings Architecture]] | Marked current and linked loader. |
| [[ADR-0008 Feature-Owned Settings Files]] | Marked accepted. |
| [[MerlinConfigurationBuilderExtensions]] | Added code atlas note. |
| [[Modular Runtime Refactor Progress]] | Marked feature-owned settings implemented and next task Plan 012. |
| [[Current Work Dashboard]] | Added modular runtime refactor active row and completion entry. |
| [[2026 Change Log]] | Added implementation entry. |

## Remaining Work

- Implement [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] when ready.
- Fix known BargeIn idle-capture and correction regeneration failures in their separate scoped bugfix passes.
- Optionally shrink Development feature override files after each owner confirms exact intended override surface.

## Risks / Follow-Up

- More files means load order matters; this is mitigated by `UseMerlinConfiguration`, `Settings/README.md`, and focused tests.
- Any future settings move must preserve section names unless a separate plan explicitly approves renaming.
