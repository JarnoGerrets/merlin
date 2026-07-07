using Merlin.Backend.Next.Kernel.Safety;

namespace Merlin.Backend.Next.Kernel.Capabilities;

public sealed record CapabilityResult(
    CapabilityResultKind Kind,
    string CapabilityId,
    string? UserFacingText = null,
    object? Payload = null,
    SafetyDecision? SafetyDecision = null,
    string? ErrorCode = null);
