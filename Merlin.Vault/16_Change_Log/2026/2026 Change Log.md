---
type: changelog
status: current
year: 2026
---

# 2026 Change Log

## 2026-07-07

- Reviewed Modular Runtime Refactor Master Plan readiness:
  - confirmed the master plan is governance-only and not directly executable
  - kept runtime implementation No-Go for the master plan itself
  - confirmed Plan 011 is implemented and Plan 012 is the next executable child plan
  - backend build passed; focused settings tests passed; full backend tests still have unrelated/pre-existing failures
  - runtime code changed: no
  - related run: [[RUN-2026-07-07-013 Modular Runtime Master Plan Review]]

- Implemented Feature-Owned Settings Migration:
  - added `Merlin.Backend/Settings` feature-owned JSON files
  - reduced root appsettings files to host/global settings
  - added `UseMerlinConfiguration` / `AddMerlinSettings` loader
  - preserved existing section names and Development override behavior
  - added `Merlin.Backend/Settings/README.md`
  - added focused configuration tests
  - related run: [[RUN-2026-07-07-012 Feature-Owned Settings Migration]]

- Completed AskClarification PR10.4 closure and live-validation readiness review:
  - confirmed PR10.4a-e is implementation-complete
  - kept AskClarification live dead-end status fixed but pending manual live validation
  - created [[AskClarification PR10.4 Live UX Validation Checklist]]
  - created separate derived bugfix work for BargeIn idle-capture failures and correction regeneration failures
  - runtime code changed: no
  - related run: [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]]

- Implemented AskClarification PR10.4e full clarification/recomposition ownership:
  - pending clarification records now retain spoken-answer checkpoint context
  - consumed pending clarification responses are delegated from BargeIn to `LiveInterruptionIntegrationService`
  - successful responses generate a recomposed continuation and suppress legacy cleanup/generic routing
  - failed or incomplete response ownership clears interruption state and fails closed
  - full PR10.4 pending clarification ownership is implemented
  - related run: [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]]

- Implemented AskClarification PR10.4d stale `InterruptionState=handling` watchdog:
  - added configurable BargeIn watchdog options
  - ownerless stale handling now clears to `none`
  - active capture, pending clarification, held playback, and interruption-owned speech prevent cleanup
  - full PR10.4 remains blocked by PR10.4e full clarification/recomposition ownership
  - related run: [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]]

- Implemented AskClarification PR10.4c awaiting clarification state and timeout recovery:
  - added canonical `awaiting_interruption_clarification` interruption state
  - pending AskClarification creation now emits awaiting state
  - pending clarification responses transition back to `handling`
  - pending expiry/cancellation clears awaiting state back to `none`
  - active timeout recovery uses `PendingInterruptionClarificationTimeoutMs`
  - full PR10.4 remains blocked by stale handling watchdog and recomposition ownership
  - related run: [[RUN-2026-07-07-008 AskClarification PR10.4c Awaiting State Timeout]]

- Implemented the Derived Work Planning Layer for Merlin.Vault:
  - created [[PE-0260 Derived Work Planning Rules]]
  - added stable `DW`, `PLAN`, and `PROMPT` ID rules for discovered follow-up work
  - added derived implementation plan/prompt templates and [[Derived Work Index]]
  - updated `AGENT.md`, writeback rules, prompt conventions, bundles, indexes, dashboard, and run-report template
  - runtime code changed: no
  - related run: [[RUN-2026-07-07-007 Derived Work Planning Layer]]

- Implemented AskClarification PR10.4b pending unclear-interruption clarification owner:
  - added pending owner model/interface/service with expiry, consume, and cancel APIs
  - added opt-in live AskClarification owner creation
  - pending clarification responses now bypass normal backend voice request routing
  - full PR10.4 remains blocked by awaiting state, timeout recovery, watchdog, and recomposition ownership
  - related run: [[RUN-2026-07-07-006 AskClarification PR10.4b Pending Owner]]

- Completed AskClarification PR10.4 prerequisite investigation:
  - full PR10.4 implementation remains No-Go
  - required sequence is PR10.4b pending owner, PR10.4c awaiting state/timeout, PR10.4d watchdog, PR10.4e full recomposition ownership
  - runtime code changed: no
  - related run: [[RUN-2026-07-07-005 AskClarification PR10.4 Prerequisite Investigation]]

- Added strict Go / No-Go rules to the Merlin Vault agent operating system:
  - created [[PE-0008 Go No-Go Rules]]
  - added PE-0008 to implementation, bugfix, refactor, and investigation bundles
  - updated prompt conventions, templates, preflight checklist, writeback rules, dashboard, and AskClarification lesson
  - runtime code changed: no
  - related run: [[RUN-2026-07-07-004 Go No-Go Rules]]

- Finalized AskClarification PR7 dead-end safe fallback verification:
  - plan status corrected to partial / not ready for full-agent execution
  - 9 previously failing backend tests rerun individually and classified
  - full pending unclear-interruption clarification owner remains future/follow-up
  - related run: [[RUN-2026-07-07-003 AskClarification Dead-End Safe Fallback]]

- Implemented AskClarification live dead-end recovery minimal fix:
  - removed the stale live PR7 deferred AskClarification path
  - added terminal fallback resume/cleanup for ownerless live interruption strategies
  - added `in the pool` regression coverage
  - related run: [[RUN-2026-07-07-002 AskClarification Live Dead-End Recovery]]

- Enriched Merlin.Vault operating system with prompt bundles, run naming rules, bug lifecycle, current work dashboard, maintenance checklist, implementation plan lifecycle, and curated plan naming cleanup.

- Added Agent Operating System structure to Merlin.Vault:
  - prompt extensions
  - agent run reports
  - progress reports
  - changelog
  - writeback rules
- Related run: [[RUN-2026-07-07-001 Vault Agent Operating System]]
