using System.Text.Json;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class ConversationSummaryStore : IConversationSummaryStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<ConversationSummaryStore> _logger;
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private List<ConversationSummary> _summaries = [];

    public ConversationSummaryStore(ILogger<ConversationSummaryStore> logger)
        : this(GetDefaultStorePath(), logger)
    {
    }

    internal ConversationSummaryStore(string storePath, ILogger<ConversationSummaryStore> logger)
    {
        _storePath = storePath;
        _logger = logger;
        Load();
    }

    public bool IsHealthy { get; private set; } = true;

    public IReadOnlyCollection<ConversationSummary> GetAll()
    {
        lock (_syncRoot)
        {
            return _summaries.Select(CloneSummary).ToArray();
        }
    }

    public ConversationSummary SaveSummary(ConversationSummary summary)
    {
        lock (_syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var existing = _summaries.FirstOrDefault(item =>
                string.Equals(item.SummaryId, summary.SummaryId, StringComparison.OrdinalIgnoreCase));

            var saved = new ConversationSummary
            {
                SummaryId = string.IsNullOrWhiteSpace(summary.SummaryId)
                    ? Guid.NewGuid().ToString("N")
                    : summary.SummaryId,
                CreatedAtUtc = existing?.CreatedAtUtc ?? (summary.CreatedAtUtc == default ? now : summary.CreatedAtUtc),
                LastUpdatedUtc = now,
                Title = summary.Title.Trim(),
                SummaryText = summary.SummaryText.Trim(),
                Tags = summary.Tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                MessageCount = summary.MessageCount
            };

            ReplaceSummary(saved);
            Save();
            return CloneSummary(saved);
        }
    }

    public IReadOnlyList<ConversationSummary> GetRecentSummaries(int count)
    {
        lock (_syncRoot)
        {
            return _summaries
                .OrderByDescending(summary => summary.LastUpdatedUtc)
                .Take(Math.Max(0, count))
                .Select(CloneSummary)
                .ToArray();
        }
    }

    public IReadOnlyList<ConversationSummary> SearchSummaries(string query)
    {
        lock (_syncRoot)
        {
            var normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return [];
            }

            return _summaries
                .Where(summary =>
                    Normalize(summary.Title).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || Normalize(summary.SummaryText).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || summary.Tags.Any(tag => Normalize(tag).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(summary => summary.LastUpdatedUtc)
                .Select(CloneSummary)
                .ToArray();
        }
    }

    private void Load()
    {
        lock (_syncRoot)
        {
            try
            {
                var directory = Path.GetDirectoryName(_storePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(_storePath))
                {
                    _summaries = [];
                    Save();
                    IsHealthy = true;
                    return;
                }

                var json = File.ReadAllText(_storePath);
                var document = JsonSerializer.Deserialize<ConversationSummaryDocument>(json, JsonSerializerOptions);
                _summaries = document?.Summaries?.Select(CloneSummary).ToList() ?? [];
                IsHealthy = true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to load conversation summary store. Starting with an empty store.");
                _summaries = [];
                IsHealthy = false;
            }
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new ConversationSummaryDocument
        {
            Summaries = _summaries
                .OrderByDescending(summary => summary.LastUpdatedUtc)
                .Select(CloneSummary)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(document, JsonSerializerOptions);
        var tempPath = $"{_storePath}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, overwrite: true);
        IsHealthy = true;
    }

    private void ReplaceSummary(ConversationSummary summary)
    {
        _summaries.RemoveAll(item =>
            string.Equals(item.SummaryId, summary.SummaryId, StringComparison.OrdinalIgnoreCase));
        _summaries.Add(summary);
    }

    private static ConversationSummary CloneSummary(ConversationSummary summary)
    {
        return new ConversationSummary
        {
            SummaryId = summary.SummaryId,
            CreatedAtUtc = summary.CreatedAtUtc,
            LastUpdatedUtc = summary.LastUpdatedUtc,
            Title = summary.Title,
            SummaryText = summary.SummaryText,
            Tags = summary.Tags.ToArray(),
            MessageCount = summary.MessageCount
        };
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Merlin", "conversation-summaries.json");
    }

    private sealed class ConversationSummaryDocument
    {
        public IReadOnlyCollection<ConversationSummary> Summaries { get; init; } = [];
    }
}
