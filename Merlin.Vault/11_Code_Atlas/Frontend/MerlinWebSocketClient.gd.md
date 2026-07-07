---
type: code-atlas
status: current
project: Merlin.Frontend
tags:
  - merlin
  - code-atlas
---

# MerlinWebSocketClient.gd

## File

`Merlin.Frontend/Scripts/MerlinWebSocketClient.gd`

Verified present in current repo.

## Purpose

Godot WebSocket client for backend connection. It sends chat/voice stream packets, reads backend packets with frame budgets, parses large payloads on a worker thread, and emits typed signals to Main.gd.

## Related Features

- [[Dashboard UI Control]]
- [[Motion Control]]
- [[Browser Workspace]]
- [[Voice Interruption System]]
- [[Streaming Responses and TTS]]

## Main Types / Classes

- `MerlinWebSocketClient.gd` GDScript class or script.

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `connect_to_backend` / `disconnect_from_backend` | func | Open/close WebSocketPeer and update connection state. | WebSocketPeer | Main.gd controls | Uses default localhost URL. |
| `send_message` | func | Sends text assistant request with correlation/source/client mode. | `_send_json` | Main.gd | Main text route. |
| voice stream send methods | func | Send start/chunk/end/cancel and speech presence packets. | `_send_json`; binary/text packet helpers | Main.gd voice mode | Frontend streaming flag currently controls path. |
| `_process` | func | Polls socket, reads limited packets per frame, drains parsed payloads, emits performance metrics. | `_read_available_packets`; `_drain_parsed_payloads` | Godot frame loop | Protects frame time. |
| `_route_raw_message` | func | Decides whether packet is small control packet or queued for thread parsing. | `_looks_like_control_packet`; `_queue_payload_parse` | `_read_available_packets` | Avoids large JSON parse stalls. |
| `_payload_parse_worker` | func | Background parses queued raw JSON and returns dictionaries/errors. | JSON parser; mutex/semaphore | thread | Keeps UI responsive. |
| `_emit_timed` | func | Emits typed signals and tracks slow signal work. | signal emit | packet drain | Diagnostics. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `_socket` | WebSocketPeer | Backend connection. | process/send | connect/disconnect | lifetime of node |
| `_payload_parse_queue` / `_payload_parsed_queue` | arrays protected by mutex | Worker-thread JSON queues. | process/worker | route/worker/drain | emptied over frames |
| `_state` | string | connection state. | UI | `_set_state` | disconnected on close |
| `last_assistant_ui_state_sequence` | int | Helps ignore stale UI state. | Main/client | assistant UI route | updated per packet |

## Dependencies

| Dependency | Used For |
| --- | --- |
| WebSocketPeer | network transport. |
| Thread/Mutex/Semaphore | background JSON parse. |
| Main.gd | sender/receiver of all signals. |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| `connection_state_changed`, `response_received`, `visual_event_received`, `assistant_ui_state_received`, etc. | Main.gd | typed backend events. |
| JSON/Binary packets | backend | chat and voice stream requests. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| backend WebSocket frames | backend | `_read_available_packets` / parser |

## External Side Effects

Opens network connection to backend, sends voice/text packets, runs a parse thread.

## Safety / Guardrails

Keep frontend gesture state gated by `_ui_control_mode_active` or browser workspace state as appropriate. Backend owns command routing; frontend owns visual/window manipulation.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| WebSocketHandlerTests.cs | Backend contract behavior. | No Godot WebSocket client tests. |

## Known Risks / Fragility

Frame-budget parsing can delay large payload handling. Thread synchronization errors would drop backend events or stall the UI.

## Change Notes for Agents

Godot frontend behavior is mostly manually validated. Keep UI state and gesture state resets explicit when browser workspace or UI-control mode changes.
