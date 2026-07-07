namespace Merlin.Backend.Next.Kernel.Capabilities;

public sealed record CapabilityDescriptor(
    string Id,
    string ModuleId,
    string DisplayName,
    string Description,
    IReadOnlyList<string> ExampleUtterances,
    IReadOnlySet<string> RequiredSurfaceCapabilities,
    CapabilityRiskLevel RiskLevel);
