using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class TopicSummaryBuilder
{
    public string Build(ConversationTopicRecord topic, IReadOnlyCollection<string> concepts, string closeReason)
    {
        var summary = string.IsNullOrWhiteSpace(topic.Summary)
            ? $"The topic '{topic.Title}' was active but had little captured discussion."
            : topic.Summary.Trim();
        var conceptText = concepts.Count == 0 ? "none captured" : string.Join(", ", concepts.Take(20));
        return $"Topic: {topic.Title}\n\nSummary:\n{summary}\n\nKey concepts: {conceptText}\n\nOutcome: Closed because {closeReason}.";
    }
}

public sealed class TopicImportanceScorer
{
    private static readonly string[] ImportantConcepts =
    [
        "Merlin", "memory", "DeepInfra", "routing", "voice", "interruption",
        "architecture", "prompt compiler", "SQLite"
    ];

    public double Score(ConversationTopicRecord topic, IReadOnlyCollection<string> concepts, string closeReason)
    {
        var score = 0.5;
        if (topic.Title.Contains("Merlin", StringComparison.OrdinalIgnoreCase) ||
            concepts.Contains("Merlin", StringComparer.OrdinalIgnoreCase))
        {
            score += 0.2;
        }

        if (concepts.Any(concept => ImportantConcepts.Contains(concept, StringComparer.OrdinalIgnoreCase)))
        {
            score += 0.2;
        }

        if ((topic.Summary?.Length ?? 0) > 300)
        {
            score += 0.1;
        }

        if (closeReason is TopicCloseReasons.UserRequestedSummary or TopicCloseReasons.ImplementationCompleted)
        {
            score += 0.1;
        }

        return Math.Clamp(score, 0.1, 1.0);
    }
}

public sealed class TopicClosingService
{
    private readonly IConceptStore _conceptStore;
    private readonly IConversationStateStore _conversationStore;
    private readonly CurrentConversationMemoryService _currentConversation;
    private readonly IMemoryStore _memoryStore;
    private readonly MemoryWriter _memoryWriter;
    private readonly TopicImportanceScorer _scorer;
    private readonly TopicSummaryBuilder _summaryBuilder;

    public TopicClosingService(
        CurrentConversationMemoryService currentConversation,
        IConversationStateStore conversationStore,
        IMemoryStore memoryStore,
        IConceptStore conceptStore,
        MemoryWriter memoryWriter,
        TopicSummaryBuilder summaryBuilder,
        TopicImportanceScorer scorer)
    {
        _currentConversation = currentConversation;
        _conversationStore = conversationStore;
        _memoryStore = memoryStore;
        _conceptStore = conceptStore;
        _memoryWriter = memoryWriter;
        _summaryBuilder = summaryBuilder;
        _scorer = scorer;
    }

    public async Task<TopicCloseResult> CloseCurrentTopicAsync(
        string reason,
        bool userConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        var state = await _currentConversation.GetOrCreateCurrentStateAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(state.ActiveTopicId))
        {
            return new TopicCloseResult { Closed = false, Reason = "No active topic." };
        }

        var topic = await _conversationStore.GetTopicAsync(state.ActiveTopicId, cancellationToken);
        if (topic is null || !IsMeaningful(topic))
        {
            return new TopicCloseResult { Closed = false, TopicId = state.ActiveTopicId, Reason = "Topic had no meaningful content." };
        }

        var concepts = state.ActiveConcepts.Count > 0 ? state.ActiveConcepts : ExtractConceptsFromSummary(topic.Summary);
        var summary = _summaryBuilder.Build(topic, concepts, reason);
        var now = DateTimeOffset.UtcNow;
        var memory = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            MemoryType = "episode",
            Title = topic.Title,
            Content = summary,
            Summary = summary.Length <= 500 ? summary : summary[..497] + "...",
            Project = concepts.Contains("Merlin", StringComparer.OrdinalIgnoreCase) ? "Merlin" : null,
            Topic = topic.Title,
            Importance = _scorer.Score(topic, concepts, reason),
            Confidence = 0.85,
            UserConfirmed = userConfirmed,
            CreatedAt = now,
            UpdatedAt = now,
            Source = $"topic_close:{reason}",
            SourceConversationId = topic.ConversationId
        };

        await _memoryStore.SaveMemoryAsync(memory, cancellationToken);
        foreach (var conceptName in concepts.Take(20))
        {
            var concept = await _conceptStore.GetOrCreateConceptAsync(conceptName, "topic", cancellationToken);
            await _conceptStore.LinkMemoryToConceptAsync(memory.Id, concept.Id, 0.8, cancellationToken);
        }

        await _conversationStore.EndTopicAsync(topic.Id, "closed", topic.Summary, cancellationToken);
        return new TopicCloseResult
        {
            Closed = true,
            TopicId = topic.Id,
            MediumMemoryId = memory.Id,
            Summary = summary,
            Concepts = concepts,
            Reason = reason
        };
    }

    public async Task<TopicCloseResult?> CloseIfUserRequestedAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        var lower = userMessage.ToLowerInvariant();
        var requested = lower.Contains("save this topic", StringComparison.Ordinal) ||
            lower.Contains("summarize this topic", StringComparison.Ordinal) ||
            lower.Contains("store this discussion", StringComparison.Ordinal) ||
            lower.Contains("create a memory of this", StringComparison.Ordinal) ||
            lower.Contains("remember this whole discussion", StringComparison.Ordinal);

        return requested
            ? await CloseCurrentTopicAsync(TopicCloseReasons.UserRequestedSummary, userConfirmed: true, cancellationToken)
            : null;
    }

    private static bool IsMeaningful(ConversationTopicRecord topic)
    {
        return !string.IsNullOrWhiteSpace(topic.Summary) && topic.Summary.Length >= 10 &&
            !string.Equals(topic.Title, "General conversation", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractConceptsFromSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return [];
        }

        return summary.Split([' ', ',', '.', ':', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }
}
