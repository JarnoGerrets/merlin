---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# NeutralMotionProfile

## File

`Merlin.Backend/Services/Motion/Profiles/NeutralMotionProfile.cs`

Verified present in current repo.

## Purpose

Safe no-op motion profile used when the active surface is Unknown. It lets motion mode stay enabled without dispatching gestures into dashboard or browser behavior.

## Related Features

- [[Motion Control Profile Layer]]
- [[Active Surface Layer]]

## Main Types / Classes

- `NeutralMotionProfile` implements `IMotionControlProfile`.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Descriptor` | public property | Profile id `motion.neutral`, Unknown surface kind, safe-noop capability. | object initializer | registry/service/tests | Signals no pointer side effects are needed. |
| `CanHandle` | public | Returns true only for `ActiveSurfaceKind.Unknown`. | snapshot kind | registry | Fallback profile. |
| `ActivateAsync` | public | Logs activation. | logger | motion service | No side effects. |
| `DeactivateAsync` | public | Logs deactivation. | logger | motion service | No side effects. |
| `HandleGestureAsync` | public | Ignores gesture and logs safe no-op action. | logger | motion service | Does not forward to frontend/browser. |
| `OnActiveSurfaceChangedAsync` | public | No-op; motion service handles switching. | none | motion service | Safe callback. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `Descriptor` | `MotionControlProfileDescriptor` | no-op profile identity/capability. | registry | object initializer | service lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `ILogger<NeutralMotionProfile>` | Diagnostics only. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| log `MotionProfileAction` | application log | Shows ignored gesture type/pointer id. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| `VisionGestureEvent` | MotionControlModeService | `HandleGestureAsync` |

## External Side Effects

No external side effects.

## Safety / Guardrails

This profile must remain side-effect free; it is the safety fallback when surface context is unknown or stale.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `MotionControlProfileRegistryTests.cs` | Unknown surface resolves neutral. | No behavior beyond resolution. |
| `MotionControlModeServiceTests.cs` | Disabled/neutral paths indirectly. | No long-running fallback test. |

## Known Risks / Fragility

If `CanHandle` becomes too broad, it can mask dashboard/browser profile bugs. If it emits actions, unknown surface becomes unsafe.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
