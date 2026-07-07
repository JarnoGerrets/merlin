---
type: implementation-plan-index
status: current
system: Voice
---

# Voice Implementation Plans

| Plan | Source | Status | Related Feature | Use Now? | Notes |
| --- | --- | --- | --- | --- | --- |
| [[Always-On Interruption And Live Utterance Routing Plan]] | `Merlin.ToDo/always-on-interruption-and-live-utterance-routing.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[AskClarification Live Dead-End Recovery Plan]] | `Merlin.ToDo/askclarification_implementation/merlin_askclarification_dead_end_fix.md` | implemented | [[Voice Interruption System]] | no | PR 10.4a-e implemented. |
| [[PLAN-2026-07-07-009 AskClarification Stale Handling Watchdog Plan]] | derived from [[RUN-2026-07-07-008 AskClarification PR10.4c Awaiting State Timeout]] | implemented | [[Voice Interruption System]] | no | PR10.4d implemented in [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]]. |
| [[PLAN-2026-07-07-010 AskClarification Full Clarification Recomposition Ownership Plan]] | derived from [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]] | implemented | [[Voice Interruption System]] | no | PR10.4e implemented in [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]]. |
| [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]] | derived from [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]] | ready | [[Voice Interruption System]] | yes | Scoped bugfix for four known BargeIn idle-capture failures. |
| [[Conversational Interruption Redesign V2 Plan]] | `Merlin.ToDo/interruption_intelligence/merlin_conversational_interruption_redesign_v2.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Responsive Feedback Migration V2 Plan]] | `Merlin.ToDo/interruption_intelligence/merlin_responsive_feedback_migration_plan_v2.md` | current | [[Voice Interruption System]], [[Responsive Feedback]] | yes | Verify code before executing. |
| [[Conversational Interruption Redesign Original Plan]] | `Merlin.ToDo/interruption_intelligence/originals/merlin_conversational_interruption_redesign.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Responsive Feedback Migration Original Plan]] | `Merlin.ToDo/interruption_intelligence/originals/merlin_responsive_feedback_migration_plan.md` | current | [[Voice Interruption System]], [[Responsive Feedback]] | yes | Verify code before executing. |
| [[Echo Aware Self Speech Suppression Plan]] | `Merlin.ToDo/Merlin_Echo_Aware_Self_Speech_Suppression_Implementation.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Fast Near-End Ducking Path Plan]] | `Merlin.ToDo/Merlin_Fast_Near_End_Ducking_Path_Implementation.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Instant Ducking And Natural Hard Stop Plan]] | `Merlin.ToDo/Merlin_Instant_Ducking_And_Natural_Hard_Stop_Implementation.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Playback Clock Aligned Reference Tap Plan]] | `Merlin.ToDo/Merlin_Playback_Clock_Aligned_Reference_Tap_For_Correlation.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Playback Mic Correlation Self Echo Suppression Plan]] | `Merlin.ToDo/Merlin_Playback_Mic_Correlation_Self_Echo_Suppression.md` | current | [[Voice Interruption System]] | yes | Verify code before executing. |
| [[Voice Correction Learning Plan]] | `Merlin.ToDo/VoiceCorrectionLearning.md` | current | [[Voice Interruption System]], [[Correction Layer]], [[Control Profile DB]] | yes | Verify code before executing. |

## Lifecycle Rules

Use [[Implementation Plan Lifecycle]]. Link curated plans by their human-readable note names and prefer prompt bundles first.
