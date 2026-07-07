---
type: prompt-extension
id: PE-0004
status: active
applies_to:
  - all systems
required_for:
  - implementation
  - bugfix
  - refactor
  - test-only
---

# PE-0004 Testing and Validation

## Required Validation

Run relevant tests for affected systems.

For backend changes, default commands:

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

## Rules

1. Do not ignore failing tests.
2. If a test expectation changed intentionally, explain why.
3. If validation cannot be run, report why.
4. Add or update tests for meaningful behavior changes.
