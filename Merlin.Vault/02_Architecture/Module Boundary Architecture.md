---
type: architecture
status: planned
area: cross-cutting
tags:
  - merlin
  - architecture
  - modules
  - modular-runtime
---

# Module Boundary Architecture

## Purpose

Modules are feature-owned vertical slices.

A module owns what Merlin can do in a feature area:

- capability descriptors;
- capability handlers;
- feature-specific policies;
- feature-specific state;
- feature settings;
- feature diagnostics;
- feature tests;
- migration wrappers around existing services.

The goal is that adding a new feature should not require editing every central router/gate/service.

## Module Contract

Proposed interface:

```csharp
public interface IMerlinModule
{
    string ModuleId { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void RegisterCapabilities(ICapabilityRegistry registry);

    void RegisterSurfaces(ISurfaceRegistry registry);

    void RegisterSafetyPolicies(ISafetyPolicyRegistry registry);
}
```

The exact shape may change during implementation, but the ownership rule should remain:

```text
The module that owns behavior registers the behavior.
The kernel only coordinates.
```

## Module List

### Conversation Module

Owns:

- `conversation.chat`
- `conversation.no_tool`
- `conversation.clarify`
- fallback behavior;
- model-agnostic conversational prompt assembly;
- response style/presentation hints.

Does not own:

- DeepInfra HTTP calls;
- Ollama HTTP calls;
- memory database internals;
- speech playback.

### Apps Module

Owns:

- `app.open`
- `app.focus`
- `app.close`
- trusted/default app registry seed data;
- app action policy;
- system command safety boundaries.

Does not own:

- global route pipeline;
- arbitrary shell execution;
- UI speech output.

### Memory Module

Owns:

- memory retrieval;
- memory save/forget capability;
- profile facts;
- prompt memory context provider;
- memory extraction;
- memory lifecycle/hygiene;
- memory privacy boundaries.

Does not own:

- runtime pending state;
- active turn state;
- general conversation output delivery.

### Web Module

Owns:

- `web.search`
- `web.research`
- source metadata;
- search provider policy;
- citation/source handling.

Does not own:

- browser workspace page control;
- raw browser UI actions;
- model provider implementation details.

### Browser Module

Owns:

- BrowserWorkspace behavior;
- BrowserHost IPC port usage;
- browser surface provider;
- browser page snapshot handling;
- browser media controls;
- browser page-aware actions;
- browser safety policy;
- site/profile learning later.

Does not own:

- raw native input bypassing safety;
- global command routing;
- general app control.

### Voice Module

Owns:

- voice input ownership;
- STT port usage;
- TTS/speech playback orchestration;
- barge-in;
- speech presence;
- interruption integration;
- voice state;
- self-speech suppression;
- voice diagnostics.

Does not own:

- general capability dispatch;
- browser action execution;
- model provider implementation details;
- durable memory facts.

### Vision / Motion Module

The existing planning structure often treats motion/vision separately. The final module split can either be:

```text
Merlin.Modules.Vision
Merlin.Modules.Motion
```

or:

```text
Merlin.Modules.Input.MotionVision
```

It should own:

- hand tracking sidecar usage;
- gesture state;
- motion profile activation;
- pointer/pinch/scroll semantics;
- calibration settings;
- profile-specific routing.

It must not bypass browser safety for native clicks.

## Feature Settings Ownership

Each module owns its feature settings.

Examples:

| Module | Settings |
| --- | --- |
| Apps | `application-launch.settings.json`, `trusted-apps.settings.json` |
| Browser | `browser-workspace.settings.json`, `web-destinations.settings.json`, `browser-safety.settings.json` |
| Voice | `barge-in.settings.json`, `speech-presence.settings.json`, `stt.settings.json`, `tts.settings.json`, `interruption.settings.json` |
| Memory | `memory.settings.json`, `core-memory.settings.json`, `trusted-registry.settings.json` if registry remains memory-backed |
| Web | `web-search.settings.json`, `web-research.settings.json` |
| Conversation | `conversation.settings.json`, fallback/clarification prompts/settings |

## Migration Pattern Per Module

Each module should migrate in this order:

1. define options/settings files;
2. define capability descriptors;
3. define handler interfaces;
4. wrap old service in a new handler;
5. run in shadow mode where possible;
6. enable one capability in hybrid mode;
7. add tests;
8. update vault/code atlas;
9. retire legacy route for that capability;
10. repeat.

## Avoided Anti-Patterns

| Anti-Pattern | Why Bad |
| --- | --- |
| Central `CommandRouter` switchboard for every feature. | New features remain expensive. |
| Central `CapabilityRegistry` hardcoded with all module details. | Kernel becomes feature-aware. |
| Module directly sends WebSocket events everywhere. | Output adapter/presentation gets bypassed. |
| Module directly calls DeepInfra/Ollama. | Provider swap/fallback remains tangled. |
| Browser/motion native clicks bypass BrowserPageSafetyGuard. | Safety regression. |
| Voice interruption writes directly into conversation state. | Pending ownership becomes fragile. |

## Related Notes

- [[Modular Runtime Architecture]]
- [[Feature-Owned Settings Architecture]]
- [[Browser Module Migration Plan]]
- [[Voice Module Migration Plan]]
