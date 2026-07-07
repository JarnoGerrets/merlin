---
type: implementation-plan
plan_id: PLAN-2026-07-07-011
derived_work_id:
status: implemented
task_type: refactor
derived_work_type: refactor
origin_run:
origin_task: User requested full documentation for a major Merlin modular runtime refactor and feature-owned settings migration.
origin_evidence: Current backend architecture, current vault conventions, uploaded appsettings files, and discussion of Host/Kernel/Modules/Adapters target structure.
related_features:
  - Modular Runtime Refactor
  - Feature-Owned Settings
affected_systems:
  - backend
  - configuration
  - settings
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
risk_level: medium
ready_for_agent: false
created_prompt: PROMPT-2026-07-07-011
implemented_by: RUN-2026-07-07-012
superseded_by:
---

# PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan

## Plan Status

Status: implemented
Ready for agent use: false
Reason: Implemented in [[RUN-2026-07-07-012 Feature-Owned Settings Migration]].
Related feature: Feature-Owned Settings
Related architecture:
- [[Feature-Owned Settings Architecture]]
- [[ADR-0008 Feature-Owned Settings Files]]

## Goal

Split the global settings mountain into feature-owned JSON files while preserving existing configuration section names and runtime behavior.

This prepares the codebase for future `Merlin.Host`, `Merlin.Kernel`, `Merlin.Modules.*`, and `Merlin.Adapters.*` extraction.

## Scope

1. Add `Merlin.Backend/Settings/` folder structure.
2. Add a configuration loading extension such as `AddMerlinSettings`.
3. Move feature sections from root settings files into dedicated feature files.
4. Keep section names unchanged for compatibility.
5. Add `Settings/README.md`.
6. Keep environment-specific overrides small.
7. Add focused tests or configuration validation where safe.
8. Do not change runtime behavior.

## Non-Goals

1. Do not rename option properties.
2. Do not migrate trusted registries to DB.
3. Do not change default values.
4. Do not implement module runtime.
5. Do not delete legacy option classes.
6. Do not alter voice/browser behavior.
7. Do not add strict validation that breaks existing dev config without clear evidence.

## Proposed File Moves

### Kernel / Runtime

| Current Section | New File |
| --- | --- |
| `CapabilityDomains` | `Settings/Kernel/capability-domains.settings.json` |

Long term this section should become module-registered descriptors, but this plan only moves it.

### Memory

| Current Section | New File |
| --- | --- |
| `MerlinDatabase` | `Settings/Modules/Memory/memory.settings.json` |
| `CoreMemory` | `Settings/Modules/Memory/core-memory.settings.json` |
| `TrustedRegistry` | `Settings/Modules/Memory/trusted-registry.settings.json` or `Settings/Modules/Apps/trusted-registry.settings.json` after owner decision |

Owner decision:
- If trusted registry primarily governs app/command trust, put it under Apps.
- If it is implemented through memory/database infrastructure, keep a note that the persistence layer is Memory but policy owner is Apps/Kernel safety.

### Model / Conversation / Adapters

| Current Section | New File |
| --- | --- |
| `LocalAI` | `Settings/Adapters/Ollama/ollama.settings.json` |
| `Llm` | `Settings/Adapters/DeepInfra/deepinfra.settings.json` |
| `StreamingResponses` | `Settings/Modules/Conversation/streaming-responses.settings.json` |
| `AcknowledgementSpeech` | `Settings/Modules/Conversation/acknowledgement-speech.settings.json` |
| `ResponsiveFeedback` | `Settings/Modules/Conversation/responsive-feedback.settings.json` |

### Browser

| Current Section | New File |
| --- | --- |
| `BrowserWorkspace` | `Settings/Modules/Browser/browser-workspace.settings.json` |
| `WebDestinations` | `Settings/Modules/Browser/web-destinations.settings.json` |

### Web

| Current Section | New File |
| --- | --- |
| `WebSearch` | `Settings/Modules/Web/web-search.settings.json` |

### Voice

| Current Section | New File |
| --- | --- |
| `VoiceInput` | `Settings/Modules/Voice/voice-input.settings.json` |
| `GpuScheduling` | `Settings/Modules/Voice/gpu-scheduling.settings.json` |
| `SpeechPresence` | `Settings/Modules/Voice/speech-presence.settings.json` |
| `BargeIn` | `Settings/Modules/Voice/barge-in.settings.json` |
| `InterruptionHandling` | `Settings/Modules/Voice/interruption-handling.settings.json` |
| `Voice` | `Settings/Modules/Voice/stt.settings.json` |
| `Tts` | `Settings/Modules/Voice/tts.settings.json` |
| `Piper` | `Settings/Modules/Voice/piper.settings.json` |

Optional split:
- Keep Chatterbox settings inside `Tts` for now.
- Later split Chatterbox into `chatterbox.settings.json` only if option binding supports it.

### UI / Vision / Apps

| Current Section | New File |
| --- | --- |
| `ChatLog` | `Settings/Modules/Conversation/chat-log.settings.json` or `Settings/Adapters/Godot/chat-log.settings.json` after owner decision |
| `Vision` | `Settings/Modules/Vision/vision.settings.json` |
| `ApplicationLaunch` | `Settings/Modules/Apps/application-launch.settings.json` |

## Implementation Phases

### Phase 1 - Discovery / Baseline

ID: PLAN-2026-07-07-011-P1

Goal:
Confirm current config binding behavior and baseline tests.

Steps:
1. Inspect `Program.cs` configuration setup.
2. Identify all `IOptions<T>` / `GetSection(...)` usages.
3. Confirm section names used in code.
4. Record current appsettings sections.
5. Run current backend build before changes.

Validation:
```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
```

Exit criteria:
- section usage list is known;
- no runtime changes yet.

### Phase 2 - Add Settings Loader

ID: PLAN-2026-07-07-011-P2

Goal:
Add a single config extension so `Program.cs` stays clean.

Steps:
1. Add `Merlin.Backend/Configuration/MerlinConfigurationBuilderExtensions.cs` or similar.
2. Add `AddMerlinSettings(IHostEnvironment environment)`.
3. Add helper `AddMerlinSettingsFile(basePath, env)`.
4. Keep existing root appsettings load behavior.
5. Ensure feature files are optional initially.

Exit criteria:
- build passes;
- `Program.cs` only calls one new extension;
- no behavior changes.

### Phase 3 - Move Settings by Feature Group

ID: PLAN-2026-07-07-011-P3

Goal:
Move sections to feature-owned files without changing values.

Order:
1. low-risk: `ApplicationLaunch`, `WebDestinations`, `WebSearch`;
2. medium-risk: `BrowserWorkspace`, `ChatLog`, `Vision`;
3. model/conversation: `LocalAI`, `Llm`, `StreamingResponses`, `AcknowledgementSpeech`, `ResponsiveFeedback`;
4. memory/database: `MerlinDatabase`, `CoreMemory`, `TrustedRegistry`;
5. voice high-risk config: `VoiceInput`, `SpeechPresence`, `BargeIn`, `InterruptionHandling`, `Voice`, `Tts`, `Piper`, `GpuScheduling`;
6. `CapabilityDomains` last because long-term owner changes later.

For each group:
- copy section exactly;
- remove or leave root duplicate only after verifying load order;
- ensure development override only includes changed values.

Exit criteria:
- app starts/builds;
- option binding still finds same sections.

### Phase 4 - Settings README

ID: PLAN-2026-07-07-011-P4

Create `Merlin.Backend/Settings/README.md`.

Required columns:
- setting concern;
- JSON file;
- section name;
- option class;
- owner module;
- environment override file;
- validation status.

### Phase 5 - Tests / Validation

ID: PLAN-2026-07-07-011-P5

Add a focused configuration test if feasible:

- build a test configuration using appsettings + settings files;
- assert required sections exist;
- assert important nested sections bind;
- assert no critical section is missing.

Do not over-test all values.

### Phase 6 - Vault Writeback

ID: PLAN-2026-07-07-011-P6

Update:
- agent run;
- changelog;
- [[Feature-Owned Settings Architecture]];
- [[Modular Runtime Refactor Progress]] if copied;
- code atlas if configuration loader is added;
- current-state note if root settings status changes.

## Go / No-Go Preflight

Go only if:

- current section names can be preserved;
- loader can be added without replacing existing configuration stack;
- settings files can be optional during transition;
- build can validate the move.

No-Go if:

- config binding depends on root-file-only assumptions that cannot be preserved;
- development overrides would silently replace entire nested objects incorrectly;
- current code uses raw file paths to appsettings sections rather than configuration sections;
- the agent cannot determine load order.

Partial-Go allowed:
- Move only low-risk sections first: `ApplicationLaunch`, `WebDestinations`, `WebSearch`.

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
- [[PE-0220 Refactor Task Rules]]

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

Implemented in [[RUN-2026-07-07-012 Feature-Owned Settings Migration]].

Runtime behavior impact:

- Existing section names are preserved.
- Root appsettings files are now host/global only.
- Feature-owned settings files live under `Merlin.Backend/Settings`.
- `Program.cs` calls `UseMerlinConfiguration` to preserve root, feature, environment, user-secret, environment-variable, and command-line load order.
- Focused configuration tests verify required sections load and Development overrides still win.
