---
type: architecture
status: planned
area: cross-cutting
tags:
  - merlin
  - architecture
  - strangler
  - migration
  - modular-runtime
---

# Strangler Migration Architecture

## Purpose

This note defines how Merlin should migrate safely from the current backend to the planned modular runtime.

The migration must avoid a big-bang rewrite.

Current Merlin has fragile but valuable behavior around:

- voice capture;
- STT and TTS;
- speech playback;
- barge-in;
- interruption clarification;
- browser workspace;
- active surface state;
- pending confirmations;
- Godot WebSocket UI;
- memory prompt context;
- motion/vision control.

The new architecture must be built beside the existing runtime and enabled gradually.

## Runtime Modes

Add a runtime mode model:

```text
Legacy
Shadow
Hybrid
NextFirst
NextOnly
```

### Legacy

Only current behavior executes.

`Merlin.Next` may be registered but receives no traffic.

### Shadow

Current behavior executes.

`Merlin.Next` receives a copy of the request and logs what it would have done.

Shadow mode must be read-only:

- no clicks;
- no app launches;
- no memory writes;
- no TTS playback;
- no browser actions;
- no messages sent;
- no pending operation ownership mutation unless explicitly shadow-only trace state.

### Hybrid

A per-capability allowlist determines whether Next or Legacy owns execution.

Example:

```json
{
  "MerlinNext": {
    "Mode": "Hybrid",
    "HandledCapabilities": [
      "app.open",
      "url.open"
    ]
  }
}
```

If `app.open` is selected, Next executes and Legacy must not.

If a non-enabled capability is selected, Legacy executes.

### NextFirst

Next attempts to handle requests first.

Legacy fallback remains for capabilities that Next cannot handle or is not allowed to execute.

### NextOnly

Legacy routing is disabled.

This mode is only allowed after all critical behavior has been migrated and tested.

## Ownership Rule

No double execution.

```text
In Shadow:
  Legacy executes, Next predicts.

In Hybrid:
  Exactly one runtime executes the selected capability.

In NextFirst:
  Next executes if it owns the capability; otherwise Legacy fallback.

In NextOnly:
  Next executes or fails safely.
```

## Cutover Table

Maintain a cutover table in the master implementation plan or progress report.

Example:

| Capability / Feature | Legacy Path | Next Path | Mode | Notes |
| --- | --- | --- | --- | --- |
| `app.open` | active | implemented | Next handles | First safe vertical slice. |
| `app.close` | active | planned | Legacy handles | Needs safety confirmation check. |
| `web.search` | active/partial | planned | Legacy handles | Adapter boundary first. |
| `browser.media.pause` | active | shadow | Legacy handles | Needs surface registry. |
| `browser.page.click` | active | planned | Legacy handles | Safety-critical. |
| `voice.stop_speaking` | active | planned | Legacy handles | Voice migration late. |
| interruption clarification | active | planned | Legacy handles | High risk. |

## Migration Sequence

1. Feature-owned settings files.
2. `Merlin.Next` folder skeleton.
3. runtime mode configuration.
4. shadow bridge.
5. kernel contracts.
6. trace logging/comparison.
7. first safe vertical slice.
8. capability registry and module registration.
9. surface registry.
10. adapter wrappers.
11. browser module migration.
12. voice module migration.
13. legacy retirement.

## Trace Logging

Shadow mode should log:

- request ID;
- source;
- user text;
- legacy path result if available;
- Next route decision;
- selected capability;
- confidence;
- active surface;
- safety decision;
- would-execute handler;
- mismatch reason;
- execution disabled reason.

Example:

```text
NextShadowTrace
  requestId = ...
  source = backend_idle_voice
  text = "pause the video"
  activeSurface = browser.tab.youtube
  nextCapability = browser.media.pause
  nextSafety = safe
  execution = disabled_shadow_mode
  legacyOutcome = AskClarification
  match = false
```

## Go / No-Go Gate

Before enabling any capability in Hybrid mode, require:

1. capability descriptor exists;
2. handler exists;
3. safety policy exists if side-effectful;
4. tests cover success/failure;
5. legacy behavior is understood;
6. no double execution path exists;
7. fallback behavior is safe;
8. vault/code atlas updated;
9. manual validation listed for live-only systems.

## Risks

| Risk | Mitigation |
| --- | --- |
| New and old runtime both execute side effects. | Per-capability ownership and execution guard. |
| Shadow logging changes timing in voice pipeline. | Keep shadow async, bounded, non-blocking, optionally disabled. |
| New runtime reads stale active surface. | Surface snapshot in turn context; trace mismatch. |
| Legacy fallback executes after partial Next side effect. | Capability results must identify side-effect status before fallback. |
| Agent tries to migrate voice first. | Master plan marks voice late and high risk. |

## Related Notes

- [[Modular Runtime Architecture]]
- [[Merlin Next Skeleton And Runtime Modes Plan]]
- [[Kernel Contracts Shadow Bridge Plan]]
- [[First Vertical Slice Apps AppOpen Plan]]
