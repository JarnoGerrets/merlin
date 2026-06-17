namespace Merlin.Backend.Services.BargeIn;

public sealed class WindowsWasapiAcousticEchoCancellationService : IAcousticEchoCancellationService
{
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly IWindowsAecStatus _status;

    public WindowsWasapiAcousticEchoCancellationService(
        IWindowsAecStatus status,
        IBargeInDiagnosticsLogger diagnostics)
    {
        _status = status;
        _diagnostics = diagnostics;
    }

    public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
    {
        var mode = _status.IsActive ? AecMode.Active : AecMode.Unavailable;
        _diagnostics.AecInitialized(mode, _status.StatusReason);
        return Task.CompletedTask;
    }

    public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
    {
        if (_status.IsActive)
        {
            return new AecProcessResult
            {
                EchoReducedFrame = microphoneFrame,
                Mode = AecMode.Active,
                IsEchoCancellationActive = true,
                Reason = "Microphone frame came from a Windows WASAPI Communications capture stream with platform AEC active."
            };
        }

        return new AecProcessResult
        {
            EchoReducedFrame = microphoneFrame,
            Mode = AecMode.Unavailable,
            IsEchoCancellationActive = false,
            Reason = _status.StatusReason
        };
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
