---
type: architecture
status: planned
area: cross-cutting
tags:
  - merlin
  - architecture
  - adapters
  - modular-runtime
---

# Adapter Boundary Architecture

## Purpose

Adapters isolate external systems and infrastructure from Merlin feature logic.

A module should not care whether a model is DeepInfra or Ollama, whether audio comes from NAudio or another backend, whether BrowserHost talks over stdin/stdout or another IPC transport, or whether output is delivered to Godot, Discord, or a future overlay.

Adapters implement ports.

Modules and kernel consume ports.

## Adapter Categories

| Adapter | Example Port | Used By |
| --- | --- | --- |
| DeepInfra | `IReasoningModelClient` | Conversation, web research, classification. |
| Ollama | `IReasoningModelClient` / fallback provider | Conversation, classification. |
| WebSocket/Godot | `IFrontendClient`, `IOutputSink`, `IUiEventSink` | Kernel presentation, Voice, Browser, Motion. |
| BrowserHost IPC | `IBrowserHostClient` | Browser module. |
| Windows audio | `IAudioInputAdapter`, `IAudioOutputAdapter` | Voice module. |
| STT worker | `ISttClient` | Voice module. |
| TTS worker | `ITtsClient` | Voice module. |
| Vision sidecar | `IVisionSidecarClient` | Vision/Motion module. |
| Web search provider | `IWebSearchProvider` | Web module. |

## Port Shape

Ports should express Merlin concepts, not raw provider details.

Example bad dependency:

```csharp
DeepInfraClient.CreateChatCompletionAsync(...)
```

Example better dependency:

```csharp
IReasoningModelClient.GenerateAsync(ModelRequest request, CancellationToken ct)
```

Example bad Browser dependency:

```csharp
_process.StandardInput.WriteLine(JsonSerializer.Serialize(command));
```

Example better dependency:

```csharp
IBrowserHostClient.ClickElementAsync(BrowserElementTarget target, CancellationToken ct)
```

## Adapter Ownership

Adapters own:

- provider-specific configuration;
- connection details;
- retries/timeouts/circuit breakers;
- serialization/deserialization;
- process lifecycle when it is infrastructure-specific;
- protocol translation.

Adapters do not own:

- route decisions;
- safety decisions;
- user-facing phrasing;
- module policy;
- feature-specific fallback behavior unless the port explicitly owns provider fallback.

## Current-to-Planned Mapping

| Current Concern | Planned Port / Adapter |
| --- | --- |
| DeepInfra config under `Llm` | `DeepInfraAdapterOptions`, `IReasoningModelClient` implementation. |
| LocalAI/Ollama config | `OllamaAdapterOptions`, same model port. |
| BrowserHost process/protocol | `IBrowserHostClient`, `BrowserHostIpcAdapter`. |
| Godot WebSocket events | `IFrontendEventSink`, `GodotWebSocketAdapter`. |
| Python STT process | `ISttClient`, `WhisperPythonAdapter`. |
| Chatterbox/Piper TTS | `ITtsClient`, `ChatterboxAdapter`, `PiperAdapter`. |
| Vision sidecar | `IVisionSidecarClient`, `PythonVisionSidecarAdapter`. |

## Migration Approach

Do not rewrite providers first.

Use wrapping:

```text
existing concrete service
→ adapter interface
→ new module/kernel consumes interface
```

Only after the new boundaries are proven should internals be cleaned up.

## Risks

| Risk | Mitigation |
| --- | --- |
| Adapters become dumping grounds for feature logic. | Keep feature policy in modules. |
| Ports are too low-level and leak provider details. | Define ports using Merlin domain concepts. |
| Rewriting adapters breaks working external integrations. | Wrap current implementation first; refactor internals later. |
| Provider retries are duplicated. | Provider adapter owns retries/circuit breaker. |

## Related Notes

- [[Modular Runtime Architecture]]
- [[Adapter Boundary Migration Plan]]
- [[Voice Pipeline Architecture]]
- [[BrowserHost Architecture]]
