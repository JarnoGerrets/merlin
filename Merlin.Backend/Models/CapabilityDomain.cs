namespace Merlin.Backend.Models;

public sealed class CapabilityDomain
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsImplemented { get; init; }

    public string? ImplementedIntent { get; init; }

    public string? MissingMessage { get; init; }

    public string SafetyLevel { get; init; } = string.Empty;
}
