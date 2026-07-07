---
type: project
status: current
---

# Agent Run Naming Rules

## Purpose

Agent run reports need stable IDs so changelogs, bugs, progress reports, and feature notes can reference the same work.

## Run ID Format

Use:

`RUN-YYYY-MM-DD-NNN`

Example:

`RUN-2026-07-07-001`

## File Name Format

Use:

`RUN-YYYY-MM-DD-NNN Short Task Title.md`

Example:

`RUN-2026-07-07-001 Motion Profile Cleanup.md`

## Frontmatter

```yaml
---
type: agent-run
run_id: RUN-YYYY-MM-DD-NNN
date: YYYY-MM-DD
run_type: implementation | bugfix | investigation | documentation | refactor | test-only
related_features:
  - Feature Name
status: completed | partial | failed | blocked
branch:
commit_before:
commit_after:
agent:
---
```

## Rules

1. Use the next available NNN number for the date.
2. One meaningful task equals one agent run report.
3. Link agent run reports from changelog, progress reports, bugs, and affected feature notes.
4. If commit hashes are unknown, leave them blank and say unknown.
