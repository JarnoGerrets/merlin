---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# BrowserPageSafetyGuard

## File

`Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPageSafetyGuard.cs`

Verified present in current repo.

## Purpose

Classifies page-aware browser actions as safe, needs confirmation, or blocked using action type, element text/title/href/role, sensitive fields, executable downloads, and risky phrases.

## Related Features

- [[Browser Pinch Click]]
- [[Browser Scroll Gestures]]
- [[Browser Pointer Overlay]]
- [[Browser Control]]
- [[Safety and Confirmation]]

## Main Types / Classes

- `BrowserPageSafetyGuard` and directly nested/helper records or enums in the source file.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `Evaluate` | public | Builds normalized context text, applies safe phrase, sensitive-field, executable-download, confirmation, and block rules. | helper methods | BrowserWorkspaceService page actions | Central safety classifier. |
| `IsSensitiveField` | private | Detects password/payment/email/tel and sensitive form fields. | element fields | `Evaluate` | Prevents blind typing/clicking into risky fields. |
| `ContainsExecutableDownload` | private | Detects executable/script download URLs. | string checks | `Evaluate` | Blocks risky downloads. |
| `ContainsPhrase` / `Normalize` | private | Performs normalized phrase matching. | regex helpers | `Evaluate` | Keeps decisions consistent. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `SafePhrases` | array | Low-risk action phrases. | `Evaluate` | static init | process lifetime |
| `ConfirmationPhrases` | risk/phrase tuples | Terms requiring confirmation. | `Evaluate` | static init | process lifetime |
| `BlockPhrases` | risk/phrase tuples | Terms blocked outright. | `Evaluate` | static init | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `BrowserPageSafetyContext` | Supplies action, target, URL, query, nearby elements. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `BrowserPageSafetyDecision` | BrowserWorkspaceService | Level, risk, reason, message. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| page action candidate | BrowserWorkspaceService | `Evaluate` |

## External Side Effects

No direct side effects; service returns a decision only.

## Safety / Guardrails

Raw browser motion actions are not the same as page-aware DOM actions. Keep BrowserWorkspace active, bounds, focus, and confidence checks conservative.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `BrowserPageSafetyGuardTests.cs` | Safe/confirm/block decisions, sensitive fields, downloads. | No live DOM. |
| `CommandRouterTests.cs` | Indirect page-action confirmation flow. | No WebView click. |

## Known Risks / Fragility

Phrase-based safety can over-prompt or under-detect site-specific danger. Raw motion click bypasses this DOM-level guard.

## Change Notes for Agents

Read source and linked browser motion/safety flow notes before editing. Keep pure decision logic separate from BrowserHost I/O.
