using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class TargetScopeDetector : ITargetScopeDetector
{
    public TargetScopeDetectionResult Detect(string userText)
    {
        var original = userText ?? string.Empty;
        var text = Normalize(original);
        var action = DetectAction(text);
        var extractedTarget = ExtractTarget(original, action);
        var scores = new List<CapabilityScore>
        {
            Score(TargetScopes.LocalFiles, "file_access", text, [
                "file", "files", "folder", "folders", "directory", "path", "downloads",
                "documents", "desktop", "where this file", "where the file", "hard drive"
            ], "local file/folder target"),
            Score(TargetScopes.ProjectRepo, "codex_research", text, [
                "our setup", "this repo", "our code", "codebase", "project files", "implementation",
                "build error", "test failure", "compare our config", "in merlin", "backend",
                "frontend", "our chatterbox", "our godot"
            ], "project/repo target"),
            Score(TargetScopes.Calendar, "calendar", text, [
                "meeting", "meetings", "calendar", "appointment", "event", "schedule",
                "availability", "am i free", "tomorrow", "next week"
            ], "calendar target"),
            Score(TargetScopes.Email, "email", text, [
                "email", "emails", "mail", "inbox", "message from", "from school",
                "school email", "from work", "draft"
            ], "email target"),
            Score(TargetScopes.Memory, "memory_lookup", text, [
                "what did we discuss", "what did i say earlier", "remember", "saved memory",
                "yesterday in our chat", "conversation", "last time we talked", "we discussed"
            ], "memory/conversation-history target"),
            Score(TargetScopes.System, "system_resource", text, [
                "cpu", "ram", "memory usage", "system memory", "disk", "battery", "network",
                "what time is it", "current time", "current date", "timezone", "time zone",
                "default microphone", "settings", "volume"
            ], "system/device target"),
            Score(TargetScopes.Application, "open_app", text, [
                "open chrome", "launch", "start", "focus", "close app", "start discord",
                "launch spotify"
            ], "application target"),
            Score(TargetScopes.Web, "web_research", text, [
                "web", "internet", "online", "latest", "current price", "current pricing",
                "pricing", "release", "newest", "official docs", "documentation", "known issue",
                "github issue", "package version", "changelog", "vendor docs", "cuda",
                "vram", "deepinfra", "godot docs", "faster-whisper"
            ], "public/current/external target"),
            new("general_conversation", TargetScopes.Conversation, 0.20, "default conversation candidate")
        };

        ApplyContextAdjustments(text, action, scores);

        var orderedScores = scores
            .Select(score => score with { Score = Math.Clamp(score.Score, 0, 1) })
            .OrderByDescending(score => score.Score)
            .ToList();
        var best = orderedScores[0];
        var targetScope = best.Score >= 0.35 ? best.TargetScope : TargetScopes.Conversation;
        var confidence = best.Score >= 0.35 ? best.Score : 0.35;

        return new TargetScopeDetectionResult(
            action,
            targetScope,
            confidence,
            orderedScores,
            extractedTarget,
            best.Reason);
    }

    private static CapabilityScore Score(
        string scope,
        string capabilityId,
        string text,
        IReadOnlyList<string> terms,
        string reason)
    {
        var matches = terms.Count(term => ContainsWholePhrase(text, term));
        var score = matches == 0 ? 0.06 : Math.Min(0.52 + matches * 0.12, 0.92);
        return new CapabilityScore(capabilityId, scope, score, matches == 0 ? "no strong target signal" : reason);
    }

    private static void ApplyContextAdjustments(string text, string action, List<CapabilityScore> scores)
    {
        Add(scores, TargetScopes.Web, ContainsAny(text, ["search the web", "search web", "search the internet", "official docs"]) ? 0.28 : 0, "explicit public web/docs request");
        Add(scores, TargetScopes.Web, ContainsAny(text, ["current", "latest", "newest", "pricing"]) ? 0.18 : 0, "current public information request");
        Add(scores, TargetScopes.ProjectRepo, ContainsAny(text, ["our ", "this repo", "our setup", "based on the docs", "compared to official docs"]) ? 0.40 : 0, "project-owned context");
        Add(scores, TargetScopes.LocalFiles, ContainsAny(text, ["file exists", "folder"]) ? 0.18 : 0, "file location context");
        Add(scores, TargetScopes.Memory, ContainsAny(text, ["we discussed", "yesterday"]) ? 0.20 : 0, "past conversation context");
        Add(scores, TargetScopes.Web, action is "search" ? 0.08 : 0, "search action weakly supports web only with target signals");
    }

    private static void Add(List<CapabilityScore> scores, string scope, double amount, string reason)
    {
        if (amount <= 0)
        {
            return;
        }

        var index = scores.FindIndex(score => score.TargetScope == scope);
        if (index < 0)
        {
            return;
        }

        var score = scores[index];
        scores[index] = score with
        {
            Score = score.Score + amount,
            Reason = score.Reason == "no strong target signal" ? reason : $"{score.Reason}; {reason}"
        };
    }

    private static string DetectAction(string text)
    {
        if (ContainsAny(text, ["fix", "update", "change", "implement"])) return "implement";
        if (ContainsAny(text, ["check", "verify", "compare"])) return "verify";
        if (ContainsAny(text, ["search"])) return "search";
        if (ContainsAny(text, ["find out", "find"])) return "find";
        if (ContainsAny(text, ["look up", "lookup"])) return "lookup";
        if (ContainsAny(text, ["open", "launch", "start"])) return "open";
        return "ask";
    }

    private static string? ExtractTarget(string original, string action)
    {
        var text = original.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var prefix in new[]
        {
            "please look up ", "look up ", "lookup ", "can you find out ", "find out ",
            "search the web for ", "search web for ", "search the internet for ", "search for ",
            "find official ", "find ", "check if ", "check whether ", "fix "
        })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return text[prefix.Length..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('.', '!', '?', ';', ':', ',').ToLowerInvariant();
        return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string value, IReadOnlyCollection<string> terms)
    {
        return terms.Any(term => ContainsWholePhrase(value, term));
    }

    private static bool ContainsWholePhrase(string value, string phrase)
    {
        var index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        var afterIndex = index + phrase.Length;
        var afterIsBoundary = afterIndex >= value.Length || !char.IsLetterOrDigit(value[afterIndex]);
        return beforeIsBoundary && afterIsBoundary;
    }
}
