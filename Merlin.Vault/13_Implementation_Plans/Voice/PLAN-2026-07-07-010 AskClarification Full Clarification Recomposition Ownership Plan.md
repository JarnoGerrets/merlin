---
type: implementation-plan
plan_id: PLAN-2026-07-07-010
derived_work_id: DW-2026-07-07-010
status: implemented
ready_for_agent: false
task_type: implementation
derived_work_type: phase
origin_run: RUN-2026-07-07-009
origin_task: PR10.4d stale InterruptionState handling watchdog.
origin_evidence: PR10.4a safe fallback, PR10.4b pending owner, PR10.4c awaiting state/timeout, and PR10.4d stale handling watchdog were implemented before this plan; PR10.4e completed the remaining clarification/recomposition ownership work.
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
  - PE-0260
risk_level: high
created_prompt: PROMPT-2026-07-07-010
implemented_by: RUN-2026-07-07-010
superseded_by:
---

# PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan

## Plan Status

Status: implemented
Ready for agent use: false
Reason: PR10.4e was implemented in [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]].

Related feature: [[Voice Interruption System]]
Related architecture: [[Voice Pipeline Architecture]]
Related code atlas: [[LiveInterruptionIntegrationService]], [[BargeInCoordinator]], [[PendingInterruptionClarificationService]], [[AssistantSpeechPlaybackService]]
Related bug notes: [[AskClarification Live Dead-End]]
Created prompt: [[PROMPT-2026-07-07-010 Implement AskClarification Full Recomposition Ownership]]

## Goal

Implement PR10.4e: full AskClarification clarification/recomposition ownership.

Merlin should be able to ask an unclear-interruption clarification, wait for the user's answer through the pending owner, bind that answer to the interrupted turn, and continue or recompose without legacy cleanup or generic command routing stealing the flow.

## Required Existing Prerequisites

| Prerequisite | Expected Status |
| --- | --- |
| PR10.4a safe terminal fallback | implemented |
| PR10.4b pending unclear-interruption clarification owner | implemented |
| PR10.4c awaiting clarification state and timeout/cancel cleanup | implemented |
| PR10.4d stale handling watchdog | implemented |

No-Go if any prerequisite has regressed.

## Scope

- Bind consumed pending clarification responses into the executable PR10 clarification/recomposition path.
- Ensure interruption-owned clarification and continuation speech suppress legacy cleanup and semantic routing.
- Reuse existing playback, model, and speech output ports.
- Preserve global stop/cancel behavior.
- Preserve pending clarification timeout/cancel cleanup.
- Preserve the PR10.4a terminal fallback when full ownership cannot execute safely.

## Non-Goals

- Do not refactor BargeIn architecture broadly.
- Do not move pending clarification state into playback, LiveUtteranceGate, or responsive feedback.
- Do not bypass `AssistantSpeechPlaybackService` for holds, flushes, stops, or queue state.
- Do not change unrelated correction regeneration behavior.
- Do not use the watchdog as a substitute for real ownership.

## Implementation Notes

The likely ownership path is:

1. `LiveInterruptionIntegrationService` creates the pending owner for an unclear live interruption.
2. `BargeInCoordinator` consumes the next matching response before generic command routing.
3. The consumed response is handed to the live interruption/recomposition owner instead of only raising a generic route event.
4. The owner either generates the clarification/recomposed continuation or fails closed through the existing terminal fallback.
5. Outcomes that queue interruption-owned speech must set `AllowLegacyCleanup=false` and `AllowLegacySemanticRouting=false`.

## Required Tests

- Pending clarification response generates continuation/recomposition when all PR10.4e flags and owners are enabled.
- Pending clarification answer does not route to CommandRouter/backend voice request.
- Legacy cleanup does not cancel interruption-owned clarification/continuation speech.
- Unsupported or failed recomposition falls back terminally and clears state.
- `in the pool` PR10.4a regression still resumes/cleans up when pending ownership is not applicable.
- Stale handling watchdog does not fire during valid recomposition ownership.

## Validation

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore --filter "ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification|BargeIn" -p:UseSharedCompilation=false
```

If the broad filter still fails because of known idle-capture failures, classify them with evidence and run PR10.4e-specific tests separately.

## Acceptance Criteria

- Full AskClarification pending clarification response has an executable owner.
- Merlin can ask, wait, consume the answer, and continue/recompose without falling into generic command routing.
- Timeout/cancel/watchdog behavior remains intact.
- Full PR10.4 can be marked implemented only after this plan passes focused validation.

## Implementation Result

Implemented in [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]].

Result:

- `LiveInterruptionIntegrationService` is the executable owner for consumed pending clarification responses.
- Pending clarification records now retain the original spoken-answer checkpoint context needed to generate a continuation.
- `BargeInCoordinator` consumes pending clarification answers before generic routing and hands them to the live owner.
- Successful pending clarification responses flush/replace the held answer, generate a recomposed continuation, speak interruption-owned continuation output, and clear interruption state.
- Failed or incomplete ownership paths fail closed, clear state, and suppress generic semantic routing.

Validation:

- Build passed.
- PR10.4e-focused tests passed.
- `ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification` passed.
- Broad `ConversationalInterruptionLiveIntegrationTests|PendingInterruptionClarification|BargeIn` passed 200 tests and failed 4 known adjacent idle-capture tests; these are not introduced by PR10.4e.
