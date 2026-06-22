using System.Text.RegularExpressions;
using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Services;

public enum MediumMemoryQualityDecision
{
    SaveActive,
    Skip,
    Archive
}

public sealed record MediumMemoryQualityInput
{
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyCollection<string> Concepts { get; init; } = [];
    public string? CloseReason { get; init; }
    public bool UserConfirmed { get; init; }
}

public sealed record MediumMemoryQualityResult
{
    public required MediumMemoryQualityDecision Decision { get; init; }
    public required string Reason { get; init; }
    public string? Category { get; init; }
}

public static class MediumMemoryQualityGate
{
    private static readonly string[] DanglingTitlePrefixes =
    [
        "and ",
        "but ",
        "because",
        "when ",
        "when you",
        "so then",
        "then ",
        "and then",
        "but when"
    ];

    private static readonly string[] WorkingContextTerms =
    [
        "merlin",
        "memory",
        "refactor",
        "promptblock",
        "prompt compiler",
        "responsivefeedback",
        "responsive feedback",
        "trusted_registry",
        "trusted registry",
        "debug",
        "diagnostic",
        "bug",
        "fix",
        "test",
        "tests",
        "c#",
        "dotnet",
        "backend",
        "frontend",
        "architecture",
        "decision",
        "implementation",
        "pr ",
        "phase",
        "roadmap",
        "schema",
        "sqlite",
        "database",
        "retrieval",
        "topic-close",
        "topic close",
        "vad",
        "interruption",
        "voice",
        "core memory",
        "fail closed",
        "open task",
        "unresolved",
        "next step"
    ];

    private static readonly string[] DurableDecisionTerms =
    [
        "decided",
        "decision",
        "must",
        "next step",
        "todo",
        "remember this",
        "save this",
        "important",
        "blocker",
        "root cause"
    ];

    private static readonly string[] GenericConversationTerms =
    [
        "general conversation",
        "meaning of life",
        "deeply personal",
        "philosophical",
        "many people find meaning",
        "ordinary factual",
        "casual chat"
    ];

    private static readonly string[] PollutedSummaryPatterns =
    [
        "general conversation about general conversation",
        "general conversation about since this is",
        "general conversation about it depends",
        "general conversation about there is no single answer",
        "general conversation about many people",
        "user discussed user discussed",
        "assistant response touched on user discussed",
        "message might be incomplete",
        "could you please clarify",
        "could you clarify",
        "please provide more context",
        "i'm here to help",
        "let me know what you need",
        "you ' re"
    ];

    public static MediumMemoryQualityResult EvaluateTopicClose(MediumMemoryQualityInput input)
    {
        var title = TopicSummarySanitizer.CleanText(input.Title, 200);
        var sessionSummary = TopicSummarySanitizer.SanitizeForSession(input.Summary, input.Title);
        var combined = Normalize($"{title} {sessionSummary} {string.Join(' ', input.Concepts)} {input.CloseReason}");
        var hasWorkingContext = HasAny(combined, WorkingContextTerms);
        var hasDurableDecision = input.UserConfirmed || HasAny(combined, DurableDecisionTerms);

        if (string.IsNullOrWhiteSpace(sessionSummary))
        {
            return Skip("empty_or_unusable_summary");
        }

        if (IsLowValueTitle(title) && !hasWorkingContext)
        {
            return Skip("low_value_or_partial_title");
        }

        if (ContainsPollution(combined))
        {
            return Skip("polluted_or_assistant_contaminated_summary");
        }

        if (IsGenericConversation(combined) && !(hasWorkingContext || hasDurableDecision))
        {
            return Skip("generic_conversation_without_working_context");
        }

        if (!(hasWorkingContext || hasDurableDecision))
        {
            return Skip("no_medium_memory_working_context");
        }

        return new MediumMemoryQualityResult
        {
            Decision = MediumMemoryQualityDecision.SaveActive,
            Reason = "useful_recent_working_context",
            Category = ClassifyCategory(combined)
        };
    }

    public static bool ShouldRenderMemory(RetrievedMemory memory)
    {
        if (!string.Equals(memory.MemoryType, "episode", StringComparison.OrdinalIgnoreCase))
        {
            return !ContainsPollution(Normalize($"{memory.Title} {memory.Summary} {memory.Content}"));
        }

        var title = TopicSummarySanitizer.CleanText(memory.Title, 200);
        var body = TopicSummarySanitizer.SanitizeForSession(memory.Summary ?? memory.Content, memory.Title);
        var combined = Normalize($"{title} {body} {memory.Content}");
        if (IsLowValueTitle(title))
        {
            return false;
        }

        if (ContainsPollution(combined))
        {
            return false;
        }

        if (IsGenericConversation(combined) && !HasAny(combined, WorkingContextTerms))
        {
            return false;
        }

        return true;
    }

    private static bool IsLowValueTitle(string title)
    {
        var normalized = Normalize(title);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (normalized.Contains(" ' ", StringComparison.Ordinal) ||
            normalized.Contains("you ' re", StringComparison.Ordinal))
        {
            return true;
        }

        if (DanglingTitlePrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        if (TopicSummarySanitizer.IsLowValueUserMessage(title))
        {
            return true;
        }

        return !HasMeaningfulNounLikeToken(normalized);
    }

    private static bool HasMeaningfulNounLikeToken(string text)
    {
        return Regex.Matches(text, @"[a-z0-9_#]+")
            .Select(match => match.Value)
            .Any(token => token.Length >= 4 || token is "c#" or "db" or "pr" or "vad");
    }

    private static bool IsGenericConversation(string text) =>
        HasAny(text, GenericConversationTerms);

    private static bool ContainsPollution(string text) =>
        HasAny(text, PollutedSummaryPatterns);

    private static bool HasAny(string text, IEnumerable<string> terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string ClassifyCategory(string text)
    {
        if (text.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("root cause", StringComparison.OrdinalIgnoreCase))
        {
            return "debugging_session";
        }

        if (text.Contains("architecture", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("decision", StringComparison.OrdinalIgnoreCase))
        {
            return "architecture_decision";
        }

        if (text.Contains("pr ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("phase", StringComparison.OrdinalIgnoreCase))
        {
            return "completed_phase_or_pr";
        }

        if (text.Contains("next step", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("todo", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("unresolved", StringComparison.OrdinalIgnoreCase))
        {
            return "open_task";
        }

        return "project_work";
    }

    private static MediumMemoryQualityResult Skip(string reason) => new()
    {
        Decision = MediumMemoryQualityDecision.Skip,
        Reason = reason
    };

    private static string Normalize(string value) =>
        string.Join(" ", value.ToLowerInvariant().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
