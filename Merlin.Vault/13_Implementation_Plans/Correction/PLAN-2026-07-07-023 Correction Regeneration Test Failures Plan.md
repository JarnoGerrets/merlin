---
type: implementation-plan
plan_id: PLAN-2026-07-07-023
derived_work_id: DW-2026-07-07-023
status: ready
ready_for_agent: true
task_type: bugfix
derived_work_type: bugfix
origin_run: RUN-2026-07-07-011
origin_task: AskClarification PR10.4 closure and live-validation readiness review.
origin_evidence: Full backend validation has known pre-existing CorrectionRegenerationDispatcherTests failures separate from AskClarification PR10.4.
related_features:
  - Correction Layer
  - Voice Interruption System
affected_systems:
  - backend
  - voice
  - correction
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
  - PE-0100
  - PE-0150
  - PE-0260
risk_level: high
created_prompt: PROMPT-2026-07-07-023
implemented_by:
superseded_by:
---

# PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan

## Plan Status

Status: ready
Ready for agent use: true
Reason: Correction regeneration failures are known, repeatedly classified as separate from AskClarification PR10.4, and need a scoped bugfix pass.
Derived work type: bugfix
Origin run: [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]]
Related feature: [[Correction Layer]], [[Voice Interruption System]]
Related code atlas: [[Correction Flow]], [[CorrectionRequestBuilder]]
Related bug notes: [[Correction Regeneration Test Failures]]
Created prompt: [[PROMPT-2026-07-07-023 Fix Correction Regeneration Test Failures]]

## Goal

Fix the known correction regeneration test failures without changing AskClarification PR10.4 pending clarification ownership.

## Why This Exists

AskClarification PR10.4 closure found that correction regeneration failures remain in the broader backend suite but are unrelated to the PR10.4 pending clarification work.

Known failing tests from prior classification:

- `CorrectionRegenerationDispatcherTests.Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest` row `i mean family car`
- `CorrectionRegenerationDispatcherTests.Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest` row `i mean what is the purpose of a voice`
- `CorrectionRegenerationDispatcherTests.OldCancelledCorrelationIdCannotSuppressNewCorrectionCorrelationId`
- `CorrectionRegenerationDispatcherTests.Correction_NewCorrectedResponseCanEnqueueSpeech`
- `CorrectionRegenerationDispatcherTests.Correction_CancelsOldTurnAndDispatchesNewRequest_WithNewCorrelationId`

## Go / No-Go Preflight

Before runtime changes, rerun the known failing correction tests and inspect current correction ownership.

Go only if:

- failures reproduce or current equivalent failures are identified;
- root cause is in correction regeneration dispatch/cancellation/correlation ownership;
- fix can preserve live interruption, playback cancellation, and AskClarification pending owner behavior.

No-Go if:

- failure requires a broader correction architecture redesign;
- tests contradict current intended behavior and need a separate requirements decision;
- fix would alter BargeIn idle-capture or PR10.4 behavior.

## Scope

- Diagnose correction regeneration cancellation/correlation ownership.
- Fix the failing correction tests narrowly.
- Update correction code atlas/progress/bug notes.
- Preserve live interruption PR10.4 tests.

## Non-Goals

- Do not modify AskClarification PR10.4 pending clarification ownership.
- Do not change BargeIn idle-capture behavior.
- Do not implement correction learning or control profile learning.
- Do not rewrite the correction pipeline unless a No-Go follow-up plan is created first.

## Validation Commands

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "CorrectionRegenerationDispatcherTests" -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification" -p:UseSharedCompilation=false
```

## Acceptance Criteria

- Known correction regeneration failures pass.
- PR10.4 focused tests remain green.
- Any remaining backend failures are classified with evidence.

## Final Agent Report Must Include

- Go/No-Go result
- root cause
- files changed
- behavior changed
- tests run
- PR10.4 regression status
- vault notes updated
