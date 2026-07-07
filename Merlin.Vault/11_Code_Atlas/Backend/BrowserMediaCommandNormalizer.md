---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserMediaCommandNormalizer

## File

`Merlin.Backend/Services/Context/ActiveSurface/BrowserMediaCommandNormalizer.cs`

Verified present in current repo.

## Purpose

Normalizes spoken browser media phrases into ActiveSurface media capabilities. It is the central phrase table for pause/play/fullscreen/skip-ad variants and prevents ambiguous media words from controlling the browser unless the active surface supports the capability.

## Related Features

- [[Browser Control]]
- [[Active Surface Layer]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserMediaCommandNormalizer` implements `IBrowserMediaCommandNormalizer`.
- `BrowserMediaCommandMatch` is the value returned to routing.
- Generated regex helpers normalize whitespace and punctuation.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `TryMatchExplicit` | public | Normalizes text and matches full explicit phrases from `ExplicitPhrases`. | `Normalize`; `ExplicitPhrases.TryGetValue` | `CommandRouter`; `LiveUtteranceGate`; `WebDestinationParser` | Explicit phrases do not require an active surface check. |
| `TryMatchAmbiguous` | public | Resolves short commands such as `pause`, `play`, `fullscreen`, and `skip` only when the current `ActiveSurfaceSnapshot` has the mapped capability. | `Normalize`; `AmbiguousPhrases`; `ActiveSurfaceSnapshot.Capabilities` | `CommandRouter`; `LiveUtteranceGate` | This is the guard that keeps dashboard speech from becoming browser media control. |
| `Normalize` | public static | Lowercases, removes punctuation noise, normalizes contractions indirectly through wrappers, strips polite leading/trailing wrappers. | `StripLeadingWrappers`; `StripTrailingWrappers`; regex helpers | Tests, router, web destination parsing | Preserves command payload words after command prefixes. |
| `CommonActionForCapability` | public static | Converts ActiveSurface media capabilities to BrowserHost common-action strings like `pause`, `play`, `fullscreen`, `skip_ad`. | switch expression | `CommandRouter`; `WebDestinationParser` | Current fullscreen intent collapses here, so response wording must not rely only on this mapping. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `ExplicitPhrases` | `Dictionary<string,string>` | Exact media phrases mapped to capability constants. | matchers | static initializer | process lifetime |
| `AmbiguousPhrases` | `Dictionary<string,string>` | Short words allowed only with active-surface support. | `TryMatchAmbiguous` | static initializer | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `ActiveSurfaceSnapshot` | Supplies current surface kind and capabilities for ambiguous command disambiguation. |
| `ActiveSurfaceCapabilities` | Names the media capabilities returned by matches. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `BrowserMediaCommandMatch` | `CommandRouter` / `LiveUtteranceGate` | Capability, confidence, and reason for browser media command routing. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| Spoken text | voice routing | `TryMatchExplicit` / `TryMatchAmbiguous` |
| `ActiveSurfaceSnapshot` | `IActiveSurfaceService` or request context | `TryMatchAmbiguous` |

## External Side Effects

No direct external side effects. It only returns match data; execution happens in `CommandRouter` and `BrowserWorkspaceService`.

## Safety / Guardrails

Keep ambiguous commands gated by ActiveSurface. Add phrase variants here rather than scattering browser media text matching into lower layers. Do not make one-word commands bypass current surface context.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserMediaCommandNormalizerTests.cs` | Explicit variants, polite wrappers, ambiguous active-surface gating. | Does not prove BrowserHost DOM execution. |
| `CommandRouterTests.cs` | Confirms routed common actions and responses for browser media commands. | End-to-end WebView behavior is manual/host-level. |

## Known Risks / Fragility

Fullscreen enter/exit intent is currently flattened into a common action. Phrase additions can accidentally steal general browser history commands such as `forward` if not scoped carefully.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
