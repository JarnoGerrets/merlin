using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Merlin.VoiceTest.Models;

namespace Merlin.VoiceTest.Services;

public sealed class WhisperTranscriptionService
{
    public async Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        VoiceTestOptions options,
        CancellationToken cancellationToken)
    {
        var scriptPath = ResolvePath(options.WhisperScriptPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = options.PythonExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Directory.GetCurrentDirectory()
        };
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";
        foreach (var argument in options.PythonArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(wavPath);
        startInfo.ArgumentList.Add("--model-size");
        startInfo.ArgumentList.Add(options.Model);
        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add(options.DeviceType);
        startInfo.ArgumentList.Add("--compute-type");
        startInfo.ArgumentList.Add(options.ComputeType);
        startInfo.ArgumentList.Add("--language");
        startInfo.ArgumentList.Add(options.Language);
        startInfo.ArgumentList.Add("--beam-size");
        startInfo.ArgumentList.Add(options.BeamSize.ToString());
        startInfo.ArgumentList.Add("--vad-min-silence-duration-ms");
        startInfo.ArgumentList.Add(options.EndSilenceMs.ToString());
        startInfo.ArgumentList.Add("--initial-prompt");
        startInfo.ArgumentList.Add(options.InitialPrompt);

        var commandLine = $"{Quote(startInfo.FileName)} {string.Join(" ", startInfo.ArgumentList.Select(Quote))}";
        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Faster-Whisper process.");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, options.ProcessTimeoutSeconds)));
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                return new TranscriptionResult
                {
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                    CommandLine = commandLine,
                    StandardErrorTail = Tail(stderr),
                    Succeeded = false,
                    Error = $"Faster-Whisper exited with code {process.ExitCode}: {Tail(stderr)}"
                };
            }

            var payload = JsonSerializer.Deserialize<WhisperPayload>(stdout.Trim(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new TranscriptionResult
            {
                Text = payload?.Text ?? string.Empty,
                Language = payload?.Language ?? string.Empty,
                AudioDurationSeconds = payload?.Duration ?? 0,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                CommandLine = commandLine,
                StandardErrorTail = Tail(stderr),
                Succeeded = true
            };
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new TimeoutException($"Faster-Whisper timed out after {options.ProcessTimeoutSeconds} seconds.");
        }
    }

    public bool CheckPythonExecutable(VoiceTestOptions options)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = options.PythonExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--version");
            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public (bool? Detected, string Detail) DetectCuda(VoiceTestOptions options)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = options.PythonExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("import torch; print(torch.cuda.is_available())");
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (null, "Could not start Python.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            if (process.ExitCode != 0)
            {
                return (null, $"torch CUDA check failed: {Tail(error)}");
            }

            return (output.Contains("True", StringComparison.OrdinalIgnoreCase), output.Trim());
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private static string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        foreach (var root in CandidateRoots())
        {
            var candidate = Path.GetFullPath(Path.Combine(root, configuredPath));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            yield return directory.FullName;
            var voiceTest = Path.Combine(directory.FullName, "Merlin.VoiceTest");
            if (Directory.Exists(voiceTest))
            {
                yield return voiceTest;
            }

            directory = directory.Parent;
        }
    }

    private static string Tail(string value)
    {
        var lines = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Environment.NewLine, lines.TakeLast(20));
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup after timeout.
        }
    }

    private sealed class WhisperPayload
    {
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public double Duration { get; set; }
    }
}
