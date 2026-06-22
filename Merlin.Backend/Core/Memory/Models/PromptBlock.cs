namespace Merlin.Backend.Core.Memory.Models;

public sealed class PromptBlock
{
    public string Type { get; init; } = "";
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public int Priority { get; init; }
    public bool Required { get; init; }
    public int EstimatedTokens { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
