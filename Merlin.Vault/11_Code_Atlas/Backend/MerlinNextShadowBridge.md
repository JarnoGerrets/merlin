---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
  - modular-runtime
  - shadow
  - kernel
---

# MerlinNextShadowBridge

## Files

- `Merlin.Backend/Next/Host/MerlinNextShadowBridge.cs`
- `Merlin.Backend/Next/Host/IMerlinNextShadowBridge.cs`
- `Merlin.Backend/Next/Host/LegacyMerlinRequestAdapter.cs`
- `Merlin.Backend/Next/Kernel/Runtime/MerlinNextShadowRuntime.cs`
- `Merlin.Backend/Next/Kernel/Runtime/IMerlinNextRuntime.cs`
- `Merlin.Backend/Next/Kernel/Runtime/MerlinNextShadowTrace.cs`
- `Merlin.Backend/Services/CommandRouter.cs`

## Purpose

Provides the first read-only bridge from legacy `CommandRouter` requests into the planned Merlin Next kernel runtime.

## Current Behavior

`CommandRouter` creates a `MerlinRequest` snapshot after current request normalization and active-surface selection, then calls the optional `IMerlinNextShadowBridge`.

The production bridge starts bounded fire-and-forget shadow work only when all are true:

- `MerlinNext.Enabled == true`
- `MerlinNext.ShadowEnabled == true`
- `MerlinNext.Mode == Shadow`

`MerlinNextShadowRuntime` returns/logs a `NoDecision` trace with `ExecutionDisabledReason = disabled_shadow_mode`.

## Side Effects

No real side effects are allowed in this phase.

The shadow path must not:

- execute tools;
- open apps;
- touch BrowserHost;
- write memory;
- speak or publish UI events;
- create pending operations;
- alter cancellation, confirmation, or interruption ownership.

## Failure Behavior

Bridge startup and shadow runtime exceptions are caught and logged. Legacy routing continues.

## Tests

`MerlinNextShadowBridgeTests` covers:

- disabled-by-default bridge behavior;
- enabled Shadow mode invoking the read-only runtime;
- disabled-execution trace shape.

`CommandRouterTests` covers:

- normalized request snapshots being passed to the bridge while legacy still executes;
- bridge exceptions not breaking legacy responses.

## Related Notes

- [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]]
- [[Kernel Brainstem Architecture]]
- [[Strangler Migration Architecture]]
