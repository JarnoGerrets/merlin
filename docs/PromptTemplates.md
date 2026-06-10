# Merlin Prompt Templates

Use these to keep future prompts short. Start with:

`Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.`

## A. Small Bugfix Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Fix this bug: `<describe observed behavior>`.

Expected behavior: `<describe expected behavior>`.

Constraints: Do not add unrelated features. Add/update focused tests if behavior changes.

## B. Focused Feature Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Add `<feature name>`.

Scope:
- `<specific behavior 1>`
- `<specific behavior 2>`

Do not add: `<explicit exclusions>`.

Acceptance:
- `<manual or automated check>`

## C. Architecture Migration Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Migrate `<old architecture>` to `<new architecture>`.

Goals:
- `<goal 1>`
- `<goal 2>`

Non-goals:
- `<non-goal 1>`
- `<non-goal 2>`

Preserve existing behavior unless explicitly listed.

## D. Test-Only Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Add or update tests for `<component/behavior>`.

Do not change production code unless a test exposes a real bug; explain any required production change first.

Run:

```powershell
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

## E. Frontend-Only Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Update Godot frontend behavior: `<describe UI behavior>`.

Backend contract: `<response/request fields involved>`.

Do not change backend behavior.

Acceptance:
- `<manual frontend check>`

## F. Backend-Only Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Update backend behavior: `<describe behavior>`.

Do not change Godot frontend unless required by a response contract change.

Acceptance:
- `<test/manual check>`

## G. Repository Review Prompt

Read docs/ProjectContext.md, docs/RepositoryMap.md, and docs/AgentRules.md. Apply only relevant rules.

Task: Review `<area/files>` for bugs, regressions, safety issues, and missing tests.

Do not edit files. Report findings first, with file/line references where possible.
