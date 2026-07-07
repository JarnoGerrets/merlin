---
type: code-atlas
status: current
project: Merlin
tags:
  - merlin
  - code-atlas
---

# VisionSidecarClient

## File

`Merlin.Backend/Services/Vision/VisionSidecarClient.cs`

Verified present in current repo.

## Purpose

Serializes backend commands to the Python vision worker and parses JSON lines emitted by the worker into `VisionSidecarMessage` objects.

## Related Features

- [[Vision Sidecar]]
- [[Motion Control]]

## Main Types / Classes

- `VisionSidecarClient`
- `VisionSidecarMessage` model consumed by host.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `SerializeCommand` | public | Converts anonymous command payloads such as `vision.start_tracking` to a single JSON line. | `System.Text.Json.JsonSerializer` | `VisionSidecarHost.SendCommandLockedAsync` | Uses web JSON defaults. |
| `TryParseMessage` | public | Parses stdout JSON lines from Python into `VisionSidecarMessage`; returns false for invalid JSON. | `JsonSerializer.Deserialize` | `VisionSidecarHost.HandleOutputLineAsync`; tests | Preserves worker payload fields such as gesture coordinates, calibration ratios, status, errors. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `JsonOptions` | `JsonSerializerOptions` | Web defaults for command/message casing. | both methods | static initializer | process lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| `System.Text.Json` | JSON protocol encoding/decoding. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| JSON command line | Python `vision_worker.py` stdin | Serialized command string from backend. |
| `VisionSidecarMessage` | VisionSidecarHost | Parsed worker event. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| stdout line | Python worker | `TryParseMessage` |

## External Side Effects

No direct process or file side effects; host owns stdin/stdout process I/O.

## Safety / Guardrails

Keep protocol property names compatible with `vision_worker.py`. Unknown or malformed lines must not throw through the output loop.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `VisionSidecarClientTests.cs` | Command serialization, gesture/error/calibration parsing, worker source invariants for capture profiles and calibration. | No live camera. |

## Known Risks / Fragility

Changing JSON casing or message field names breaks the worker protocol silently. Invalid parse behavior protects the long-running host output loop.

## Change Notes for Agents

Read the source file and the linked flow/feature notes before editing. Preserve routing, safety, and lifecycle ownership; this file is connected to live voice or motion paths.
