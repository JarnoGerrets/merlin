namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ContinuationRecompositionResult
{
    public string ContinuationText { get; init; } = "";

    public bool AvoidedRepeatingSpokenContent { get; init; }

    public bool IncludedClarificationContext { get; init; }
}
