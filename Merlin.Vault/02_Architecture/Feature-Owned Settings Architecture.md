---
type: architecture
status: current
area: cross-cutting
tags:
  - merlin
  - architecture
  - settings
  - configuration
---

# Feature-Owned Settings Architecture

## Purpose

The current root `appsettings.json` has become a global mountain of parameters. It mixes host settings, database settings, model providers, streaming, acknowledgement speech, interruption handling, voice input, chat UI, browser workspace, web destinations, GPU scheduling, speech presence, barge-in, STT, Piper, Chatterbox, capability domains, vision, and application launch data.

Feature-owned settings split that global file into module-owned JSON files.

The rule:

```text
The module that owns behavior owns the settings.
```

Implementation status: initial migration implemented in [[RUN-2026-07-07-012 Feature-Owned Settings Migration]].

## Goals

1. Make settings discoverable by feature area.
2. Reduce accidental tuning of the wrong subsystem.
3. Prepare the repo for `Merlin.Host`, `Merlin.Kernel`, `Merlin.Modules.*`, and `Merlin.Adapters.*`.
4. Preserve existing section names during the first migration to avoid behavior changes.
5. Add typed option validation where safe.
6. Keep environment overrides small and intentional.

## Non-Goals

1. Do not rename every option in the first pass.
2. Do not change runtime behavior.
3. Do not move trusted runtime data to DB in the first settings split.
4. Do not introduce a new custom config framework.
5. Do not delete existing option classes unless explicitly covered by a later plan.

## Planned Settings Layout

```text
Merlin.Backend/
  appsettings.json
  appsettings.Development.json

  Settings/
    README.md

    Kernel/
      kernel.settings.json
      capability-domains.settings.json

    Modules/
      Apps/
        application-launch.settings.json
        trusted-apps.settings.json

      Browser/
        browser-workspace.settings.json
        web-destinations.settings.json
        browser-safety.settings.json

      Conversation/
        conversation.settings.json
        acknowledgement-speech.settings.json
        responsive-feedback.settings.json
        streaming-responses.settings.json

      Memory/
        memory.settings.json
        core-memory.settings.json
        trusted-registry.settings.json

      Web/
        web-search.settings.json
        web-research.settings.json

      Voice/
        voice-input.settings.json
        speech-presence.settings.json
        barge-in.settings.json
        interruption-handling.settings.json
        stt.settings.json
        tts.settings.json
        piper.settings.json
        chatterbox.settings.json
        gpu-scheduling.settings.json

      Vision/
        vision.settings.json

    Adapters/
      DeepInfra/
        deepinfra.settings.json

      Ollama/
        ollama.settings.json

      BrowserHost/
        browser-host.settings.json

      Godot/
        godot-websocket.settings.json

      WindowsAudio/
        windows-audio.settings.json
```

## Root AppSettings Target

The root file should eventually contain only host/global startup concerns:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Merlin": {
    "Runtime": {
      "Mode": "Legacy",
      "ShadowNextEnabled": false,
      "HandledCapabilities": []
    },
    "Modules": {
      "Kernel": true,
      "Conversation": true,
      "Apps": true,
      "Memory": true,
      "Web": true,
      "Browser": true,
      "Voice": true,
      "Vision": true
    }
  }
}
```

## Configuration Loader

Implemented code atlas: [[MerlinConfigurationBuilderExtensions]]

Avoid a giant list in `Program.cs`.

Add an extension like:

```csharp
public static class MerlinConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddMerlinSettings(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment)
    {
        var env = environment.EnvironmentName;

        return configuration
            .AddMerlinSettingsFile("Settings/Kernel/kernel", env)
            .AddMerlinSettingsFile("Settings/Kernel/capability-domains", env)
            .AddMerlinSettingsFile("Settings/Modules/Apps/application-launch", env)
            .AddMerlinSettingsFile("Settings/Modules/Browser/browser-workspace", env)
            .AddMerlinSettingsFile("Settings/Modules/Browser/web-destinations", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/voice-input", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/barge-in", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/speech-presence", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/interruption-handling", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/stt", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/tts", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/piper", env)
            .AddMerlinSettingsFile("Settings/Modules/Voice/chatterbox", env)
            .AddMerlinSettingsFile("Settings/Modules/Vision/vision", env)
            .AddEnvironmentVariables();
    }

    private static IConfigurationBuilder AddMerlinSettingsFile(
        this IConfigurationBuilder configuration,
        string basePath,
        string environmentName)
    {
        return configuration
            .AddJsonFile($"{basePath}.settings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{basePath}.{environmentName}.settings.json", optional: true, reloadOnChange: true);
    }
}
```

The exact implemented list is documented in `Merlin.Backend/Settings/README.md`.

## Environment Override Rule

Environment-specific files should only contain overrides.

Bad:

```text
Development file duplicates the entire BargeIn section.
```

Good:

```json
{
  "BargeIn": {
    "Enabled": false,
    "SaveDebugAudio": true,
    "DebugOverlayEnabled": true
  }
}
```

## CapabilityDomains

`CapabilityDomains` is not long-term app configuration. It is capability registry metadata.

Short term:

```text
Move to Settings/Kernel/capability-domains.settings.json
```

Long term:

```text
Delete global CapabilityDomains and let modules register capability descriptors.
```

## Settings Index

Create:

```text
Merlin.Backend/Settings/README.md
```

It should map each feature to its file, owner, option class, and validation status.

Example:

| Concern | File | Option Class | Owner |
| --- | --- | --- | --- |
| Voice input ownership | `Settings/Modules/Voice/voice-input.settings.json` | `VoiceInputOptions` | Voice module |
| Barge-in | `Settings/Modules/Voice/barge-in.settings.json` | `BargeInOptions` | Voice module |
| Browser workspace | `Settings/Modules/Browser/browser-workspace.settings.json` | `BrowserWorkspaceOptions` | Browser module |
| Application launch | `Settings/Modules/Apps/application-launch.settings.json` | `ApplicationLaunchOptions` | Apps module |
| Web search | `Settings/Modules/Web/web-search.settings.json` | `WebSearchOptions` | Web module |

## Validation

Each typed options class should eventually use:

```csharp
services
    .AddOptions<BargeInOptions>()
    .Bind(configuration.GetSection("BargeIn"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Do not add strict validation for fragile legacy sections until defaults and environment overrides are confirmed.

## Related Notes

- [[Feature-Owned Settings Migration Plan]]
- [[Modular Runtime Architecture]]
- [[ADR-0008 Feature-Owned Settings Files]]
