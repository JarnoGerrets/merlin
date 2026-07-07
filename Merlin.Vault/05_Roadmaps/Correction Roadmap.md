---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Correction Roadmap

## Scope

Focused roadmap for Correction.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant notes | Next action |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Stabilize correction/barge-in tests | partial | current tests | learning/site profiles | yes | Current suite has 9 failures | [[Correction Layer]], [[Voice Interruption System]] | Fix in isolation |
| Harden browser close/reset | partial | BrowserWorkspaceService, ActiveSurfaceService | reliable browser UX | yes | Known stale UI state | [[Browser Workspace]] | Sync close/reset/frontend restore |
| Add raw motion click safety | partial | BrowserPinchClickController, BrowserPageSafetyGuard | learned control | yes | Raw click bypasses page safety | [[Safety and Confirmation]], [[Browser Pinch Click]] | Add safety adapter |
| Tune motion profile config | partial | Motion profile layer | better UX | yes | Current profiles exist | [[Motion Control Profile Layer]] | Add diagnostics/config |
| Control Profile DB | future | correction, safety, page-aware control | site/app profiles | no | Foundations not stable | [[Control Profile DB]] | Do not build yet |

## Linked Implementation Plans

- [[AskClarification Dead End Fix Plan]]
- [[Correction Classification And Semantic Rewrite Plan]]
- [[Correction Regeneration Token And Short Stop Fix Plan]]
- [[Live Turn Correction Regeneration Plan]]
