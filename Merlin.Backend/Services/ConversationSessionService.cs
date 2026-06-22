using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

// Legacy JSON conversation session memory. Quarantined after Memory Refactor PR 5.
// Do not use for normal conversation. Core SQLite memory is the active brain.
public sealed class ConversationSessionService : IConversationSessionService
{
    private const int MaxMessages = 20;
    private const int SummaryBatchSize = 10;
    private const int MaxSummaryLength = 4000;
    private readonly object _syncRoot = new();
    private readonly IConversationSummaryStore _summaryStore;
    private List<ConversationMessage> _messages = [];
    private ConversationSessionState _state;

    public ConversationSessionService(IConversationSummaryStore summaryStore)
    {
        _summaryStore = summaryStore;
        _state = CreateState();
    }

    public ConversationSession CurrentSession
    {
        get
        {
            lock (_syncRoot)
            {
                return CreateSnapshot();
            }
        }
    }

    public ConversationSession CreateSession()
    {
        lock (_syncRoot)
        {
            _state = CreateState();
            _messages = [];
            return CreateSnapshot();
        }
    }

    public void AddUserMessage(string content)
    {
        AddMessage("User", content);
    }

    public void AddAssistantMessage(string content)
    {
        AddMessage("Assistant", content);
    }

    public void AddMessage(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        lock (_syncRoot)
        {
            _messages.Add(new ConversationMessage
            {
                Role = NormalizeRole(role),
                Content = content.Trim(),
                TimestampUtc = DateTimeOffset.UtcNow
            });

            _state.LastUpdatedUtc = DateTimeOffset.UtcNow;
            CompactIfNeeded();
        }
    }

    public IReadOnlyList<ConversationMessage> GetRecentMessages(int maxMessages = MaxMessages)
    {
        lock (_syncRoot)
        {
            return _messages
                .TakeLast(Math.Max(0, maxMessages))
                .Select(CloneMessage)
                .ToArray();
        }
    }

    public void UpdateRunningSummary(string summary)
    {
        lock (_syncRoot)
        {
            _state.RunningSummary = summary.Trim();
            _state.LastUpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    public ConversationSummary? FinalizeCurrentSession()
    {
        lock (_syncRoot)
        {
            if (_messages.Count == 0 && string.IsNullOrWhiteSpace(_state.RunningSummary))
            {
                CreateSession();
                return null;
            }

            var summary = BuildConversationSummary();
            var savedSummary = _summaryStore.SaveSummary(summary);
            _state = CreateState();
            _messages = [];
            return savedSummary;
        }
    }

    public void ClearSession()
    {
        CreateSession();
    }

    private void CompactIfNeeded()
    {
        while (_messages.Count > MaxMessages)
        {
            var batchSize = Math.Min(SummaryBatchSize, _messages.Count);
            var messagesToSummarize = _messages.Take(batchSize).ToArray();
            var summaryFragment = CreateDeterministicSummary(messagesToSummarize);

            _state.RunningSummary = TrimSummary(string.IsNullOrWhiteSpace(_state.RunningSummary)
                ? summaryFragment
                : $"{_state.RunningSummary} {summaryFragment}");

            _messages = _messages.Skip(batchSize).ToList();
        }
    }

    private static string CreateDeterministicSummary(IReadOnlyCollection<ConversationMessage> messages)
    {
        var fragments = messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => $"{message.Role}: {message.Content}");

        return string.Join(" ", fragments);
    }

    private static string TrimSummary(string summary)
    {
        return summary.Length <= MaxSummaryLength
            ? summary
            : summary[^MaxSummaryLength..];
    }

    private ConversationSession CreateSnapshot()
    {
        return new ConversationSession
        {
            SessionId = _state.SessionId,
            CreatedAtUtc = _state.CreatedAtUtc,
            LastUpdatedUtc = _state.LastUpdatedUtc,
            RunningSummary = _state.RunningSummary,
            Messages = _messages.Select(CloneMessage).ToArray()
        };
    }

    private ConversationSummary BuildConversationSummary()
    {
        var messages = _messages.ToArray();
        var combinedText = string.Join(
            " ",
            new[] { _state.RunningSummary }
                .Concat(messages.Select(message => message.Content))
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        return new ConversationSummary
        {
            SummaryId = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = _state.CreatedAtUtc,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Title = GenerateTitle(combinedText),
            SummaryText = GenerateSummaryText(combinedText),
            Tags = GenerateTags(combinedText),
            MessageCount = messages.Length
        };
    }

    private static string GenerateTitle(string text)
    {
        var normalized = text.ToLowerInvariant();
        if (normalized.Contains("merlin") && normalized.Contains("backend"))
        {
            return "Merlin Backend Development";
        }

        if (normalized.Contains("local ai") || normalized.Contains("localai") || normalized.Contains("ollama"))
        {
            return "Local AI Discussion";
        }

        if (normalized.Contains("tool"))
        {
            return "Merlin Tool Discussion";
        }

        return "Conversation Summary";
    }

    private static string GenerateSummaryText(string text)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Conversation completed.";
        }

        var sentences = normalized
            .Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Take(4)
            .ToArray();

        var summary = sentences.Length == 0
            ? normalized
            : string.Join(". ", sentences) + ".";

        return summary.Length <= 800
            ? summary
            : summary[..800].Trim() + "...";
    }

    private static IReadOnlyCollection<string> GenerateTags(string text)
    {
        var normalized = text.ToLowerInvariant();
        var tags = new List<string> { "merlin" };

        if (normalized.Contains("backend"))
        {
            tags.Add("backend");
        }

        if (normalized.Contains("local ai") || normalized.Contains("localai") || normalized.Contains("ollama"))
        {
            tags.Add("local-ai");
        }

        if (normalized.Contains("application") || normalized.Contains("resolver"))
        {
            tags.Add("applications");
        }

        if (normalized.Contains("intent"))
        {
            tags.Add("intent-parsing");
        }

        if (normalized.Contains("tool"))
        {
            tags.Add("tools");
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ConversationMessage CloneMessage(ConversationMessage message)
    {
        return new ConversationMessage
        {
            Role = message.Role,
            Content = message.Content,
            TimestampUtc = message.TimestampUtc
        };
    }

    private static ConversationSessionState CreateState()
    {
        var now = DateTimeOffset.UtcNow;
        return new ConversationSessionState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now,
            LastUpdatedUtc = now
        };
    }

    private static string NormalizeRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "assistant" => "Assistant",
            "system" => "System",
            _ => "User"
        };
    }

    private sealed class ConversationSessionState
    {
        public string SessionId { get; init; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset LastUpdatedUtc { get; set; }

        public string RunningSummary { get; set; } = string.Empty;
    }
}
