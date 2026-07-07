---
type: project
status: current
tags:
  - merlin
  - build
  - tests
---

# Build and Test Commands

## Backend

```powershell
cd C:\Users\jarno\Source\Merlin
dotnet build Merlin.Backend\Merlin.Backend.csproj --no-restore -p:UseSharedCompilation=false
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

## Run Backend Development Server

```powershell
cd C:\Users\jarno\Source\Merlin\Merlin.Backend
dotnet build .\Merlin.Backend.csproj
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:ASPNETCORE_URLS="http://localhost:5000"
.\bin\Debug\net8.0\Merlin.Backend.exe
```

## Godot / Frontend

No single canonical validation command was confirmed in this pass. Existing agents have used Godot headless load checks when working on frontend scripts. Add the exact command here after the next verified frontend validation.
