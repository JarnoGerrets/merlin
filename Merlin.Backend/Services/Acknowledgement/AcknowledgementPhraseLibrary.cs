using System.Collections.Concurrent;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Acknowledgement;

public sealed class AcknowledgementPhraseLibrary : IAcknowledgementPhraseLibrary
{
    private static readonly IReadOnlyList<AcknowledgementPhrase> Phrases =
    [
        new("general_reasoning_01", AcknowledgementCategory.GeneralReasoning, "Good question, sir. Let me gather my thoughts."),
        new("general_reasoning_02", AcknowledgementCategory.GeneralReasoning, "Interesting question. Let me think that through."),
        new("general_reasoning_03", AcknowledgementCategory.GeneralReasoning, "Understood. I will reason through that carefully."),
        new("general_reasoning_04", AcknowledgementCategory.GeneralReasoning, "Give me a moment, sir. I want to answer that properly."),

        new("deep_architecture_01", AcknowledgementCategory.DeepTechnicalArchitecture, "Understood. Let me reason through the architecture."),
        new("deep_architecture_02", AcknowledgementCategory.DeepTechnicalArchitecture, "Good point, sir. I will think through the tradeoffs."),
        new("deep_architecture_03", AcknowledgementCategory.DeepTechnicalArchitecture, "Let me map that out carefully."),
        new("deep_architecture_04", AcknowledgementCategory.DeepTechnicalArchitecture, "That needs a proper answer. Give me a moment."),

        new("research_recommendation_01", AcknowledgementCategory.ResearchRecommendation, "Of course, sir. Let me look into that properly."),
        new("research_recommendation_02", AcknowledgementCategory.ResearchRecommendation, "I will check the relevant details."),
        new("research_recommendation_03", AcknowledgementCategory.ResearchRecommendation, "Understood. I will compare the options carefully."),
        new("research_recommendation_04", AcknowledgementCategory.ResearchRecommendation, "Let me gather the important details first."),

        new("local_system_tool_01", AcknowledgementCategory.LocalSystemTool, "I am checking that now."),
        new("local_system_tool_02", AcknowledgementCategory.LocalSystemTool, "Checking now, sir."),
        new("local_system_tool_03", AcknowledgementCategory.LocalSystemTool, "One moment. I am checking the system."),
        new("local_system_tool_04", AcknowledgementCategory.LocalSystemTool, "Right away."),

        new("memory_search_01", AcknowledgementCategory.MemorySearch, "Of course. Let me check memory."),
        new("memory_search_02", AcknowledgementCategory.MemorySearch, "I will look through memory for that."),
        new("memory_search_03", AcknowledgementCategory.MemorySearch, "Let me retrieve the relevant context."),

        new("memory_save_01", AcknowledgementCategory.MemorySave, "Saved."),
        new("memory_save_02", AcknowledgementCategory.MemorySave, "Saved to long-term memory."),
        new("memory_save_03", AcknowledgementCategory.MemorySave, "Stored."),

        new("deepinfra_progress_01", AcknowledgementCategory.DeepInfraPendingProgress, "I have the context. I am putting it together now."),
        new("deepinfra_progress_02", AcknowledgementCategory.DeepInfraPendingProgress, "I am taking a little more care with this one."),
        new("deepinfra_progress_03", AcknowledgementCategory.DeepInfraPendingProgress, "Still working through it, sir."),

        new("tool_progress_01", AcknowledgementCategory.ToolPendingProgress, "The system check is still running."),
        new("tool_progress_02", AcknowledgementCategory.ToolPendingProgress, "I am still waiting for the tool result."),
        new("tool_progress_03", AcknowledgementCategory.ToolPendingProgress, "That command is taking a little longer than expected."),

        new("memory_progress_01", AcknowledgementCategory.MemoryPendingProgress, "I found some relevant memory. I am putting it together."),
        new("memory_progress_02", AcknowledgementCategory.MemoryPendingProgress, "I am narrowing down the memory context."),
        new("memory_progress_03", AcknowledgementCategory.MemoryPendingProgress, "I am still piecing that together."),

        new("generic_progress_01", AcknowledgementCategory.GenericStillWorkingProgress, "I am still on it."),
        new("generic_progress_02", AcknowledgementCategory.GenericStillWorkingProgress, "Still working on it, sir."),
        new("generic_progress_03", AcknowledgementCategory.GenericStillWorkingProgress, "This is taking a little longer than usual."),
        new("generic_progress_04", AcknowledgementCategory.GenericStillWorkingProgress, "Almost there.")
    ];

    private readonly ConcurrentDictionary<string, PhraseUsage> _usage = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastPhraseId;

    public AcknowledgementPhraseLibrary(IOptions<AcknowledgementSpeechOptions> options)
    {
        _ = options.Value;
    }

    public IReadOnlyCollection<string> CommonPhrases => Phrases.Select(phrase => phrase.Text).ToArray();

    public AcknowledgementPhrase Select(AcknowledgementCategory category, DateTimeOffset now, TimeSpan cooldown)
    {
        return SelectFrom(Phrases.Where(phrase => phrase.Category == category).ToArray(), now, cooldown);
    }

    public AcknowledgementPhrase SelectProgress(RequestProgressState state, DateTimeOffset now, TimeSpan cooldown)
    {
        var category = state switch
        {
            RequestProgressState.WaitingOnDeepInfra => AcknowledgementCategory.DeepInfraPendingProgress,
            RequestProgressState.WaitingOnTool => AcknowledgementCategory.ToolPendingProgress,
            RequestProgressState.WaitingOnMemory => AcknowledgementCategory.MemoryPendingProgress,
            _ => AcknowledgementCategory.GenericStillWorkingProgress
        };

        return Select(category, now, cooldown);
    }

    private AcknowledgementPhrase SelectFrom(IReadOnlyList<AcknowledgementPhrase> candidates, DateTimeOffset now, TimeSpan cooldown)
    {
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No acknowledgement phrases are configured for the requested category.");
        }

        var eligible = candidates
            .Where(phrase => !string.Equals(phrase.Id, _lastPhraseId, StringComparison.OrdinalIgnoreCase))
            .Where(phrase => !WasUsedWithin(phrase.Id, now, cooldown))
            .OrderBy(phrase => UsageFor(phrase.Id).LastUsedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(phrase => UsageFor(phrase.Id).RecentUseCount)
            .ThenBy(phrase => phrase.Id, StringComparer.Ordinal)
            .ToList();

        var selected = eligible.Count > 0
            ? eligible[0]
            : candidates
                .Where(phrase => !string.Equals(phrase.Id, _lastPhraseId, StringComparison.OrdinalIgnoreCase))
                .DefaultIfEmpty(candidates[0])
                .OrderBy(phrase => UsageFor(phrase.Id).LastUsedUtc ?? DateTimeOffset.MinValue)
                .ThenBy(phrase => UsageFor(phrase.Id).RecentUseCount)
                .ThenBy(phrase => phrase.Id, StringComparer.Ordinal)
                .First();

        var usage = _usage.AddOrUpdate(
            selected.Id,
            _ => new PhraseUsage(now, 1),
            (_, existing) => existing with
            {
                LastUsedUtc = now,
                RecentUseCount = existing.RecentUseCount + 1
            });
        _lastPhraseId = selected.Id;
        _ = usage;
        return selected;
    }

    private bool WasUsedWithin(string phraseId, DateTimeOffset now, TimeSpan cooldown)
    {
        var usage = UsageFor(phraseId);
        return usage.LastUsedUtc is not null && now - usage.LastUsedUtc.Value < cooldown;
    }

    private PhraseUsage UsageFor(string phraseId)
    {
        return _usage.GetValueOrDefault(phraseId) ?? new PhraseUsage(null, 0);
    }

    private sealed record PhraseUsage(DateTimeOffset? LastUsedUtc, int RecentUseCount);
}
