---
type: project
status: current
area: cross-cutting
tags:
  - merlin
---

# Build and Test Commands

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Known current test status from vault validation: backend build passes; full backend tests fail 9 correction/barge-in tests.
