---
type: roadmap
status: current
tags:
  - merlin
  - roadmap
---

# Master Roadmap

Work is grouped by dependency order.

## Foundation / Core Routing

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| Active Surface Layer | implemented | none | motion profiles, context routing | yes | n/a | [[Active Surface Layer]] |
| Motion Control Profile Layer | implemented | active surface, vision sidecar | site/app profiles | yes | n/a | [[Motion Control Profile Layer]] |
| CommandRouter decomposition | planned | stable behavior tests | cleaner future features | no | avoid refactor during feature work | [[Command Routing Architecture]] |

## Voice and Interruption

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| Stabilize correction/barge-in tests | partial | current tests | correction-driven profiles | yes | n/a | [[Correction Layer]] |
| Surface-aware ambiguous media commands | partial | active surface | better browser media UX | yes | n/a | [[Voice Interruption System]] |

## Browser Workspace

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| BrowserHost lifecycle reset | partial | BrowserWorkspaceService | reliable UI restoration | yes | n/a | [[Browser Workspace]] |
| Raw motion click safety integration | planned | BrowserPageSafetyGuard, pointer profile | learned browser profiles | yes | n/a | [[Safety and Confirmation Architecture]] |
| Site-specific YouTube controls | future | Control Profile DB, correction loop | rich media control | no | would clutter generic browser control | [[Control Profile DB]] |

## Motion Control

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| Region/pinch calibration polish | partial | vision sidecar | reliable motion | yes | n/a | [[Motion Control]] |
| Per-profile sensitivity | planned | motion profiles | app/site profiles | yes | n/a | [[Motion Control Profile Layer]] |
| App/site gesture profiles | future | Control Profile DB, active surface, safety | advanced control | no | learned profile layer missing | [[Control Profile DB]] |

## Memory and Correction

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| Correction stability | partial | tests | learning from mistakes | yes | n/a | [[Correction Layer]] |
| Correction-driven profile learning | future | Control Profile DB | adaptive UI control | no | profile DB not built | [[Control Profile DB]] |

## Widgets

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| Spotify Widget | future | widget base, Spotify API/auth | music surface profile | no | auth/control not implemented | [[Spotify Widget]] |

## External Apps

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| External app active surfaces | future | focus detection, safety | app profiles | no | surface detection missing | [[External App Control]] |
| File Browser | future | safety/confirmation, active surface | file workflows | no | destructive action risk | [[File Browser]] |

## Learned Control Profiles

| Item | Status | Depends on | Blocks | Ready? | Do not build yet reason | Relevant notes |
| --- | --- | --- | --- | --- | --- | --- |
| Control Profile DB | future | active surface, browser page-aware control, correction, motion profiles | YouTube/Spotify/app profiles | no | foundation and safety need stabilization | [[Control Profile DB]] |
