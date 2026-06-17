using System.Text.RegularExpressions;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class ExplicitMemoryRequestDetector
{
    private static readonly (string Pattern, string Trigger, string? TypeHint)[] ExplicitStoragePatterns =
    [
        ("^please\\s+save\\s+(?:into|to|in)\\s+long-?term\\s+memory\\s+that\\s+", "save into long-term memory that", "long_term"),
        ("^save\\s+(?:into|to)\\s+long-?term\\s+memory\\s+that\\s+", "save to long-term memory that", "long_term"),
        ("^store\\s+in\\s+long-?term\\s+memory\\s+that\\s+", "store in long-term memory that", "long_term"),
        ("^add\\s+to\\s+long-?term\\s+memory\\s+that\\s+", "add to long-term memory that", "long_term"),
        ("^please\\s+save\\s+(?:into|to|in)\\s+memory\\s+that\\s+", "save to memory that", null),
        ("^save\\s+(?:into|to|in)\\s+memory\\s+that\\s+", "save to memory that", null),
        ("^store\\s+(?:into|in)\\s+memory\\s+that\\s+", "store in memory that", null),
        ("^add\\s+to\\s+memory\\s+that\\s+", "add to memory that", null),
        ("^put\\s+in\\s+memory\\s+that\\s+", "put in memory that", null),
        ("^save\\s+as\\s+project\\s+decision\\s+that\\s+", "save as project decision that", "project_decision"),
        ("^save\\s+as\\s+architecture\\s+decision\\s+that\\s+", "save as architecture decision that", "architecture_decision"),
        ("^save\\s+as\\s+(?:user\\s+)?preference\\s+that\\s+", "save as preference that", "user_preference"),
        ("^save\\s+as\\s+tool\\s+preference\\s+that\\s+", "save as tool preference that", "tool_preference")
    ];

    private static readonly (string Pattern, string Trigger)[] ImperativePatterns =
    [
        ("^please\\s+remember\\s+that\\s+", "please remember that"),
        ("^remember\\s+that\\s+", "remember that"),
        ("^remember\\s+this\\s+", "remember this"),
        ("^note\\s+that\\s+", "note that"),
        ("^note\\s+this\\s+", "note this"),
        ("^keep\\s+in\\s+mind\\s+that\\s+", "keep in mind that"),
        ("^from\\s+now\\s+on[,]?\\s+", "from now on"),
        ("^in\\s+the\\s+future[,]?\\s+", "in the future"),
        ("^always\\s+", "always"),
        ("^never\\s+", "never")
    ];

    private static readonly string[] RecallPrefixes =
    [
        "i remember",
        "do you remember",
        "what do you remember",
        "what did i say",
        "what did we say",
        "what did we decide",
        "what was the verdict",
        "can you recall",
        "recall",
        "search memory",
        "find memories",
        "show memories"
    ];

    public ExplicitMemoryRequest Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains("remind me", StringComparison.OrdinalIgnoreCase))
        {
            return new ExplicitMemoryRequest { IsExplicitMemoryRequest = false, Confidence = 0, Reason = "No explicit memory trigger." };
        }

        var trimmed = text.Trim();
        var normalized = Regex.Replace(trimmed.ToLowerInvariant(), "\\s+", " ");
        if (RecallPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return new ExplicitMemoryRequest
            {
                IsExplicitMemoryRequest = false,
                Confidence = 0,
                Reason = "Recall/search wording takes priority over memory save."
            };
        }

        foreach (var pattern in ExplicitStoragePatterns)
        {
            var match = Regex.Match(trimmed, pattern.Pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return BuildSaveRequest(trimmed[match.Length..], pattern.Trigger, pattern.TypeHint, 0.98);
            }
        }

        foreach (var pattern in ImperativePatterns)
        {
            var match = Regex.Match(trimmed, pattern.Pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var content = trimmed[match.Length..];
                if (pattern.Trigger is "always" or "never")
                {
                    content = $"{pattern.Trigger} {content}";
                }

                return BuildSaveRequest(content, pattern.Trigger, null, 0.9);
            }
        }

        return new ExplicitMemoryRequest { IsExplicitMemoryRequest = false, Confidence = 0, Reason = "No explicit memory trigger." };
    }

    private static ExplicitMemoryRequest BuildSaveRequest(string rawContent, string trigger, string? typeHint, double confidence)
    {
        var content = CleanContent(rawContent);
        return new ExplicitMemoryRequest
        {
            IsExplicitMemoryRequest = !string.IsNullOrWhiteSpace(content),
            ContentToRemember = content,
            TriggerPhrase = trigger,
            MemoryTypeHint = typeHint,
            Confidence = confidence,
            Reason = $"Detected explicit memory save trigger '{trigger}'."
        };
    }

    private static string CleanContent(string content)
    {
        var cleaned = content.Trim(' ', '.', ':', '-', '\"');
        return string.IsNullOrWhiteSpace(cleaned)
            ? string.Empty
            : char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }
}

public sealed class MemoryTypeClassifier
{
    public string Classify(string content, string? typeHint = null)
    {
        if (!string.IsNullOrWhiteSpace(typeHint))
        {
            return typeHint switch
            {
                "long_term" => "fact",
                "project_decision" => "project_decision",
                "architecture_decision" => "architecture_decision",
                "user_preference" => "user_preference",
                "tool_preference" => "tool_preference",
                _ => typeHint
            };
        }

        var lower = content.ToLowerInvariant();
        if (lower.Contains("i prefer", StringComparison.Ordinal) ||
            lower.Contains("i like", StringComparison.Ordinal) ||
            lower.Contains("i don't like", StringComparison.Ordinal))
        {
            return "user_preference";
        }

        if (lower.Contains("when formatting", StringComparison.Ordinal) ||
            lower.Contains("when using", StringComparison.Ordinal) ||
            lower.Contains("tool", StringComparison.Ordinal))
        {
            return "tool_preference";
        }

        if (lower.Contains("merlin should", StringComparison.Ordinal) ||
            lower.Contains("memory system should", StringComparison.Ordinal) ||
            lower.Contains("deepinfra should", StringComparison.Ordinal) ||
            lower.Contains("sqlite", StringComparison.Ordinal) ||
            lower.Contains("full chat history", StringComparison.Ordinal))
        {
            return "architecture_decision";
        }

        if (lower.Contains("goal", StringComparison.Ordinal) ||
            lower.Contains("token usage", StringComparison.Ordinal) ||
            lower.Contains("local-first", StringComparison.Ordinal) ||
            lower.Contains("cost", StringComparison.Ordinal))
        {
            return "project_goal";
        }

        if (lower.Contains("implement", StringComparison.Ordinal) ||
            lower.Contains("phase", StringComparison.Ordinal) ||
            lower.Contains("repository", StringComparison.Ordinal))
        {
            return "implementation_note";
        }

        return "fact";
    }
}

public sealed class MemoryWriter
{
    private readonly IConceptExtractionService _conceptExtractor;
    private readonly IConceptStore _conceptStore;
    private readonly ExplicitMemoryRequestDetector _detector;
    private readonly IMemoryStore _memoryStore;
    private readonly MemoryTypeClassifier _typeClassifier;

    public MemoryWriter(
        IMemoryStore memoryStore,
        IConceptStore conceptStore,
        IConceptExtractionService conceptExtractor,
        ExplicitMemoryRequestDetector detector,
        MemoryTypeClassifier typeClassifier)
    {
        _memoryStore = memoryStore;
        _conceptStore = conceptStore;
        _conceptExtractor = conceptExtractor;
        _detector = detector;
        _typeClassifier = typeClassifier;
    }

    public ExplicitMemoryRequest DetectExplicitRequest(string userMessage) => _detector.Detect(userMessage);

    public async Task<MemorySaveResult> SaveExplicitMemoryAsync(
        string userMessage,
        string? conversationId = null,
        string? turnId = null,
        CancellationToken cancellationToken = default)
    {
        var request = _detector.Detect(userMessage);
        if (!request.IsExplicitMemoryRequest || string.IsNullOrWhiteSpace(request.ContentToRemember))
        {
            return new MemorySaveResult { Saved = false, Message = request.Reason };
        }

        var content = request.ContentToRemember.Trim();
        var duplicate = await FindDuplicateAsync(content, cancellationToken);
        if (duplicate is not null)
        {
            return new MemorySaveResult
            {
                Saved = false,
                WasDuplicate = true,
                ExistingMemoryId = duplicate.Id,
                MemoryId = duplicate.Id,
                MemoryType = duplicate.MemoryType,
                Title = duplicate.Title,
                Concepts = _conceptExtractor.ExtractConceptNames(content),
                Message = "I already had that saved."
            };
        }

        var type = _typeClassifier.Classify(content, request.MemoryTypeHint);
        var now = DateTimeOffset.UtcNow;
        var memory = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            MemoryType = type,
            Title = CreateTitle(content),
            Content = content,
            Summary = content.Length <= 240 ? content : content[..237] + "...",
            Project = content.Contains("Merlin", StringComparison.OrdinalIgnoreCase) ? "Merlin" : null,
            Topic = type,
            Importance = ScoreImportance(type, content),
            Confidence = request.Confidence,
            UserConfirmed = true,
            CreatedAt = now,
            UpdatedAt = now,
            Source = "explicit_user_memory",
            SourceConversationId = conversationId,
            SourceTurnId = turnId
        };

        await _memoryStore.SaveMemoryAsync(memory, cancellationToken);
        var concepts = await LinkConceptsAsync(memory.Id, content, cancellationToken);

        return new MemorySaveResult
        {
            Saved = true,
            MemoryId = memory.Id,
            MemoryType = type,
            Title = memory.Title,
            Concepts = concepts,
            Message = "Saved."
        };
    }

    public async Task<IReadOnlyList<string>> LinkConceptsAsync(
        string memoryId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var conceptNames = _conceptExtractor.ExtractConceptNames(content)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        foreach (var conceptName in conceptNames)
        {
            var concept = await _conceptStore.GetOrCreateConceptAsync(conceptName, "extracted", cancellationToken);
            var weight = conceptName is "Merlin" or "memory" ? 1.0 : 0.85;
            await _conceptStore.LinkMemoryToConceptAsync(memoryId, concept.Id, weight, cancellationToken);
        }

        return conceptNames;
    }

    private async Task<MemoryRecord?> FindDuplicateAsync(string content, CancellationToken cancellationToken)
    {
        var normalized = Normalize(content);
        var candidates = await _memoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = FirstSearchTerm(content),
            MemoryTypes = ["architecture_decision", "project_decision", "project_goal", "user_preference", "tool_preference", "implementation_note", "fact"],
            Limit = 50
        }, cancellationToken);

        return candidates.Select(candidate => candidate.Memory)
            .FirstOrDefault(memory => Normalize(memory.Content) == normalized || Normalize(memory.Title ?? string.Empty) == normalized);
    }

    private static string FirstSearchTerm(string content)
    {
        var words = Regex.Matches(content, "[A-Za-z0-9]+")
            .Select(match => match.Value)
            .Where(word => word.Length > 3)
            .ToList();
        return words.FirstOrDefault() ?? content;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();

    private static string CreateTitle(string content)
    {
        var title = Regex.Replace(content, "^(Merlin should|The user prefers|I prefer)\\s+", string.Empty, RegexOptions.IgnoreCase).Trim();
        title = title.TrimEnd('.');
        return title.Length <= 80 ? title : title[..77] + "...";
    }

    private static double ScoreImportance(string type, string content)
    {
        var baseScore = type switch
        {
            "architecture_decision" => 0.9,
            "project_goal" => 0.85,
            "user_preference" => 0.8,
            "tool_preference" => 0.75,
            "implementation_note" => 0.7,
            _ => 0.6
        };

        if (content.Contains("never", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("always", StringComparison.OrdinalIgnoreCase))
        {
            baseScore += 0.05;
        }

        return Math.Clamp(baseScore, 0.1, 1.0);
    }
}
