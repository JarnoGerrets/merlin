using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public interface IPlaybackReferenceTap
{
    event EventHandler<BargeInSpeechContext>? SpeechStarted;

    event EventHandler<BargeInSpeechContext>? SpeechStopped;

    void NotifySpeechStarted(BargeInSpeechContext context);

    void NotifySpeechStopped(BargeInSpeechContext context);

    void PushPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId);

    void PushConsumedPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId);

    ReadOnlyMemory<float> GetLatestReferenceFrame(int sampleCount);

    bool TryGetReferenceWindow(int delayMs, int sampleCount, Span<float> destination);

    PlaybackReferenceDebugSnapshot GetDebugSnapshot();
}

public interface IAssistantPlaybackMonitor
{
    bool IsPlaybackActive { get; }

    DateTimeOffset? PlaybackStartedAt { get; }

    double CurrentPlaybackEnergy { get; }

    double RecentPlaybackEnergy { get; }
}

public interface IAcousticEchoCancellationService : IAsyncDisposable
{
    Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default);

    AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame);
}

public interface IBargeInVadService
{
    VadFrameResult ProcessFrame(VadFrameInput input, BargeInOptions options);

    void Reset();
}

public interface ISpeakerDuckingService
{
    event EventHandler<SpeakerDuckingChangedEventArgs>? DuckingChanged;

    float CurrentVolumeMultiplier { get; }

    bool IsDucked { get; }

    void StartDucking(BargeInSpeechContext context);

    void StartDucking(BargeInSpeechContext context, string reason);

    void Restore(BargeInSpeechContext context, string reason);
}

public interface IBargeInTriggerBuffer
{
    void AddFrame(BargeInAudioFrame frame);

    IReadOnlyList<BargeInAudioFrame> CaptureTriggeredWindow(BargeInAudioFrame triggerFrame, BargeInOptions options);

    IReadOnlyList<BargeInAudioFrame> CaptureTriggeredWindow(
        BargeInAudioFrame triggerFrame,
        BargeInOptions options,
        DateTimeOffset capturedUntil);

    BargeInTriggeredCapture CaptureTriggeredWindowWithDiagnostics(
        BargeInAudioFrame triggerFrame,
        BargeInOptions options,
        DateTimeOffset capturedUntil,
        string? currentAssistantTurnId);

    void Reset(string reason = "manual", string? assistantTurnId = null);
}

public interface IContinuousMicAudioBuffer
{
    BargeInAudioFrame Append(BargeInAudioFrame frame, BargeInOptions options);

    ContinuousMicAudioRange GetAudioRange(
        DateTimeOffset triggerTimestamp,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int requestedPreRollMs,
        BargeInOptions options);

    long DroppedFrames { get; }

    int BufferMilliseconds { get; }

    void Reset();
}

public interface IBargeInSttService
{
    Task<BargeInSttResult> TranscribeTriggerAsync(
        IReadOnlyList<BargeInAudioFrame> frames,
        BargeInOptions options,
        CancellationToken cancellationToken);
}

public interface IInterruptionCaptureDiagnosticsWriter
{
    Task SaveAsync(
        InterruptionCaptureDiagnostic diagnostic,
        IReadOnlyList<BargeInAudioFrame> frames,
        IReadOnlyList<InterruptionCaptureFrameDiagnostic> frameDiagnostics,
        CancellationToken cancellationToken);
}

public interface IInterruptionClassifier
{
    InterruptionClassificationResult Classify(InterruptionClassificationInput input, BargeInOptions options);
}

public interface ISelfSpeechSuppressionGate
{
    SelfSpeechGateResult Evaluate(SelfSpeechGateInput input, BargeInOptions options);

    void Reset();
}

public interface ISelfSpeechGateDiagnosticsWriter
{
    void Write(SelfSpeechGateDiagnosticEntry entry, BargeInOptions options);
}

public interface IBargeInCoordinator
{
    event Func<CorrectionRegenerationRequested, CancellationToken, Task>? CorrectionRegenerationRequested;

    event Func<BackendVoiceRequestCaptured, CancellationToken, Task>? BackendVoiceRequestCaptured;

    event Func<LiveUserUtteranceRouted, CancellationToken, Task>? LiveUserUtteranceRouted;

    bool IsMonitoring { get; }

    Task ProcessMicrophoneFrameAsync(BargeInAudioFrame frame, CancellationToken cancellationToken = default);

    Task StartLiveMonitoringAsync(CancellationToken cancellationToken = default);
}

public interface IBargeInAudioCaptureService
{
    Task SubmitMicrophoneFrameAsync(BargeInAudioFrame frame, CancellationToken cancellationToken = default);
}

public interface IWindowsAecStatus
{
    bool IsActive { get; }

    string ProviderName { get; }

    string StatusReason { get; }

    void MarkActive(string reason);

    void MarkUnavailable(string reason);
}

public interface IBargeInDiagnosticsLogger
{
    void MonitorStarted(BargeInSpeechContext context, AecMode aecMode);

    void MonitorStopped(BargeInSpeechContext context);

    void AecInitialized(AecMode mode, string reason);

    void PlaybackReferenceFrameReceived(string? correlationId, int sampleCount);

    void MicFrameProcessed(BargeInSpeechContext context, long frameCount);

    void EchoReducedFrameProcessed(BargeInSpeechContext context, long frameCount, AecMode aecMode);

    void VadPossibleSpeech(BargeInSpeechContext context, VadFrameResult result, AecMode aecMode);

    void TriggerBufferCaptured(BargeInSpeechContext context, int frameCount, TimeSpan duration);

    void GatedSttStarted(BargeInSpeechContext context, TimeSpan duration);

    void GatedSttResult(BargeInSpeechContext context, BargeInSttResult result);

    void ClassificationResult(BargeInSpeechContext context, InterruptionClassificationResult result);

    void StateChanged(BargeInSpeechContext context, BargeInState state, string reason);

    void ActionSelected(BargeInSpeechContext context, BargeInAction action, string reason);

    void PlaybackResumed(BargeInSpeechContext context, string reason);

    void CorrectionRegenerationStarted(BargeInSpeechContext context, string correctionText);

    void Ignored(BargeInSpeechContext context, string reason);

    void Accepted(BargeInSpeechContext context, InterruptionClassificationResult result);

    void AssistantTurnCancelled(BargeInSpeechContext context, InterruptionClassificationResult result);
}
