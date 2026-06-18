using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class FakeWebSearchProvider : IWebSearchProvider
{
    public Task<WebSearchResponse> SearchAsync(
        WebSearchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = request.Query.Trim();
        var results = CreateResults(query, request.PreferOfficialSources)
            .Take(Math.Max(1, request.MaxResults))
            .ToList();

        return Task.FromResult(new WebSearchResponse(
            query,
            results,
            "Fake",
            true,
            null));
    }

    private static IReadOnlyList<WebSearchResult> CreateResults(string query, bool preferOfficialSources)
    {
        if (query.Contains("godot", StringComparison.OrdinalIgnoreCase))
        {
            return [
                Result("Godot Engine documentation", "https://docs.godotengine.org/en/stable/", "docs.godotengine.org", "Official Godot documentation and manual pages.", "Godot Docs", 0.98),
                Result("Godot GitHub repository", "https://github.com/godotengine/godot", "github.com/godotengine/godot", "Official Godot Engine source repository and issues.", "GitHub", 0.86),
                Result($"Search result for {query}", "https://example.com/godot-transparent-window", "example.com", "Community notes related to the requested Godot topic.", "Example", 0.62)
            ];
        }

        if (query.Contains("deepinfra", StringComparison.OrdinalIgnoreCase))
        {
            return [
                Result("DeepInfra pricing", "https://deepinfra.com/pricing", "deepinfra.com/pricing", "Official DeepInfra pricing page.", "DeepInfra", 0.97),
                Result("DeepInfra documentation", "https://deepinfra.com/docs", "deepinfra.com/docs", "Official documentation for DeepInfra services and models.", "DeepInfra", 0.83)
            ];
        }

        if (query.Contains("chatterbox", StringComparison.OrdinalIgnoreCase))
        {
            return [
                Result("Chatterbox project repository", "https://github.com/resemble-ai/chatterbox", "github.com/resemble-ai/chatterbox", "Project repository and implementation notes.", "GitHub", 0.91),
                Result("Chatterbox Turbo latency discussion", "https://example.com/chatterbox-turbo-latency", "example.com", "Public result mentioning Chatterbox Turbo latency measurements.", "Example", 0.72)
            ];
        }

        if (query.Contains("faster-whisper", StringComparison.OrdinalIgnoreCase))
        {
            return [
                Result("faster-whisper repository", "https://github.com/SYSTRAN/faster-whisper", "github.com/SYSTRAN/faster-whisper", "Official faster-whisper repository and documentation.", "GitHub", 0.94),
                Result("CTranslate2 documentation", "https://opennmt.net/CTranslate2/", "opennmt.net/CTranslate2", "CTranslate2 documentation relevant to decoding and GPU memory behavior.", "CTranslate2", 0.82)
            ];
        }

        return [
            Result($"Top public result for {query}", "https://example.com/search-result-1", "example.com", $"A deterministic fake result for {query}.", "Example", preferOfficialSources ? 0.7 : 0.8),
            Result($"Second public result for {query}", "https://example.org/search-result-2", "example.org", $"Another fake result for {query}.", "Example", 0.6)
        ];
    }

    private static WebSearchResult Result(
        string title,
        string url,
        string displayUrl,
        string snippet,
        string sourceName,
        double rankScore)
    {
        return new WebSearchResult(title, url, displayUrl, snippet, sourceName, null, rankScore);
    }
}
