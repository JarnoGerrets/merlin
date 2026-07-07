---
type: feature
status: partial
area: cross-cutting
tags:
  - merlin
  - feature
  - status/partial
  - layer/cross-cutting
---

# Safety and Confirmation

## Summary

Risk classification and confirmation for commands/page actions.

## Status

partial

## Verified Against Code

Status verified: yes

Evidence:
- ConfirmationService and PendingInteractionService are registered.
- BrowserPageSafetyGuard.cs exists with tests.
- Raw browser motion clicks bypass BrowserPageSafetyGuard.

## What Exists Today

Confirmation exists for tool/page-action paths but not every raw input path.

## Current Behavior

Routing decides target; safety decides execution permission where integrated.

## Planned Behavior

Unified safety policy for raw motion clicks, file/external app control, and learned profiles.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPageSafetyGuard.cs` | BrowserPageSafetyGuard | Page action safety | Risk/confirmation. |
| `Merlin.Backend/Services/ConfirmationService.cs` | ConfirmationService | Pending confirmations | Command confirmation path. |

## Code Atlas

- [[BrowserPageSafetyGuard]]
- [[Browser Page Action Safety Flow]]

## Related Systems

- [[Browser Page-Aware Control]]
- [[Command Routing Architecture]]
- [[Control Profile DB]]
- [[External App Control]]
- [[File Browser]]

## Dependencies

- [[Command Routing Architecture]]

## Dependents

- [[Browser Page-Aware Control]]
- [[File Browser]]
- [[External App Control]]
- [[Control Profile DB]]

## Readiness

Ready for implementation: yes

Reason:
Hardening current safety gaps is ready.

Blocked by:
- Raw input safety adapter missing.

Next safe action:
Protect raw BrowserHost pointer click path.

## Non-Goals / Do Not Build Yet

- Do not let ActiveSurface or learned profiles bypass safety.

## Known Bugs / Fragility

- Raw motion clicks bypass BrowserPageSafetyGuard.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrowserPageSafetyGuardTests.cs` | Page action safety | Raw motion click path gap. |

## Relevant Implementation Plans

- None currently promoted for this feature.

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (1 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
