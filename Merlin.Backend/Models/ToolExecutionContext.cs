namespace Merlin.Backend.Models;

public sealed class ToolExecutionContext
{
    public string OriginalMessage { get; init; } = string.Empty;

    public string NormalizedCommand { get; init; } = string.Empty;

    public string? Intent { get; init; }

    public CapabilityRouteResult? Route { get; init; }
}
