namespace Merlin.Backend.Models;

public sealed class ToolMetadata
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Examples { get; init; } = [];
}
