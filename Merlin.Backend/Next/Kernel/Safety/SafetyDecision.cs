namespace Merlin.Backend.Next.Kernel.Safety;

public sealed record SafetyDecision(
    SafetyDecisionKind Kind,
    string Reason,
    string? ConfirmationPrompt = null);
