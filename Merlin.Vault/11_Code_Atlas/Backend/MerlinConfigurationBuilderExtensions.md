---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
  - configuration
  - modular-runtime
---

# MerlinConfigurationBuilderExtensions

## File

`Merlin.Backend/Configuration/MerlinConfigurationBuilderExtensions.cs`

## Purpose

Owns Merlin backend configuration loading order for feature-owned settings files.

## Load Order

`UseMerlinConfiguration` rebuilds application configuration in this order:

1. `appsettings.json`
2. base feature settings under `Settings/**/*.settings.json`
3. `appsettings.{Environment}.json`
4. environment feature settings under `Settings/**/*.{Environment}.settings.json`
5. user secrets in Development
6. environment variables
7. command-line arguments

This preserves the old root-file behavior while allowing feature-owned files to replace the global settings mountain.

## Main APIs

| API | Role |
| --- | --- |
| `UseMerlinConfiguration(ConfigurationManager, IHostEnvironment, string[])` | Rebuilds app configuration with root, feature-owned settings, environment overrides, user secrets, env vars, and command-line args. |
| `AddMerlinSettings(IConfigurationBuilder)` | Adds all base feature-owned settings files. |
| `AddMerlinSettings(IConfigurationBuilder, string)` | Adds all environment-specific feature-owned settings files. |

## Related Files

- `Merlin.Backend/Settings/README.md`
- `Merlin.Backend/appsettings.json`
- `Merlin.Backend/appsettings.Development.json`
- `Merlin.Backend.Tests/MerlinSettingsConfigurationTests.cs`

## Tests

`MerlinSettingsConfigurationTests` verifies required feature-owned sections load and Development overrides still win.

## Related Notes

- [[Feature-Owned Settings Architecture]]
- [[ADR-0008 Feature-Owned Settings Files]]
- [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]]
