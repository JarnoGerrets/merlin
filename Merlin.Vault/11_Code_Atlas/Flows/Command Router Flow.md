---
type: flow
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Command Router Flow

## Summary

CommandRouter.RouteAsync evaluates motion/browser/memory/tool paths and returns AssistantResponse.

## Current Flow

1. AssistantRequest
2. speech normalization
3. browser/motion/page/memory checks
4. tool/capability dispatch
5. response polishing
6. AssistantResponse

## Mermaid Diagram

```mermaid
flowchart LR
    N0[AssistantRequest] --> N1[speech normalization]
    N1[speech normalization] --> N2[browser/motion/page/memory checks]
    N2[browser/motion/page/memory checks] --> N3[tool/capability dispatch]
    N3[tool/capability dispatch] --> N4[response polishing]
    N4[response polishing] --> N5[AssistantResponse]
```

## Related Feature And Architecture Notes

- [[Command Routing Architecture]]
- [[CommandRouter]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
