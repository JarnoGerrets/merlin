---
type: implementation-plan
plan_id: PLAN-2026-07-07-009
derived_work_id: DW-2026-07-07-009
status: implemented
ready_for_agent: false
task_type: implementation
derived_work_type: prerequisite
origin_run: RUN-2026-07-07-008
origin_task: PR10.4c awaiting clarification state and timeout recovery.
origin_evidence: PR10.4c implemented awaiting clarification state and pending timeout cleanup; full PR10.4 remains blocked by missing stale `InterruptionState=handling` watchdog and recomposition ownership.
related_features:
  - Voice Interruption System
affected_systems:
  - backend
  - voice
required_prompt_bundles:
  - PB-0005
required_prompt_extensions:
  - PE-0001
  - PE-0002
  - PE-0003
  - PE-0004
  - PE-0005
  - PE-0007
  - PE-0008
  - PE-0150
  - PE-0210
  - PE-0260
risk_level: high
created_prompt: PROMPT-2026-07-07-009
implemented_by: RUN-2026-07-07-009
superseded_by:
---

# PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan

## Plan Status

Status: implemented
Ready for agent use: false
Reason: PR10.4d is implemented. PR10.4e was later implemented in [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]].
Derived work type: prerequisite
Origin run: [[RUN-2026-07-07-008 AskClarification PR10.4c Awaiting State Timeout]]
Origin evidence: Full PR10.4 remains blocked because ownerless stale `handling` can still outlive actual interruption work if no active capture, hold, pending clarification, or interruption-owned speech remains.
Related feature: [[Voice Interruption System]]
Related architecture: [[Voice Pipeline Architecture]]
Related code atlas: [[BargeInCoordinator]], [[PendingInterruptionClarificationService]], [[LiveInterruptionIntegrationService]], [[AssistantSpeechPlaybackService]]
Related bug notes: [[AskClarification Live Dead-End]]
Created prompt: [[PROMPT-2026-07-07-009 Implement AskClarification Stale Handling Watchdog]]
Implemented by: [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]]

## Goal

Implement PR10.4d: an owner-aware stale `InterruptionState=handling` watchdog that clears only ownerless stale handling state without cancelling valid in-progress interruption work.

## Why This Exists

PR10.4c made the pending clarification waiting state explicit and recoverable, but the broader `handling` state can still become stale if the first interruption-handling owner disappears. Full PR10.4 recomposition should not proceed until the state machine has a narrow recovery path for ownerless `handling`.

## Go / No-Go Preflight

Before runtime changes, verify a safe existing seam exists around BargeIn/live interruption handling.

Go only if:
- `BargeInCoordinator` or a small service it owns can observe handling start/end;
- pending clarification state can be queried through `IPendingInterruptionClarificationService`;
- active hold/playback or interruption-owned speech can be queried or conservatively treated as active;
- recovery can emit UI/backend state without cancelling playback or queues;
- tests can prove the watchdog does not clear active owned work.

No-Go if:
- implementation would require broad BargeIn architecture refactoring;
- recovery would need to manipulate playback internals;
- active ownership cannot be checked safely;
- tests cannot distinguish ownerless stale handling from active interruption work.

## Scope

- Add configurable stale handling watchdog for live interruption handling.
- Clear `InterruptionState=handling` to `none` only when no active owner remains.
- Log watchdog observations and recovery decisions.
- Preserve existing pending clarification, hold, playback, and speech ownership boundaries.
- Add focused BargeIn/live interruption tests.

## Non-Goals

- Do not implement full PR10.4 recomposition.
- Do not change correction regeneration behavior.
- Do not refactor BargeIn broadly.
- Do not cancel playback, holds, queues, or model calls from the watchdog unless a specific owner reports stale ownership.
- Do not hide introduced test failures behind the watchdog.

## Dependencies

| Dependency | Status | Evidence |
| --- | --- | --- |
| Pending clarification owner | present | [[PendingInterruptionClarificationService]] |
| Awaiting clarification state and timeout recovery | present | [[RUN-2026-07-07-008 AskClarification PR10.4c Awaiting State Timeout]] |
| Assistant UI state broadcaster | present | `AssistantUiStateBroadcaster` |
| BargeIn handling start/end seam | must verify | `BargeInCoordinator` owns `_handlingTrigger` and capture sessions. |

## Affected Systems

- Voice interruption system
- Barge-in capture/routing
- Assistant UI/backend state events
- Pending clarification owner

## Owning Components

| Component / File | Expected Role | Must Verify? |
| --- | --- | --- |
| `BargeInCoordinator.cs` | Own watchdog integration and handling lifecycle observation. | yes |
| `PendingInterruptionClarificationService.cs` | Expose active pending state; must not own BargeIn session internals. | yes |
| `AssistantUiStateBroadcaster.cs` | Emit recovery state. | yes |
| `AssistantSpeechPlaybackService.cs` | Playback/hold owner; watchdog must not bypass it. | yes |

## Phases

### Phase 1 - Discovery / Verification

ID: PLAN-2026-07-07-009-P1

Goal: verify active-owner checks and identify exact watchdog seam.

Steps:
- Inspect BargeIn handling lifecycle.
- Inspect active capture/session/hold/pending/interruption speech observability.
- Decide whether watchdog belongs directly in `BargeInCoordinator` or a small owned helper.

Validation:
- No runtime changes until Go/No-Go is clear.

Exit criteria:
- A safe owner-aware watchdog path is identified.

### Phase 2 - Implementation

ID: PLAN-2026-07-07-009-P2

Goal: implement configurable owner-aware recovery.

Steps:
- Add config for enabling watchdog and timeout/poll durations.
- Track handling start time/reason.
- Emit `interruptionState=none` only when no owner remains beyond timeout.
- Log recovery and non-recovery decisions.

Validation:
- Focused BargeIn/live tests pass.

Exit criteria:
- Ownerless stale handling clears; active owned work remains untouched.

### Phase 3 - Tests / Hardening

ID: PLAN-2026-07-07-009-P3

Goal: prove watchdog behavior is safe.

Required tests:
- handling watchdog clears ownerless handling state;
- watchdog does not clear while pending clarification exists;
- watchdog does not clear while provisional hold is active;
- watchdog does not clear while interruption-owned speech is active or conservatively unknown;
- watchdog is disabled by default or follows existing config safety convention.

Validation:
```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "BargeIn|ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification" -p:UseSharedCompilation=false
```

Exit criteria:
- PR10.4d-specific tests pass.
- Known unrelated failures are classified, not hidden.

### Phase 4 - Vault Writeback

ID: PLAN-2026-07-07-009-P4

Goal: update vault status and next PR10.4 work.

Required updates:
- agent run
- changelog
- progress report
- [[Voice Interruption System]]
- [[AskClarification Live Dead-End Recovery Plan]]
- [[AskClarification Live Dead-End]]
- affected code atlas notes

Exit criteria:
- PR10.4d status is accurately recorded.
- PR10.4e derived work is created if no new blocker appears.

## Final Agent Report Must Include

- Go/No-Go result
- files changed
- behavior changed
- tests run
- failed tests classification
- vault notes updated
- whether PR10.4d is complete
- whether full PR10.4 remains blocked
- derived work created, if any
- remaining work

## Implementation Result

Implemented in [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]].

Runtime changes:

- Added `BargeInOptions.EnableInterruptionHandlingWatchdog`.
- Added `BargeInOptions.InterruptionHandlingWatchdogTimeoutMs`.
- `BargeInCoordinator` now observes emitted `interruptionState=handling` state and schedules an owner-aware watchdog.
- The watchdog emits `interruptionState=none` with reason `stale_interruption_handling_watchdog_recovered` only when no active capture, pending clarification, held playback, or interruption-owned speech remains.
- The watchdog defers when an owner is still active.

Focused validation:

- `dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false` passed.
- `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "InterruptionHandlingWatchdog" -p:UseSharedCompilation=false` passed, 4 watchdog tests.
- `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "InterruptionHandlingWatchdog|BargeInOptions_DefaultProviderIsWebRtcApm" -p:UseSharedCompilation=false` passed, 5 tests including default config coverage.
- `dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification|BargeIn" -p:UseSharedCompilation=false` latest run passed 195 tests and failed 5 BargeIn tests; 4 are known idle-capture failures and `BurstPromotion_AllowsMixedAllowUncertainFrames` passed individually afterward.

Remaining work:

- PR10.4e full clarification/recomposition ownership was later implemented in [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]].
