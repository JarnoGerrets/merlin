---
type: implementation-plan
plan_id: PLAN-2026-07-07-022
derived_work_id: DW-2026-07-07-022
status: ready
ready_for_agent: true
task_type: bugfix
derived_work_type: bugfix
origin_run: RUN-2026-07-07-011
origin_task: AskClarification PR10.4 closure and live-validation readiness review.
origin_evidence: Broad PR10.4 validation continues to fail four pre-existing adjacent BargeIn idle-capture tests.
related_features:
  - Voice Interruption System
affected_systems:
  - backend
  - voice
required_prompt_bundles:
  - PB-0009
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
  - PE-0260
risk_level: high
created_prompt: PROMPT-2026-07-07-022
implemented_by:
superseded_by:
---

# PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan

## Plan Status

Status: ready
Ready for agent use: true
Reason: The failing tests are repeatedly reproduced and classified as separate from AskClarification PR10.4.
Derived work type: bugfix
Origin run: [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]]
Origin evidence: Broad `ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification|BargeIn` validation after PR10.4e passed 200 tests and failed four known BargeIn idle-capture tests.
Related feature: [[Voice Interruption System]]
Related code atlas: [[BargeInCoordinator]]
Related bug notes: [[BargeIn Idle Capture Test Failures]]
Created prompt: [[PROMPT-2026-07-07-022 Fix BargeIn Idle Capture Test Failures]]

## Goal

Fix the four known BargeIn idle-capture test failures without changing AskClarification PR10.4 ownership behavior.

## Why This Exists

The AskClarification PR10.4 closure review found that PR10.4a-e is implementation-complete, but broad voice validation remains red because idle-capture BargeIn tests fail before or outside pending clarification ownership.

Failing tests:

- `BargeInCoordinatorTests.ProcessMicrophoneFrame_BackendIdleVoice_AddsCaptureIdAndTimelineLogs`
- `BargeInCoordinatorTests.ProcessMicrophoneFrame_IdleAecOnlyEnergy_DoesNotExtendCapture`
- `BargeInCoordinatorTests.ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest`
- `BargeInCoordinatorTests.ProcessMicrophoneFrame_IdleRawMicSpeech_KeepsCaptureActive`

## Go / No-Go Preflight

Before runtime changes, rerun the four tests individually to confirm the current baseline.

Go only if:

- failures still reproduce;
- the fix can stay within BargeIn idle capture / endpointing / timeline behavior;
- PR10.4 pending clarification, awaiting state, stale handling watchdog, and recomposition ownership tests can remain intact.

No-Go if:

- failures no longer reproduce;
- root cause points to a broader audio/AEC architecture issue requiring a separate investigation;
- a fix would weaken global stop/cancel, pending clarification, or playback ownership.

## Scope

- Diagnose idle voice capture promotion and endpointing behavior.
- Fix timeline/request emission expectations or production behavior narrowly, depending on code reality.
- Preserve PR10.4 pending clarification response consumption and stale handling watchdog behavior.
- Add or adjust tests only when they reflect intended behavior.

## Non-Goals

- Do not change correction regeneration behavior.
- Do not change AskClarification PR10.4 logic.
- Do not refactor BargeIn broadly.
- Do not loosen tests just to make the suite green.

## Validation Commands

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ProcessMicrophoneFrame_BackendIdleVoice_AddsCaptureIdAndTimelineLogs|ProcessMicrophoneFrame_IdleAecOnlyEnergy_DoesNotExtendCapture|ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest|ProcessMicrophoneFrame_IdleRawMicSpeech_KeepsCaptureActive" -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification|BargeIn" -p:UseSharedCompilation=false
```

## Acceptance Criteria

- The four BargeIn idle-capture tests pass.
- PR10.4 focused tests remain green.
- Any remaining broad-filter failures are classified with evidence.

## Final Agent Report Must Include

- Go/No-Go result
- root cause
- files changed
- behavior changed
- tests run
- PR10.4 regression status
- vault notes updated
