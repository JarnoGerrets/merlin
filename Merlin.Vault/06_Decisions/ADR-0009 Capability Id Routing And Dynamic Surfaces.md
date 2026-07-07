---
type: adr
status: proposed
tags:
  - merlin
  - adr
  - routing
  - surfaces
  - capabilities
---

# ADR-0009 Capability Id Routing And Dynamic Surfaces

## Status

Proposed.

## Context

Current Merlin routing still relies partly on central routing logic, intent names, tool matching, and static/limited surface concepts. As more features are added, command behavior becomes harder to extend safely.

Future Merlin needs context-aware routing across dashboard, browser, Spotify widget, Discord, file browser, Steam, WhatsApp, and future surfaces.

## Decision

Route to explicit capability IDs.

Examples:

```text
app.open
app.close
browser.media.pause
browser.page.click
memory.search
web.search
conversation.chat
voice.stop_speaking
```

Use a dynamic surface registry rather than a fixed enum-style list.

Examples:

```text
dashboard.main
browser.workspace
browser.tab.youtube
spotify.widget
discord.channel
windows.file_explorer
```

Modules register their own capabilities and surfaces.

The kernel dispatches selected capability IDs to handlers and uses surface context to resolve ambiguous commands.

## Consequences

Positive:

- new features can add capabilities without central switchboards;
- active surface can disambiguate commands like `pause`;
- safety can run against explicit capability IDs;
- shadow traces become more precise.

Negative:

- routing contracts must be introduced carefully;
- legacy intent names must be mapped during migration;
- capability ownership table must be kept current.

## Related Notes

- [[Modular Runtime Architecture]]
- [[Kernel Brainstem Architecture]]
- [[Dynamic Surface Registry Plan]]
- [[Capability Routing And Module Registration Plan]]
