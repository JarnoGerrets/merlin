# Phase 03 — Tool Reversibility And Rollback

## Purpose

This phase lets Merlin repair recent safe side effects after the user corrects themselves.

Example target behavior:

```text
User: open Facebook
Merlin: opens Facebook immediately
User: sorry, I meant Google
Merlin: closes the Facebook tab/window it opened, if safe
Merlin: opens Google
```

The goal is hyper-responsive tools without waiting on every action.

Core principle:

```text
Fast when safe.
Confirm when risky.
Rollback when possible.
Never rollback user-owned resources.
Only rollback Merlin-owned resources.
```

## Why this matters

A fixed pending delay, such as 500 ms before opening a website, makes Merlin feel less responsive. For low-risk reversible actions like opening a URL, immediate execution plus safe rollback feels better.

But rollback must be safe. Merlin must not close random user browser tabs or apps.

## Explicit non-goals

Do not implement rollback for all tools.

Do not close arbitrary user-owned browser tabs.

Do not undo irreversible actions such as sent messages.

Do not remove confirmation gates for risky tools.

Do not implement Live Answer Steering.

## Existing repo areas to inspect first

```text
Merlin.Backend/Tools/OpenUrlTool.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/ToolRegistry.cs
Merlin.Backend/Tools/
Merlin.Backend/Services/IntentRouting/
Merlin.Backend/Services/BargeIn/BargeInCoordinator.cs
Merlin.Backend/Services/LiveAssistantTurnService.cs
Merlin.Backend/Models/LiveAssistantTurn.cs
Merlin.Backend.Tests/
```

Current important finding:

`OpenUrlTool` appears to launch through `IProcessLauncher`. This likely does not provide a safe browser tab id or Merlin-owned browser resource identity. So the first version should add the ledger and policy model, then implement true URL rollback only when the opened resource can be safely tracked.

## Tool categories

Define tool side-effect categories:

```text
ReadOnly
SafeEphemeral
SafeCompensatable
UnsafeCompensatable
Irreversible
RequiresConfirmation
```

Examples:

```text
ReadOnly:
  time, date, weather display, simple question

SafeEphemeral:
  UI overlay, temporary visual state

SafeCompensatable:
  open URL in Merlin-owned tab/window
  open app if Merlin can close only its own launched instance and no user work exists

UnsafeCompensatable:
  delete file if recycle-bin restore is tracked
  change setting if previous value is stored

Irreversible:
  send WhatsApp/email/message
  submit form
  purchase/order/pay

RequiresConfirmation:
  shell command
  file deletion
  sending messages
  important setting changes
```

## New service concept

Suggested folder:

```text
Merlin.Backend/Services/ToolExecution/
  ToolExecutionLedger.cs
  ToolExecutionRecord.cs
  ToolReversibilityPolicy.cs
  ICompensatableToolResult.cs
  ToolCompensationService.cs
  ToolExecutionOwnership.cs
```

## Execution record model

Suggested shape:

```csharp
public sealed class ToolExecutionRecord
{
    public required string ExecutionId { get; init; }
    public required string TurnId { get; init; }
    public required string CorrelationId { get; init; }
    public required string ToolName { get; init; }
    public required string UserIntent { get; init; }
    public required string TargetDescription { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public ToolExecutionStatus Status { get; set; }
    public ToolSideEffectLevel SideEffectLevel { get; init; }
    public bool OwnedByMerlin { get; init; }
    public bool CanCompensate { get; init; }
    public string? CompensationDescription { get; init; }
    public ICompensatableToolResult? Compensation { get; init; }
}
```

Suggested statuses:

```text
Planned
Executing
Executed
Superseded
CompensationPending
Compensated
CompensationFailed
Cancelled
Failed
```

## Compensation interface

Suggested:

```csharp
public interface ICompensatableToolResult
{
    bool CanCompensate { get; }
    string CompensationDescription { get; }
    Task<CompensationResult> CompensateAsync(CancellationToken cancellationToken);
}
```

Compensation must be safe and must include a safety check immediately before acting.

## OpenUrl rollback requirements

Safe rollback for OpenUrl requires Merlin to know what it opened.

Acceptable safe approaches:

```text
- Dedicated Merlin-owned browser window/profile
- Chrome/Edge DevTools Protocol with tab id tracking
- Playwright-controlled browser window
- Browser extension or local browser integration that returns owned tab/window id
```

Potentially unsafe approach:

```text
Process.Start(url) and then guess which tab opened.
```

Do not close tabs by guessing.

## Minimal version for this phase

If safe browser ownership cannot be implemented quickly, still implement:

```text
- ToolExecutionLedger
- ToolReversibilityPolicy
- ToolExecutionRecord creation for OpenUrlTool
- Correct logs and compensation placeholders
- Correction route can detect that previous OpenUrl is theoretically compensatable but currently lacks safe resource ownership
- No unsafe tab closing
```

Then later add true browser-control integration.

## Correction behavior

When a replacement utterance arrives after a recent tool execution:

```text
1. Find recent active/relevant ToolExecutionRecord.
2. Check whether replacement relates to the same active flow.
3. Check whether previous tool can compensate.
4. Check whether resource is Merlin-owned.
5. Check age window, e.g. within 5–15 seconds.
6. Run safety check.
7. Compensate old action if safe.
8. Execute replacement.
```

Example:

```text
OpenUrlTool(facebook.com) executed
User: sorry I meant Google
→ if Merlin-owned Facebook tab exists and still points to Facebook
→ close it
→ open Google
```

If compensation is not safe:

```text
Facebook is already open. Opening Google instead.
```

Do not over-explain unless necessary.

## Risk policy

Maintain a policy table so future tools can declare:

```text
sideEffectLevel
requiresConfirmation
canCancelBeforeCommit
canCompensateAfterExecution
requiresOwnershipForCompensation
compensationWindow
```

## Logging requirements

Add logs:

```text
ToolExecutionRecorded
ToolExecutionOwnershipCaptured
ToolCompensationRequested
ToolCompensationSafetyCheckPassed
ToolCompensationSafetyCheckFailed
ToolCompensated
ToolCompensationSkippedNoOwnership
ToolCompensationSkippedUnsafe
ToolCompensationFailed
ReplacementExecutedAfterCompensation
ReplacementExecutedWithoutCompensation
```

Include:

```text
turn id
correlation id
execution id
tool name
target
ownedByMerlin
canCompensate
reason
```

## Tests to add

Unit tests:

1. Read-only tool is not compensation candidate.
2. Safe compensatable tool with ownership can compensate.
3. Safe compensatable tool without ownership does not compensate.
4. OpenUrl record is created with correct side-effect policy.
5. Replacement within correction window attempts compensation when safe.
6. Replacement outside correction window does not compensate automatically.
7. Irreversible tool never compensates.
8. RequiresConfirmation tools still require confirmation.
9. Compensation safety check failure prevents rollback.
10. Compensation failure is logged and replacement can still proceed if safe.

Integration tests if feasible:

1. `open Facebook` then `sorry I meant Google` records the previous action and replacement.
2. If no browser ownership exists, Merlin does not close arbitrary tabs.
3. If a fake owned tab exists, compensation closes only that fake owned tab.

## Acceptance criteria

This phase is done when:

```text
- Tool execution records exist for side-effect tools.
- A reversibility policy exists.
- Tools can declare compensation capability.
- OpenUrl is classified as safe-compensatable only when ownership can be proven.
- Correction/replacement can trigger compensation for recent safe owned actions.
- Merlin never closes user-owned resources by guessing.
- Risky/irreversible actions still require confirmation or cannot rollback.
- Logs show why rollback happened or was skipped.
- Tests cover compensation safety.
```

