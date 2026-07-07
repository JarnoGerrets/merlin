---
type: prompt-extension
id: PE-0001
status: active
applies_to:
  - all systems
required_for:
  - implementation
  - bugfix
  - refactor
  - investigation
---

# PE-0001 Agent Preflight

## Use When

Always use before implementation, bugfix, refactor, or investigation tasks.

## Required Reading

1. [[00_Index]]
2. [[Scope Rules]]
3. [[Status Rules]]
4. [[Agent Preflight Checklist]]
5. Relevant feature note
6. Relevant architecture note
7. Relevant code atlas notes
8. Relevant roadmap
9. Relevant bug notes

## Rules

1. Verify actual code before trusting old plans.
2. Do not implement blocked or future work unless explicitly requested.
3. Check non-goals before editing.
4. Check whether the task is implementation, bugfix, investigation, refactor, test-only, or documentation-only.
