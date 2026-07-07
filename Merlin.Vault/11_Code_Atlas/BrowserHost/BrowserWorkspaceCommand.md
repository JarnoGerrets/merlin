---
type: code-atlas
status: current
project: Merlin.BrowserHost
tags:
  - merlin
  - code-atlas
---

# BrowserWorkspaceCommand

## File

`Merlin.BrowserHost/BrowserWorkspaceCommand.cs`

Verified present in current repo.

## Purpose

Typed representation of JSON commands sent by backend to BrowserHost stdin, including navigation, page actions, pointer overlay, and snapshot options.

## Related Features

- [[Browser Workspace]]
- [[Browser Control]]
- [[Browser Pointer Overlay]]
- [[Browser Page-Aware Control]]

## Main Types / Classes

- `BrowserWorkspaceCommand` and related command/script models in BrowserHost.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `BrowserWorkspaceCommand` record | internal | Defines command type plus optional URL/query/action/element/request/pointer fields. | JSON serializer | BrowserWorkspaceForm.HandleCommand | Must match backend nested command record. |
| `BrowserPageSnapshotRequestOptions` record | internal | Carries snapshot request options such as request id, text limits, and element limits. | JSON serializer | page snapshot command handler | Must match backend options. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| record properties | nullable primitives/models | Command payload from backend. | form handlers | JSON deserialization | per command |

## Dependencies

| Dependency | Used For |
| --- | --- |
| System.Text.Json | stdin command deserialization. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| typed command object | BrowserWorkspaceForm | In-memory command dispatch. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| JSON command line | backend stdin writer | deserialization |

## External Side Effects

No side effects by itself.

## Safety / Guardrails

BrowserHost should stay a scoped browser executor. It should not decide assistant intent or safety policy; backend services decide that before sending commands.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| Backend protocol tests | Indirect via BrowserWorkspaceService command creation. | No BrowserHost deserialization unit test. |

## Known Risks / Fragility

Property name drift between backend and host silently breaks commands.

## Change Notes for Agents

Keep BrowserHost command JSON compatible with `BrowserWorkspaceService`. If a payload shape changes, update [[Backend BrowserHost Commands]] and backend tests/atlas notes together.
