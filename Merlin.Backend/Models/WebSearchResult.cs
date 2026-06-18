namespace Merlin.Backend.Models;

public sealed record WebSearchResult(
    string Title,
    string Url,
    string DisplayUrl,
    string Snippet,
    string? SourceName,
    DateTimeOffset? PublishedAt,
    double? RankScore);
