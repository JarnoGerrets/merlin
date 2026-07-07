---
type: current-state
status: current
area: cross-cutting
tags:
  - merlin
---

# Current Blocked Features

| Feature | Blocked by | Why blocked | Unblock condition |
| --- | --- | --- | --- |
| [[Control Profile DB]] | correction stability, browser page safety, raw motion click safety | Learning/storing controls before safety and correction are stable risks teaching wrong actions. | Passing correction/barge-in tests and a defined safety model for raw clicks. |
| [[Site Control Profiles]] | full motion control, profile DB, safety | User decided learned/site controls should come after full motion control. | Motion control profile layer stable and raw click safety solved. |
| [[Spotify Widget]] | auth/widget foundation and UI surface decisions | Plan exists but runtime widget/auth is not implemented. | Decide auth flow and widget host model. |
| [[File Browser]] | file safety and UI architecture | Future feature; no current runtime system. | Define safe file read/write/delete boundaries. |
| Deep [[External App Control]] | trust registry and safety model | Opening apps exists, but rich app control would require a safer universal UI/control layer. | Active surface/app profile design and confirmations. |
