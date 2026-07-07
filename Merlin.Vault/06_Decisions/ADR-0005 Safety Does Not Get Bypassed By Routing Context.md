---
type: adr
status: current
area: cross-cutting
tags:
  - merlin
  - decision
---

# ADR-0005 Safety Does Not Get Bypassed By Routing Context

ActiveSurface and motion profiles decide routing and interpretation. Safety/confirmation decides whether an action may execute. Learned profiles and raw pointer clicks must not bypass safety.
