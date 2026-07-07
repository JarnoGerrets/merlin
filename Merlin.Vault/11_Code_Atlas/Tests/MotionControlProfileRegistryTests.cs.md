---
type: code-atlas
status: current
project: Merlin.Backend.Tests
tags:
  - merlin
  - code-atlas
---

# MotionControlProfileRegistryTests

## File

`Merlin.Backend.Tests/MotionControlProfileRegistryTests.cs`

Verified present in current repo.

## Purpose

Regression test atlas for `MotionControlProfileRegistryTests`.

## Related Features

- [[Current Test Coverage]]

## Main Types / Classes

- `MotionControlProfileRegistryTests`

## Important Methods / Functions

| Method | Visibility | Responsibility | Calls | Called By | Notes |
| --- | --- | --- | --- | --- | --- |
| `test cases` | public | Protects behavior named by the test fixture and assertions. | fake services and production classes | dotnet test | Failures in this file should be treated as regression signals for the related feature. |

## State Owned

| Field / Property | Type | Meaning | Readers | Writers | Reset / Lifetime |
| --- | --- | --- | --- | --- | --- |
| `test fakes/fixtures` | test-local objects | Capture calls, emitted messages, and service state under test. | assertions | test setup | single test lifetime |

## Dependencies

| Dependency | Used For |
| --- | --- |
| dependency | runtime collaboration |

## Events / Messages Emitted

| Event / Message | Destination | Payload / Notes |
| --- | --- | --- |
| none | none | No events emitted directly. |

## Events / Messages Consumed

| Event / Message | Source | Handler |
| --- | --- | --- |
| none | none | No events consumed directly. |

## External Side Effects

- Side effects belong to the owning service or runtime described in related architecture notes.

## Safety / Guardrails

- Preserve existing safety and confirmation boundaries.
- Do not route around ActiveSurface, BrowserPageSafetyGuard, or profile lifecycle ownership.

## Tests

| Test File | What It Covers | Gaps |
| --- | --- | --- |
| `MotionControlProfileRegistryTests.cs` | This is the test file. | May not cover live devices or UI. |

## Known Risks / Fragility

- Changes can affect linked flows; update feature and flow notes when behavior changes.

## Change Notes for Agents

Before changing `MotionControlProfileRegistryTests`, read the linked feature note, architecture note, flow note, and bug index entry for the affected system.
