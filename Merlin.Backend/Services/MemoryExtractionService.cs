using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

// Legacy JSON conversation memory extraction. Quarantined after Memory Refactor PR 5.
// Do not use for normal conversation. Core SQLite memory is the active brain.
public sealed class MemoryExtractionService : IMemoryExtractionService
{
    private readonly object _syncRoot = new();
    private readonly ILongTermMemoryStore _memoryStore;
    private readonly List<MemoryCandidate> _pendingCandidates = [];

    public MemoryExtractionService(ILongTermMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
    }

    public IReadOnlyCollection<MemoryCandidate> PendingCandidates
    {
        get
        {
            lock (_syncRoot)
            {
                return _pendingCandidates.Select(CloneCandidate).ToArray();
            }
        }
    }

    public IReadOnlyList<MemoryCandidate> ExtractFromSummary(ConversationSummary summary)
    {
        var text = $"{summary.Title} {summary.SummaryText} {string.Join(' ', summary.Tags)}";
        var normalized = text.ToLowerInvariant();
        var candidates = new List<MemoryCandidate>();

        if (normalized.Contains("prefers godot") || normalized.Contains("prefer godot"))
        {
            candidates.Add(CreateCandidate(
                "preference",
                "ui_framework",
                "Godot",
                $"conversation-summary:{summary.SummaryId}",
                0.85));
        }

        if (normalized.Contains("godot") && normalized.Contains(".net"))
        {
            candidates.Add(CreateCandidate(
                "project",
                "merlin_architecture",
                "Merlin uses a Godot frontend and .NET backend.",
                $"conversation-summary:{summary.SummaryId}",
                0.9));
        }
        else if (normalized.Contains("godot") && normalized.Contains("frontend"))
        {
            candidates.Add(CreateCandidate(
                "project",
                "frontend",
                "Merlin uses a Godot frontend.",
                $"conversation-summary:{summary.SummaryId}",
                0.85));
        }

        if (normalized.Contains("trusted command") && normalized.Contains("localai"))
        {
            candidates.Add(CreateCandidate(
                "operational",
                "trusted_commands",
                "Trusted commands bypass LocalAI.",
                $"conversation-summary:{summary.SummaryId}",
                0.85));
        }

        if (normalized.Contains("conversation summar"))
        {
            candidates.Add(CreateCandidate(
                "task",
                "conversation_summaries",
                "Conversation summaries are stored locally.",
                $"conversation-summary:{summary.SummaryId}",
                0.75));
        }

        AddCandidates(candidates);
        return candidates.Select(CloneCandidate).ToArray();
    }

    public IReadOnlyList<MemoryCandidate> ExtractFromTrustedApplication(TrustedApplicationMapping mapping)
    {
        var candidate = CreateCandidate(
            "operational",
            $"trusted_application_{NormalizeKey(mapping.Alias)}",
            $"{mapping.DisplayName} is a trusted application target for '{mapping.Alias}'.",
            "trusted-application",
            0.9);

        AddCandidates([candidate]);
        return [CloneCandidate(candidate)];
    }

    public IReadOnlyList<MemoryCandidate> ExtractFromTrustedCommand(TrustedCommandMapping mapping)
    {
        var candidate = CreateCandidate(
            "operational",
            $"trusted_command_{NormalizeKey(mapping.OriginalCommand)}",
            $"The trusted command '{mapping.OriginalCommand}' maps to '{mapping.NormalizedCommand}'.",
            "trusted-command",
            0.9);

        AddCandidates([candidate]);
        return [CloneCandidate(candidate)];
    }

    public MemoryRecord? ApproveCandidate(string candidateId)
    {
        lock (_syncRoot)
        {
            var candidate = _pendingCandidates.FirstOrDefault(item =>
                string.Equals(item.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase));
            if (candidate is null)
            {
                return null;
            }

            _pendingCandidates.Remove(candidate);
            return _memoryStore.MergeMemory(new MemoryRecord
            {
                Category = candidate.Category,
                Key = candidate.Key,
                Value = candidate.Value,
                Source = candidate.Source,
                Confidence = candidate.Confidence
            });
        }
    }

    public bool RejectCandidate(string candidateId)
    {
        lock (_syncRoot)
        {
            return _pendingCandidates.RemoveAll(item =>
                string.Equals(item.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    private void AddCandidates(IReadOnlyCollection<MemoryCandidate> candidates)
    {
        lock (_syncRoot)
        {
            foreach (var candidate in candidates)
            {
                _pendingCandidates.RemoveAll(item =>
                    string.Equals(item.Category, candidate.Category, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Key, candidate.Key, StringComparison.OrdinalIgnoreCase));
                _pendingCandidates.Add(candidate);
            }
        }
    }

    private static MemoryCandidate CreateCandidate(
        string category,
        string key,
        string value,
        string source,
        double confidence)
    {
        return new MemoryCandidate
        {
            CandidateId = Guid.NewGuid().ToString("N"),
            Category = category,
            Key = NormalizeKey(key),
            Value = value,
            Source = source,
            Confidence = Math.Clamp(confidence, 0, 1),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static MemoryCandidate CloneCandidate(MemoryCandidate candidate)
    {
        return new MemoryCandidate
        {
            CandidateId = candidate.CandidateId,
            Category = candidate.Category,
            Key = candidate.Key,
            Value = candidate.Value,
            Source = candidate.Source,
            Confidence = candidate.Confidence,
            CreatedAtUtc = candidate.CreatedAtUtc
        };
    }

    private static string NormalizeKey(string key)
    {
        return string.Join(
                ' ',
                key.Trim()
                    .ToLowerInvariant()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Replace(' ', '_');
    }
}
