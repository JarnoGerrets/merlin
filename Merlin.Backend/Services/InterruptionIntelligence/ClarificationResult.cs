namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ClarificationResult
{
    public string ReplyText { get; init; } = "";

    public string ClarificationContext { get; init; } = "";

    public bool ShouldRecomposeContinuation { get; init; } = true;

    public bool UserQuestionAnswered { get; init; } = true;
}
