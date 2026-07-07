---
type: project
status: current
tags:
  - merlin
---

# Project Overview

Merlin is a local assistant project with a .NET backend, Godot frontend, Python voice/vision sidecars, and a WinForms/WebView2 browser host. It supports voice input, speech playback, interruption handling, memory, browser workspace control, dashboard UI control, and camera-based motion gestures.

## Current Goals

- Keep voice, browser, motion, memory, and UI systems understandable.
- Separate implemented behavior from planned work.
- Use Active Surface and Motion Profiles as foundations before site/app-specific control.

## Key Entry Points

- Backend: `Merlin.Backend/Program.cs`
- WebSocket: `Merlin.Backend/WebSocket/WebSocketHandler.cs`
- Frontend: `Merlin.Frontend/Scripts/Main.gd`
- Browser host: `Merlin.BrowserHost/BrowserWorkspaceForm.cs`
- Vision sidecar: `Merlin.Backend/VisionScripts/vision_worker.py`
- Existing plans: `Merlin.ToDo/`
