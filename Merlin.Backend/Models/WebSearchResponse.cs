namespace Merlin.Backend.Models;

public sealed record WebSearchResponse(
    string Query,
    IReadOnlyList<WebSearchResult> Results,
    string Provider,
    bool IsSuccess,
    string? ErrorMessage);
