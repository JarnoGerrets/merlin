using System.Diagnostics;
using System.Text;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Vision;

public sealed class VisionSidecarHost : IVisionSidecarHost, IAsyncDisposable
{
    private readonly VisionSidecarClient _client;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VisionSidecarHost> _logger;
    private readonly VisionGestureEventRouter _router;
    private readonly IOptionsMonitor<VisionOptions> _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private StreamWriter? _input;
    private TaskCompletionSource<bool>? _readySignal;
    private int _restartAttempts;

    public VisionSidecarHost(
        IOptionsMonitor<VisionOptions> options,
        IWebHostEnvironment environment,
        VisionSidecarClient client,
        VisionGestureEventRouter router,
        ILogger<VisionSidecarHost> logger)
    {
        _options = options;
        _environment = environment;
        _client = client;
        _router = router;
        _logger = logger;
        State = options.CurrentValue.Enabled ? VisionHealthState.Idle : VisionHealthState.Disabled;
    }

    public VisionHealthState State { get; private set; }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            State = VisionHealthState.Disabled;
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureProcessStartedLockedAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StartTrackingAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            State = VisionHealthState.Disabled;
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureProcessStartedLockedAsync(cancellationToken);
            var options = _options.CurrentValue;
            _logger.LogInformation("VisionSidecarStartTrackingRequested");
            await SendCommandLockedAsync(new
            {
                type = "vision.start_tracking",
                cameraName = options.PreferredCameraName,
                cameraIndex = options.CameraIndex,
                modelAssetPath = ResolvePath(options.ModelAssetPath),
                width = options.Width,
                height = options.Height,
                fps = options.Fps,
                mirrorPreview = options.MirrorPreview,
                debugPreview = options.DebugPreview,
                emitRateHz = options.EmitRateHz,
                maxHands = options.MaxHands,
                primaryLostGraceMs = options.PrimaryLostGraceMs,
                primarySwitchDistanceThreshold = options.PrimarySwitchDistanceThreshold,
                pinchStartRatio = options.PinchStartRatio,
                pinchHoldRatio = options.PinchHoldRatio,
                pinchReleaseRatio = options.PinchReleaseRatio,
                pinchDebounceMs = options.PinchDebounceMs,
                smoothingAlpha = options.SmoothingAlpha,
                pointerDeadzone = options.PointerDeadzone
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            State = VisionHealthState.Faulted;
            _logger.LogWarning(exception, "VisionFaulted");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopTrackingAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled || _process is null || _process.HasExited)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_process is null || _process.HasExited)
            {
                return;
            }

            _logger.LogInformation("VisionSidecarStopTrackingRequested");
            await SendCommandLockedAsync(new { type = "vision.stop_tracking" }, cancellationToken);
        }
        catch (Exception exception)
        {
            State = VisionHealthState.Faulted;
            _logger.LogWarning(exception, "VisionFaulted");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    await SendCommandLockedAsync(new { type = "vision.shutdown" }, cancellationToken);
                    await _process.WaitForExitAsync(cancellationToken);
                }
                catch
                {
                    TryKillProcess();
                }
            }

            DisposeProcess();
            State = _options.CurrentValue.Enabled ? VisionHealthState.Stopped : VisionHealthState.Disabled;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync(CancellationToken.None);
        _gate.Dispose();
    }

    private async Task EnsureProcessStartedLockedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false } && _input is not null)
        {
            return;
        }

        await StartProcessLockedAsync(cancellationToken);
        var ready = _readySignal ?? throw new InvalidOperationException("Vision ready signal was not initialized.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await ready.Task.WaitAsync(linked.Token);
    }

    private Task StartProcessLockedAsync(CancellationToken cancellationToken)
    {
        DisposeProcess();
        var options = _options.CurrentValue;
        var scriptPath = ResolvePath(options.WorkerScriptPath);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Vision worker script was not found.", scriptPath);
        }

        _readySignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        State = VisionHealthState.Starting;
        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(options.PythonExecutable) ? "python" : options.PythonExecutable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _environment.ContentRootPath
        };
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";
        startInfo.ArgumentList.Add(scriptPath);

        _logger.LogInformation("VisionSidecarStarting ScriptPath: {ScriptPath}. PythonExecutable: {PythonExecutable}.", scriptPath, startInfo.FileName);
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start vision sidecar process.");
        _input = _process.StandardInput;
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            var wasTracking = State is VisionHealthState.Tracking;
            State = VisionHealthState.Faulted;
            _readySignal?.TrySetException(new InvalidOperationException("Vision sidecar exited before ready."));
            _logger.LogWarning("VisionSidecarExited ExitCode: {ExitCode}.", TryGetExitCode(_process));
            if (wasTracking && _restartAttempts == 0)
            {
                _restartAttempts++;
                _logger.LogWarning("VisionSidecarRestartAttempted");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250));
                    await StartTrackingAsync(CancellationToken.None);
                });
            }
        };

        _ = Task.Run(() => ReadOutputLoopAsync(_process, cancellationToken), CancellationToken.None);
        _ = Task.Run(() => DrainErrorAsync(_process), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task ReadOutputLoopAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!process.HasExited)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                await HandleOutputLineAsync(line, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            State = VisionHealthState.Faulted;
            _logger.LogWarning(exception, "VisionFaulted");
        }
    }

    private async Task HandleOutputLineAsync(string line, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (!line.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            _logger.LogInformation("Vision sidecar stdout: {Line}", line);
            return;
        }

        if (!_client.TryParseMessage(line, out var message))
        {
            _logger.LogWarning("Vision sidecar returned malformed JSON: {Line}", line);
            return;
        }

        if (message is null)
        {
            return;
        }

        switch (message.Type)
        {
            case "vision.ready":
                State = VisionHealthState.Ready;
                _restartAttempts = 0;
                _readySignal?.TrySetResult(true);
                _logger.LogInformation("VisionSidecarReady");
                break;
            case "vision.tracking_started":
                State = VisionHealthState.Tracking;
                _logger.LogInformation(
                    "VisionSidecarTrackingStarted CameraName: {CameraName}. ActualWidth: {ActualWidth}. ActualHeight: {ActualHeight}. ActualFps: {ActualFps}.",
                    message.CameraName,
                    message.ActualWidth,
                    message.ActualHeight,
                    message.ActualFps);
                break;
            case "vision.tracking_stopped":
                State = VisionHealthState.Ready;
                _logger.LogInformation("VisionSidecarTrackingStopped");
                break;
            case "vision.error":
                State = VisionHealthState.Faulted;
                _logger.LogWarning("VisionFaulted Error: {Error}. Message: {Message}.", message.Error, message.Message);
                break;
            case "gesture.pointer.move":
            case "gesture.pinch.start":
            case "gesture.pinch.move":
            case "gesture.pinch.end":
                await _router.RouteAsync(new VisionGestureEvent
                {
                    Type = message.Type,
                    PointerId = string.IsNullOrWhiteSpace(message.PointerId) ? "primary" : message.PointerId,
                    X = message.X,
                    Y = message.Y,
                    Confidence = message.Confidence,
                    Source = string.IsNullOrWhiteSpace(message.Source) ? "webcam" : message.Source
                }, cancellationToken);
                break;
            default:
                _logger.LogDebug("Vision sidecar event ignored. Type: {Type}.", message.Type);
                break;
        }
    }

    private async Task SendCommandLockedAsync(object command, CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited || _input is null)
        {
            throw new InvalidOperationException("Vision sidecar process is not running.");
        }

        var json = _client.SerializeCommand(command);
        _logger.LogInformation(
            "VisionSidecarCommandSending Length: {Length}. Json: {Json}",
            json.Length,
            json);
        await _input.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _input.FlushAsync(cancellationToken);
    }

    private async Task DrainErrorAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                _logger.LogInformation("Vision sidecar: {Line}", line);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void DisposeProcess()
    {
        TryKillProcess();
        _input?.Dispose();
        _input = null;
        _process?.Dispose();
        _process = null;
    }

    private void TryKillProcess()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, _environment.ContentRootPath);
    }

    private static int? TryGetExitCode(Process? process)
    {
        try
        {
            return process?.HasExited == true ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }
}
