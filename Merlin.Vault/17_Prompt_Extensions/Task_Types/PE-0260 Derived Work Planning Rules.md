---
type: prompt-extension
id: PE-0260
status: active
applies_to:
  - implementation
  - bugfix
  - investigation
  - refactor
  - documentation
required_for:
  - tasks with discovered prerequisites
  - tasks with No-Go blockers
  - tasks with concrete follow-up work
---

# PE-0260 Derived Work Planning Rules

## Purpose

Do not leave concrete follow-up work only in chat, remaining-work notes, or vague run-report bullets.

When a task discovers actionable next work, create or update a derived implementation plan and a matching short implementation prompt.

Derived work generation is writeback, not execution. Do not implement the follow-up unless the current user prompt explicitly asks for that follow-up scope.

## Trigger Conditions

Create derived work artifacts when discovering:

1. required prerequisite work;
2. No-Go blockers with concrete fix direction;
3. approved Partial-Go remainder;
4. new bug or fragility outside current scope;
5. missing test seam required before safe implementation;
6. architecture owner/service gap;
7. separable next phase;
8. stale or under-specified plan that needs correction;
9. repeated unresolved follow-up across runs.

## Non-Triggers

Do not create derived work artifacts for:

1. vague ideas;
2. speculation;
3. out-of-scope side projects;
4. future features not requested by the user;
5. tiny cleanup that belongs only in the current run report;
6. findings not grounded in code, vault notes, logs, tests, or the user request;
7. more than three derived items from one run unless explicitly requested.

## Required Artifacts

For each concrete derived work item, create or update:

1. an implementation plan under `13_Implementation_Plans/<Area>/`;
2. a short prompt under `08_Implementation_Prompts/`;
3. relevant indexes;
4. current run report links;
5. progress/current dashboard links if the item affects active or blocked work.

## ID Rules

Use:

- `DW-YYYY-MM-DD-NNN` for the derived work item;
- `PLAN-YYYY-MM-DD-NNN` for the implementation plan;
- `PROMPT-YYYY-MM-DD-NNN` for the implementation prompt.

Use the same date and sequence number for the linked set.

Allocate the next available number for the current date by scanning `13_Implementation_Plans`, `08_Implementation_Prompts`, and `14_Agent_Runs/YYYY`. If unsure, use the next higher number and do not reuse an existing ID.

## Status Rules

Set plan status carefully:

- `ready` only if scope, dependencies, owner, validation, and non-goals are clear;
- `draft` if the plan is useful but still needs detail;
- `blocked` if a prerequisite is missing;
- `future` if useful later but not current/safe;
- `obsolete` if replaced or no longer valid.

## Final Report Requirement

Final agent response must include:

```text
Derived work created:
- <plan path>
- <prompt path>
```

or:

```text
Derived work created: none
Reason: no concrete follow-up work discovered.
```
