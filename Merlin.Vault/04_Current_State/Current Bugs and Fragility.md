---
type: current-state
status: current
tags:
  - merlin
  - bug
---

# Current Bugs and Fragility

See [[09_Bugs/Index|Bug Index]] for detailed entries.

Highest-impact fragility:

- Browser overlay z-order/lifecycle and close/reset state.
- DPI/multi-monitor uncertainty for native overlays and screen clicks.
- Raw motion clicks bypass browser page safety.
- Pause/play/stop routing ambiguity when active surface is wrong.
- Correction/barge-in timing tests currently fail in full backend suite.
- Dashboard gesture logic remains centralized in frontend `Main.gd`.
