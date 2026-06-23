namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IAnswerRecomposer
{
    string BuildClarificationPrompt(ClarificationRequest request);

    string BuildContinuationRecompositionPrompt(ContinuationRecompositionRequest request);

    ClarificationResult ParseClarificationResult(string modelOutput);

    ContinuationRecompositionResult ParseContinuationRecompositionResult(string modelOutput);
}
