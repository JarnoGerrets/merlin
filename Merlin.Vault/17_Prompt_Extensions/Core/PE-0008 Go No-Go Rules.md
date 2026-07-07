---
type: prompt-extension
id: PE-0008
status: active
applies_to:
  - implementation
  - bugfix
  - refactor
  - investigation
required_for:
  - implementation-task
  - bugfix-task
  - refactor-task
---

# PE-0008 Go / No-Go Rules

## Purpose

Prevent agents from implementing partial or improvised runtime changes after discovering that a requested feature/fix is blocked.

## Core Rule

Go = implement.

No-go = stop before runtime changes and report blockers.

Partial-go = only allowed if explicitly approved by the user or the task prompt.

## Definitions

### Go

A task is Go when:

- required owners/services already exist,
- required architecture boundaries are clear,
- required dependencies are present,
- the test seam is available or can be added safely,
- the requested implementation can be completed without inventing a parallel subsystem,
- non-goals can be respected.

### No-Go

A task is No-Go when any of these are true:

- required prerequisite system is missing,
- required owner/service does not exist,
- requested behavior would require a broader architecture change first,
- implementation would require inventing a parallel subsystem,
- implementation would bypass safety/confirmation/cancellation/interruption rules,
- test seams are absent and cannot be added safely in the requested scope,
- existing feature state contradicts the plan,
- the task would silently implement a reduced/fallback behavior not approved by the user.

### Partial-Go

A task is Partial-Go only when the prompt explicitly allows a reduced scope.

Acceptable wording:

```text
If full implementation is blocked, implement the listed fallback scope.
```

or:

```text
Partial implementation is approved only for these specific items: ...
```

If the prompt does not explicitly approve partial implementation, treat blockers as No-Go.

## Required No-Go Report

When a No-Go is found, do not change runtime code.

Instead, produce a report with:

1. Requested task.
2. Go/no-go result.
3. Exact blocker(s).
4. Missing prerequisite(s).
5. Evidence from code/vault.
6. Why implementation would be unsafe or premature.
7. Required prerequisite work.
8. Proposed implementation sequence.
9. Suggested prompt for the prerequisite task.
10. Vault notes updated.

## Allowed No-Go Changes

During No-Go, the agent may only change documentation/vault notes, such as:

- add an agent run report,
- add/update bug note,
- update implementation plan status to blocked,
- update roadmap/progress report,
- add investigation report.

No runtime code changes are allowed.

## Forbidden No-Go Behavior

Do not:

- implement a minimal fallback,
- remove branches,
- change production behavior,
- add temporary runtime hacks,
- mark the task partial unless explicitly approved,
- convert no-go into a smaller implementation,
- silently skip missing architecture and proceed.

## Final Response Requirement

If No-Go:

```text
No runtime code changed.
Task blocked.
Required prerequisite work:
...
Suggested next prompt:
...
```
