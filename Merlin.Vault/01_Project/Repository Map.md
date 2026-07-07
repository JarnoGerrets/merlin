---
type: project
status: current
tags:
  - merlin
---

# Repository Map

## In-Scope Production Projects

| Project | Purpose | Important folders | Important files | Related systems | Notes |
| --- | --- | --- | --- | --- | --- |
| `Merlin.Backend` | ASP.NET Core runtime for routing, voice, browser workspace, motion, memory, safety, and WebSocket server. | `Services`, `Core/Memory`, `VisionScripts`, `Configuration` | `Program.cs`, `Services/CommandRouter.cs`, `Services/BrowserWorkspace/BrowserWorkspaceService.cs`, `VisionScripts/vision_worker.py` | [[Backend Architecture]], [[Voice Pipeline Architecture]], [[Motion Architecture]] | Main production runtime. |
| `Merlin.Backend.Tests` | xUnit regression tests for backend services. | root test files | `CommandRouterTests.cs`, `MotionControlModeServiceTests.cs`, `BargeInTests.cs`, `CorrectionRegenerationTests.cs` | [[Current Test Coverage]] | Current full suite has known failures documented in bugs. |
| `Merlin.BrowserHost` | WinForms/WebView2 host process controlled by backend. | root project files | `BrowserWorkspaceForm.cs`, `BrowserWorkspaceCommand.cs`, `NativeBrowserPointerOverlayWindow.cs`, `NativeBrowserInputService.cs` | [[BrowserHost Architecture]], [[Browser Workspace]] | Real project exists and is in scope. |
| `Merlin.Frontend` | Godot frontend for orb, dashboard UI, voice capture, WebSocket client, and dashboard gesture behavior. | `Scripts`, `Scripts/UI/Windows`, `Scenes` | `Scripts/Main.gd`, `Scripts/MerlinWebSocketClient.gd`, `Scripts/UI/Windows/MerlinWindow.gd` | [[Frontend Architecture]], [[Dashboard UI Control]] | Dashboard gesture behavior remains centralized in `Main.gd`. |
| `Merlin.ToDo` | Legacy source backup for plans, reports, prompts, and historical implementation notes now imported into the vault. | feature folders | motion/browser/memory/voice markdown | [[Source Material Index]], [[Implementation Plans Index]], [[Agent Reports Index]] | Retained temporarily as source material; new durable planning should live in `Merlin.Vault`, not here. |
| `Merlin.Vault` | Obsidian project brain for future agents. | all vault folders | `00_Index.md`, feature/architecture/code atlas notes | all systems | Update after implementation. |
| `docs` | Project documentation, support notes, design context, diagnostics, and historical reference material. | root docs and support folders | `AgentRules.md`, `ProjectContext.md`, `RepositoryMap.md`, `Roadmap.md` | [[08_Implementation_Prompts/Index]], [[07_Agent_Reports/Index]] | Files in docs should be indexed when relevant, but the vault remains the curated source of truth. |

## Ignored Projects/Folders

The main project brain intentionally ignores these unless a task explicitly targets one of them:

- `Merlin.OrbLab`
- `Merlin.UiCanvas`
- `Merlin.VoiceTest`
- `STT_FAILURE_DIAGNOSTICS`
- `mnt`
- `scripts`
- `tools`
- `tools/chatterbox-bench`

Do not treat ignored folders as missing coverage.
