using System.Text.RegularExpressions;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Search;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class FollowUpCueDetector
{
    private static readonly string[] Cues =
    [
        "this", "that", "it", "the system", "the thing", "what about", "and then",
        "okay but", "so now", "can you create the md", "create the todo",
        "write the prompt", "same", "continue", "tell me more", "how should"
    ];

    public IReadOnlyList<string> Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lower = text.ToLowerInvariant();
        return Cues
            .Where(cue => cue.Length <= 4
                ? Regex.IsMatch(lower, $"\\b{Regex.Escape(cue)}\\b", RegexOptions.IgnoreCase)
                : lower.Contains(cue, StringComparison.Ordinal))
            .Distinct()
            .ToList();
    }
}

public sealed class ActiveConceptMerger
{
    public IReadOnlyList<string> Merge(IReadOnlyCollection<string> existing, IReadOnlyCollection<string> incoming, int maxConcepts = 40)
    {
        return existing.Concat(incoming)
            .Where(concept => !string.IsNullOrWhiteSpace(concept))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxConcepts, 1, 100))
            .ToList();
    }
}

public sealed class TopicBoundaryDetector
{
    private static readonly string[] ExplicitNewTopicCues =
    [
        "new topic", "different question", "separate thing", "unrelated"
    ];

    private readonly FollowUpCueDetector _followUpCueDetector;

    public TopicBoundaryDetector(FollowUpCueDetector followUpCueDetector)
    {
        _followUpCueDetector = followUpCueDetector;
    }

    public TopicBoundaryDecision Analyze(
        string userMessage,
        CurrentConversationState current,
        IReadOnlyCollection<string> detectedConcepts)
    {
        var title = CreateTopicTitle(userMessage, detectedConcepts);
        if (string.IsNullOrWhiteSpace(current.ActiveTopicId))
        {
            return new TopicBoundaryDecision
            {
                IsNewTopic = true,
                ShouldClosePreviousTopic = false,
                Confidence = 0.9,
                SuggestedTopicTitle = title,
                Reason = "No active topic exists.",
                DetectedConcepts = detectedConcepts.ToList()
            };
        }

        var lower = userMessage.ToLowerInvariant();
        if (ExplicitNewTopicCues.Any(cue => lower.Contains(cue, StringComparison.Ordinal)))
        {
            return new TopicBoundaryDecision
            {
                IsNewTopic = true,
                ShouldClosePreviousTopic = true,
                Confidence = 0.95,
                SuggestedTopicTitle = title,
                Reason = "User explicitly indicated a new or unrelated topic.",
                DetectedConcepts = detectedConcepts.ToList()
            };
        }

        var followUpCues = _followUpCueDetector.Detect(userMessage);
        var active = current.ActiveConcepts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = detectedConcepts.Count == 0
            ? 0
            : detectedConcepts.Count(active.Contains) / (double)Math.Max(detectedConcepts.Count, active.Count == 0 ? 1 : Math.Min(active.Count, detectedConcepts.Count));

        if (followUpCues.Count > 0 || overlap >= 0.35)
        {
            return new TopicBoundaryDecision
            {
                IsNewTopic = false,
                ShouldClosePreviousTopic = false,
                Confidence = followUpCues.Count > 0 ? 0.85 : 0.75,
                SuggestedTopicTitle = current.ActiveTopicTitle,
                Reason = followUpCues.Count > 0 ? "Follow-up cue detected." : "Message shares active topic concepts.",
                DetectedConcepts = detectedConcepts.ToList(),
                FollowUpCues = followUpCues
            };
        }

        return new TopicBoundaryDecision
        {
            IsNewTopic = true,
            ShouldClosePreviousTopic = true,
            Confidence = 0.7,
            SuggestedTopicTitle = title,
            Reason = "Low concept overlap and no follow-up cue.",
            DetectedConcepts = detectedConcepts.ToList(),
            FollowUpCues = followUpCues
        };
    }

    public static string CreateTopicTitle(string userMessage, IReadOnlyCollection<string> concepts)
    {
        if (concepts.Count > 0)
        {
            return string.Join(" / ", concepts.Take(3));
        }

        var compact = Regex.Replace(userMessage.Trim(), "\\s+", " ");
        return compact.Length <= 80 ? compact : compact[..77] + "...";
    }
}

public sealed class CurrentConversationMemoryService
{
    private readonly ActiveConceptMerger _conceptMerger;
    private readonly IConceptExtractionService _conceptExtractor;
    private readonly TopicBoundaryDetector _topicBoundaryDetector;
    private readonly Stores.IConversationStateStore _conversationStore;
    private readonly IRuntimeTopicSession _runtimeTopicSession;
    private readonly ILogger<CurrentConversationMemoryService> _logger;

    public CurrentConversationMemoryService(
        Stores.IConversationStateStore conversationStore,
        IConceptExtractionService conceptExtractor,
        TopicBoundaryDetector topicBoundaryDetector,
        ActiveConceptMerger conceptMerger,
        IRuntimeTopicSession runtimeTopicSession,
        ILogger<CurrentConversationMemoryService> logger)
    {
        _conversationStore = conversationStore;
        _conceptExtractor = conceptExtractor;
        _topicBoundaryDetector = topicBoundaryDetector;
        _conceptMerger = conceptMerger;
        _runtimeTopicSession = runtimeTopicSession;
        _logger = logger;
    }

    public async Task<CurrentConversationState> GetOrCreateCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationStore.GetOrCreateActiveConversationAsync(cancellationToken);
        var topic = await _conversationStore.GetActiveTopicAsync(conversation.Id, cancellationToken);
        if (topic is null)
        {
            return CreateNeutralState(conversation.Id);
        }

        if (!_runtimeTopicSession.IsTopicTouchedInCurrentProcess(topic.Id))
        {
            _logger.LogInformation(
                "stale_current_topic_ignored. TopicId: {TopicId}. TopicTitle: {TopicTitle}. TopicUpdatedAt: {TopicUpdatedAt}. BackendStartedAt: {BackendStartedAt}. Reason: {Reason}. ConversationId: {ConversationId}. CorrelationId: {CorrelationId}.",
                topic.Id,
                topic.Title,
                topic.StartedAt,
                _runtimeTopicSession.BackendStartedAtUtc,
                "topic_not_touched_in_current_process",
                conversation.Id,
                null);
            return CreateNeutralState(conversation.Id);
        }

        return ToState(conversation.Id, topic, touchedInCurrentProcess: true);
    }

    public async Task<TopicBoundaryDecision> AnalyzeUserMessageAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateCurrentStateAsync(cancellationToken);
        var concepts = _conceptExtractor.ExtractConceptNames(userMessage);
        return _topicBoundaryDetector.Analyze(userMessage, state, concepts);
    }

    public async Task<CurrentConversationState> ApplyUserMessageAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateCurrentStateAsync(cancellationToken);
        if (TopicSummarySanitizer.IsLowValueUserMessage(userMessage))
        {
            return state;
        }

        var concepts = _conceptExtractor.ExtractConceptNames(userMessage);
        var decision = _topicBoundaryDetector.Analyze(userMessage, state, concepts);
        ConversationTopicRecord topic;

        if (decision.IsNewTopic)
        {
            topic = await _conversationStore.StartTopicAsync(
                state.ConversationId,
                decision.SuggestedTopicTitle ?? TopicBoundaryDetector.CreateTopicTitle(userMessage, concepts),
                cancellationToken);
            _runtimeTopicSession.MarkTopicTouched(topic.Id);
            state = state with { ActiveConcepts = [], RecentSummary = null };
        }
        else
        {
            topic = await _conversationStore.GetTopicAsync(state.ActiveTopicId!, cancellationToken)
                ?? await _conversationStore.StartTopicAsync(state.ConversationId, decision.SuggestedTopicTitle ?? "General conversation", cancellationToken);
            _runtimeTopicSession.MarkTopicTouched(topic.Id);
        }

        var mergedConcepts = _conceptMerger.Merge(ParseConcepts(topic.Summary).Concat(state.ActiveConcepts).ToList(), concepts);
        var summary = TopicSummarySanitizer.BuildRollingUserSummary(topic.Summary, userMessage, mergedConcepts);
        await _conversationStore.UpdateTopicSummaryAsync(topic.Id, summary, cancellationToken);

        return ToState(state.ConversationId, topic with { Summary = summary }, mergedConcepts);
    }

    public async Task<CurrentConversationState> UpdateAfterAssistantResponseAsync(
        string assistantResponse,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateCurrentStateAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(state.ActiveTopicId))
        {
            return state;
        }

        var topic = await _conversationStore.GetTopicAsync(state.ActiveTopicId, cancellationToken);
        if (topic is null)
        {
            return state;
        }

        if (TopicSummarySanitizer.IsClarificationBoilerplate(assistantResponse))
        {
            return ToState(state.ConversationId, topic, state.ActiveConcepts);
        }

        var concepts = _conceptExtractor.ExtractConceptNames(assistantResponse);
        var merged = _conceptMerger.Merge(state.ActiveConcepts, concepts);
        var summary = TopicSummarySanitizer.BuildRollingAssistantSummary(topic.Summary, assistantResponse, merged);
        await _conversationStore.UpdateTopicSummaryAsync(topic.Id, summary, cancellationToken);
        _runtimeTopicSession.MarkTopicTouched(topic.Id);
        return ToState(state.ConversationId, topic with { Summary = summary }, merged);
    }

    private static CurrentConversationState CreateNeutralState(string conversationId) => new()
    {
        ConversationId = conversationId,
        LastUpdatedUtc = DateTimeOffset.UtcNow
    };

    private static CurrentConversationState ToState(
        string conversationId,
        ConversationTopicRecord topic,
        IReadOnlyList<string>? concepts = null,
        bool touchedInCurrentProcess = true)
    {
        var parsedConcepts = concepts ?? ParseConcepts(topic.Summary);
        return new CurrentConversationState
        {
            ConversationId = conversationId,
            ActiveTopicId = topic.Id,
            ActiveTopicTitle = topic.Title,
            ActiveTopicUpdatedAt = topic.StartedAt,
            ActiveTopicTouchedInCurrentProcess = touchedInCurrentProcess,
            RecentSummary = TopicSummarySanitizer.SanitizeForSession(topic.Summary, topic.Title),
            ActiveConcepts = parsedConcepts,
            CurrentGoal = topic.Title,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    private static IReadOnlyList<string> ParseConcepts(string? summary)
    {
        var cleaned = TopicSummarySanitizer.CleanText(summary);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return [];
        }

        if (cleaned.StartsWith("User discussed ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["User discussed ".Length..];
        }
        else if (cleaned.StartsWith("Assistant response touched on ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["Assistant response touched on ".Length..];
        }

        var markerIndex = cleaned.IndexOf(':');
        if (markerIndex < 0)
        {
            return [];
        }

        return cleaned[..markerIndex]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("User discussed ", StringComparison.OrdinalIgnoreCase)
                ? value["User discussed ".Length..].Trim()
                : value)
            .Where(value => value.Length is > 1 and < 80)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
    }
}
