namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class AnswerRecomposer : IAnswerRecomposer
{
    private readonly AnswerRecomposerPromptBuilder _promptBuilder = new();
    private readonly AnswerRecomposerJsonParser _jsonParser = new();

    public string BuildClarificationPrompt(ClarificationRequest request)
    {
        return _promptBuilder.BuildClarificationPrompt(request);
    }

    public string BuildContinuationRecompositionPrompt(ContinuationRecompositionRequest request)
    {
        return _promptBuilder.BuildContinuationRecompositionPrompt(request);
    }

    public ClarificationResult ParseClarificationResult(string modelOutput)
    {
        return _jsonParser.ParseClarificationResult(modelOutput);
    }

    public ContinuationRecompositionResult ParseContinuationRecompositionResult(string modelOutput)
    {
        return _jsonParser.ParseContinuationRecompositionResult(modelOutput);
    }
}
