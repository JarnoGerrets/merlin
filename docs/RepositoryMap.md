# Merlin Repository Map

Use this file to find the right code quickly. Inspect only the sections relevant to the current task.

## Backend Entrypoint

- `Merlin.Backend/Program.cs`
  - Registers DI, options, tools, parsers, WebSocket support, and `/ws`.
  - Inspect when adding/removing services or options. Keep minimal.

## WebSocket Handling

- `Merlin.Backend/WebSocket/WebSocketHandler.cs`
  - Accepts WebSocket connections, deserializes requests, calls CommandRouter, serializes responses.
  - Inspect for protocol, JSON, connection, or invalid-message behavior.

## Command Routing

- `Merlin.Backend/Services/CommandRouter.cs`
  - Converts parsed intent into tool execution or structured non-execution responses.
  - Inspect for response shaping, error codes, responseType, correlation IDs, and tool execution flow.

## Intent Parsing

- `Merlin.Backend/Services/HybridIntentParser.cs`
  - Orchestrates trusted command, rule-based, LocalAI, and fallback classification.
  - Inspect for parser order and confidence behavior.

- `Merlin.Backend/Services/TrustedCommandIntentParser.cs`
  - Resolves trusted saved command mappings before other parsers.
  - Inspect for trusted command behavior.

- `Merlin.Backend/Services/RuleBasedIntentParser.cs`
  - Handles deterministic known commands and high-confidence executable intents.
  - Inspect for exact tool commands and conservative rules.

- `Merlin.Backend/Services/LocalAIIntentParser.cs`
  - Uses Ollama to classify semantic intent into structured JSON.
  - Inspect for prompt, allowed intents, capability domains, and validation.

## Capability Classification

- `Merlin.Backend/Models/CapabilityDomain.cs`
  - Domain metadata model for implemented, missing, and unsupported capabilities.

- `Merlin.Backend/Configuration/CapabilityOptions.cs`
  - Default capability domain catalog.

- `Merlin.Backend/Services/CapabilityClassifier.cs`
  - Final deterministic fallback for gibberish, obvious destructive actions, obvious missing domains, and conversation.
  - Inspect when fallback classification is wrong.

- `Merlin.Backend/appsettings.json`
- `Merlin.Backend/appsettings.Development.json`
  - Runtime capability domains and LocalAI/application settings.

## Response Polishing

- `Merlin.Backend/Services/ResponsePolisher.cs`
  - May only change response message text.
  - Inspect for final wording of missing capability, unsupported action, unknown input, and LocalAI unavailable messages.

## Tools

- `Merlin.Backend/Tools/ITool.cs`
  - Tool contract.

- `Merlin.Backend/Tools/OpenApplicationTool.cs`
  - Opens configured/trusted/confirmed apps.

- `Merlin.Backend/Tools/OpenUrlTool.cs`
  - Opens safe HTTP/HTTPS URLs.

- `Merlin.Backend/Tools/ToolDiscoveryTool.cs`
  - Lists available tools.

- `Merlin.Backend/Tools/StatusTool.cs`
  - Returns diagnostics.

- `Merlin.Backend/Tools/ConfirmationTool.cs`
  - Confirms pending safe actions.

- `Merlin.Backend/Tools/GeneralConversationTool.cs`
  - Routes general conversation to LocalAI chat.

## Application Resolution

- `Merlin.Backend/Services/ApplicationResolver.cs`
  - Resolves requested application names from config, trusted apps, Start Menu, and PATH.
  - Inspect for application discovery and candidate matching.

- `Merlin.Backend/Configuration/ApplicationLaunchOptions.cs`
- `Merlin.Backend/Configuration/ApplicationLaunchTarget.cs`
  - Configured application mappings and aliases.

## Confirmation / Trusted Mappings

- `Merlin.Backend/Services/ConfirmationService.cs`
  - Stores pending confirmations in memory.

- `Merlin.Backend/Services/TrustedApplicationStore.cs`
  - Persists trusted application mappings.

- `Merlin.Backend/Services/TrustedCommandStore.cs`
  - Persists trusted command mappings.

## LocalAI / Ollama

- `Merlin.Backend/Configuration/LocalAIOptions.cs`
  - Ollama endpoint/model/settings.

- `Merlin.Backend/Services/OllamaLocalAIClient.cs`
  - HTTP client for Ollama.

- `Merlin.Backend/Services/LocalAIHealthService.cs`
- `Merlin.Backend/Services/LocalAIWarmupHostedService.cs`
  - LocalAI health and warmup.

- `Merlin.Backend/Services/LocalAIChatService.cs`
  - Chat generation and conversation context injection.

- `Merlin.Backend/Configuration/merlin-constitution.md`
- `Merlin.Backend/Services/AssistantPolicyProvider.cs`
  - Central assistant policy loaded into LocalAI prompts.

## Conversation / Session / Memory

- `Merlin.Backend/Services/ConversationSessionService.cs`
  - Session-scoped working memory and running summary.

- `Merlin.Backend/Services/ConversationSummaryStore.cs`
  - Persistent distilled conversation summaries.

- `Merlin.Backend/Services/LongTermMemoryStore.cs`
  - Persistent structured memory records.

- `Merlin.Backend/Services/MemoryExtractionService.cs`
  - Creates approval-required memory candidates.

## Configuration

- `Merlin.Backend/appsettings.json`
  - Default backend configuration.

- `Merlin.Backend/appsettings.Development.json`
  - Local development overrides, including LocalAI.

## Frontend

- `Merlin.Frontend/Scripts/Main.gd`
  - Main Godot UI behavior, command-center styling, notification rendering, assistant log rendering, typewriter response display, responseType display, tools panel, debug info, and CoreOrb state coordination.

- `Merlin.Frontend/Scripts/CoreOrb.gd`
  - Layered state-driven animated orb component for idle, thinking, speaking, tool execution, and error states.

- `Merlin.Frontend/Scenes/CoreOrb.tscn`
  - Scene for the CoreOrb component and its visual layers.

- `Merlin.Frontend/Scripts/MerlinWebSocketClient.gd`
  - WebSocket connection, sending, receiving, state handling.

- `Merlin.Frontend/Main.tscn`
  - Main command-center scene layout with top status strip, centered CoreOrb, activity panel, notification panel, assistant log/tools panel, command input, and overlay container.

## Tests

- `Merlin.Backend.Tests/*Tests.cs`
  - Backend unit tests by component.

- `Merlin.Backend.Tests/Test*.cs`
  - Test helpers and fake options/services.

Run:

```powershell
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```
