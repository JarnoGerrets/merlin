---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/merlin_capability_specs/00_SharedCapabilityFoundation.md
classification: architecture-plan
related_features:
  - External App Control
status: future
imported_to_vault: true
---

# 00 - Shared Capability Foundation

## Goal

Create one consistent pattern for adding large assistant capabilities to Merlin without turning the backend into random one-off tool classes. The missing capability set includes web search, news, email, calendar, file access, system settings, software installation, and destructive file actions. These introduce permissions, private data, credentials, confirmations, error states, and higher user trust requirements.

## Core principle

Add each capability as a narrow, safe, observable vertical slice first. Do not make Merlin generally autonomous until the capability has:

- a small trusted core,
- strong tests,
- clear confirmation behavior,
- privacy-aware audit logging,
- voice-friendly acknowledgement/progress/final responses,
- honest tool discovery metadata.

## Capability maturity levels

| Level | Name | Meaning | Example |
|---|---|---|---|
| 0 | Missing | Domain is known but no implementation exists. | `web_search` current state. |
| 1 | Recognized | Router detects the domain and gives a helpful unavailable response. | "I understand you want email, but it is not connected yet." |
| 2 | Read-only | Merlin can inspect or retrieve information, but cannot change anything. | Search files, read calendar, fetch web results. |
| 3 | Draft/staged | Merlin can prepare an action but not execute it without explicit confirmation. | Draft email, prepare calendar invite, prepare package install. |
| 4 | Confirmed write | Merlin can execute specific confirmed actions. | Create an event after confirmation. |
| 5 | Trusted automation | Merlin can act with stored permissions inside narrow boundaries. | Trusted recurring briefing or trusted URL open. |

Never jump directly from level 0 to level 5.

## Shared backend contracts

Add shared interfaces only where they reduce duplication. Avoid over-abstracting before the second implementation needs it.

```csharp
public interface ICapabilityPermissionService
{
    Task<PermissionDecision> CheckAsync(CapabilityRequest request, CancellationToken cancellationToken);
}

public interface ICapabilityAuditLog
{
    Task RecordAsync(CapabilityAuditEvent auditEvent, CancellationToken cancellationToken);
}

public interface ICapabilityCredentialStore
{
    Task<bool> HasCredentialAsync(string providerId, CancellationToken cancellationToken);
    Task StoreCredentialReferenceAsync(CredentialReference reference, CancellationToken cancellationToken);
    Task RemoveCredentialAsync(string providerId, CancellationToken cancellationToken);
}
```

Do not store raw OAuth tokens, API keys, passwords, refresh tokens, or private keys in plain JSON. Use Windows Credential Manager, DPAPI-protected storage, or another OS-level secret store.

## Capability domain config pattern

Every new domain in `appsettings.json` should follow the existing `CapabilityDomains` style:

```json
{
  "id": "web_search",
  "name": "Web Search",
  "description": "Search the web or retrieve live/current information from the internet.",
  "isImplemented": true,
  "implementedIntent": "web_search",
  "missingMessage": null,
  "safetyLevel": "safe_readonly"
}
```

Suggested expanded safety levels:

- `safe_readonly`
- `private_readonly`
- `confirmation_required`
- `high_risk_confirmation`
- `admin_confirmation`
- `unsupported`
- `missing`

## Tool metadata requirements

Every new tool should expose:

- stable tool name,
- human-readable description,
- examples,
- capability domain id,
- safety level,
- whether it reads private data,
- whether it writes/changes anything,
- whether it requires credentials,
- whether it supports voice mode,
- whether it supports text mode,
- whether confirmation is required.

Example:

```csharp
new ToolMetadata(
    name: "web.search",
    description: "Searches the web for current information and returns cited results.",
    examples: ["search the web for Godot C# export issues", "what is the latest DeepInfra pricing"],
    capabilityDomainId: "web_search",
    safetyLevel: "safe_readonly",
    requiresConfirmation: false,
    readsPrivateData: false,
    writesExternalState: false
);
```

## Routing strategy

Use the existing routing stack, but keep the domain selection hierarchical:

1. Cheap normalization through `SpeechCommandNormalizer`.
2. Fast rule-based domain candidates.
3. Capability classifier chooses from a small relevant set.
4. Tool-specific argument parser extracts structured inputs.
5. Safety gate decides whether to execute, clarify, stage confirmation, or refuse.

The router should not send every user message to a giant model with every tool description. That will become slow, expensive, and brittle.

## Confirmation strategy

Confirmation should be based on risk, not implementation convenience.

### No confirmation

Allowed for:

- Web search.
- News retrieval.
- Reading public pages.
- Reading system time/date/timezone.
- Listing non-private capability examples.

### Soft confirmation

Useful for private read actions:

- "Can I check your calendar for tomorrow?"
- "Can I search your email for messages from school?"
- "Can I inspect your Downloads folder?"

This is especially important the first time a capability accesses a private source.

### Hard confirmation

Required for actions that change external or local state:

- Send email.
- Create/update/delete calendar event.
- Change system setting.
- Install/update/uninstall software.
- Move/delete files.

Hard confirmations must include exact target, exact action, and consequences.

## Voice UX rules

Merlin is voice-first. Every capability must define three response layers:

1. Immediate acknowledgement: short, cached if possible.
2. Progress update: only if operation exceeds configured latency thresholds.
3. Final spoken result: concise, not a wall of citations or file paths.

Examples:

- Web search: "I found three strong sources. The main answer is..."
- Email: "I found two relevant emails. The newest one says..."
- Calendar: "You are free after three in the afternoon."
- File access: "I found the file in Downloads. It looks like..."

The visual/chat response may contain structured details, source links, tables, file paths, and action buttons. The spoken response should not.

## Visual state integration

Add visual events for capability execution:

- `capability_detected`
- `permission_requested`
- `external_fetching`
- `private_data_reading`
- `draft_created`
- `confirmation_pending`
- `action_executing`
- `action_completed`
- `action_blocked`
- `action_failed`

The orb should make public reads feel lighter than private reads or dangerous actions.

## Audit logging

Every privacy-sensitive or state-changing capability needs an audit record.

Minimum fields:

- Timestamp UTC.
- Capability id.
- Tool name.
- User input summary.
- Parsed action.
- Target resource summary.
- Whether private data was read.
- Whether external state was changed.
- Confirmation id if applicable.
- Result status.
- Error category.

Do not log private content by default. Log summaries, counts, ids, domains, or hashes where possible.

## Permission scopes

Suggested scopes:

- `web.public_search`
- `news.public_read`
- `email.read_metadata`
- `email.read_body`
- `email.draft`
- `email.send`
- `calendar.read`
- `calendar.write`
- `files.list`
- `files.read`
- `files.write_staged`
- `system.settings.read`
- `system.settings.write_allowlisted`
- `software.package_search`
- `software.install_confirmed`
- `files.delete_reversible`

Permissions should support:

- deny always,
- ask every time,
- allow this session,
- allow this folder/source/account only,
- allow trusted action after confirmation.

## Error taxonomy

Every capability should map provider/internal errors to user-friendly categories:

- `not_configured`
- `permission_denied`
- `credentials_missing`
- `credentials_expired`
- `network_unavailable`
- `provider_rate_limited`
- `provider_error`
- `ambiguous_request`
- `unsafe_request`
- `target_not_found`
- `action_cancelled`
- `internal_error`

## Test pattern

For each capability add tests for:

- Domain classification.
- Rule-based intent examples.
- Ambiguous phrase handling.
- Safety classification.
- Confirmation staging.
- Cancel confirmation.
- Execute confirmation.
- Provider success.
- Provider timeout.
- Provider failure.
- Credentials missing.
- Permission denied.
- Speech response shape.
- Tool discovery metadata.
- Audit log creation.

## Implementation checklist

- [ ] Add capability domain config.
- [ ] Add options class if needed.
- [ ] Add provider interface.
- [ ] Add concrete provider.
- [ ] Add tool class.
- [ ] Register tool in `ToolRegistry`.
- [ ] Add classification examples.
- [ ] Add rule-based parser examples.
- [ ] Add safety policy.
- [ ] Add confirmation policy.
- [ ] Add speech templates.
- [ ] Add visual events.
- [ ] Add tests.
- [ ] Update tool discovery.
- [ ] Update docs.

## Agent instruction

When implementing any capability from this pack, start with the smallest safe vertical slice. Do not add provider-specific code directly into the tool if it can be hidden behind a provider interface. Do not implement write actions until read-only behavior is tested. Do not bypass confirmation for convenience.
