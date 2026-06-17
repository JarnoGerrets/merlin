namespace Merlin.Backend.Models;

public sealed record AssistantResponsePresentation(
    string SpokenText,
    string DisplayText,
    string CacheKey,
    bool PreferPhraseCache = true,
    bool IsReplayable = true);
