---
type: state
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Confirmation State Ownership

| State | Owner | Readers | Writers | Lifetime | Reset Conditions | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Pending tool/page confirmation | `ConfirmationService` / `PendingInteractionService` / Browser page safety models | `CommandRouter`, browser page action flow, tests | risky command/page action handlers | until confirmed, cancelled, expired, or superseded | confirmation response, cancellation, timeout | If stale confirmations survive, a later short reply can approve the wrong action. |
| Browser page pending confirmation | `BrowserPagePendingConfirmation` and `BrowserWorkspaceService` pending action path | BrowserWorkspaceService confirmation handlers | `EvaluateSafety`/`CreateClickConfirmation` | page-action request lifetime | stale URL/page mismatch, explicit confirmation, cancellation | Page changes between prompt and confirmation can make the target stale. |

## Related Notes

- [[Safety and Confirmation]]
- [[Browser Page Action Safety Flow]]
