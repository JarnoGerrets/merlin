---
type: agent-run
run_id: RUN-2026-07-07-009
date: 2026-07-07
run_type: implementation
related_features:
  - Voice Interruption System
status: completed
branch:
commit_before: 5314f441
commit_after:
agent: Codex
---

# RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog

## Task

Implement PR10.4d only: stale `InterruptionState=handling` watchdog.

## Prompt / Source

- User prompt: implement [[PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan]] using [[AGENT]].
- Execution prompt: [[PROMPT-2026-07-07-009 Implement AskClarification Stale Handling Watchdog]].
- Required preflight source: [[RUN-2026-07-07-008 AskClarification PR10.4c Awaiting State Timeout]].

## Go / No-Go Result

Go.

Evidence:

- `BargeInCoordinator` owns handling/capture lifecycle and emits `AssistantUiStateEvent`.
- `IPendingInterruptionClarificationService.HasActivePendingForTurn` exists for pending owner checks.
- `IAssistantSpeechPlaybackService.GetActivePlaybackSnapshot` exposes active/held/interruption speech ownership.
- `AssistantUiStateBroadcaster` can emit recovery state without manipulating playback internals.
- Focused tests can exercise the watchdog through an internal diagnostics emission seam without relying on known red idle-capture tests.

## Baseline BargeIn Failure Classification

Before PR10.4d runtime changes, the four BargeIn failures from RUN-2026-07-07-008 were rerun individually and reproduced.

| Test | Baseline Result | Classification | Evidence |
| --- | --- | --- | --- |
| `ProcessMicrophoneFrame_BackendIdleVoice_AddsCaptureIdAndTimelineLogs` | failed | pre-existing adjacent failure | Empty backend voice request collection before watchdog work; idle-capture/timeline path, not stale handling recovery. |
| `ProcessMicrophoneFrame_IdleAecOnlyEnergy_DoesNotExtendCapture` | failed | pre-existing adjacent failure | Expected `sustained_silence`, actual null before watchdog work; idle AEC endpointing path. |
| `ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest` | failed | pre-existing adjacent failure | Empty backend voice request collection before watchdog work; backend idle voice capture path. |
| `ProcessMicrophoneFrame_IdleRawMicSpeech_KeepsCaptureActive` | failed | pre-existing adjacent failure | Capture active assertion failed before watchdog work; idle raw mic capture path. |

These are adjacent to BargeIn, but they do not block PR10.4d because the watchdog is scoped to emitted `interruptionState=handling` recovery and does not change idle capture promotion, endpointing, or backend idle voice request routing.

## Scope

Implemented:

- Configurable BargeIn stale handling watchdog.
- Owner-aware recovery from stale `handling` to `none`.
- Focused tests for cleanup and non-cleanup.
- Vault writeback and next derived PR10.4e work item.

Non-goals preserved:

- No PR10.4e recomposition ownership.
- No broad BargeIn refactor.
- No playback internal manipulation.
- No changes to unrelated correction regeneration behavior.
- No attempt to hide known idle-capture failures.

## Files Changed

Runtime/tests:

- `Merlin.Backend/Configuration/BargeInOptions.cs`
- `Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs`
- `Merlin.Backend.Tests/BargeInTests.cs`

Vault:

- `Merlin.Vault/08_Implementation_Prompts/Index.md`
- `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-009 Implement AskClarification Stale Handling Watchdog.md`
- `Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-010 Implement AskClarification Full Recomposition Ownership.md`
- `Merlin.Vault/09_Bugs/AskClarification Live Dead-End.md`
- `Merlin.Vault/13_Implementation_Plans/Derived Work Index.md`
- `Merlin.Vault/13_Implementation_Plans/Voice/AskClarification Live Dead-End Recovery Plan.md`
- `Merlin.Vault/13_Implementation_Plans/Voice/Index.md`
- `Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan.md`
- `Merlin.Vault/13_Implementation_Plans/Voice/PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan.md`
- `Merlin.Vault/01_Project/Current Work Dashboard.md`
- `Merlin.Vault/03_Features/Voice Interruption System.md`
- `Merlin.Vault/11_Code_Atlas/Backend/BargeInCoordinator.md`
- `Merlin.Vault/11_Code_Atlas/Backend/LiveInterruptionIntegrationService.md`
- `Merlin.Vault/11_Code_Atlas/Backend/PendingInterruptionClarificationService.md`
- `Merlin.Vault/11_Code_Atlas/Flows/Live Utterance Flow.md`
- `Merlin.Vault/15_Progress_Reports/Voice Pipeline Progress.md`
- `Merlin.Vault/16_Change_Log/2026/2026 Change Log.md`
- `Merlin.Vault/14_Agent_Runs/2026/RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog.md`

## Behavior Changed

- `BargeInOptions.EnableInterruptionHandlingWatchdog` controls the stale handling watchdog.
- `BargeInOptions.InterruptionHandlingWatchdogTimeoutMs` controls the recovery threshold.
- `BargeInCoordinator` now observes emitted `interruptionState=handling`.
- If no owner remains after the threshold, BargeIn emits:

```text
BaseState: idle
InterruptionState: none
Reason: stale_interruption_handling_watchdog_recovered
```

- Cleanup is deferred while any of these owners are active:
  - active BargeIn capture/session,
  - pending interruption clarification,
  - held playback,
  - interruption clarification/continuation speech.

## Tests / Validation

| Command | Result |
| --- | --- |
| `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` | passed; existing `FloorYieldController._yieldedCurrentPlayback` warning remains |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "InterruptionHandlingWatchdog" -p:UseSharedCompilation=false` | passed, 4/4 |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "InterruptionHandlingWatchdog\|BargeInOptions_DefaultProviderIsWebRtcApm" -p:UseSharedCompilation=false` | passed, 5/5 |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests\|PendingInterruptionClarification\|BargeIn" -p:UseSharedCompilation=false` | failed, latest run 195 passed / 5 failed |
| `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "FullyQualifiedName~BargeInCoordinatorTests.BurstPromotion_AllowsMixedAllowUncertainFrames" -p:UseSharedCompilation=false` | passed individually |

Broad-filter failures:

| Test | Classification |
| --- | --- |
| `ProcessMicrophoneFrame_BackendIdleVoice_AddsCaptureIdAndTimelineLogs` | pre-existing adjacent idle-capture failure |
| `ProcessMicrophoneFrame_IdleAecOnlyEnergy_DoesNotExtendCapture` | pre-existing adjacent idle-capture failure |
| `ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest` | pre-existing adjacent idle-capture failure |
| `ProcessMicrophoneFrame_IdleRawMicSpeech_KeepsCaptureActive` | pre-existing adjacent idle-capture failure |
| `BurstPromotion_AllowsMixedAllowUncertainFrames` | adjacent transient/flaky broad-run failure; passed individually after the broad run |

## Vault Updates

- Marked [[PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan]] implemented.
- Marked [[PROMPT-2026-07-07-009 Implement AskClarification Stale Handling Watchdog]] implemented.
- Updated [[AskClarification Live Dead-End Recovery Plan]] with PR10.4d implementation status.
- Updated [[AskClarification Live Dead-End]] to show watchdog prerequisite complete and PR10.4e remaining.
- Updated [[Voice Interruption System]] and relevant code atlas notes.
- Updated [[Voice Pipeline Progress]], [[Current Work Dashboard]], and [[2026 Change Log]].
- Created PR10.4e derived work:
  - [[PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan]]
  - [[PROMPT-2026-07-07-010 Implement AskClarification Full Recomposition Ownership]]

## Remaining Work

- PR10.4e full clarification/recomposition ownership remains.
- Known BargeIn idle-capture failures remain and should be fixed separately.
- Known correction regeneration failures remain from earlier runs and should be fixed separately.

## Risks

- The watchdog is deliberately conservative. It only clears ownerless `handling`; it does not cancel playback, model calls, queues, or holds.
- If a future valid owner does not expose observable state, the watchdog could clear after timeout. PR10.4e should keep ownership visible through pending clarification, capture, playback, or a future explicit owner signal.

## Derived Work

Created:

- [[PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan]]
- [[PROMPT-2026-07-07-010 Implement AskClarification Full Recomposition Ownership]]

No new unexpected blocker was discovered.
