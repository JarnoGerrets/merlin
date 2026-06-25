using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class LiveInterruptionModelPort : IInterruptionModelPort
{
    private readonly ILocalAIChatService _chatService;
    private readonly IAnswerRecomposer _answerRecomposer;
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<LiveInterruptionModelPort> _logger;

    public LiveInterruptionModelPort(
        ILocalAIChatService chatService,
        IAnswerRecomposer answerRecomposer,
        IOptions<InterruptionHandlingOptions> options,
        ILogger<LiveInterruptionModelPort> logger)
    {
        _chatService = chatService;
        _answerRecomposer = answerRecomposer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClarificationResult> GenerateClarificationAsync(
        ClarificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLiveModelCalls)
        {
            throw new InvalidOperationException("Live interruption model calls are disabled.");
        }

        if (!_options.EnableClarificationCalls)
        {
            throw new InvalidOperationException("Live interruption clarification calls are disabled.");
        }

        var prompt = _answerRecomposer.BuildClarificationPrompt(request);
        _logger.LogInformation(
            "conversational_interruption_clarification_model_call_started MaxTokens: {MaxTokens}. PromptChars: {PromptChars}.",
            request.MaxTokens,
            prompt.Length);
        var result = await _chatService.GenerateResponseAsync(prompt, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Message))
        {
            throw new InvalidOperationException($"Clarification model call failed: {result.ErrorCode ?? "unknown"}.");
        }

        return _answerRecomposer.ParseClarificationResult(result.Message);
    }

    public async Task<ContinuationRecompositionResult> GenerateContinuationAsync(
        ContinuationRecompositionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLiveModelCalls)
        {
            throw new InvalidOperationException("Live interruption model calls are disabled.");
        }

        if (!_options.EnableContinuationRecomposition)
        {
            throw new InvalidOperationException("Live interruption continuation recomposition is disabled.");
        }

        var prompt = _answerRecomposer.BuildContinuationRecompositionPrompt(request);
        _logger.LogInformation(
            "conversational_interruption_continuation_model_call_started MaxTokens: {MaxTokens}. PromptChars: {PromptChars}.",
            request.MaxTokens,
            prompt.Length);
        var result = await _chatService.GenerateResponseAsync(prompt, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Message))
        {
            throw new InvalidOperationException($"Continuation model call failed: {result.ErrorCode ?? "unknown"}.");
        }

        return _answerRecomposer.ParseContinuationRecompositionResult(result.Message);
    }
}
