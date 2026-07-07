---
type: project
status: current
tags:
  - merlin
  - agent/process
---

# Agent Writeback Rules

Every implementation agent must update the vault after completing meaningful work.

## Core Rule

If code changes but the vault does not, the task is incomplete.

## Required Writeback After Implementation

1. Create an agent run report in `14_Agent_Runs`.
2. Update affected feature notes in `03_Features`.
3. Update affected architecture notes in `02_Architecture` if architecture changed.
4. Update affected Code Atlas notes in `11_Code_Atlas` if files/classes changed.
5. Update relevant roadmap notes in `05_Roadmaps` if status or next steps changed.
6. Update current-state notes in `04_Current_State` if implementation status changed.
7. Add or update bug notes in `09_Bugs` if bugs, regressions, or fragility were found.
8. Update progress reports in `15_Progress_Reports`.
9. Add a changelog entry in `16_Change_Log`.

## Required Writeback After Investigation

1. Create or update an agent report in `07_Agent_Reports` or `14_Agent_Runs`.
2. Update feature/architecture/code-atlas notes if the investigation changes understanding.
3. Add bugs to `09_Bugs` if found.
4. Add open questions where relevant.

## Required Writeback After Documentation-Only Tasks

1. Create an agent run report if the documentation change is meaningful.
2. Update indexes affected by the documentation change.
3. Add changelog entry if the vault structure changed.

## No-Go Writeback

If an agent reaches No-Go:

1. Do not change runtime code.
2. Create an agent run report with status `blocked`.
3. Update the implementation plan status to `blocked`.
4. Add or update bug/limitation notes if relevant.
5. Update progress report with blocker.
6. Add proposed prerequisite work.

## Derived Work Writeback

If meaningful implementation, bugfix, investigation, refactor, or documentation work discovers concrete follow-up work, the agent must create or update derived work artifacts.

Required for each concrete follow-up:

1. Implementation plan in `13_Implementation_Plans/<Area>/`.
2. Short implementation prompt in `08_Implementation_Prompts/`.
3. Links from the current agent run report.
4. Relevant index updates.
5. Dashboard/progress updates if the item affects active, blocked, or next-safe work.

Use stable linked IDs:

- `DW-YYYY-MM-DD-NNN`
- `PLAN-YYYY-MM-DD-NNN`
- `PROMPT-YYYY-MM-DD-NNN`

Do not leave concrete follow-up work only in `Remaining Work`.

Do not create derived artifacts for vague ideas, speculation, out-of-scope side projects, or tiny cleanup notes that belong only in the current run report.

## No Silent Changes

Do not change code without updating the vault.

Do not change architecture without updating architecture notes.

Do not add bugs to chat only; add them to the vault.

Do not leave feature statuses stale.

## Final Agent Response

Every final agent response after implementation must include:
- files changed
- tests run
- vault notes updated
- bugs found
- remaining work
- derived work created

## Agent Run IDs

Use [[Agent Run Naming Rules]]. Every meaningful run report should include a stable `run_id` using `RUN-YYYY-MM-DD-NNN`.

## Related Operating Notes

- [[Current Work Dashboard]]
- [[Vault Maintenance Checklist]]
- [[Bug Lifecycle Rules]]
- [[Implementation Plan Lifecycle]]
- [[Prompt Extension Selection Guide]]
