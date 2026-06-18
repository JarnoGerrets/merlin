namespace Merlin.Backend.Models;

public sealed record WebSearchRequest(
    string Query,
    int MaxResults,
    string? PreferredLanguage,
    string? Region,
    bool PreferOfficialSources,
    SearchFreshness Freshness);
