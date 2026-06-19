using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class SpeakerDuckingService : ISpeakerDuckingService
{
    private readonly ILogger<SpeakerDuckingService> _logger;
    private readonly BargeInOptions _options;
    private readonly object _syncRoot = new();
    private bool _isDucked;

    public event EventHandler<SpeakerDuckingChangedEventArgs>? DuckingChanged;

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
        StartDucking(context, "speech_active");
    }

    public void StartDucking(BargeInSpeechContext context, string reason)
    {
        lock (_syncRoot)
        {
            if (_isDucked)
            {
                return;
            }

            _isDucked = true;
        }

        var multiplier = CurrentVolumeMultiplier;
        DuckingChanged?.Invoke(this, new SpeakerDuckingChangedEventArgs
        {
            IsDucked = true,
            VolumeMultiplier = multiplier,
            Reason = reason,
            FadeDuration = TimeSpan.FromMilliseconds(Math.Max(0, _options.DuckingFadeMs)),
            ChangedAtUtc = DateTimeOffset.UtcNow
        });

        _logger.LogInformation(
            "Speaker ducking started. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. VolumePercent: {VolumePercent}. FadeMs: {FadeMs}. Reason: {Reason}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            _options.DuckingVolumePercent,
            _options.DuckingFadeMs,
            reason);
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

        var multiplier = CurrentVolumeMultiplier;
        DuckingChanged?.Invoke(this, new SpeakerDuckingChangedEventArgs
        {
            IsDucked = false,
            VolumeMultiplier = multiplier,
            Reason = reason,
            FadeDuration = TimeSpan.FromMilliseconds(Math.Max(0, _options.DuckingRestoreMs)),
            ChangedAtUtc = DateTimeOffset.UtcNow
        });

        _logger.LogInformation(
            "Speaker ducking restored. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. RestoreMs: {RestoreMs}. Reason: {Reason}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            _options.DuckingRestoreMs,
            reason);
    }
}
