namespace Merlin.Backend.Services.BargeIn;

public sealed class DegradedAcousticEchoCancellationService : IAcousticEchoCancellationService
{
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private bool _initialized;

    public DegradedAcousticEchoCancellationService(IBargeInDiagnosticsLogger diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
    {
        _initialized = true;
        _diagnostics.AecInitialized(
            AecMode.DegradedNoOp,
            "Real acoustic echo cancellation is not active. Microphone frames are passed through unchanged; interruption acceptance must use stricter wake-word behavior when configured.");
        return Task.CompletedTask;
    }

    public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
    {
        if (!_initialized)
        {
            _diagnostics.AecInitialized(AecMode.DegradedNoOp, "AEC process called before initialization; using degraded pass-through.");
            _initialized = true;
        }

        return new AecProcessResult
        {
            EchoReducedFrame = microphoneFrame,
            Mode = AecMode.DegradedNoOp,
            IsEchoCancellationActive = false,
            Reason = "Degraded no-op AEC provider. Echo is not removed."
        };
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
