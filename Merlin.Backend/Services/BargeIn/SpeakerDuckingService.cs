using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class SpeakerDuckingService : ISpeakerDuckingService
{
    private readonly ILogger<SpeakerDuckingService> _logger;
    private readonly BargeInOptions _options;
    private readonly object _syncRoot = new();
    private bool _isDucked;

    public SpeakerDuckingService(IOptions<BargeInOptions> options, ILogger<SpeakerDuckingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public float CurrentVolumeMultiplier
    {
        get
        {
            lock (_syncRoot)
            {
                return _isDucked ? Math.Clamp(_options.DuckingVolumePercent / 100f, 0.0f, 1.0f) : 1.0f;
            }
        }
    }

    public bool IsDucked
    {
        get
        {
            lock (_syncRoot)
            {
                return _isDucked;
            }
        }
    }

    public void StartDucking(BargeInSpeechContext context)
    {
        lock (_syncRoot)
        {
            if (_isDucked)
            {
                return;
            }

            _isDucked = true;
        }

        _logger.LogInformation(
            "Speaker ducking started. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. VolumePercent: {VolumePercent}. FadeMs: {FadeMs}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            _options.DuckingVolumePercent,
            _options.DuckingFadeMs);
    }

    public void Restore(BargeInSpeechContext context, string reason)
    {
        lock (_syncRoot)
        {
            if (!_isDucked)
            {
                return;
            }

            _isDucked = false;
        }

        _logger.LogInformation(
            "Speaker ducking restored. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. RestoreMs: {RestoreMs}. Reason: {Reason}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            _options.DuckingRestoreMs,
            reason);
    }
}
