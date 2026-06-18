using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class WebSearchTool : ITool
{
    private readonly WebSearchService _webSearchService;

    public WebSearchTool(WebSearchService webSearchService)
    {
        _webSearchService = webSearchService;
    }

    public string Name => "Web Search";

    public string Description => "Search public web results and return source titles, URLs, domains, and snippets.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "search the web for chatterbox turbo latency",
        "web_search current DeepInfra pricing",
        "find official Godot docs for transparent windows"
    ];

    public bool CanHandle(string command)
    {
        return Normalize(command).StartsWith("web_search ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalize(command), "web_search", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var query = TryGetQuery(context);
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);
        var query = normalized.StartsWith("web_search ", StringComparison.OrdinalIgnoreCase)
            ? command.Trim()["web_search ".Length..].Trim()
            : string.Empty;
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    private async Task<ToolResult> ExecuteQueryAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Failure("Please tell me what to search for.", "WEB_SEARCH_EMPTY_QUERY");
        }

        var response = await _webSearchService.SearchAsync(query, cancellationToken);
        if (!response.IsSuccess)
        {
            return Failure(response.ErrorMessage ?? "Web search could not return results.", "WEB_SEARCH_PROVIDER_ERROR");
        }

        if (response.Results.Count == 0)
        {
            return Failure("I couldn't find reliable public results for that search.", "WEB_SEARCH_NO_RESULTS");
        }

        return new ToolResult
        {
            Success = true,
            Message = FormatResults(response),
            SpokenText = "I found a few public results. I can show the links on screen.",
            ToolName = Name,
            Intent = "web_search",
            CapabilityId = "web_search",
            CapabilityName = "Web Search",
            ResponseType = "tool"
        };
    }

    private static string TryGetQuery(ToolExecutionContext context)
    {
        if (context.Route?.Arguments.TryGetValue("query", out var query) == true
            && !string.IsNullOrWhiteSpace(query))
        {
            return query;
        }

        var normalized = Normalize(context.NormalizedCommand);
        return normalized.StartsWith("web_search ", StringComparison.OrdinalIgnoreCase)
            ? context.NormalizedCommand.Trim()["web_search ".Length..].Trim()
            : string.Empty;
    }

    private static ToolResult Failure(string message, string errorCode)
    {
        return new ToolResult
        {
            Success = false,
            Message = message,
            SpokenText = message,
            ErrorCode = errorCode,
            ToolName = "Web Search",
            Intent = "web_search",
            CapabilityId = "web_search",
            CapabilityName = "Web Search",
            ResponseType = "limitation"
        };
    }

    private static string FormatResults(WebSearchResponse response)
    {
        var lines = new List<string>
        {
            "Here are the top public results I found:",
            string.Empty
        };

        for (var index = 0; index < response.Results.Count; index++)
        {
            var result = response.Results[index];
            lines.Add($"{index + 1}. {result.Title}");
            lines.Add($"   {result.DisplayUrl}");
            lines.Add($"   {result.Snippet}");
            lines.Add($"   {result.Url}");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines).TrimEnd();
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
