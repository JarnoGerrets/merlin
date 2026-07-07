---
type: project
status: current
tags:
  - merlin
  - repository-map
---

# Repository Map

| Project/folder | Purpose | Important files | Relevant systems | Notes |
| --- | --- | --- | --- | --- |
| `Merlin.Backend` | ASP.NET/.NET backend runtime | `Program.cs`, `Services/CommandRouter.cs`, `WebSocket/WebSocketHandler.cs` | voice, routing, memory, browser, motion, safety | Main service composition and APIs. |
| `Merlin.Backend/Services/Context/ActiveSurface` | Current active surface context | `ActiveSurfaceService.cs`, `KnownSurfaces.cs` | routing, browser, motion | Foundation for context-aware commands. |
| `Merlin.Backend/Services/Motion` | Motion profile layer | `MotionControlModeService.cs`, profile classes | motion, vision, browser | Selects one active motion profile. |
| `Merlin.Backend/Services/BrowserWorkspace` | Backend browser orchestration | `BrowserWorkspaceService.cs`, motion/page-control subfolders | browser workspace | Launches BrowserHost and sends commands. |
| `Merlin.Backend/VisionScripts` | Python vision worker | `vision_worker.py` | camera, gestures, calibration | Emits gesture events to backend. |
| `Merlin.Backend/VoiceScripts` | Python voice/STT/TTS workers | `transcribe_faster_whisper.py`, `voice_worker.py`, `chatterbox_worker.py` | voice | Local speech pipeline helpers. |
| `Merlin.Backend.Tests` | Backend tests | `CommandRouterTests.cs`, `BargeInTests.cs`, `MotionControlModeServiceTests.cs` | all backend systems | Main regression suite. |
| `Merlin.Frontend` | Godot frontend | `Scripts/Main.gd`, `MerlinWebSocketClient.gd`, UI window scripts | UI, orb, dashboard motion | User-facing Merlin UI. |
| `Merlin.OrbLab` | Orb/UI lab mirror | `Scripts/*` | frontend experimentation | Appears to share/junction scripts with frontend in places. |
| `Merlin.BrowserHost` | WinForms/WebView2 browser host | `BrowserWorkspaceForm.cs`, `NativeBrowserPointerOverlayWindow.cs`, JS script files | browser workspace | Separate process controlled by backend stdin/stdout. |
| `Merlin.ToDo` | Existing plans/reports/prompts | many `.md` files | project planning | Original docs stay here; vault links them. |
| `Merlin.Vault` | Obsidian documentation vault | `00_Index.md` | project brain | Start future agent work here. |
| configuration files | Runtime options | `appsettings.json`, `appsettings.Development.json`, `*.Options.cs` | all backend systems | Do not infer implementation from config alone. |
