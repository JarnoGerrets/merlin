---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/interruption_intelligence/originals/merlin_responsive_feedback_migration_plan.md
related_features:
  - Voice Interruption System
  - Responsive Feedback
status: current
ready_for_agent: true
---

## Plan Status

Status: current
Ready for agent use: yes
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: [[Voice Interruption System]], [[Responsive Feedback]]
Related architecture: [[Voice Pipeline Architecture]]
Related code atlas: [[AssistantSpeechPlaybackService]]
Original source: `Merlin.ToDo/interruption_intelligence/originals/merlin_responsive_feedback_migration_plan.md`

# Responsive Feedback Migration Original Plan

## Document Purpose

This document describes how to migrate Merlin's current acknowledgement/progress speech system into a broader, context-aware `ResponsiveFeedback` system.

The goal is to make Merlin feel faster, more alive, and less repetitive while the backend is thinking, routing, executing tools, opening apps, or waiting on longer operations.

This document is intentionally implementation-oriented. It is meant to be given to a coding agent working inside the Merlin repository.

---

## Very Important Summary

Merlin already has a partial version of this feature.

The existing system lives mainly in:

```text
Merlin.Backend/Services/Acknowledgement/
```

Known files:

```text
Merlin.Backend/Services/Acknowledgement/AcknowledgementPolicy.cs
Merlin.Backend/Services/Acknowledgement/AcknowledgementPhraseLibrary.cs
Merlin.Backend/Services/Acknowledgement/AcknowledgementSpeechService.cs
Merlin.Backend/Services/Acknowledgement/RequestProgressSpeechService.cs
```

The migration should **not** create a totally unrelated second feedback system.

The preferred direction is:

```text
Existing Acknowledgement/Progress system
        ↓
Context-aware Responsive Feedback system
```

The migration should reuse:

```text
IAssistantSpeechPlaybackService
IRequestProgressSpeechService behavior
Acknowledgement cooldowns
Progress thresholds
Existing phrase cache behavior
Existing speech cancellation behavior
Existing live turn/correlation id flow
Existing backend-owned voice policy
```

The first implementation should focus on backend speech feedback. Frontend UI feedback should be optional and should come after the backend migration is stable.

---

## Goals

### Primary Goal

Upgrade Merlin's current acknowledgement/progress speech system into a flexible feedback orchestration layer that can choose appropriate quick feedback based on the current backend context.

Example feedback:

```text
"Got it."
"Opening that."
"I’ll check."
"I’ll prepare it first."
"This may take a second."
"I’m still working through that."
"I won’t send anything yet."
```

### Secondary Goals

The new system should:

- Reduce repeated generic acknowledgements.
- Give feedback that matches the kind of task Merlin is doing.
- Avoid expensive runtime AI/model usage.
- Avoid local embedding models.
- Avoid GPU usage.
- Avoid CPU-heavy semantic generation.
- Reuse Chatterbox phrase cache where possible.
- Keep final responses from being delayed by unnecessary filler speech.
- Stay compatible with barge-in/interruption behavior.
- Support future frontend visual feedback without requiring it in phase 1.

---

## Non-Goals

Do not do these during the first migration:

- Do not add a local LLM.
- Do not add an embedding model.
- Do not add a vector database.
- Do not add a GPU dependency.
- Do not generate feedback text through the main LLM.
- Do not replace all acknowledgement code in one risky pass.
- Do not rewrite the whole request pipeline.
- Do not rewrite the TTS system.
- Do not rewrite the frontend orb system.
- Do not add frontend fake assistant messages as final responses.
- Do not make Merlin chatty.
- Do not speak progress updates while the user is talking.
- Do not allow feedback speech to become more important than the actual answer.

---

## Current Architecture Summary

Based on the current structure report, the relevant Merlin structure is:

```text
Merlin/
  Merlin.Backend/
    Program.cs
    WebSocket/
      WebSocketHandler.cs
    Models/
      AssistantRequest.cs
      AssistantResponse.cs
      AssistantVisualEvent.cs
      LiveAssistantTurn.cs
    Services/
      CommandRouter.cs
      LiveAssistantTurnService.cs
      SpeechPolicyService.cs
      AssistantSpeechPlaybackService.cs
      TtsRouter.cs
      ChatterboxTtsProvider.cs
      ChatterboxWorkerClient.cs
      PiperVoiceService.cs
      HybridIntentParser.cs
      ToolRegistry.cs
      Acknowledgement/
        AcknowledgementPolicy.cs
        AcknowledgementPhraseLibrary.cs
        AcknowledgementSpeechService.cs
        RequestProgressSpeechService.cs
      IntentRouting/
        MerlinIntentRouter.cs
      BargeIn/
      SpeechPresence/
    Tools/
      Interfaces/
        ITool.cs
    VoiceScripts/
      chatterbox_worker.py
    Configuration/
      AcknowledgementSpeechOptions.cs
      TtsOptions.cs
      VoiceOptions.cs
      VoiceInputOptions.cs
      BargeInOptions.cs
      LocalAIOptions.cs
      LlmOptions.cs
      WebSearchOptions.cs
      CapabilityOptions.cs
      ApplicationLaunchOptions.cs
    appsettings.json
    appsettings.Development.json

  Merlin.Backend.Tests/
    RequestProgressSpeechServiceTests.cs
    AcknowledgementIntegrationTests.cs
    WebSocketHandlerTests.cs

  Merlin.Frontend/
    Scripts/
      Main.gd
      MerlinWebSocketClient.gd
      CoreOrb3D.gd
      CoreOrb.gd
```

---

## Current Request Flow

The main backend request flow currently appears to be:

```text
Frontend / voice / WebSocket
        ↓
WebSocketHandler
        ↓
AssistantRequest
        ↓
CommandRouter.RouteAsync
        ↓
HybridIntentParser / MerlinIntentRouter
        ↓
ToolRegistry
        ↓
ITool.ExecuteAsync(...)
        ↓
AssistantResponse
        ↓
WebSocketHandler.SendResponseAsync
        ↓
AssistantSpeechPlaybackService, if speech is allowed
```

Relevant backend files:

```text
Merlin.Backend/Program.cs
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Backend/Models/AssistantRequest.cs
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/HybridIntentParser.cs
Merlin.Backend/Services/IntentRouting/MerlinIntentRouter.cs
Merlin.Backend/Services/ToolRegistry.cs
Merlin.Backend/Tools/Interfaces/ITool.cs
Merlin.Backend/Models/AssistantResponse.cs
```

The current best place to integrate the feedback system is:

```text
Merlin.Backend/Services/CommandRouter.cs
```

This is the central orchestration point where Merlin already:

- Normalizes input.
- Updates live turn state.
- Parses intent.
- Selects tools.
- Starts acknowledgement/progress speech.
- Executes tools.
- Cancels progress when the final response is ready.
- Produces the final `AssistantResponse`.

---

## Current Lifecycle Concepts

Merlin already has live turn states in:

```text
Merlin.Backend/Models/LiveAssistantTurn.cs
```

Existing states include:

```text
IdleListening
CapturingUserSpeech
Interpreting
ProcessingTurn
PlanningTool
AwaitingToolCommit
ExecutingTool
Speaking
PausedByUser
Interrupted
Completed
Failed
Cancelled
Superseded
```

This is important because the responsive feedback system should not invent a separate lifecycle unless absolutely necessary.

Instead, it should map current turn/task states into feedback context.

Example:

```text
Live turn state: Interpreting
Feedback phase: starting/interpreting

Live turn state: PlanningTool
Feedback phase: planning

Live turn state: ExecutingTool
Feedback phase: executing

Live turn state: Speaking
Feedback phase: final_speech
```

---

## Migration Strategy

Use a staged migration.

The migration should have five main phases:

```text
Phase 0: Safety preparation and baseline tests
Phase 1: Introduce feedback model and selector
Phase 2: Integrate feedback with existing acknowledgement/progress services
Phase 3: Add context-aware feedback cards and configuration
Phase 4: Optional frontend visual feedback
Phase 5: Cleanup, naming consolidation, documentation
```

The safest first version is:

```text
Backend voice feedback only
Existing TTS/audio path
Existing Chatterbox phrase cache
Existing cancellation rules
No frontend protocol change
No embedding model
No vector database
```

---

# Phase 0: Safety Preparation

## Objective

Before changing behavior, document and protect the current acknowledgement/progress behavior with tests.

This phase should ensure that the migration does not break the currently working quick acknowledgement system.

## Files to inspect first

```text
Merlin.Backend/Services/CommandRouter.cs
Merlin.Backend/Services/Acknowledgement/AcknowledgementPolicy.cs
Merlin.Backend/Services/Acknowledgement/AcknowledgementPhraseLibrary.cs
Merlin.Backend/Services/Acknowledgement/AcknowledgementSpeechService.cs
Merlin.Backend/Services/Acknowledgement/RequestProgressSpeechService.cs
Merlin.Backend/Configuration/AcknowledgementSpeechOptions.cs
Merlin.Backend.Tests/RequestProgressSpeechServiceTests.cs
Merlin.Backend.Tests/AcknowledgementIntegrationTests.cs
```

## Required baseline observations

Before editing code, the agent should identify and write down:

- Where immediate acknowledgement is started.
- Where progress speech is started.
- Where progress speech is cancelled.
- Where `MarkMainResponseReady()` is called.
- How `pendingSpeechCancellation.Cancel()` is used.
- How phrase ids are used as cache keys.
- Whether acknowledgements are queued as a unique speech item type.
- How `cancelOnlyBeforePlayback` currently behaves.
- How speech is gated by `SpeechPolicyService`.
- Whether acknowledgement speech is allowed only in orb/voice mode.

## Baseline tests to run

Run existing backend tests.

Suggested commands may vary depending on solution structure, but likely:

```powershell
dotnet test
```

or:

```powershell
dotnet test Merlin.Backend.Tests/Merlin.Backend.Tests.csproj
```

If tests fail before any changes, capture the failures and do not hide them. The migration plan should not be blamed for pre-existing failures.

## Phase 0 success criteria

- Existing tests run or known failures are documented.
- Current acknowledgement/progress behavior is understood.
- No production code has been changed yet.
- The agent can point to the exact code locations for acknowledgement start, progress update, final suppression, and TTS queueing.

---

# Phase 1: Introduce Feedback Domain Model

## Objective

Add a lightweight domain model that represents feedback as structured context and cards.

This phase should not change runtime behavior yet. It should only introduce types that can later replace or extend hard-coded phrase selection.

## Preferred folder

Add new files under:

```text
Merlin.Backend/Services/Feedback/
```

Preferred namespace:

```csharp
Merlin.Backend.Services.Feedback
```

Reason:

- `Acknowledgement` is too narrow for the future feature.
- `Feedback` can include acknowledgement, progress, confirmation-safety reminders, app-opening feedback, search feedback, and frontend visual feedback.
- The old system can remain in place while the new system is introduced.

## Alternative folder

If the project strongly prefers smaller refactors:

```text
Merlin.Backend/Services/Acknowledgement/
```

But the preferred long-term location is still:

```text
Merlin.Backend/Services/Feedback/
```

## New file: FeedbackPhase.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackPhase.cs
```

Purpose:

Represent where the request currently is.

Suggested enum:

```csharp
public enum FeedbackPhase
{
    Unknown = 0,
    Starting,
    Interpreting,
    Planning,
    Executing,
    Waiting,
    StillWorking,
    NeedsConfirmation,
    Completing,
    Failed
}
```

Notes:

- Do not overfit this enum.
- Keep names generic.
- Map current `LiveAssistantTurnState` into these phases later.
- Avoid adding dozens of phases in the first version.

## New file: FeedbackDomain.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackDomain.cs
```

Purpose:

Represent the kind of work Merlin is doing.

Suggested enum:

```csharp
public enum FeedbackDomain
{
    General = 0,
    Conversation,
    LocalTool,
    ExternalApp,
    FileSearch,
    WebSearch,
    Memory,
    Calendar,
    Messaging,
    Voice,
    System,
    Confirmation
}
```

Notes:

- This does not need perfect classification at first.
- The system can start with `General`, `ExternalApp`, `FileSearch`, `WebSearch`, `Memory`, and `Confirmation`.
- Additional domains can be added later.

## New file: FeedbackDurationEstimate.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackDurationEstimate.cs
```

Purpose:

Represent expected wait time.

Suggested enum:

```csharp
public enum FeedbackDurationEstimate
{
    Unknown = 0,
    Instant,
    Short,
    Medium,
    Long
}
```

Suggested meaning:

```text
Instant: likely < 300ms
Short: likely < 2s
Medium: likely 2s to 6s
Long: likely > 6s
```

Do not make this overly exact. It is a hint for phrase selection, not a scheduler.

## New file: FeedbackConfidence.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackConfidence.cs
```

Purpose:

Represent how confident Merlin currently is.

Suggested enum:

```csharp
public enum FeedbackConfidence
{
    Unknown = 0,
    Low,
    Medium,
    High
}
```

Usage examples:

```text
High:
  "Got it, opening that."

Medium:
  "I’ll check that."

Low:
  "Let me work out what you mean."
```

## New file: FeedbackOutputMode.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackOutputMode.cs
```

Purpose:

Represent how feedback may be emitted.

Suggested enum:

```csharp
[Flags]
public enum FeedbackOutputMode
{
    None = 0,
    Speech = 1,
    VisualState = 2,
    ActivityText = 4,
    Notification = 8
}
```

Phase 1 can define this, but only `Speech` needs to be supported initially.

## New file: FeedbackContext.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackContext.cs
```

Purpose:

A small immutable context object representing the current situation.

Suggested shape:

```csharp
public sealed class FeedbackContext
{
    public string CorrelationId { get; init; } = "";
    public string RawUserText { get; init; } = "";
    public string NormalizedUserText { get; init; } = "";

    public FeedbackPhase Phase { get; init; } = FeedbackPhase.Unknown;
    public FeedbackDomain Domain { get; init; } = FeedbackDomain.General;
    public FeedbackDurationEstimate DurationEstimate { get; init; } = FeedbackDurationEstimate.Unknown;
    public FeedbackConfidence Confidence { get; init; } = FeedbackConfidence.Unknown;

    public string? Intent { get; init; }
    public string? ToolName { get; init; }
    public string? TargetName { get; init; }

    public bool IsVoiceInteraction { get; init; }
    public bool IsOrbClient { get; init; }
    public bool IsExternalAction { get; init; }
    public bool NeedsConfirmation { get; init; }
    public bool IsUserWaiting { get; init; } = true;
    public bool AllowSpeech { get; init; }
    public bool AllowVisualFeedback { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
```

Important implementation notes:

- Do not put service dependencies inside this class.
- Do not put behavior inside this class beyond simple helper methods if needed.
- Keep it serializable-friendly.
- Do not store sensitive content longer than necessary.
- Be careful with raw user text. Feedback cards should not directly speak arbitrary raw user text unless explicitly allowed.

## New file: FeedbackCard.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackCard.cs
```

Purpose:

Represent one possible feedback phrase or behavior.

Suggested shape:

```csharp
public sealed class FeedbackCard
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";

    public FeedbackOutputMode OutputMode { get; init; } = FeedbackOutputMode.Speech;

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public Dictionary<string, double> Vector { get; init; } = new();

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(60);
    public bool InterruptibleBeforePlayback { get; init; } = true;
    public bool IsReplayableSpeech { get; init; } = true;

    public int Priority { get; init; } = 0;
    public bool RequiresConfirmationContext { get; init; }
    public bool DisallowWhenFinalAnswerLikelyReady { get; init; } = true;
}
```

Important:

- `Id` should become the stable speech cache key.
- `Text` must be safe and truthful.
- `Vector` should be manually weighted, not generated by an AI model.
- `Tags` are useful for debugging and fallback selection.
- `Cooldown` prevents repetition.
- `Priority` lets some safety feedback beat generic feedback.

## New file: FeedbackSelection.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackSelection.cs
```

Purpose:

Represent the chosen card and selection diagnostics.

Suggested shape:

```csharp
public sealed class FeedbackSelection
{
    public FeedbackCard Card { get; init; } = default!;
    public double Score { get; init; }
    public string Reason { get; init; } = "";
    public DateTimeOffset SelectedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
```

## New file: IFeedbackCardProvider.cs

Path:

```text
Merlin.Backend/Services/Feedback/IFeedbackCardProvider.cs
```

Purpose:

Provide available feedback cards.

Suggested shape:

```csharp
public interface IFeedbackCardProvider
{
    IReadOnlyList<FeedbackCard> GetCards();
}
```

First implementation can be hard-coded.

Future implementation can load JSON.

## New file: IFeedbackSelector.cs

Path:

```text
Merlin.Backend/Services/Feedback/IFeedbackSelector.cs
```

Purpose:

Choose a card for a context.

Suggested shape:

```csharp
public interface IFeedbackSelector
{
    FeedbackSelection? Select(FeedbackContext context);
}
```

## New file: FeedbackVectorBuilder.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackVectorBuilder.cs
```

Purpose:

Convert a `FeedbackContext` into a small manual vector.

Example vector keys:

```text
phase.starting
phase.interpreting
phase.planning
phase.executing
phase.still_working
domain.general
domain.external_app
domain.file_search
domain.web_search
domain.memory
duration.short
duration.medium
duration.long
confidence.low
confidence.medium
confidence.high
risk.confirmation
interaction.voice
interaction.orb
action.external
```

Example output:

```csharp
{
    ["phase.executing"] = 1.0,
    ["domain.external_app"] = 1.0,
    ["duration.short"] = 0.8,
    ["confidence.high"] = 0.7,
    ["interaction.voice"] = 1.0,
    ["action.external"] = 1.0
}
```

Important:

- This is not an embedding.
- This is not ML.
- This is just cheap weighted matching.
- Runtime cost should be tiny.

## New file: FeedbackSelector.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackSelector.cs
```

Purpose:

Select the best feedback card using weighted similarity, cooldowns, and anti-repeat behavior.

Suggested behavior:

1. Build context vector.
2. Get available cards.
3. Filter disabled cards.
4. Filter cards blocked by cooldown.
5. Filter cards whose required output mode is not allowed.
6. Compute similarity score.
7. Add priority boost.
8. Add small random tie-breaker.
9. Select best card above threshold.
10. Record usage for cooldown/anti-repeat.

Pseudo-code:

```csharp
public FeedbackSelection? Select(FeedbackContext context)
{
    var contextVector = _vectorBuilder.Build(context);
    var cards = _cardProvider.GetCards();

    var candidates = cards
        .Where(card => IsAllowed(card, context))
        .Select(card => new
        {
            Card = card,
            Score = Similarity(contextVector, card.Vector) + PriorityBoost(card)
        })
        .Where(candidate => candidate.Score >= _options.MinimumScore)
        .OrderByDescending(candidate => candidate.Score)
        .ThenBy(_ => Random.Shared.NextDouble())
        .ToList();

    var selected = candidates.FirstOrDefault();
    if (selected is null)
    {
        return null;
    }

    _cooldowns.MarkUsed(selected.Card.Id);

    return new FeedbackSelection
    {
        Card = selected.Card,
        Score = selected.Score,
        Reason = "best_weighted_match"
    };
}
```

Do not copy this blindly if the project style differs. Adapt to current style.

## Similarity function

Use a very cheap dot-product style score.

Suggested:

```csharp
private static double Similarity(
    IReadOnlyDictionary<string, double> context,
    IReadOnlyDictionary<string, double> card)
{
    double score = 0;

    foreach (var (key, cardWeight) in card)
    {
        if (context.TryGetValue(key, out var contextWeight))
        {
            score += contextWeight * cardWeight;
        }
    }

    return score;
}
```

Optional normalization can come later. Do not over-engineer this in the first pass.

## Phase 1 success criteria

- New feedback model types compile.
- No runtime behavior has changed yet.
- No existing tests are broken.
- New selector unit tests can be written without touching `CommandRouter`.

---

# Phase 2: Add Responsive Feedback Orchestrator

## Objective

Introduce a small orchestrator that can use the selector and emit speech through the existing playback path.

## New file: IResponsiveFeedbackOrchestrator.cs

Path:

```text
Merlin.Backend/Services/Feedback/IResponsiveFeedbackOrchestrator.cs
```

Suggested shape:

```csharp
public interface IResponsiveFeedbackOrchestrator
{
    Task TryEmitImmediateFeedbackAsync(
        FeedbackContext context,
        CancellationToken cancellationToken);

    IRequestProgressHandle? StartProgressFeedback(
        FeedbackContext context,
        CancellationToken cancellationToken);

    void MarkMainResponseReady(string correlationId);
}
```

The actual method signatures may need to match current service conventions.

## New file: ResponsiveFeedbackOrchestrator.cs

Path:

```text
Merlin.Backend/Services/Feedback/ResponsiveFeedbackOrchestrator.cs
```

Responsibilities:

- Receive a `FeedbackContext`.
- Ask `IFeedbackSelector` for a card.
- Emit selected speech through the existing speech pipeline.
- Reuse `IAssistantSpeechPlaybackService.EnqueueAsync`.
- Use card id as `speechCacheKey`.
- Use `IsReplayableSpeech = true` for stable canned feedback.
- Respect `SpeechPolicyService` or equivalent speech gating.
- Start progress feedback through existing `IRequestProgressSpeechService` or a migrated wrapper.
- Suppress/cancel pending feedback when main response is ready.
- Log selected card id, phase, domain, score, and reason.

Do not let this class execute tools, parse intent, or make frontend decisions beyond optional feedback event dispatching.

## Relationship to existing acknowledgement services

The orchestrator should either:

### Option A: Wrap existing acknowledgement services

```text
ResponsiveFeedbackOrchestrator
  -> IFeedbackSelector
  -> IAcknowledgementSpeechService
  -> IRequestProgressSpeechService
```

This is the safest path.

### Option B: Replace phrase selection but reuse playback

```text
ResponsiveFeedbackOrchestrator
  -> IFeedbackSelector
  -> IAssistantSpeechPlaybackService
  -> IRequestProgressSpeechService
```

This is cleaner long-term but riskier.

### Recommended first version

Use Option A or a hybrid:

```text
ResponsiveFeedbackOrchestrator
  - uses new selector for immediate feedback
  - delegates still-working updates to existing RequestProgressSpeechService
```

Then later migrate progress phrases to cards.

## How immediate feedback should behave

Immediate feedback should happen early, but not too early.

Possible timing:

```text
CommandRouter receives request
  ↓
normalizes text
  ↓
sets turn state to Interpreting
  ↓
optionally emits low-context feedback
  ↓
parses intent
  ↓
updates context with intent/tool info
  ↓
optionally emits more specific feedback if no immediate feedback was already emitted
```

Important rule:

```text
At most one immediate feedback line per user request.
```

Do not speak both:

```text
"Got it."
"Opening WhatsApp."
```

for the same fast operation unless there is a strong reason.

## Context-aware feedback after intent parse

The best feedback often requires knowing the domain/tool.

Example:

```text
User: "Open Discord"
```

Before intent parse:

```text
Generic feedback:
"Got it."
```

After intent/tool is known:

```text
Better feedback:
"Opening Discord."
```

Recommended approach:

- For very quick low-risk acknowledgement, use generic feedback only if parsing might take long.
- Prefer task-aware feedback after intent/tool detection if this happens quickly.
- Use a short delay/debounce to avoid speaking a generic line just before the better line is available.

Practical first version:

```text
Only emit immediate feedback after intent/tool is known.
```

Reason:

- Simpler.
- More accurate.
- Less chatty.
- Still fast enough if intent parsing is usually quick.

Later optimization:

```text
If intent parsing exceeds X ms, emit generic "I’ll check."
```

## Progress feedback behavior

Current progress service already supports:

- First progress threshold.
- Second progress threshold.
- Long wait threshold.
- Maximum update count.
- Cancellation when final answer is ready.
- Anti-repeat behavior.
- State-aware phrase selection.

Do not duplicate this.

Instead:

```text
ResponsiveFeedbackOrchestrator.StartProgressFeedback(context)
        ↓
existing RequestProgressSpeechService
```

Later, `RequestProgressSpeechService` can select `FeedbackCard`s instead of hard-coded phrase categories.

## Phase 2 success criteria

- Orchestrator exists and is registered in DI.
- It can emit one selected feedback phrase through the existing speech playback path.
- It can start existing progress feedback.
- It can mark main response ready.
- Existing acknowledgement/progress tests still pass or are intentionally updated.
- New tests prove feedback failures do not block the final response.

---

# Phase 3: Configuration and Feedback Cards

## Objective

Move from hard-coded phrases toward configurable feedback cards while keeping behavior safe.

## New file: ResponsiveFeedbackOptions.cs

Path:

```text
Merlin.Backend/Configuration/ResponsiveFeedbackOptions.cs
```

Suggested options:

```csharp
public sealed class ResponsiveFeedbackOptions
{
    public bool Enabled { get; set; } = true;

    public bool EnableSpeechFeedback { get; set; } = true;
    public bool EnableVisualFeedback { get; set; } = false;

    public bool UseCardSelectorForImmediateFeedback { get; set; } = true;
    public bool UseCardSelectorForProgressFeedback { get; set; } = false;

    public int ImmediateFeedbackDelayMs { get; set; } = 150;
    public int MinimumMsBeforeGenericFeedback { get; set; } = 250;

    public double MinimumSelectionScore { get; set; } = 0.5;

    public int GlobalCooldownMs { get; set; } = 1500;
    public int DefaultCardCooldownSeconds { get; set; } = 60;
    public int SameTextCooldownSeconds { get; set; } = 120;

    public int MaxImmediateFeedbackPerTurn { get; set; } = 1;

    public bool PreferTaskAwareFeedback { get; set; } = true;
    public bool SuppressIfMainResponseReady { get; set; } = true;

    public bool UseStableCardIdAsSpeechCacheKey { get; set; } = true;
    public bool MarkFeedbackAsReplayableSpeech { get; set; } = true;

    public string? CardsFilePath { get; set; }
}
```

Register in:

```text
Merlin.Backend/Program.cs
```

Near existing options registration:

```csharp
builder.Services.Configure<ResponsiveFeedbackOptions>(
    builder.Configuration.GetSection("ResponsiveFeedback"));
```

## appsettings.json section

Add near `AcknowledgementSpeech`:

```json
"ResponsiveFeedback": {
  "Enabled": true,
  "EnableSpeechFeedback": true,
  "EnableVisualFeedback": false,
  "UseCardSelectorForImmediateFeedback": true,
  "UseCardSelectorForProgressFeedback": false,
  "ImmediateFeedbackDelayMs": 150,
  "MinimumMsBeforeGenericFeedback": 250,
  "MinimumSelectionScore": 0.5,
  "GlobalCooldownMs": 1500,
  "DefaultCardCooldownSeconds": 60,
  "SameTextCooldownSeconds": 120,
  "MaxImmediateFeedbackPerTurn": 1,
  "PreferTaskAwareFeedback": true,
  "SuppressIfMainResponseReady": true,
  "UseStableCardIdAsSpeechCacheKey": true,
  "MarkFeedbackAsReplayableSpeech": true,
  "CardsFilePath": null
}
```

## Feedback cards source

Start with hard-coded cards in:

```text
Merlin.Backend/Services/Feedback/DefaultFeedbackCardProvider.cs
```

Then later optionally support JSON:

```text
Merlin.Backend/Configuration/feedback-cards.json
```

Do not make JSON loading mandatory in the first pass.

Reason:

- Hard-coded provider is easier to test.
- JSON loading adds file path, deployment, reload, and validation concerns.
- Current acknowledgement phrase library is hard-coded too, so this fits current project style.

## New file: DefaultFeedbackCardProvider.cs

Path:

```text
Merlin.Backend/Services/Feedback/DefaultFeedbackCardProvider.cs
```

Initial card categories:

```text
General start
External app open
File/search
Web/search
Memory
Confirmation/safety
Still working
Failure fallback
```

## Example cards

### General immediate cards

```csharp
new FeedbackCard
{
    Id = "general_start_01",
    Text = "Got it.",
    Tags = new[] { "general", "start", "short" },
    Vector = new()
    {
        ["phase.starting"] = 1.0,
        ["domain.general"] = 0.6,
        ["duration.short"] = 0.5,
        ["interaction.voice"] = 0.8
    },
    Cooldown = TimeSpan.FromSeconds(90),
    IsReplayableSpeech = true
}
```

```csharp
new FeedbackCard
{
    Id = "general_checking_01",
    Text = "I’ll check.",
    Tags = new[] { "general", "checking" },
    Vector = new()
    {
        ["phase.interpreting"] = 0.8,
        ["phase.planning"] = 0.8,
        ["domain.general"] = 0.6,
        ["duration.medium"] = 0.5
    },
    Cooldown = TimeSpan.FromSeconds(90),
    IsReplayableSpeech = true
}
```

### External open cards

```csharp
new FeedbackCard
{
    Id = "external_open_01",
    Text = "Opening that.",
    Tags = new[] { "external", "open", "app" },
    Vector = new()
    {
        ["phase.executing"] = 1.0,
        ["domain.external_app"] = 1.0,
        ["action.external"] = 0.8,
        ["duration.short"] = 0.6,
        ["confidence.high"] = 0.5
    },
    Cooldown = TimeSpan.FromSeconds(75),
    IsReplayableSpeech = true
}
```

If dynamic target names are supported later:

```text
"Opening {targetName}."
```

But first version should avoid dynamic TTS if cached phrase reuse is important.

### Search cards

```csharp
new FeedbackCard
{
    Id = "file_search_01",
    Text = "I’m looking through your files.",
    Tags = new[] { "file", "search" },
    Vector = new()
    {
        ["phase.executing"] = 1.0,
        ["domain.file_search"] = 1.0,
        ["duration.medium"] = 0.7
    },
    Cooldown = TimeSpan.FromSeconds(120),
    IsReplayableSpeech = true
}
```

```csharp
new FeedbackCard
{
    Id = "web_search_01",
    Text = "I’m checking that online.",
    Tags = new[] { "web", "search" },
    Vector = new()
    {
        ["phase.executing"] = 1.0,
        ["domain.web_search"] = 1.0,
        ["duration.medium"] = 0.7
    },
    Cooldown = TimeSpan.FromSeconds(120),
    IsReplayableSpeech = true
}
```

### Confirmation/safety cards

```csharp
new FeedbackCard
{
    Id = "confirmation_prepare_01",
    Text = "I’ll prepare it first and ask before sending.",
    Tags = new[] { "confirmation", "safety", "messaging" },
    Vector = new()
    {
        ["phase.needs_confirmation"] = 1.0,
        ["risk.confirmation"] = 1.0,
        ["domain.messaging"] = 0.8
    },
    Cooldown = TimeSpan.FromSeconds(120),
    IsReplayableSpeech = true,
    Priority = 10
}
```

Important:

Safety/confirmation cards should be truthful. Do not say:

```text
"Sent."
```

unless the tool actually sent something.

Use safe phrases:

```text
"I’ll prepare it first."
"I won’t send anything yet."
"I’ll ask before sending."
```

### Still working cards

These may initially remain in `RequestProgressSpeechService`.

Later, migrate to cards:

```csharp
new FeedbackCard
{
    Id = "still_working_01",
    Text = "This may take a second.",
    Tags = new[] { "progress", "still_working" },
    Vector = new()
    {
        ["phase.still_working"] = 1.0,
        ["duration.medium"] = 0.7
    },
    Cooldown = TimeSpan.FromSeconds(120),
    IsReplayableSpeech = true
}
```

```csharp
new FeedbackCard
{
    Id = "still_working_long_01",
    Text = "I’m still working through that.",
    Tags = new[] { "progress", "long_wait" },
    Vector = new()
    {
        ["phase.still_working"] = 1.0,
        ["duration.long"] = 1.0
    },
    Cooldown = TimeSpan.FromSeconds(180),
    IsReplayableSpeech = true
}
```

## Truthfulness rules for cards

Feedback cards must never imply success before success is known.

Allowed before completion:

```text
"Opening that."
"I’m checking."
"I’ll prepare it."
"Looking now."
"This may take a second."
```

Not allowed before completion:

```text
"Done."
"I found it."
"I fixed it."
"I sent it."
"That worked."
```

Allowed after completion only if connected to actual tool result:

```text
"Done."
"That’s open."
"I found it."
```

The first migration should avoid completion feedback unless already present and safe.

## Phase 3 success criteria

- Feedback card provider exists.
- Selector can choose between multiple categories.
- Card ids are stable and testable.
- Cards use truthful language.
- Cards are not too chatty.
- Existing phrase cache can reuse card ids as speech cache keys.
- JSON loading is optional, not required.

---

# Phase 4: Integrate with CommandRouter

## Objective

Connect the new feedback system into the actual request lifecycle.

Primary file:

```text
Merlin.Backend/Services/CommandRouter.cs
```

## Current likely integration points

Based on the structure report, `CommandRouter` already:

- Receives `AssistantRequest`.
- Normalizes input.
- Updates live turn state.
- Parses intent.
- Selects tool.
- Starts acknowledgement/progress.
- Executes tool.
- Cancels progress when main response is ready.

The migration should preserve this flow.

## Suggested final shape

Conceptually:

```text
CommandRouter.RouteAsync(request)
  ↓
normalize request
  ↓
build base FeedbackContext
  ↓
parse intent
  ↓
select tool
  ↓
enrich FeedbackContext with intent/tool/domain/duration/risk
  ↓
ResponsiveFeedbackOrchestrator.TryEmitImmediateFeedbackAsync(context)
  ↓
ResponsiveFeedbackOrchestrator.StartProgressFeedback(context)
  ↓
execute tool / generate response
  ↓
progressHandle.MarkMainResponseReady()
  ↓
pendingSpeechCancellation.Cancel()
  ↓
return AssistantResponse
```

## Important: do not block main flow

Feedback emission must not block the real request.

Bad:

```csharp
await _feedbackOrchestrator.TryEmitImmediateFeedbackAsync(context, cancellationToken);
```

if that method waits for speech synthesis/playback to finish.

Good:

```csharp
await _feedbackOrchestrator.TryQueueImmediateFeedbackAsync(context, cancellationToken);
```

if that method only queues speech and returns quickly.

Or:

```csharp
_ = _feedbackOrchestrator.TryEmitImmediateFeedbackAsync(context, cancellationToken);
```

only if the existing project safely handles fire-and-forget tasks with logging and exception handling.

Preferred:

- Keep async but ensure it queues only.
- Do not await long TTS generation/playback.
- Ensure exceptions are swallowed/logged inside feedback service.

## When to emit immediate feedback

Recommended first version:

```text
Emit immediate feedback after intent/tool is known.
```

Reason:

- Better phrase selection.
- Avoids generic filler.
- Avoids double feedback.
- Keeps implementation simpler.

Future enhancement:

```text
If intent parsing takes longer than X ms, emit a generic acknowledgement.
```

But do not implement this until the simpler version is stable.

## How to build FeedbackContext in CommandRouter

Add a mapper/helper rather than spreading mapping logic everywhere.

Possible file:

```text
Merlin.Backend/Services/Feedback/FeedbackContextFactory.cs
```

Interface:

```csharp
public interface IFeedbackContextFactory
{
    FeedbackContext CreateInitial(AssistantRequest request, string normalizedText);
    FeedbackContext EnrichWithRouting(
        FeedbackContext context,
        IntentResult? intentResult,
        ITool? tool);
}
```

Actual types may differ depending on existing intent/tool classes.

Responsibilities:

- Extract correlation id.
- Determine `IsVoiceInteraction`.
- Determine `IsOrbClient`.
- Determine `AllowSpeech`.
- Map intent/tool to feedback domain.
- Estimate duration.
- Determine `NeedsConfirmation`.
- Determine `IsExternalAction`.

## Domain mapping examples

The mapping should be conservative.

Example mappings:

```text
Tool or intent indicates application launch
  -> FeedbackDomain.ExternalApp
  -> IsExternalAction = true
  -> DurationEstimate.Short

Tool or intent indicates file search/open
  -> FeedbackDomain.FileSearch
  -> DurationEstimate.Medium

Tool or intent indicates web search
  -> FeedbackDomain.WebSearch
  -> DurationEstimate.Medium

Tool or intent indicates memory
  -> FeedbackDomain.Memory
  -> DurationEstimate.Short or Medium

Pending confirmation exists
  -> FeedbackDomain.Confirmation
  -> NeedsConfirmation = true
```

Do not guess too aggressively. Unknown cases should map to:

```text
FeedbackDomain.General
FeedbackDurationEstimate.Unknown
FeedbackConfidence.Unknown
```

## Progress handle migration

Currently progress behavior likely returns a handle from `RequestProgressSpeechService`.

Do not remove it abruptly.

Instead:

```text
Before:
  var progressHandle = _requestProgressSpeechService.Start(...)

After:
  var progressHandle = _responsiveFeedbackOrchestrator.StartProgressFeedback(context, ...)
```

Internally, the orchestrator can still call the old service.

## Suppression migration

Preserve current suppression behavior exactly.

Existing important calls:

```text
progressHandle.MarkMainResponseReady()
pendingSpeechCancellation.Cancel()
```

The new orchestrator should not weaken these.

If moved behind the orchestrator, there should still be clear methods:

```csharp
progressHandle?.MarkMainResponseReady();
_feedbackOrchestrator.MarkMainResponseReady(correlationId);
```

or:

```csharp
feedbackTurn.MarkMainResponseReady();
```

## Phase 4 success criteria

- `CommandRouter` uses `ResponsiveFeedbackOrchestrator`.
- Existing acknowledgement/progress behavior is still preserved.
- At most one immediate feedback line is emitted per turn.
- Progress feedback is still cancelled/suppressed when final response is ready.
- Feedback errors never block the final response.
- Tests cover delayed tool execution and fast tool execution.

---

# Phase 5: Audio and Caching Strategy

## Objective

Reuse the existing Chatterbox phrase cache instead of building a new static audio system first.

Relevant files:

```text
Merlin.Backend/Services/AssistantSpeechPlaybackService.cs
Merlin.Backend/Services/TtsRouter.cs
Merlin.Backend/Services/ChatterboxTtsProvider.cs
Merlin.Backend/Services/ChatterboxWorkerClient.cs
Merlin.Backend/Services/Acknowledgement/AcknowledgementSpeechService.cs
Merlin.Backend/Configuration/AcknowledgementSpeechOptions.cs
```

## Current known behavior

The current Chatterbox path supports:

```text
speechCacheKey
isReplayableSpeech
SpeechSynthesisLogContext
ChatterboxEnablePhraseCache
ChatterboxCacheDir
```

Existing acknowledgement phrases already use phrase ids as cache keys.

## First migration rule

Do not add this yet:

```text
FeedbackAudioClipProvider
StaticWavClipProvider
PreGeneratedAudioManifest
```

unless there is already a clear static clip abstraction.

Instead:

```text
FeedbackCard.Id -> speechCacheKey
FeedbackCard.IsReplayableSpeech -> isReplayableSpeech
FeedbackCard.Text -> TTS text
```

## Cache key naming

Use stable predictable cache keys:

```text
feedback:{cardId}
```

Examples:

```text
feedback:general_start_01
feedback:external_open_01
feedback:file_search_01
feedback:confirmation_prepare_01
feedback:still_working_01
```

Do not include raw user text in cache keys.

Avoid dynamic target names in cache keys for phase 1.

## Dynamic text warning

This is tempting:

```text
"Opening WhatsApp."
"Opening Discord."
"Opening Visual Studio."
```

But dynamic phrases reduce cache reuse.

Recommended phase 1:

```text
"Opening that."
"Bringing that up."
```

Recommended phase 2:

- Allow dynamic target names only for common targets.
- Or allow dynamic target names but mark them as less replayable.
- Or use template cards with strict sanitization.

## Playback item type

Check current `SpeechPlaybackItemType`.

If it already has acknowledgement/progress types, use existing ones first.

Possible future addition:

```csharp
SpeechPlaybackItemType.Feedback
```

But add this only if the existing item types are too limiting.

## Interruption and barge-in risk

Extra speech can affect:

```text
BargeIn
SpeechPresence
Self-echo suppression
Assistant playback state
User interruption timing
```

So feedback speech must be:

- Short.
- Rare.
- Cancelled before playback when possible.
- Avoided when final response is already ready.
- Avoided while user speech is being captured.
- Marked clearly as assistant playback so echo/speech-presence systems can account for it.

## Phase 5 success criteria

- Feedback phrases use stable cache keys.
- Cached/replayable behavior matches existing acknowledgement behavior.
- No new audio format or static clip system is required.
- Final speech is not meaningfully delayed by feedback.
- Barge-in behavior does not regress.

---

# Phase 6: Optional Frontend Feedback

## Objective

Add optional frontend visual/text feedback only after backend speech feedback is stable.

Relevant files:

```text
Merlin.Backend/Models/AssistantVisualEvent.cs
Merlin.Backend/WebSocket/WebSocketHandler.cs
Merlin.Frontend/Scripts/MerlinWebSocketClient.gd
Merlin.Frontend/Scripts/Main.gd
Merlin.Frontend/Scripts/CoreOrb3D.gd
Merlin.Frontend/Scripts/CoreOrb.gd
```

## Recommended first frontend behavior

Do not show feedback as final assistant chat messages.

Instead, use temporary activity/status text.

Example frontend display:

```text
Opening that...
Checking...
Preparing it first...
Still working...
```

Target frontend surfaces:

```text
Main.gd activity_label
Main.gd notification surface, optionally
CoreOrb3D visual mode
```

## Backend packet options

### Option A: Extend visual state

Use existing `visual_state` packets.

Example conceptual packet:

```json
{
  "type": "visual_state",
  "state": "tool",
  "correlationId": "...",
  "detail": "Opening that."
}
```

Pros:

- Reuses existing packet path.
- Minimal frontend changes.

Cons:

- Visual state may become overloaded.
- Text feedback and orb state are not the same concept.

### Option B: Extend AssistantVisualEvent

Use existing visual event model.

Example conceptual event:

```json
{
  "type": "visual_event",
  "event": "FEEDBACK_TEXT",
  "value": 1,
  "correlationId": "...",
  "detail": "Opening that."
}
```

Pros:

- Good for temporary events.
- Does not pretend feedback is a final response.

Cons:

- Requires frontend event handling.

### Option C: New feedback packet type

Example:

```json
{
  "type": "feedback",
  "correlationId": "...",
  "text": "Opening that.",
  "displayMode": "activity",
  "severity": "neutral",
  "expiresAfterMs": 3000
}
```

Pros:

- Cleanest long-term.
- Clear separation between final answer, visual state, and feedback text.

Cons:

- More protocol surface.
- More tests needed.

## Recommended frontend phase 1 choice

Use Option B:

```text
AssistantVisualEvent with event = FEEDBACK_TEXT
```

Reason:

- It is event-like.
- It can be temporary.
- It avoids changing final response semantics.
- It reuses existing WebSocket event plumbing.

If existing frontend event handling makes this awkward, use Option A as a smaller step.

## Frontend behavior rules

- Feedback text should not stay forever.
- Feedback text should not overwrite final answer.
- Feedback text should not appear as chat history.
- Feedback text should be cleared when final response arrives.
- Feedback text should be cleared on error/completion/cancel.
- Feedback text should be optional/configurable.

## New option

Add to `ResponsiveFeedbackOptions`:

```csharp
public bool EnableFrontendActivityText { get; set; } = false;
```

Default false in first migration.

## Phase 6 success criteria

- Backend can optionally emit temporary feedback text.
- Frontend can display it in activity label or notification surface.
- Final assistant responses remain unchanged.
- Existing WebSocket response handling still works.
- Feature can be disabled with config.

---

# Phase 7: Testing Plan

## Unit tests for selector

Add:

```text
Merlin.Backend.Tests/FeedbackSelectorTests.cs
```

Test cases:

### Selects external app card

Given:

```text
Phase = Executing
Domain = ExternalApp
Duration = Short
Confidence = High
```

Expect selected card id:

```text
external_open_01
```

or one of the external open cards.

### Selects file search card

Given:

```text
Phase = Executing
Domain = FileSearch
Duration = Medium
```

Expect file/search card.

### Selects confirmation safety card

Given:

```text
NeedsConfirmation = true
Domain = Messaging or Confirmation
Phase = NeedsConfirmation
```

Expect confirmation/safety card with higher priority than generic cards.

### Suppresses low-score matches

Given:

```text
Context with no meaningful match
MinimumSelectionScore high
```

Expect no selection.

### Applies cooldown

Given same card selected once.

Immediately selecting again should choose:

- Different matching card, or
- No card.

Depending on available alternatives.

### Does not select speech card when speech is disallowed

Given:

```text
AllowSpeech = false
```

Expect no speech-only card.

### Respects max one feedback per turn

Depending on design, test through orchestrator or turn-level state.

## Unit tests for vector builder

Add:

```text
Merlin.Backend.Tests/FeedbackVectorBuilderTests.cs
```

Test cases:

- Starting phase sets `phase.starting`.
- Executing external app sets `domain.external_app` and `action.external`.
- Voice/orb interaction sets `interaction.voice` and `interaction.orb`.
- Confirmation context sets `risk.confirmation`.
- Unknowns do not throw.

## Unit tests for context factory

Add:

```text
Merlin.Backend.Tests/FeedbackContextFactoryTests.cs
```

Test cases:

- Maps `AssistantRequest.CorrelationId`.
- Maps normalized text.
- Maps voice/orb mode.
- Maps intent/tool to domain.
- Conservative fallback to general domain.
- Does not crash on missing/null intent result.

## Orchestrator tests

Add:

```text
Merlin.Backend.Tests/ResponsiveFeedbackOrchestratorTests.cs
```

Test cases:

### Queues selected feedback

Given selector returns card.

Expect speech playback service receives:

```text
Text = card.Text
CacheKey = feedback:{card.Id}
IsReplayableSpeech = true
```

### Does nothing when selector returns null

Expect no playback call.

### Does not throw into caller when playback fails

Given playback throws.

Expect orchestrator catches/logs and caller continues.

### Respects disabled config

Given `ResponsiveFeedback.Enabled = false`.

Expect no feedback.

### Respects speech disabled config

Given `EnableSpeechFeedback = false`.

Expect no speech feedback.

### Marks main response ready

Ensure orchestrator either:

- Calls through to progress handle, or
- Cancels pending feedback safely.

## CommandRouter integration tests

Add or update:

```text
Merlin.Backend.Tests/AcknowledgementIntegrationTests.cs
```

or:

```text
Merlin.Backend.Tests/ResponsiveFeedbackIntegrationTests.cs
```

Test cases:

### Feedback does not block final answer

Use a fake delayed/throwing playback service.

Expected:

- `CommandRouter.RouteAsync` still returns final response.

### Fast tool suppresses progress feedback

Given tool completes immediately.

Expect:

- No still-working feedback after main response ready.

### Slow tool gets progress feedback

Given delayed tool.

Expect:

- Progress feedback starts after configured threshold.

### One immediate feedback per turn

Given matching card and progress enabled.

Expect:

- Immediate feedback at most once.

### Confirmation flow chooses safety phrase

Given request that produces confirmation.

Expect:

- Feedback phrase does not imply action was performed.
- Phrase says prepare/ask/safe behavior.

## WebSocket tests if frontend packet added

Update:

```text
Merlin.Backend.Tests/WebSocketHandlerTests.cs
```

Test cases:

- Feedback event packet is well-formed.
- `correlationId` is included.
- Final `AssistantResponse` remains unchanged.
- Feedback event is not sent when disabled.

## Regression tests around old behavior

Do not delete existing tests unless replaced by equivalent tests.

Existing test files to preserve/extend:

```text
Merlin.Backend.Tests/RequestProgressSpeechServiceTests.cs
Merlin.Backend.Tests/AcknowledgementIntegrationTests.cs
```

## Test success criteria

- Existing tests pass.
- New selector tests pass.
- New orchestrator tests pass.
- CommandRouter behavior remains stable.
- Feedback failure does not break final responses.
- Progress cancellation still works.

---

# Phase 8: DI Registration

## Objective

Register new feedback services cleanly.

File:

```text
Merlin.Backend/Program.cs
```

Add near existing acknowledgement registrations.

Suggested registrations:

```csharp
builder.Services.Configure<ResponsiveFeedbackOptions>(
    builder.Configuration.GetSection("ResponsiveFeedback"));

builder.Services.AddSingleton<IFeedbackCardProvider, DefaultFeedbackCardProvider>();
builder.Services.AddSingleton<FeedbackVectorBuilder>();
builder.Services.AddSingleton<IFeedbackSelector, FeedbackSelector>();
builder.Services.AddSingleton<IFeedbackContextFactory, FeedbackContextFactory>();
builder.Services.AddSingleton<IResponsiveFeedbackOrchestrator, ResponsiveFeedbackOrchestrator>();
```

Actual lifetimes should match project conventions.

Likely:

- Card provider: singleton.
- Vector builder: singleton.
- Selector: singleton if cooldown state is thread-safe.
- Or selector scoped/transient plus separate singleton cooldown tracker.
- Context factory: singleton.
- Orchestrator: singleton if dependencies are singleton and thread-safe.

## Thread-safety warning

If `FeedbackSelector` stores cooldown/last-used state internally, it must be thread-safe.

Use one of:

```text
lock
ConcurrentDictionary
separate IFeedbackCooldownTracker
```

Preferred clean design:

```text
IFeedbackCooldownTracker
FeedbackCooldownTracker
```

Then selector remains easier to reason about.

## New optional file: FeedbackCooldownTracker.cs

Path:

```text
Merlin.Backend/Services/Feedback/FeedbackCooldownTracker.cs
```

Purpose:

Track:

- Last used card id.
- Last used card text.
- Last global feedback time.
- Per-correlation-id feedback count if needed.

Suggested interface:

```csharp
public interface IFeedbackCooldownTracker
{
    bool IsAllowed(FeedbackCard card, FeedbackContext context, DateTimeOffset now);
    void MarkUsed(FeedbackCard card, FeedbackContext context, DateTimeOffset now);
}
```

State examples:

```text
ConcurrentDictionary<string, DateTimeOffset> lastCardUse
ConcurrentDictionary<string, DateTimeOffset> lastTextUse
ConcurrentDictionary<string, int> perTurnImmediateCount
DateTimeOffset? lastGlobalUse
```

Cleanup:

- Remove old correlation ids eventually.
- Or only store short-lived entries.
- Avoid memory leaks.

## DI success criteria

- Services compile.
- Services resolve.
- No circular dependencies.
- Tests can easily inject fake selectors/providers/orchestrators.

---

# Phase 9: Migration of Existing Acknowledgement Services

## Objective

Avoid duplicate behavior and gradually move old acknowledgement code under the new feedback umbrella.

## Current system

Existing path:

```text
CommandRouter
  -> AcknowledgementSpeechService
  -> RequestProgressSpeechService
  -> AssistantSpeechPlaybackService
  -> TTS
```

Target path:

```text
CommandRouter
  -> ResponsiveFeedbackOrchestrator
      -> FeedbackSelector
      -> AssistantSpeechPlaybackService
      -> RequestProgressSpeechService
      -> TTS
```

Eventually:

```text
CommandRouter
  -> ResponsiveFeedbackOrchestrator
      -> FeedbackSelector
      -> ResponsiveProgressFeedbackService
      -> AssistantSpeechPlaybackService
      -> TTS
```

But do not jump straight to the final version.

## Migration step 9.1: keep old services alive

Do not delete:

```text
AcknowledgementPolicy.cs
AcknowledgementPhraseLibrary.cs
AcknowledgementSpeechService.cs
RequestProgressSpeechService.cs
```

in the first implementation.

Instead:

- Let new orchestrator call old services.
- Or let old phrase library be mirrored by new default cards.
- Keep tests passing.

## Migration step 9.2: route immediate feedback through new selector

The first behavior change should be:

```text
Old immediate acknowledgement phrase selection
        ↓
New FeedbackSelector card selection
```

But playback should still go through the same queue.

## Migration step 9.3: progress remains old

Keep:

```text
RequestProgressSpeechService
```

as-is initially.

Reason:

- It already handles progress timing.
- It already handles cancellation when final answer is ready.
- It already has tests.
- It is riskier to change than immediate acknowledgement.

## Migration step 9.4: optionally convert progress phrases later

After immediate feedback is stable, allow progress service to ask for cards:

```text
RequestProgressSpeechService
  -> FeedbackSelector.Select(progressContext)
```

Controlled by option:

```text
ResponsiveFeedback.UseCardSelectorForProgressFeedback
```

Default:

```text
false
```

for first migration.

## Migration step 9.5: rename only after behavior is stable

Do not rename folders/classes immediately.

Possible later rename:

```text
AcknowledgementSpeechService -> ResponsiveFeedbackSpeechService
RequestProgressSpeechService -> RequestFeedbackProgressService
AcknowledgementSpeechOptions -> ResponsiveFeedbackOptions
```

But this is a separate cleanup phase, not the initial migration.

## Phase 9 success criteria

- No duplicate progress loops.
- No duplicate speech cooldown systems fighting each other.
- Old tests still cover old behavior.
- New tests cover new selector/orchestrator behavior.
- Naming is allowed to be temporarily imperfect.

---

# Phase 10: Logging and Diagnostics

## Objective

Make feedback decisions understandable without flooding logs.

## What to log

When a feedback card is selected:

```text
correlationId
phase
domain
toolName
intent
cardId
score
reason
outputMode
cacheKey
```

When no card is selected:

```text
correlationId
phase
domain
reason
topCandidateScore, if available
```

When feedback is suppressed:

```text
correlationId
cardId, if known
suppression reason
```

Suppression reasons:

```text
disabled
speech_not_allowed
cooldown
same_turn_limit
final_response_ready
score_below_threshold
no_matching_cards
user_speaking
playback_busy
```

When playback fails:

```text
correlationId
cardId
exception message
```

Do not log sensitive full user text by default.

Maybe log normalized text only in development if current project already does that.

## Suggested log style

Use structured logging:

```csharp
_logger.LogInformation(
    "Responsive feedback selected. CorrelationId={CorrelationId} Phase={Phase} Domain={Domain} CardId={CardId} Score={Score} Reason={Reason}",
    context.CorrelationId,
    context.Phase,
    context.Domain,
    selection.Card.Id,
    selection.Score,
    selection.Reason);
```

## Diagnostics option

Add:

```csharp
public bool EnableDiagnosticsLogging { get; set; } = false;
```

to `ResponsiveFeedbackOptions`.

Keep normal logs minimal.

## Phase 10 success criteria

- Feedback behavior can be debugged.
- Logs do not spam during normal operation.
- No full raw user text is logged unnecessarily.
- Suppression reasons are visible when diagnostics are enabled.

---

# Phase 11: Rollback Plan

## Objective

The migration should be easy to disable if it causes speech, barge-in, or UX problems.

## Config rollback

Add this option:

```json
"ResponsiveFeedback": {
  "Enabled": false
}
```

When disabled, behavior should return to the previous acknowledgement/progress system.

## Code rollback strategy

Do not delete old acknowledgement services during first migration.

Keep this possible:

```text
CommandRouter uses old acknowledgement path
```

or:

```text
ResponsiveFeedbackOrchestrator delegates to old acknowledgement path
```

## Runtime rollback

If feedback causes issues:

1. Disable immediate feedback.
2. Keep progress feedback.
3. Disable frontend activity text.
4. Disable card selector for progress.
5. Fall back to old acknowledgement phrase library.

Suggested config levels:

```json
"ResponsiveFeedback": {
  "Enabled": true,
  "UseCardSelectorForImmediateFeedback": false,
  "UseCardSelectorForProgressFeedback": false,
  "EnableFrontendActivityText": false
}
```

## Phase 11 success criteria

- One config value can disable the new system.
- Old behavior is not deleted prematurely.
- Feedback can be disabled without breaking final responses.

---

# Implementation Checklist

## Preparation

- [ ] Run existing tests before changes.
- [ ] Inspect `CommandRouter.cs` acknowledgement/progress flow.
- [ ] Inspect `RequestProgressSpeechService.cs` cancellation behavior.
- [ ] Inspect `AcknowledgementSpeechService.cs` cache-key behavior.
- [ ] Inspect `AssistantSpeechPlaybackService.cs` queue behavior.
- [ ] Confirm where `SpeechPlaybackItemType` is defined.
- [ ] Confirm how `speechCacheKey` and `isReplayableSpeech` are passed.

## Add feedback model

- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackPhase.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackDomain.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackDurationEstimate.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackConfidence.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackOutputMode.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackContext.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackCard.cs`.
- [ ] Add `Merlin.Backend/Services/Feedback/FeedbackSelection.cs`.

## Add selector services

- [ ] Add `IFeedbackCardProvider.cs`.
- [ ] Add `DefaultFeedbackCardProvider.cs`.
- [ ] Add `FeedbackVectorBuilder.cs`.
- [ ] Add `IFeedbackSelector.cs`.
- [ ] Add `FeedbackSelector.cs`.
- [ ] Add `IFeedbackCooldownTracker.cs`, if needed.
- [ ] Add `FeedbackCooldownTracker.cs`, if needed.

## Add context factory

- [ ] Add `IFeedbackContextFactory.cs`.
- [ ] Add `FeedbackContextFactory.cs`.
- [ ] Map request metadata.
- [ ] Map voice/orb policy.
- [ ] Map intent/tool to domain.
- [ ] Map confirmation risk.
- [ ] Keep fallback conservative.

## Add options

- [ ] Add `Merlin.Backend/Configuration/ResponsiveFeedbackOptions.cs`.
- [ ] Add `ResponsiveFeedback` section to `appsettings.json`.
- [ ] Add development overrides only if needed.
- [ ] Register options in `Program.cs`.

## Add orchestrator

- [ ] Add `IResponsiveFeedbackOrchestrator.cs`.
- [ ] Add `ResponsiveFeedbackOrchestrator.cs`.
- [ ] Inject selector.
- [ ] Inject speech playback or acknowledgement speech service.
- [ ] Inject progress speech service.
- [ ] Inject options.
- [ ] Add safe exception handling.
- [ ] Add diagnostic logging.
- [ ] Use card id as cache key.
- [ ] Mark feedback as replayable speech.

## Register DI

- [ ] Register feedback services in `Program.cs`.
- [ ] Use appropriate singleton/transient lifetimes.
- [ ] Ensure cooldown state is thread-safe.
- [ ] Ensure no circular dependencies.

## Integrate CommandRouter

- [ ] Inject `IResponsiveFeedbackOrchestrator`.
- [ ] Inject `IFeedbackContextFactory`.
- [ ] Build initial feedback context.
- [ ] Enrich context after intent/tool selection.
- [ ] Queue immediate feedback once.
- [ ] Start progress feedback through orchestrator.
- [ ] Preserve `MarkMainResponseReady`.
- [ ] Preserve cancellation behavior.
- [ ] Ensure final response is never blocked by feedback failure.

## Tests

- [ ] Add `FeedbackSelectorTests.cs`.
- [ ] Add `FeedbackVectorBuilderTests.cs`.
- [ ] Add `FeedbackContextFactoryTests.cs`.
- [ ] Add `ResponsiveFeedbackOrchestratorTests.cs`.
- [ ] Add or update integration tests around `CommandRouter`.
- [ ] Keep existing acknowledgement tests passing.
- [ ] Run full backend test suite.

## Optional frontend

- [ ] Decide packet shape.
- [ ] Prefer visual event `FEEDBACK_TEXT` or extend visual state.
- [ ] Update `MerlinWebSocketClient.gd`.
- [ ] Update `Main.gd`.
- [ ] Ensure final answer clears temporary feedback.
- [ ] Keep feature disabled by default initially.

## Cleanup

- [ ] Remove duplicated phrase logic only after tests pass.
- [ ] Do not rename old services until behavior is stable.
- [ ] Update docs.
- [ ] Add comments explaining that feedback vectors are handcrafted metadata, not embeddings.

---

# Recommended Initial Feedback Cards

Start small. Do not add 100 cards immediately.

## General

```text
general_start_01: "Got it."
general_start_02: "Okay."
general_checking_01: "I’ll check."
general_working_01: "Working on it."
```

## External app/open

```text
external_open_01: "Opening that."
external_open_02: "Bringing that up."
external_open_03: "Launching it now."
```

## File/search

```text
file_search_01: "I’m looking through your files."
file_search_02: "Searching locally."
file_search_03: "Checking your files now."
```

## Web/search

```text
web_search_01: "I’m checking that online."
web_search_02: "Looking that up."
web_search_03: "Checking the latest info."
```

## Memory

```text
memory_lookup_01: "I’ll check what I remember."
memory_update_01: "I’ll save that."
memory_safe_01: "I’ll update that memory."
```

## Confirmation/safety

```text
confirmation_prepare_01: "I’ll prepare it first and ask before sending."
confirmation_safe_01: "I won’t send anything yet."
confirmation_review_01: "I’ll get it ready for review."
```

## Still working

```text
progress_medium_01: "This may take a second."
progress_medium_02: "I’m still checking."
progress_long_01: "I’m still working through that."
```

## Failure-safe

Use only when actual failure is known:

```text
failure_generic_01: "Something went wrong there."
failure_retry_01: "I couldn’t complete that."
```

---

# Recommended First Pull Request Scope

The first implementation should be intentionally limited.

## Include

- New feedback model classes.
- New default feedback card provider.
- New selector.
- New options.
- New orchestrator.
- DI registration.
- CommandRouter integration for immediate feedback only.
- Existing progress service still used as-is.
- Tests for selector/orchestrator/basic integration.

## Exclude

- Frontend feedback packets.
- JSON card loading.
- Dynamic phrase templates.
- Static WAV clip provider.
- Renaming `Acknowledgement` folder/classes.
- Rewriting `RequestProgressSpeechService`.
- Progress-card selector migration.

This keeps the first PR smaller and safer.

---

# Recommended Second Pull Request Scope

After the first PR is stable:

## Include

- Optional JSON card loading.
- More cards.
- More accurate domain mapping.
- Optional frontend temporary feedback event.
- Tests for WebSocket packet if frontend feedback is added.

## Exclude

- Major TTS rewrites.
- Barge-in rewrites.
- Renaming old classes.

---

# Recommended Third Pull Request Scope

After the second PR is stable:

## Include

- Migrate progress phrase selection to feedback cards.
- Add `UseCardSelectorForProgressFeedback = true`.
- Add progress card tests.
- Consider deprecating some old acknowledgement phrase code.

## Exclude

- Removing old services until equivalent tests exist.

---

# Recommended Final Cleanup Scope

Only after behavior is stable:

- Consider renaming `Services/Acknowledgement` to `Services/Feedback`.
- Or keep `Acknowledgement` as an implementation detail if the migration does not justify churn.
- Remove dead phrase selection code.
- Update documentation.
- Ensure tests cover all migrated behavior.

---

# Agent Instructions

When implementing this migration:

1. Work incrementally.
2. Do not delete the existing acknowledgement/progress services in the first pass.
3. Preserve all existing tests unless replacing them with equivalent or better tests.
4. Keep feedback short and truthful.
5. Do not make feedback generation depend on the main LLM.
6. Do not add GPU/CPU-heavy semantic systems.
7. Use handcrafted vector/tag matching only.
8. Prefer backend speech first.
9. Keep frontend changes optional and disabled by default.
10. Ensure feedback never blocks final responses.
11. Ensure feedback can be disabled by config.
12. Keep logs useful but not noisy.
13. Treat barge-in/self-echo as high-risk. Do not add excessive speech.
14. Make every behavior change testable.

---

# Acceptance Criteria

The migration is acceptable when:

- Merlin can choose feedback based on task context.
- External app/tool actions get more specific feedback than generic “got it.”
- Long-running requests still get progress speech.
- Fast final answers are not delayed by unnecessary feedback.
- Feedback phrases are varied but not random nonsense.
- Feedback uses stable phrase/cache ids.
- Feedback can be disabled through config.
- Existing acknowledgement/progress tests pass.
- New feedback selector/orchestrator tests pass.
- No local model, embedding model, vector DB, or GPU dependency is introduced.
- Barge-in/interruption behavior does not visibly regress.
- Frontend final response behavior remains unchanged.

---

# Final Target Architecture

Target architecture after full migration:

```text
CommandRouter
  ↓
FeedbackContextFactory
  ↓
ResponsiveFeedbackOrchestrator
  ├── FeedbackSelector
  │     ├── FeedbackVectorBuilder
  │     ├── FeedbackCardProvider
  │     └── FeedbackCooldownTracker
  ├── AssistantSpeechPlaybackService
  ├── RequestProgressSpeechService or ResponsiveProgressFeedbackService
  └── optional frontend feedback event sender
```

Audio path:

```text
FeedbackCard.Text
  ↓
speechCacheKey = feedback:{cardId}
  ↓
AssistantSpeechPlaybackService
  ↓
TtsRouter
  ↓
ChatterboxTtsProvider
  ↓
Chatterbox phrase cache
  ↓
audio playback
```

Frontend path, optional later:

```text
FeedbackSelection
  ↓
AssistantVisualEvent or feedback packet
  ↓
WebSocketHandler
  ↓
MerlinWebSocketClient.gd
  ↓
Main.gd activity_label / CoreOrb3D
```

---

# Final Notes

The key decision is to avoid building a second system beside the existing acknowledgement/progress system.

The migration should be framed as:

```text
Upgrade Merlin’s acknowledgement/progress speech into context-aware responsive feedback.
```

Not:

```text
Add a separate AI feedback engine.
```

The current codebase already has the most important foundations:

- Request lifecycle.
- Live turn states.
- Acknowledgement speech.
- Progress speech.
- Speech queue.
- Chatterbox phrase cache.
- Cancellation/suppression behavior.
- Tests around progress and acknowledgement.

The migration should preserve those foundations and add only the missing piece:

```text
a cheap, handcrafted, context-aware selector that chooses the right feedback at the right time.
```
