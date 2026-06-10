# Merlin Roadmap

## Current Phase

Merlin is in local vertical-slice and architecture-hardening phase.

Current focus:

- Stable WebSocket backend/frontend loop.
- Tool-based execution.
- Safe application and URL opening.
- Confirmation flow.
- Capability-domain classification.
- Structured responses and frontend rendering.
- LocalAI as controlled intent/chat layer.
- Local, distilled memory architecture.

## Next Recommended Tasks

- Improve frontend rendering for response types, debug metadata, confirmations, and diagnostics.
- Strengthen LocalAI prompt tests around capability domains.
- Add manual acceptance scripts for common WebSocket scenarios.
- Improve StatusTool output for capability domains and memory state.
- Add small UX polish for Godot chat history, copyability, and tool panel display.
- Review LocalAI conversation behavior so it does not mention internal policy documents.

## Later Tasks

- Add TimeTool as a safe read-only capability.
- Add WebSearchTool as a separate explicit tool.
- Add NewsTool only after WebSearchTool or a safe news source exists.
- Add richer confirmation UX in Godot.
- Add approved memory management UI.
- Add optional export/import for local settings and memory.

## Explicitly Postponed

- Speech-to-text.
- Text-to-speech.
- Autonomous agent workflows.
- Shell command execution.
- File editing/self-modification tools.
- General filesystem access tools.
- Database-backed memory.
- Vector embeddings or semantic search.
- Web browsing inside ChatTool.
- Auto-installing or downloading software.
