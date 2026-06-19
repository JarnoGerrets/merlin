# Current Capability Network Analysis

## Executive Summary

Merlin currently has two overlapping routing systems:

1. The active backend request path is `WebSocketHandler` -> `CommandRouter` -> `IIntentParser`/`HybridIntentParser` -> normalized command -> `ToolRegistry` -> `ITool`.
2. A newer hierarchical router exists in `Merlin.Backend/Services/IntentRouting` (`MerlinIntentRouter`, `DomainRouter`, `CapabilityRouter`, `DeterministicIntentClassifier`) and is registered in DI, but it is only used inside `HybridIntentParser` after high-confidence rule parsing and before LocalAI/fallback. It returns `RouteDecision`, then gets mapped back to the older `IntentParseResult` shape by `RouteDecisionIntentMapper`.

The current design already has useful extension points: `CapabilityDomains` in configuration, tool registration through DI, `ToolRegistry`, structured `AssistantResponse` metadata, missing/unsupported capability responses, confirmation services, and tests for several routing layers.

The fragile part is that routing still collapses into a normalized command string before execution. The richer `RouteDecision` path does not yet carry action, target scope, candidates, safety level, required permissions, or clarifying questions. Current target/scope detection is partial and rule-based: it distinguishes some "time" vs "time complexity", "memory" vs RAM, app opening vs URL opening, and local files as missing capability, but it does not yet support general action + target/scope routing for web/file/email/calendar/repo tasks.

The safest extension strategy is to evolve the existing layered architecture rather than replace it: extend capability domain metadata, make the hierarchical router produce a richer route result, preserve the `CommandRouter`/`ToolRegistry` execution path, add explicit scope and safety classification, and expand tests around ambiguous "look up/find/search/check" phrases.

## Current Request Flow

`Merlin.Backend/Program.cs` wires the runtime:

- Maps `/ws` to `WebSocketHandler`.
- Registers `CommandRouter`, `HybridIntentParser` as `IIntentParser`, `ToolRegistry`, all `ITool` implementations, LocalAI/DeepInfra services, capability options, confirmation services, runtime state, memory services, and frontend speech/visual services.
- Configures `CapabilityOptions` from `CapabilityDomains` in `appsettings.json`, falling back to defaults in `CapabilityOptions.CreateDefault()`.

Text request flow:

1. `Merlin.Frontend/Scripts/MerlinWebSocketClient.gd` sends JSON with `message`, `correlationId`, `interactionSource`, and `clientMode`.
2. `Merlin.Backend/WebSocket/WebSocketHandler.cs` receives the packet, deserializes `AssistantRequest`, and calls `CommandRouter.RouteAsync`.
3. `CommandRouter` optionally normalizes voice text with `SpeechCommandNormalizer`.
4. `CommandRouter` calls `_intentParser.ParseAsync`, which is `HybridIntentParser` in runtime DI.
5. `CommandRouter` handles special non-executable intents immediately: `unsupported_action`, `missing_capability`, and `unknown_input`.
6. For executable intents, `CommandRouter` asks `ToolRegistry.FindTool(intentResult.NormalizedCommand)`.
7. The selected `ITool` executes with `ToolExecutionContext`.
8. `ToolResult` is mapped into `AssistantResponse`, polished by `ResponsePolisher`, optionally formatted for speech, then sent back over WebSocket.

Voice stream request flow:

- `WebSocketHandler` handles `voice_stream_start`, `voice_stream_chunk`, and `voice_stream_end`.
- On `voice_stream_end`, it transcribes through `VoiceStreamSessionService`, sends a `voice_transcript` packet, then routes the transcript through the same `CommandRouter` path with `InteractionSource = "voice_stream"`.

## Current Configuration Model

`Merlin.Backend/appsettings.json` contains the main runtime configuration:

- `LocalAI`: Ollama intent parser and local fallback settings, including `MinimumConfidence`.
- `Llm`: cloud provider settings. Current default provider is `deepinfra`, with local fallback enabled.
- `CapabilityDomains`: capability metadata for implemented, missing, and unsupported domains.
- `ApplicationLaunch`: configured app aliases for `OpenApplicationTool`.
- Voice, TTS, acknowledgement, barge-in, and persistence settings.

`Merlin.Backend/Configuration/CapabilityOptions.cs` provides defaults when config does not define capability domains. The default model is the same shape as config:

- `id`
- `name`
- `description`
- `isImplemented`
- `implementedIntent`
- `missingMessage`
- `safetyLevel`

Important current capability domain examples from config/defaults:

- Implemented: `application_launch`, `url_opening`, `tool_discovery`, `diagnostics`, `confirmation`, `general_conversation`, `system_time`, `system_date`, `system_timezone`.
- Missing: `time`, `news`, `web_search`, `email`, `calendar`, `file_access`.
- Unsupported: `system_settings`, `software_installation`, `destructive_file_action`.

There is an inconsistency here: `appsettings.json` includes `system_timezone`, `system_date`, and `system_time`, plus an unimplemented aggregate `time` domain. The actual `SystemResourceTool` supports time/date/timezone, so the unimplemented `time` domain is a stale or broader placeholder.

## Current Capability Representation

There are two capability representations:

1. Configured capability domains:
   - `Merlin.Backend/Models/CapabilityDomain.cs`
   - `Merlin.Backend/Configuration/CapabilityOptions.cs`
   - Used by `LocalAIIntentParser`, `CapabilityClassifier`, `ResponsePolisher`, and `StatusTool`.

2. Hierarchical router capability definitions:
   - `Merlin.Backend/Models/CapabilityDefinition.cs`
   - `Merlin.Backend/Models/CapabilityCandidate.cs`
   - `Merlin.Backend/Services/IntentRouting/CapabilityRegistry.cs`
   - Used by `MerlinIntentRouter`.

The two catalogs are not unified. `CapabilityDomains` uses ids like `application_launch` and `url_opening`; `CapabilityRegistry` uses ids like `app.open`, `url.open`, `system.get_time`, and `memory.search`. `RouteDecisionIntentMapper` bridges some of those router ids back into old `IntentParseResult` ids.

This split is workable for now but fragile. Any new capability added only to config may be visible to LocalAI/fallback and status output but not to the hierarchical router. Any new capability added only to `CapabilityRegistry` may route internally but not have missing messages, support counts, or frontend-friendly capability names.

## Current Tool Registration / Discovery

`Program.cs` registers tools as `ITool` singletons:

- `OpenApplicationTool`
- `OpenUrlTool`
- `ToolDiscoveryTool`
- `SystemResourceTool`
- `StatusTool`
- `ConfirmationTool`
- `WakeMerlinTool`
- `EditBrowserMappingTool`
- `DeleteBrowserMappingTool`
- `DevVisualStateTool`
- `GeneralConversationTool`

`Merlin.Backend/Services/ToolRegistry.cs` stores all `ITool` instances from DI and implements:

- `FindTool(string command)`: returns the first tool whose `CanHandle(command)` is true.
- `GetTools()`: exposes registered tools for discovery/status.

`Merlin.Backend/Tools/Interfaces/ITool.cs` defines:

- `Name`
- `Description`
- `Examples`
- `CanHandle(string command)`
- `ExecuteAsync(string command)`
- optional `ExecuteAsync(ToolExecutionContext)`

`ToolDiscoveryTool` returns `ToolMetadata` for each registered tool. Current `ToolMetadata` only includes name, description, and examples. It does not include capability id, safety level, privacy/read/write flags, credentials, or confirmation requirements.

## Current Routing / Classification Flow

The active parser is `HybridIntentParser`:

1. Pending confirmation check:
   - `HybridIntentParser.TryParsePendingConfirmationCommand`
   - If a confirmation is pending, explicit confirmation/cancellation/choice/candidate-name messages route to `confirmation`.
   - If an unrelated message arrives, it consumes/clears the latest pending confirmation.

2. Pending browser mapping edit check:
   - `HybridIntentParser.TryParsePendingInteractionCommand`
   - Currently specialized to `PendingInteractionTypes.BrowserMappingEdit`.

3. Rule-based parser:
   - `RuleBasedIntentParser` handles high-confidence exact or semi-exact commands.
   - If confidence >= `LocalAI.MinimumConfidence`, the rule result wins.

4. Hierarchical router:
   - If present, `MerlinIntentRouter.RouteAsync` runs.
   - It only wins if `RouteDecision.ShouldExecuteTool` is true.
   - `NoTool` decisions are discarded and the pipeline continues to LocalAI/fallback.

5. LocalAI disabled or unavailable fallback:
   - `CapabilityClassifier.Classify`.

6. LocalAI intent parser:
   - `LocalAIIntentParser` asks Ollama for strict JSON.
   - If confidence >= `MinimumConfidence`, it wins.

7. Final fallback:
   - `CapabilityClassifier.Classify`.

`RuleBasedIntentParser` currently recognizes:

- Apps: configured app aliases plus extracted open/start/launch/pull-up target.
- URLs: URL-like targets behind open/go to/browse/visit/pull up prefixes.
- Tool discovery.
- Diagnostics.
- System resources: current time, current date, timezone.
- Confirmation phrases.
- Dev visual state commands.
- Browser mapping edit/delete.
- Trusted URL aliases.
- Wake phrases.
- A few general conversation phrases.
- A few missing capability phrases.
- A few unsupported action phrases.

`MerlinIntentRouter` currently does:

- Normalize with `TextNormalizer`.
- Emergency route first (`stop`, `cancel`).
- Score domains through `DomainRouter`.
- Select top domains above 0.35.
- Generate candidates from `CapabilityRegistry` through `CapabilityRouter`.
- Classify candidates with `DeterministicIntentClassifier`.
- Return `RouteDecision.Tool` if confidence meets the candidate threshold, otherwise `RouteDecision.NoTool`.

The newer router has multi-domain/candidate scoring internally, but the final `RouteDecision` only exposes one selected capability, confidence, domain, execute/no-execute, and reason.

## Current AI Fallback Flow

There are two separate AI roles:

1. LocalAI intent parsing:
   - `LocalAIIntentParser` uses `ILocalAIClient`/Ollama.
   - Prompt includes assistant policy, available tools, configured capability domains, allowed intents, normalization examples, and a strict JSON shape.
   - Allowed intents are currently fixed in code: `open_application`, `open_url`, `tool_discovery`, `diagnostics`, `confirmation`, `system_resource_query`, `general_conversation`, `unsupported_action`, `missing_capability`, `unknown_input`.
   - It validates capability ids against `CapabilityDomains`.
   - It validates executable normalized commands by checking `ToolRegistry.FindTool`.

2. General conversation:
   - `GeneralConversationTool` calls `LocalAIChatService`.
   - `LocalAIChatService` uses DeepInfra when `Llm.Provider = deepinfra`.
   - It prepares memory context via `MemoryOrchestrator` when a service scope factory is available.
   - It falls back to local LLM through `LocalLlmProvider` if DeepInfra fails and `UseLocalFallback` is true.
   - The system prompt explicitly tells chat not to claim missing capabilities and to say there is no web access for current/recent information.

Current important limitation: LocalAI is used for intent parsing, but DeepInfra chat is not the router. General conversation can answer/reason, but it does not execute tools and should not pretend to access live/current information.

## Current Confirmation / Safety Flow

There is no general safety classifier separate from capability classification yet.

Current safety gates are implemented in these places:

- `RuleBasedIntentParser` detects a small list of unsupported actions and routes them to `unsupported_action`.
- `CapabilityClassifier` detects destructive terms and returns `unsupported_action` with `destructive_file_action`.
- `LocalAIIntentParser` can classify `unsupported_action`, but only among its fixed allowed intents.
- `OpenUrlTool` blocks unsafe URL schemes: `file`, `ftp`, `javascript`, `data`, `cmd`, and `powershell`.
- `OpenApplicationTool` uses `ApplicationResolver` and `ConfirmationService` for unknown/discovered apps or ambiguous app candidates.
- `ConfirmationTool` executes only pending `open_application`, `open_url`, or `open_url_fallback` confirmations. Other pending action names are rejected as unsupported confirmation actions.

`ConfirmationService` stores in-memory `PendingConfirmation` objects with:

- action
- target
- display name
- requested alias
- original user command
- intent
- normalized command
- tool name
- candidate list
- expiry time

This model is good for staged actions but currently tuned for app/URL opening. It does not yet encode safety level, permission scope, private data access, exact write consequences, dry-run result, or audit metadata.

## Current Unsupported Capability Handling

Unsupported and missing capabilities are represented as `IntentParseResult` values, not tools:

- `missing_capability`
- `unsupported_action`
- `unknown_input`

`CommandRouter` intercepts these intents before `ToolRegistry` lookup and returns `AssistantResponse` with:

- `Success = false`
- `ErrorCode = MISSING_CAPABILITY`, `UNSUPPORTED_ACTION`, or `UNKNOWN_INPUT`
- `ResponseType = limitation`, `safety`, or `error`
- capability id/name from the parser if available

`ResponsePolisher` maps `MISSING_CAPABILITY` and `UNSUPPORTED_ACTION` to the configured `CapabilityDomain.MissingMessage` when possible.

This is a strong pattern to reuse. Missing/unsupported domains can be recognized without pretending a tool exists.

Current limitation: the special response message set in `CommandRouter` is generic ("Missing capability.", "Unsupported action.") and only becomes friendly if `ResponsePolisher` has an applicable domain message.

## Current Frontend / Orb State Integration

Backend response state reaches the frontend through:

- `AssistantResponse.ResponseType`
- `ToolName`
- `Intent`
- `CapabilityId`
- `CapabilityName`
- `ErrorCode`
- `Confirmation`
- `ApplicationCandidates`
- separate visual packets with `{ type = "visual_state", mode = ... }`
- `AssistantVisualEvent` packets for speech/progress events

`Merlin.Frontend/Scripts/MerlinWebSocketClient.gd` routes:

- `visual_state` packets to `visual_state_received`
- `voice_transcript` packets to `voice_transcript_received`
- packets with `event` to `visual_event_received`
- normal `AssistantResponse` payloads to `response_received`

`Merlin.Frontend/Scripts/Main.gd` uses response data for chat rendering, debug info, confirmation overlays, application choices, and orb state. It treats:

- `responseType = confirmation` as confirmation state.
- `responseType = limitation` as limitation UI.
- `responseType = safety` as safety UI.
- errors as error UI.
- successful tool responses where `toolName != "General Conversation"` or `intent == "system_resource_query"` as tool execution.

`WebSocketHandler.BuildVisualStatePacket` supports modes: `idle`, `thinking`, `speaking`, `listening`, `tool`, `confirmation`, and `error`.

Current limitation: the frontend has no structured route-result display and no candidate score/scope/safety metadata beyond existing response fields.

## Existing Tests

Important backend tests already cover:

- `CommandRouterTests`: tool execution, unknown input/command, missing capability, unsupported action, file access limitation, system resource execution, discovery, voice normalization.
- `RuleBasedIntentParserTests`: app URL parsing, discovery, diagnostics, time/date/timezone, confirmation phrases, dev visual commands, missing capability, unsupported actions, browser mapping edits.
- `HybridIntentParserTests`: parser ordering, LocalAI disabled/unavailable behavior, hierarchical router use, pending confirmations, pending browser mapping edits.
- `MerlinIntentRouterTests`: time/date/timezone routing, avoiding false time matches, memory/RAM distinction, app control, URL navigation, emergency commands, general chat fallback.
- `CapabilityClassifierTests`: missing domains for news/web/email/file, unsupported destructive actions, gibberish, harmless conversation.
- `LocalAIIntentParserTests`: JSON parsing, allowed intents, capability validation, prompt contents, missing/unsupported/unknown handling.
- `OpenApplicationToolTests`: app command handling, confirmation staging, trusted app execution, browser fallback confirmation.
- `OpenUrlToolTests`: URL normalization, blocked schemes, invalid local paths/UNC, browser-name-to-dot-com.
- `SystemResourceToolTests`: time/date/timezone tool execution.
- `ConfirmationToolTests` and `ConfirmationServiceTests`: confirmation phrases, choices, cancellation, pending candidate names.
- `WebSocketHandlerTests`: protocol/response behavior.

Missing tests for the next routing stage:

- Ambiguous "look up/find/search/check" phrases across file/email/calendar/memory/web/repo.
- Candidate score lists and confidence margins.
- Scope extraction separate from action extraction.
- Safety classification separate from capability classification.
- `web_search` vs `web_research` vs `codex_research`.
- `codex_research` vs `codex_implementation`.
- First-time private read permission prompts.
- Destructive dry-run vs execution confirmation.

## Important Files And Responsibilities

- `Merlin.Backend/Program.cs`: DI, options, HTTP/WebSocket endpoints, tool/parser registration.
- `Merlin.Backend/WebSocket/WebSocketHandler.cs`: WebSocket protocol, voice-stream envelopes, calls `CommandRouter`, emits responses and visual state.
- `Merlin.Backend/Services/CommandRouter.cs`: active routing orchestrator from parsed intent to tool execution/non-execution response.
- `Merlin.Backend/Services/HybridIntentParser.cs`: parser order, pending confirmation handling, pending interaction handling, rule/hierarchical/LocalAI/fallback selection.
- `Merlin.Backend/Services/RuleBasedIntentParser.cs`: deterministic command parsing and some missing/unsupported detection.
- `Merlin.Backend/Services/LocalAIIntentParser.cs`: Ollama-based strict JSON intent classifier.
- `Merlin.Backend/Services/CapabilityClassifier.cs`: deterministic fallback for unknown/missing/unsupported/general conversation.
- `Merlin.Backend/Services/IntentRouting/MerlinIntentRouter.cs`: newer hierarchical route engine.
- `Merlin.Backend/Services/IntentRouting/DomainRouter.cs`: domain scoring.
- `Merlin.Backend/Services/IntentRouting/CapabilityRouter.cs`: candidate generation and candidate scoring.
- `Merlin.Backend/Services/IntentRouting/DeterministicIntentClassifier.cs`: deterministic candidate selection.
- `Merlin.Backend/Services/IntentRouting/RouteDecisionIntentMapper.cs`: bridge from newer `RouteDecision` to old `IntentParseResult`.
- `Merlin.Backend/Services/IntentRouting/CapabilityRegistry.cs`: hardcoded router capability definitions.
- `Merlin.Backend/Services/ToolRegistry.cs`: command-to-tool lookup.
- `Merlin.Backend/Tools/*`: concrete tool implementations.
- `Merlin.Backend/Services/ConfirmationService.cs`: pending confirmation store.
- `Merlin.Backend/Tools/ConfirmationTool.cs`: confirmation execution.
- `Merlin.Backend/Models/AssistantResponse.cs`: backend response shape consumed by frontend.
- `Merlin.Backend/Models/RouteDecision.cs`: current structured route decision in hierarchical router.
- `Merlin.Backend/Models/CapabilityDomain.cs`: config-backed capability domain shape.
- `Merlin.Frontend/Scripts/Main.gd`: frontend response rendering and orb state transitions.
- `Merlin.Frontend/Scripts/MerlinWebSocketClient.gd`: WebSocket packet routing.
- `docs/RepositoryMap.md`: current maintainer map.
- `Merlin.ToDo/merlin_capability_specs/00_SharedCapabilityFoundation.md`: existing design notes for capability maturity, permissions, safety, audit, voice UX, and tests.

## Strengths Of The Current Design

- The active request path is understandable and testable.
- Tool execution is centralized in `CommandRouter` and `ToolRegistry`.
- Tools expose metadata for discovery.
- Non-executable capabilities are represented honestly through `missing_capability` and `unsupported_action`.
- `ResponseType` gives frontend/orb a simple semantic channel.
- Confirmation is already modeled as a pending staged action with expiry.
- LocalAI intent parsing is constrained by allowed intents, capability domain validation, and tool command validation.
- DeepInfra is kept in general conversation, not used as an unchecked action executor.
- The newer `MerlinIntentRouter` already proves the system can support domain scoring and candidate routing without replacing `CommandRouter`.

## Weaknesses / Fragile Areas

- Two capability catalogs exist: config `CapabilityDomains` and hardcoded `CapabilityRegistry`.
- `RouteDecision` is richer than `IntentParseResult` in some ways, but most of that richness is lost when mapped back to normalized commands.
- `CommandRouter` still executes by string command matching rather than by structured route + arguments.
- `ToolRegistry.FindTool` returns the first `CanHandle` match, so overlapping command grammars can become order-sensitive.
- Current scope detection is local and partial; it is not a first-class model.
- Current multi-capability scoring exists only in `MerlinIntentRouter` internals and is not surfaced to `CommandRouter`, responses, or tests.
- `CapabilityClassifier` uses small keyword lists for missing/unsupported fallback.
- `RuleBasedIntentParser` has hardcoded phrase lists for missing and unsupported capabilities.
- `LocalAIIntentParser.AllowedIntents` is hardcoded and must be updated for new executable intents.
- Safety is mixed into routing and tools, not a separate classification/gate.
- Confirmation supports app/URL actions well but not generic permission scopes, private reads, write staging, dry-run plans, or destructive action semantics.
- `system.get_cpu`, `system.get_memory`, and related capabilities exist in `CapabilityRegistry`, but `SystemResourceTool` only executes time/date/timezone today. Current router tests expect CPU/memory/disk route decisions, but unless mapped to a supported normalized command/tool, active execution will not work for those.
- `assistant.stop`, audio, memory search/save/forget, app close/focus, battery/network are in `CapabilityRegistry` but are not mapped/executable in the older tool layer.
- `RouteDecisionIntentMapper` currently maps unknown route decisions to `missing_capability` if `ShouldExecuteTool` is true, which can produce odd results for registry capabilities that have no tool mapping.

## Extension Points We Should Reuse

- `CapabilityDomains` in config for user-facing capability status, missing messages, and safety labels.
- `CapabilityRegistry`/`MerlinIntentRouter` as the place to evolve hierarchical domain/capability routing.
- `CommandRouter` as the central execution/non-execution response boundary.
- `ToolRegistry`/`ITool` for concrete capability execution.
- `ToolExecutionContext` as a small bridge that can be expanded with structured route metadata.
- `ResponsePolisher` for friendly missing/unsupported wording.
- `ConfirmationService` and `PendingInteractionService` for staged user decisions.
- `AssistantResponse`/`ResponseType` for frontend state.
- Existing acknowledgement/progress speech hooks in `CommandRouter`.
- Existing tests around `HybridIntentParser`, `MerlinIntentRouter`, `CapabilityClassifier`, `LocalAIIntentParser`, and tools.

## Places We Should Not Redesign Yet

- Do not replace `CommandRouter`; extend it to accept richer route metadata while preserving current behavior.
- Do not remove `RuleBasedIntentParser`; keep it for high-confidence local commands.
- Do not make DeepInfra the default router for every message.
- Do not bypass `ToolRegistry` or confirmation services for new tools.
- Do not add a giant verb-only keyword router for "look up/search/find/check".
- Do not implement write/destructive capabilities until read-only/dry-run routing and confirmation semantics are tested.
- Do not make web search the default for unknown questions; preserve general conversation and unknown/missing distinction.

## Recommended Extension Strategy

Extend the current architecture in layers:

1. Unify capability metadata enough that config domains and router definitions refer to the same stable capability ids.
2. Add a richer route result behind `MerlinIntentRouter` while preserving `IntentParseResult` compatibility.
3. Add scope detection before capability selection: target/scope should influence routing as much as the verb.
4. Add safety classification after capability/scope detection and before tool execution.
5. Add new tools as narrow vertical slices: public web search first, then web research, then private read-only file/email/calendar.
6. Reuse `missing_capability`/`unsupported_action` for recognized but unavailable domains.
7. Expand tests before adding execution for risky capabilities.

The practical migration path is to let `HybridIntentParser` keep returning `IntentParseResult` initially, but add optional route metadata to `IntentParseResult` or introduce a parallel `CapabilityRouteResult` carried through `ToolExecutionContext` and `AssistantResponse` diagnostics/debug fields.

## Proposed Capability Routing Model

A least-disruptive model:

- Keep `CommandRouter.RouteAsync` as the execution boundary.
- Make `MerlinIntentRouter` produce a richer `CapabilityRouteResult`.
- Map high-confidence executable route results to old normalized commands only where old tools require it.
- For new tools, pass structured arguments through `ToolExecutionContext`.
- Keep `RuleBasedIntentParser` for high-confidence local commands, but let the hierarchical router classify ambiguous capability/scope requests before LocalAI.
- Keep LocalAI as a fallback classifier for cases where deterministic scope detection cannot decide, but limit it to candidate capabilities rather than every tool.

Recommended capability ids:

- `general_conversation`
- `open_app`
- `open_url`
- `system_status`
- `system_resource`
- `memory_lookup`
- `file_access`
- `web_search`
- `web_research`
- `email`
- `calendar`
- `codex_research`
- `codex_implementation`
- `system_settings`
- `software_installation`
- `destructive_file_actions`

Existing ids can be preserved through aliases:

- `application_launch` -> `open_app`
- `url_opening` -> `open_url`
- `diagnostics` -> `system_status`
- `destructive_file_action` -> `destructive_file_actions`

## Proposed Route Result Shape

The proposed record in the task fits Merlin's direction:

```csharp
public sealed record CapabilityRouteResult(
    string Intent,
    string Action,
    string TargetScope,
    string RecommendedCapability,
    double Confidence,
    bool RequiresExternalInfo,
    bool RequiresRepoContext,
    SafetyLevel SafetyLevel,
    string? ClarifyingQuestion,
    IReadOnlyList<CapabilityScore> CandidateScores);
```

Recommended additions for Merlin:

- `string? NormalizedCommand` for backward compatibility with existing tools.
- `IReadOnlyDictionary<string, string> Arguments` for tool-specific parsed values.
- `bool ShouldExecuteTool` to match current `RouteDecision`.
- `string Reason` for current logging/test style.
- `string? CapabilityName` for frontend/debug display.
- `bool IsImplemented` or `CapabilityAvailability Availability` so missing/unsupported remains first-class.

Candidate score shape:

```csharp
public sealed record CapabilityScore(
    string CapabilityId,
    string TargetScope,
    double Score,
    string Reason);
```

This is a natural evolution of current `DomainScore`, `CapabilityCandidate`, and `IntentClassificationResult`.

## Proposed Scope Detection Model

Add a small deterministic scope detector before final capability classification. It should detect noun/target context, not just verbs.

Suggested target scopes:

- `web`: public internet/current public information.
- `local_files`: user filesystem, folders, downloads, documents.
- `project_repo`: current repository/workspace, codebase files, docs.
- `calendar`: meetings, schedule, availability, events.
- `email`: emails, inbox, school/work sender, drafts.
- `memory`: saved memory, past conversations, "what did we discuss".
- `system`: CPU/RAM/disk/time/settings/device status.
- `application`: apps/windows/program launch/focus.
- `conversation`: general explanation/reasoning without external state.
- `unknown`: insufficient target.

Example routing:

- "look up folder x" -> action `lookup`, scope `local_files`, capability `file_access`.
- "find out where this file exists" -> action `find`, scope `local_files`, capability `file_access`.
- "look up my meeting tomorrow" -> scope `calendar`, capability `calendar`.
- "look up the email from school" -> scope `email`, capability `email`.
- "find out what we discussed yesterday" -> scope `memory`, capability `memory_lookup`.
- "look up current DeepInfra pricing" -> scope `web`, capability `web_research`.
- "find out whether faster-whisper beam_size affects VRAM" -> scope `web` or `project_repo` depending on whether "our setup/repo" appears; capability `web_research` or `codex_research`.

The current system partially supports this for time/RAM/app/URL but not as a reusable model.

## Proposed Safety Classification Model

Add safety classification separate from capability selection:

- `safe_readonly`: public read or local status read. Example: web search, time, CPU usage.
- `private_read`: reads private local/account data. Example: files, email, calendar.
- `external_request`: sends a query to a web/API provider.
- `requires_confirmation`: staged write or sensitive first-time read.
- `destructive`: delete/move/overwrite/high-risk file actions.
- `privileged`: OS settings, admin changes, installation.
- `unsupported`: recognized but intentionally unavailable.

Safety gate order:

1. Route capability and scope.
2. Determine whether capability is implemented.
3. Classify safety level.
4. Check permission/credentials.
5. Decide execute, ask clarification, request permission, stage confirmation, or refuse.
6. Only then call the tool.

This should sit between route result and tool execution, most likely in/near `CommandRouter`, using `ConfirmationService` for staged actions.

## Proposed Test Matrix

Add tests for each ambiguous phrase in both current and proposed behavior.

Required examples and likely current behavior:

| User text | Likely current route today | Proposed route later |
|---|---|---|
| `open Chrome` | `open_application` via `RuleBasedIntentParser`/`OpenApplicationTool`; confirmation if discovered/untrusted. | `open_app`, scope `application`, confirmation if not trusted. |
| `open github.com` | `open_url` via rule parser or hierarchical router, then `OpenUrlTool`. | `open_url`, scope `web`, safe URL open. |
| `what time is it` | `system_resource_query` -> `SystemResourceTool` current time. | `system_resource`, scope `system`, `safe_readonly`. |
| `what is my current CPU usage` | `MerlinIntentRouter` may choose `system.get_cpu`, but `RouteDecisionIntentMapper` does not map it and `SystemResourceTool` cannot execute CPU today, so active behavior likely falls through to LocalAI/fallback or unknown/missing depending parser path. | `system_resource`, scope `system`, CPU provider, `safe_readonly`. |
| `what did we discuss yesterday` | Likely general conversation via `CapabilityClassifier`; memory system may be included in DeepInfra prompt but no explicit memory lookup route. | `memory_lookup`, scope `memory`, maybe private read, use conversation summaries. |
| `please look up folder x` | Likely general conversation or missing capability only if file keywords trigger fallback; rule list does not include this exact phrase. | `file_access`, scope `local_files`, private read/clarify path. |
| `can you find out where this file exists` | Likely missing capability via `CapabilityClassifier` because `file` appears, unless LocalAI routes differently. | `file_access`, scope `local_files`, private read. |
| `look up my meeting tomorrow` | Likely general conversation or missing if LocalAI chooses `calendar`; deterministic fallback does not detect meeting. | `calendar`, scope `calendar`, private read. |
| `look up the email from school` | Likely missing capability via `CapabilityClassifier` if email term appears. | `email`, scope `email`, private read. |
| `look up current DeepInfra pricing` | Likely general conversation; chat prompt should say no web access rather than invent current info. | `web_research`, scope `web`, external request with sources. |
| `search the web for chatterbox turbo latency` | `CapabilityClassifier` detects `web_search` missing if fallback is reached. | `web_search` or `web_research`, scope `web`. |
| `find official Godot docs for transparent windows` | Likely general conversation; may not be recognized as web because no "web/internet" term. | `web_research`, scope `web`, prefer official docs. |
| `find out whether faster-whisper beam_size affects VRAM` | Likely general conversation. | `web_research`; `codex_research` if repo/setup context is present. |
| `check if our Chatterbox setup is wrong compared to official docs` | Likely general conversation; may use memory/repo context only indirectly if in prompt, not file access. | `codex_research`, scopes `project_repo` + `web`, read-only. |
| `fix our Chatterbox setup based on the docs` | Likely general conversation; no implementation tool. | `codex_implementation`, scopes `project_repo` + `web`, requires repo context and confirmation before writes depending host mode. |
| `install this package` | Likely unsupported only if exact unsupported phrase in rule/fallback catches `install`; `CapabilityClassifier` currently destructive list does not include install. LocalAI prompt can classify as unsupported. | `software_installation`, scope `system`, privileged/confirmation or unsupported until implemented. |
| `delete all duplicate files in downloads` | Rule/fallback likely unsupported due delete/files/downloads terms, though not exact phrase everywhere. | `destructive_file_actions`, scope `local_files`, destructive; dry-run first, hard confirmation before reversible delete. |
| `change my default microphone` | Likely general conversation unless LocalAI classifies `system_settings` unsupported. | `system_settings`, scope `system`, privileged; unsupported or allowlisted confirmed setting later. |

Specific test groups:

- Verb + scope ambiguity: "look up", "find out", "search", "check".
- Private vs public read: email/calendar/files vs web.
- Repo-aware vs web-only: "our setup", "this repo", "based on official docs".
- Current/live info vs general explanation.
- Read-only vs write/fix/install/delete.
- Low confidence and clarification questions.
- Candidate score margins for near ties.

## Implementation Phases

Phase 1: Routing metadata foundation

- Add route result/candidate/safety models.
- Add scope detector with tests for required examples.
- Add compatibility mapping to `IntentParseResult` and old normalized commands.
- Surface optional route metadata in debug/diagnostics without changing frontend behavior.

Phase 2: Catalog alignment

- Align `CapabilityDomains` and `CapabilityRegistry` ids or add explicit aliases.
- Add safety level enum/string normalization.
- Expand `ToolMetadata` with capability id, safety, private read, write, credentials, and confirmation flags.

Phase 3: Public web read-only

- Implement `web_search` as a narrow read-only tool.
- Add `web_research` as a source-aware composition layer or later enhancement.
- Ensure current/live info routes to web only when target scope is public web.

Phase 4: Project/repo research

- Add `codex_research` route for repo + docs comparisons.
- Keep implementation/write actions separate as `codex_implementation`.
- Require explicit user intent before edits.

Phase 5: Private read-only capabilities

- Add `file_access` read/list/search with folder-bound permissions.
- Add `calendar` read-only.
- Add `email` metadata/search/read with privacy gates.

Phase 6: Staged write/high-risk capabilities

- Add draft-only email/calendar writes.
- Add allowlisted system settings.
- Add package install planning before execution.
- Add destructive file dry-run/quarantine/recycle-bin flow.

## Open Questions

- Should `CapabilityDomains` become the single source of truth for both router and status output, with `CapabilityRegistry` generated or configured from it?
- Should `IntentParseResult` be expanded, or should `HybridIntentParser` eventually return a new `CapabilityRouteResult`?
- Should tool matching move from `CanHandle(string)` to `CanHandle(CapabilityRouteResult)` while preserving string compatibility?
- How should Merlin distinguish `web_search` from `web_research` in UX: quick result list vs synthesized cited answer?
- Should `codex_research` and `codex_implementation` live in Merlin itself, or should they be explicit delegated modes/tools with their own permission boundary?
- What is the first acceptable private-read permission UX for file/email/calendar in the orb?
- Should safety levels be strings for config flexibility or an enum for compile-time safety?
- How should candidate scores be exposed: logs only, diagnostics/status, frontend debug panel, or all three?
- Should CPU/RAM/disk capabilities already present in `CapabilityRegistry` be mapped into `SystemResourceTool` before broader capability work?
