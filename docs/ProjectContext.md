# Merlin Project Context

Merlin is a local desktop assistant built around a .NET backend, a Godot frontend, and an optional local AI layer. The project is intentionally local-first, safety-conscious, and tool-driven. Merlin should help the user operate implemented capabilities without pretending unsupported capabilities exist.

## Current Architecture

Primary request flow:

Godot Frontend -> WebSocket -> WebSocketHandler -> CommandRouter -> HybridIntentParser -> ToolRegistry -> Tool -> AssistantResponse -> Godot Frontend

The backend exposes a WebSocket endpoint at:

`ws://localhost:5000/ws`

The frontend sends structured JSON requests:

```json
{
  "message": "open notepad",
  "correlationId": "optional-id"
}
```

The backend returns structured responses with fields such as:

`success`, `message`, `correlationId`, `errorCode`, `toolName`, `intent`, `intentConfidence`, `originalMessage`, `parserUsed`, `capabilityId`, `capabilityName`, `responseType`, `availableTools`, `diagnostics`, `confirmation`, and `applicationCandidates`.

## Backend / Frontend Split

The backend owns command understanding, safety checks, application resolution, confirmation flow, tool execution, diagnostics, local AI integration, and memory storage.

The Godot frontend owns chat display, connection state, user input, debug display, tools panel rendering, and response presentation. It should not execute actions directly.

## Implemented Capabilities

Implemented backend tools currently include:

- Open Application: opens configured, trusted, or user-confirmed local applications.
- Open URL: opens safe HTTP/HTTPS URLs in the default browser.
- Tool Discovery: lists available tools and examples.
- Status: returns diagnostics and runtime state.
- Confirmation: confirms pending safe actions.
- General Conversation: uses LocalAI chat when available.

Merlin uses capability domains to distinguish implemented capabilities, missing capabilities, unsupported actions, unknown input, and general conversation.

## LocalAI Role

LocalAI is optional and currently uses Ollama through HTTP. LocalAI may classify intent or generate conversation text, but it must not execute tools directly, scan the machine, browse the web, edit files, or launch applications. The backend verifies LocalAI output against allowed intents, configured capability domains, and the ToolRegistry.

Current intent flow:

TrustedCommandIntentParser -> RuleBasedIntentParser -> LocalAIIntentParser -> CapabilityClassifier

Trusted commands and rule-based executable commands win. LocalAI handles semantic classification. CapabilityClassifier is the deterministic final fallback.

## Tool Execution Philosophy

AI suggests. Resolver verifies. User confirms when needed. Tool executes.

All actions must go through injectable backend tools. Tool routing uses normalized commands and ToolRegistry. Discovered or untrusted applications require confirmation before launch. Destructive and unsafe actions are refused.

## Memory Direction

Memory is layered:

ConversationSessionService -> ConversationSummaryStore -> LongTermMemoryStore

Current memory is local and deterministic. Session working memory is in-memory. Conversation summaries persist distilled conversation summaries, not full transcripts. Long-term memory stores structured facts/preferences/project knowledge, not raw chat history. Extracted memory candidates require approval before becoming permanent.

## Current Limitations

Merlin does not currently have:

- WebSearchTool
- NewsTool
- TimeTool
- EmailTool
- CalendarTool
- File access/inspection tools
- STT or TTS
- Autonomous actions
- Database-backed storage
- Shell execution tools
- Web browsing inside ChatTool

Missing capabilities should return `missing_capability` with `responseType = limitation`. Unsafe actions should return `unsupported_action` with `responseType = safety`. Unclear input should return `unknown_input` with `responseType = error`.

## Preferred Roadmap

Near-term work should improve reliability, observability, capability-domain classification, test coverage, and frontend clarity.

Recommended next steps:

- Improve capability-domain prompts and validation.
- Add richer frontend rendering for response types and debug fields.
- Add explicit tools only when safety boundaries and tests are clear.
- Keep memory local, structured, and approval-based.
- Add web/search/news/time only as separate tools, not through ChatTool.

Avoid broad rewrites unless specifically requested.
