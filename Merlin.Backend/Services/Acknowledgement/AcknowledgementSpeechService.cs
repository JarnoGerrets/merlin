using System.Diagnostics;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Acknowledgement;

public sealed class AcknowledgementSpeechService : IAcknowledgementSpeechService
{
    private readonly IAssistantSpeechPlaybackService _speechPlaybackService;
    private readonly ILogger<AcknowledgementSpeechService> _logger;
    private readonly AcknowledgementSpeechOptions _options;

    public AcknowledgementSpeechService(
        IAssistantSpeechPlaybackService speechPlaybackService,
        IOptions<AcknowledgementSpeechOptions> options,
        ILogger<AcknowledgementSpeechService> logger)
    {
        _speechPlaybackService = speechPlaybackService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SpeakInitialAcknowledgementAsync(
        AcknowledgementPlaybackRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Decision.ShouldSpeakInitialAcknowledgement
            || string.IsNullOrWhiteSpace(request.Decision.PhraseText))
        {
            _logger.LogInformation(
                "Acknowledgement skipped. RequestId: {RequestId}. CorrelationId: {CorrelationId}. Reason: {Reason}",
                request.RequestId,
                request.CorrelationId,
                request.Decision.Reason);
            return;
        }

        try
        {
            var elapsed = DateTimeOffset.UtcNow - request.CommandReceivedAtUtc;
            _logger.LogInformation(
                "Acknowledgement playback requested. RequestId: {RequestId}. CorrelationId: {CorrelationId}. PhraseId: {PhraseId}. Category: {Category}. ElapsedMs: {ElapsedMs}. CachedOnly: {CachedOnly}.",
                request.RequestId,
                request.CorrelationId,
                request.Decision.PhraseId,
                request.Decision.InitialCategory,
                elapsed.TotalMilliseconds,
                _options.UseCachedAudioOnlyForAcknowledgements);

            await _speechPlaybackService.EnqueueAsync(
                request.Decision.PhraseText,
                request.CorrelationId,
                request.SendEventAsync,
                request.Decision.PhraseId,
                true,
                cancellationToken,
                SpeechPlaybackItemType.Acknowledgement,
                cancelOnlyBeforePlayback: true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Acknowledgement playback cancelled before start. RequestId: {RequestId}. CorrelationId: {CorrelationId}. PhraseId: {PhraseId}.",
                request.RequestId,
                request.CorrelationId,
                request.Decision.PhraseId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Acknowledgement playback request failed. RequestId: {RequestId}. CorrelationId: {CorrelationId}.",
                request.RequestId,
                request.CorrelationId);
        }
    }
}
