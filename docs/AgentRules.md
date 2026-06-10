# Merlin Agent Rules

Before starting a task, identify which rules are relevant and apply only those.

## Scope Control

- Do not add features beyond the requested task.
- Do not change architecture unless asked.
- Prefer small targeted changes.
- Avoid broad rewrites.
- Preserve existing tests unless intentionally updating them.
- Add or update tests for behavior changes.
- Keep `Program.cs` minimal.

## Safety Boundaries

- Do not weaken safety boundaries.
- Do not let LocalAI execute tools directly.
- Conversation is not execution.
- Tool execution must go through backend tools.
- Do not add shell execution unless explicitly requested.
- Do not add file editing or self-modification unless explicitly requested.
- Do not add autonomous behavior unless explicitly requested.
- Discovered or untrusted applications require confirmation.

## LocalAI Rules

- LocalAI may suggest intent or generate conversation text.
- LocalAI output must be validated by backend code.
- LocalAI must not scan the system, launch apps, browse the web, edit files, or call tools directly.
- Do not browse web inside ChatTool.
- Web/search/news/time must be separate tools later.

## Capability Classification

- Do not hardcode large natural-language phrase lists.
- Prefer capability domains over thousands of manual phrases.
- Use structured intent fields as the source of truth.
- `missing_capability` means reasonable but not implemented.
- `unsupported_action` means unsafe or intentionally disallowed.
- `unknown_input` means not understandable.

## Response Rules

- Use `responseType` for frontend rendering instead of treating every `success=false` as an error.
- `responseType = assistant` for normal assistant output.
- `responseType = limitation` for missing capabilities.
- `responseType = safety` for unsupported/safety refusals.
- `responseType = error` for actual errors or unclear input.
- `responseType = system` for system/status-style messages.
- `ResponsePolisher` may only change `message` text.
- `ResponsePolisher` must not change `success`, `errorCode`, `intent`, `toolName`, `capabilityId`, `capabilityName`, `responseType`, `confirmation`, `applicationCandidates`, `diagnostics`, `availableTools`, or `correlationId`.

## Frontend / Backend Split

- Backend owns parsing, safety, tools, memory, LocalAI, diagnostics, and execution.
- Frontend owns display, input, connection state, debug visibility, and rendering style.
- Keep frontend/backend responsibilities separated.

## Testing

- Run backend tests after backend behavior changes:

```powershell
dotnet test Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

- Run backend build after backend changes:

```powershell
dotnet build Merlin.Backend\Merlin.Backend.csproj
```
