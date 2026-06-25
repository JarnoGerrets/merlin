using Merlin.Backend.Models;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class AssistantSpeechInterruptionSpeechOutputPort : IInterruptionSpeechOutputPort
{
    private readonly IAssistantSpeechPlaybackService _speechPlaybackService;
    private readonly ILogger<AssistantSpeechInterruptionSpeechOutputPort> _logger;

    public AssistantSpeechInterruptionSpeechOutputPort(
        IAssistantSpeechPlaybackService speechPlaybackService,
        ILogger<AssistantSpeechInterruptionSpeechOutputPort> logger)
    {
        _speechPlaybackService = speechPlaybackService;
        _logger = logger;
    }

    public async Task SpeakInterruptionContentAsync(
        string turnId,
        string correlationId,
        string text,
        string contentKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _logger.LogInformation(
            "conversational_interruption_content_speech_queued TurnId: {TurnId}. CorrelationId: {CorrelationId}. ContentKind: {ContentKind}. TextLength: {TextLength}.",
            turnId,
            correlationId,
            contentKind,
            text.Length);

        var itemType = ContentKindToPlaybackItemType(contentKind);
        await _speechPlaybackService.EnqueueAsync(
            text,
            string.IsNullOrWhiteSpace(correlationId) ? turnId : correlationId,
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            cancellationToken,
            itemType);
    }

    private static SpeechPlaybackItemType ContentKindToPlaybackItemType(string contentKind)
    {
        return contentKind switch
        {
            "clarification" => SpeechPlaybackItemType.InterruptionClarification,
            "recomposed_continuation" => SpeechPlaybackItemType.InterruptionContinuation,
            "stop_confirmation" => SpeechPlaybackItemType.StopConfirmation,
            _ => SpeechPlaybackItemType.InterruptionClarification
        };
    }
}
