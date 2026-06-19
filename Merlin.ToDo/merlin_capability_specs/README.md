# Merlin Capability Expansion Pack

Agent-readable implementation pack for Merlin's missing high-value assistant capabilities.

## Generated files

1. `00_SharedCapabilityFoundation.md` — shared contracts, safety, routing, permissions, persistence, confirmation, tests, and UX rules.
2. `01_WebSearchCapability.md` — live/current web search with source-aware answers.
3. `02_NewsCapability.md` — current news, briefings, clustering, and source-diverse summaries.
4. `03_EmailCapability.md` — private mailbox search/read/draft/send with strict consent gates.
5. `04_CalendarCapability.md` — schedule reading, availability, event creation, invites, updates.
6. `05_FileAccessCapability.md` — safe local folder/file inspection and document reading.
7. `06_SystemSettingsCapability.md` — allowlisted local OS setting changes.
8. `07_SoftwareInstallationCapability.md` — package-manager based install/update/uninstall flow.
9. `08_DestructiveFileActionsCapability.md` — dry-run, recycle-bin/quarantine-first file deletion model.
10. `09_Top20NextCapabilities.md` — ranked roadmap of 20 capabilities to add afterwards.

## Current Merlin fit

This pack assumes the current Merlin backend shape:

- Capability domains are configured in `Merlin.Backend/appsettings.json` under `CapabilityDomains`.
- Existing tools live under `Merlin.Backend/Tools`.
- Existing service/routing pieces include `CapabilityClassifier`, `CommandRouter`, `HybridIntentParser`, `RuleBasedIntentParser`, `TrustedCommandIntentParser`, `ToolRegistry`, `ConfirmationService`, and `PendingInteractionService`.
- Existing models include capability definitions, route decisions, tool metadata, tool results, assistant responses, pending confirmations, and pending interactions.
- Existing tests already cover routing, tools, confirmation, memory, local AI, speech policy, app/url opening, and WebSocket behavior.

## Recommended implementation order

1. Shared capability foundation.
2. Web search.
3. News, because it can reuse web/search/source infrastructure.
4. File access in read-only mode.
5. Calendar read/create.
6. Email read/search/draft.
7. System settings allowlist.
8. Software installation using a package-manager backend.
9. Destructive file actions only after file access and confirmation are battle-tested.

## Non-negotiables

- Every capability gets tests before it is considered done.
- Every external side effect goes through confirmation unless it is harmless and reversible.
- Destructive actions must be staged, reversible where possible, and impossible to trigger from ambiguous voice input.
- Voice UX must include acknowledgement, progress, and final result speech that is shorter than the on-screen detail.
- Tool discovery should expose each new capability honestly, including whether it is read-only, draft-only, or confirmed-write.
