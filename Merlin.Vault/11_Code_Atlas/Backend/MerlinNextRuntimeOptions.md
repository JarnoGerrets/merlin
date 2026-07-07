---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
  - modular-runtime
  - configuration
---

# MerlinNextRuntimeOptions

## Files

- `Merlin.Backend/Next/Host/MerlinNextRuntimeOptions.cs`
- `Merlin.Backend/Next/Host/MerlinNextRuntimeMode.cs`
- `Merlin.Backend/Next/Host/MerlinNextServiceCollectionExtensions.cs`
- `Merlin.Backend/Settings/Kernel/merlin-next.settings.json`

## Purpose

Defines the inert runtime-mode configuration seam for the planned Merlin Next strangler runtime.

## Current Behavior

`AddMerlinNext` binds the `MerlinNext` configuration section to `MerlinNextRuntimeOptions` and validates:

- `HandledCapabilities` is an array;
- enabled `NextOnly` mode is rejected during the skeleton phase.

It registers the inert shadow bridge services added by Plan 013, but only configuration-gated read-only trace work can run. It does not register capability handlers, hosted execution services, hybrid dispatch, or side-effectful execution.

Default settings:

```json
{
  "MerlinNext": {
    "Enabled": false,
    "Mode": "Legacy",
    "ShadowEnabled": false,
    "HandledCapabilities": []
  }
}
```

## Runtime Impact

No external behavior changes. Legacy runtime remains the only executing path. Shadow trace work can run only when `Enabled=true`, `ShadowEnabled=true`, and `Mode=Shadow`.

## Tests

`MerlinNextRuntimeOptionsTests` covers:

- default Legacy/disabled mode;
- binding mode and handled capabilities;
- invalid mode failure;
- enabled `NextOnly` rejection.

`MerlinSettingsConfigurationTests` verifies the feature-owned `MerlinNext` section loads.

## Related Notes

- [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]]
- [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]]
- [[Modular Runtime Architecture]]
- [[Strangler Migration Architecture]]
