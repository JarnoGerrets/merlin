using System.Text.Json;
using System.Text.RegularExpressions;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class UserProfileFactService
{
    private readonly IUserProfileFactStore _profileFactStore;

    public UserProfileFactService(IUserProfileFactStore profileFactStore)
    {
        _profileFactStore = profileFactStore;
    }

    public async Task<ProfileFactUpsertResult> UpsertAsync(
        ProfileFactCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var profileId = NormalizeProfileId(candidate.ProfileId);
        var key = candidate.Key.Trim();
        var value = NormalizeValue(candidate.Value);
        var existing = await _profileFactStore.GetActiveFactByKeyAsync(profileId, key, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null && string.Equals(existing.Value, value, StringComparison.OrdinalIgnoreCase))
        {
            var confirmed = await _profileFactStore.SaveFactAsync(existing with
            {
                DisplayText = candidate.DisplayText.Trim(),
                Priority = candidate.Priority,
                Confidence = Math.Max(existing.Confidence, candidate.Confidence),
                LastConfirmedAt = now,
                UpdatedAt = now,
                MetadataJson = MergeMetadata(candidate.MetadataJson, existing.Value)
            }, cancellationToken);

            return new ProfileFactUpsertResult
            {
                NoOpDuplicate = true,
                ActiveFact = confirmed,
                AcknowledgementText = $"I already had that saved: {confirmed.DisplayText}"
            };
        }

        if (existing is not null)
        {
            await _profileFactStore.SupersedeFactAsync(existing.Id, string.Empty, cancellationToken);
        }

        var fact = new UserProfileFact
        {
            Id = Guid.NewGuid().ToString("N"),
            ProfileId = profileId,
            Key = key,
            Category = candidate.Category.Trim(),
            Value = value,
            DisplayText = candidate.DisplayText.Trim(),
            Priority = Math.Clamp(candidate.Priority, 0, 1),
            Confidence = Math.Clamp(candidate.Confidence, 0, 1),
            Status = UserProfileFactStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now,
            LastConfirmedAt = now,
            SourceType = candidate.SourceType,
            SourceMemoryId = candidate.SourceMemoryId,
            SupersedesFactId = existing?.Id,
            MetadataJson = MergeMetadata(candidate.MetadataJson, existing?.Value)
        };

        var saved = await _profileFactStore.SaveFactAsync(fact, cancellationToken);
        return new ProfileFactUpsertResult
        {
            Created = existing is null,
            Updated = existing is not null,
            ActiveFact = saved,
            SupersededFact = existing,
            AcknowledgementText = existing is null
                ? $"I'll remember that: {saved.DisplayText}"
                : $"I'll remember that instead of {FormatValue(existing.Value)}: {saved.DisplayText}"
        };
    }

    private static string NormalizeProfileId(string profileId) =>
        string.IsNullOrWhiteSpace(profileId) ? UserProfileDefaults.ProfileId : profileId.Trim();

    private static string NormalizeValue(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", "_");

    private static string FormatValue(string value) =>
        value.Replace('_', '-');

    private static string? MergeMetadata(string? candidateMetadataJson, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(candidateMetadataJson) && string.IsNullOrWhiteSpace(previousValue))
        {
            return candidateMetadataJson;
        }

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(candidateMetadataJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(candidateMetadataJson);
                if (parsed is not null)
                {
                    foreach (var item in parsed)
                    {
                        metadata[item.Key] = item.Value;
                    }
                }
            }
            catch (JsonException)
            {
                metadata["rawMetadata"] = candidateMetadataJson;
            }
        }

        if (!string.IsNullOrWhiteSpace(previousValue))
        {
            metadata["previousValue"] = previousValue;
        }

        return JsonSerializer.Serialize(metadata);
    }
}

public sealed class UserProfileFactDetector
{
    public bool TryDetect(string userMessage, out ProfileFactCandidate candidate)
    {
        candidate = default!;
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return false;
        }

        var normalized = Normalize(userMessage);
        var rawMetadata = JsonSerializer.Serialize(new { rawUserText = userMessage.Trim() });

        if (!HasExplicitProfileCue(normalized))
        {
            return false;
        }

        if (ContainsAny(normalized, "medium to long", "medium-to-long", "longer responses", "longer explanations", "more detail", "detailed responses"))
        {
            candidate = Create(
                "response.length.default",
                "response_preferences",
                ContainsAny(normalized, "medium to long", "medium-to-long") ? "medium_to_long" : "long",
                ContainsAny(normalized, "medium to long", "medium-to-long")
                    ? "Jarno prefers medium-to-long responses by default."
                    : "Jarno prefers longer, more detailed responses by default.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "short responses", "short answers", "brief responses", "brief answers"))
        {
            candidate = Create(
                "response.length.default",
                "response_preferences",
                "short",
                "Jarno prefers short responses by default.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "concise", "concise answers", "concise responses"))
        {
            candidate = Create(
                "response.style.conciseness",
                "response_preferences",
                "concise",
                "Jarno wants concise responses by default.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "always be direct", "be direct", "more direct"))
        {
            candidate = Create(
                "response.tone.default",
                "response_preferences",
                "direct",
                "Jarno prefers direct responses by default.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "critical feedback", "be critical", "critical when reviewing architecture", "critical when reviewing", "honest feedback"))
        {
            candidate = Create(
                "response.criticism.default",
                "response_preferences",
                "critical",
                "Jarno wants critical, honest feedback when reviewing ideas or technical decisions.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "object mapping packages", "object-mapping packages", "object mapper", "automapper"))
        {
            candidate = Create(
                "coding.dependencies.object_mapping",
                "coding_preferences",
                "avoid",
                "Jarno prefers not to use object-mapping packages.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "separated concerns", "separate concerns", "separation of concerns"))
        {
            candidate = Create(
                "coding.style.separate_concerns",
                "coding_preferences",
                "prefer",
                "Jarno prefers code separated by concern.",
                rawMetadata);
            return true;
        }

        if ((normalized.Contains("merlin should fail closed", StringComparison.Ordinal) ||
             normalized.Contains("fail closed", StringComparison.Ordinal) ||
             normalized.Contains("fallback braindead", StringComparison.Ordinal) ||
             normalized.Contains("braindead fallback", StringComparison.Ordinal) ||
             normalized.Contains("memoryless fallback", StringComparison.Ordinal) ||
             normalized.Contains("not want merlin to run without memory", StringComparison.Ordinal) ||
             normalized.Contains("do not want merlin to run without memory", StringComparison.Ordinal))
            && (normalized.Contains("memory", StringComparison.Ordinal) || normalized.Contains("fallback", StringComparison.Ordinal)))
        {
            candidate = Create(
                "merlin.runtime.memory_required",
                "merlin_behavior",
                "fail_closed",
                "Merlin should fail closed for normal conversation if Core Memory is unavailable.",
                rawMetadata,
                priority: 1.0);
            return true;
        }

        if (ContainsAny(normalized, "manual memory cleanup", "manual memory maintenance", "manual cleanup"))
        {
            candidate = Create(
                "merlin.memory.manual_intervention",
                "merlin_behavior",
                "automatic_hygiene",
                "Jarno wants memory hygiene to be automatic rather than requiring constant manual cleanup.",
                rawMetadata);
            return true;
        }

        if (ContainsAny(normalized, "agent prompts", "implementation prompts", "detailed plans", "extensive markdown", "extensive md"))
        {
            candidate = Create(
                "workflow.agent_prompts.detail_level",
                "workflow_preferences",
                "detailed",
                "Jarno prefers detailed implementation prompts and planning notes.",
                rawMetadata);
            return true;
        }

        return false;
    }

    private static ProfileFactCandidate Create(
        string key,
        string category,
        string value,
        string displayText,
        string metadataJson,
        double priority = 0.9) =>
        new()
        {
            Key = key,
            Category = category,
            Value = value,
            DisplayText = displayText,
            Priority = priority,
            Confidence = 1.0,
            MetadataJson = metadataJson
        };

    private static bool HasExplicitProfileCue(string normalized)
    {
        if (ContainsAny(normalized, "i want", "i prefer", "i like", "i do not want", "i don't want", "from now on", "always", "never", "remember that", "save that", "be critical", "be direct"))
        {
            return true;
        }

        return normalized.StartsWith("merlin should ", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.Ordinal));

    private static string Normalize(string value)
    {
        var lower = value.Trim().ToLowerInvariant()
            .Replace('’', '\'')
            .Replace('-', ' ');
        return Regex.Replace(lower, "\\s+", " ");
    }
}
