---
type: adr
status: accepted
tags:
  - merlin
  - adr
  - settings
  - modular-runtime
---

# ADR-0008 Feature-Owned Settings Files

## Status

Accepted.

Initial implementation completed in [[RUN-2026-07-07-012 Feature-Owned Settings Migration]].

## Context

The current root appsettings files contain many unrelated feature settings, including memory, trusted registry, local/cloud LLM, streaming, acknowledgement speech, responsive feedback, interruption handling, voice input, browser workspace, web destinations, GPU scheduling, speech presence, barge-in, STT, Piper, Chatterbox TTS, capability domains, vision, and app launch.

This makes feature tuning difficult and increases the chance of changing the wrong setting.

The future modular runtime requires settings ownership to match feature ownership.

## Decision

Split settings into feature-owned JSON files under `Merlin.Backend/Settings`.

Keep existing configuration section names during the first migration to avoid runtime behavior changes.

Add a configuration loading extension, such as `AddMerlinSettings`, so `Program.cs` does not become a long list of JSON files.

Add a settings README/index mapping concern → file → option class → owner.

## Implementation Notes

- Root `appsettings.json` now keeps host/global settings only.
- Feature sections are loaded from `Merlin.Backend/Settings`.
- Section names remain unchanged.
- `UseMerlinConfiguration` preserves environment override, user-secret, environment-variable, and command-line precedence.

## Consequences

Positive:

- settings become discoverable;
- module ownership becomes clearer before runtime migration;
- environment overrides can become small and intentional;
- later module extraction is easier.

Negative:

- there will be more files;
- load order must be documented;
- accidental duplicate section definitions must be detected;
- root and development overrides must be carefully compared.

## Related Notes

- [[Feature-Owned Settings Architecture]]
- [[Feature-Owned Settings Migration Plan]]
- [[MerlinConfigurationBuilderExtensions]]
