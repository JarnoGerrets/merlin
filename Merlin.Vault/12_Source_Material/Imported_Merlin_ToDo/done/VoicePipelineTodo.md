---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/VoicePipelineTodo.md
classification: implementation-plan
related_features:
  - Voice Interruption System
status: implemented
imported_to_vault: true
---

# Merlin Voice Pipeline ToDo

## 1. Fix first-turn STT cold start

The first voice interaction after app/backend startup can take around 20 seconds before Merlin responds.

Likely cause:

- Python STT cold start.
- Python environment/process startup.
- Whisper dependency imports.
- STT model load.
- CPU kernel/model warmup.

Evidence from earlier logs:

- First STT turn:
  - Python worker command elapsed: about 3.3 seconds.
  - Total STT elapsed: about 13.8 seconds.
  - This suggests roughly 10 seconds of startup/model/environment overhead.
- Later STT turns:
  - Total STT elapsed drops to about 1.3 seconds.
  - This suggests the pipeline is fast once warm.

Other possible cold-start contributors:

- Ollama model warmup.
- Piper first process/model startup.
- .NET JIT/startup.
- First WebSocket/HTTP flow.
- Godot recording save/read/upload.

Recommended direction:

- Start/warm the STT worker when the backend starts.
- Keep the Python STT process alive.
- Preload the Whisper model once.
- Send audio transcription jobs to the persistent worker.
- Avoid launching/importing/loading Python on the first user request.

Expected result:

- Remove the long first "hello" delay.
- Make first voice interaction feel closer to warmed steady-state performance.
