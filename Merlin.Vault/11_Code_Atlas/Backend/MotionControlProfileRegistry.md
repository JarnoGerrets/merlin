---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileRegistry

## File

`Merlin.Backend/Services/Motion/MotionControlProfileRegistry.cs`

Verified present in current repo.

## Purpose

Selects the best motion profile for an ActiveSurface. It provides the dashboard/browser/neutral routing layer that keeps motion behavior context-aware.

## Related Features

- [[Motion Control Profile Layer]]
- [[Active Surface Layer]]
- [[Browser Control]]

## Main Types / Classes

- `MotionControlProfileRegistry` implements `IMotionControlProfileRegistry`.
- Returns `MotionControlProfileResolution` for `IMotionControlProfile` instances.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Resolve` | public | If an override matches a registered profile id, returns it; otherwise filters profiles by `CanHandle(surface)`, orders by descriptor priority, and falls back to Neutral. | `_profiles`; `IMotionControlProfile.CanHandle`; `LogSelected` | `MotionControlModeService` | Dashboard/browser/unknown selection is centralized here. |
| `ListProfiles` | public | Returns profile descriptors for diagnostics/UI. | `_profiles.Select` | tests/future status tools | Does not expose profile instances. |
| `LogSelected` | private | Logs profile id, surface kind, confidence and reason. | logger | `Resolve` | Helps debug unexpected profile switches. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_profiles` | `IReadOnlyList<IMotionControlProfile>` | Registered profiles ordered by DI construction and then selected by priority. | `Resolve`, `ListProfiles` | constructor | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `IEnumerable<IMotionControlProfile>` | Injected profile set: dashboard, browser workspace, neutral. |
| `ILogger<MotionControlProfileRegistry>` | Selection diagnostics. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `MotionControlProfileResolution` | `MotionControlModeService` | Profile, confidence, and reason for activation. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `ActiveSurfaceSnapshot` | Motion service | `Resolve` |
| `MotionControlProfileOverride` | command routing/debug paths | `Resolve` |

## External Side Effects

No external side effects beyond logging.

## Safety / Guardrails

Neutral fallback must remain safe-noop. Overrides should only select registered profiles; do not instantiate profile types dynamically from user text.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `MotionControlProfileRegistryTests.cs` | Dashboard resolution, browser resolution, unknown/neutral fallback, override behavior, descriptor listing. | Does not test future site/domain profiles. |

## Known Risks / Fragility

Adding future site/app profiles will require priority rules that do not starve dashboard/browser defaults. A bad `CanHandle` implementation can consume gestures for the wrong surface.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
