using System.Diagnostics;
using System.Text.Json;
using Merlin.VoiceTest.Models;

namespace Merlin.VoiceTest.Services;

public sealed class TestSessionRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly AudioCaptureService _audioCapture;
    private readonly VoiceActivityRecorder _recorder;
    private readonly WhisperTranscriptionService _transcription;
    private readonly PhraseEvaluator _evaluator;
    private readonly TranscriptNormalizerPreview _normalizerPreview;
    private readonly ReportWriter _reportWriter;

    public TestSessionRunner(
        AudioCaptureService audioCapture,
        VoiceActivityRecorder recorder,
        WhisperTranscriptionService transcription,
        PhraseEvaluator evaluator,
        TranscriptNormalizerPreview normalizerPreview,
        ReportWriter reportWriter)
    {
        _audioCapture = audioCapture;
        _recorder = recorder;
        _transcription = transcription;
        _evaluator = evaluator;
        _normalizerPreview = normalizerPreview;
        _reportWriter = reportWriter;
    }

    public async Task<TestSessionResult> RunAsync(VoiceTestOptions options, CancellationToken cancellationToken)
    {
        var phrases = LoadPhrases(options);
        if (options.MaxPhrases is > 0)
        {
            phrases = phrases.Take(options.MaxPhrases.Value).ToList();
        }

        var sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var projectRoot = FindProjectRoot();
        var reportRoot = Path.GetFullPath(Path.Combine(projectRoot, options.Output));
        var reportDirectory = Path.Combine(reportRoot, sessionId);
        var recordingDirectory = Path.Combine(projectRoot, "Recordings", sessionId);
        Directory.CreateDirectory(reportDirectory);
        Directory.CreateDirectory(recordingDirectory);

        var environment = CreateEnvironmentSnapshot(options);
        var session = new TestSessionResult
        {
            StartedAt = DateTimeOffset.Now,
            SessionId = sessionId,
            ReportDirectory = reportDirectory,
            RecordingDirectory = recordingDirectory,
            Config = options,
            Environment = environment,
            Phrases = phrases
        };

        PrintIntro(session);
        PrintEnvironment(environment, options);

        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];
            var attemptNumber = 1;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"Phrase {i + 1}/{phrases.Count}: {phrase.Id} [{phrase.Category}]");
                Console.WriteLine($"Expected: {phrase.ExpectedText}");
                if (!string.IsNullOrWhiteSpace(phrase.Notes))
                {
                    Console.WriteLine($"Note: {phrase.Notes}");
                }

                Console.Write("Press Enter when ready, or type q to stop: ");
                var ready = Console.ReadLine();
                if (string.Equals(ready, "q", StringComparison.OrdinalIgnoreCase))
                {
                    session.FinishedAt = DateTimeOffset.Now;
                    await _reportWriter.WriteAsync(session, cancellationToken);
                    return session;
                }

                var wavPath = Path.Combine(recordingDirectory, $"{phrase.Id}_attempt{attemptNumber}.wav");
                Console.WriteLine($"Speak now: {phrase.ExpectedText}");
                var attemptStopwatch = Stopwatch.StartNew();
                var startedAt = DateTimeOffset.Now;
                var diagnostics = await _recorder.RecordAsync(wavPath, phrase, options, cancellationToken);
                Console.WriteLine("Recording saved. Transcribing...");
                var transcription = await _transcription.TranscribeAsync(wavPath, options, cancellationToken);
                attemptStopwatch.Stop();

                diagnostics.TranscriptionLatencyMs = transcription.LatencyMs;
                diagnostics.TotalAttemptMs = attemptStopwatch.Elapsed.TotalMilliseconds;
                var evaluation = _evaluator.Evaluate(phrase, transcription.Text);
                var preview = _normalizerPreview.Preview(transcription.Text);
                var attempt = new TestAttempt
                {
                    PhraseId = phrase.Id,
                    AttemptNumber = attemptNumber,
                    StartedAt = startedAt,
                    FinishedAt = DateTimeOffset.Now,
                    ExpectedText = phrase.ExpectedText,
                    ActualTranscript = transcription.Text,
                    AudioDiagnostics = diagnostics,
                    Transcription = transcription,
                    Evaluation = evaluation,
                    NormalizerPreview = preview
                };

                Console.WriteLine($"Recognized: {transcription.Text}");
                if (!transcription.Succeeded)
                {
                    Console.WriteLine($"STT error: {transcription.Error}");
                }

                if (preview.Changed)
                {
                    Console.WriteLine($"Normalizer preview: {preview.PreviewTranscript}");
                }

                Console.Write("Rate: 1=correct, 2=minor mistake, 3=wrong, 4=retry phrase, 5=skip phrase: ");
                var rating = Console.ReadLine()?.Trim() ?? string.Empty;
                attempt.UserRating = rating switch
                {
                    "1" => "correct",
                    "2" => "minor mistake",
                    "3" => "wrong",
                    "4" => "retry",
                    "5" => "skip",
                    _ => "unrated"
                };
                session.Attempts.Add(attempt);

                if (!options.KeepAudio)
                {
                    TryDelete(wavPath);
                }

                if (attempt.UserRating == "retry")
                {
                    attemptNumber++;
                    continue;
                }

                if (attempt.UserRating == "skip")
                {
                    session.SkippedPhraseIds.Add(phrase.Id);
                }

                break;
            }
        }

        session.FinishedAt = DateTimeOffset.Now;
        await _reportWriter.WriteAsync(session, cancellationToken);
        return session;
    }

    private List<TestPhrase> LoadPhrases(VoiceTestOptions options)
    {
        var fileName = options.Phrases.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? options.Phrases
            : $"{options.Phrases}_phrases.json";
        var projectRoot = FindProjectRoot();
        var path = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(projectRoot, "TestPhrases", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<TestPhrase>>(json, JsonOptions) ?? [];
    }

    private EnvironmentSnapshot CreateEnvironmentSnapshot(VoiceTestOptions options)
    {
        var devices = _audioCapture.ListInputDevices();
        var deviceNumber = devices.Count > 0 ? _audioCapture.ResolveDeviceNumber(options.Device) : -1;
        var cuda = _transcription.DetectCuda(options);
        var scriptPath = Path.GetFullPath(Path.Combine(FindProjectRoot(), options.WhisperScriptPath));
        return new EnvironmentSnapshot
        {
            InputDevices = devices.ToList(),
            MicrophoneDetected = devices.Count > 0,
            SelectedInputDevice = deviceNumber >= 0 && deviceNumber < devices.Count ? devices[deviceNumber] : "None detected",
            SttScriptExists = File.Exists(scriptPath),
            PythonExecutableDetected = _transcription.CheckPythonExecutable(options),
            CudaDetected = cuda.Detected,
            CudaDetectionDetail = cuda.Detail
        };
    }

    private static void PrintIntro(TestSessionResult session)
    {
        Console.WriteLine("Merlin VoiceTest");
        Console.WriteLine("This test records guided phrases, transcribes them with Faster-Whisper, and writes STT/audio diagnostics.");
        Console.WriteLine($"Reports: {session.ReportDirectory}");
        Console.WriteLine($"Recordings: {session.RecordingDirectory}");
        Console.WriteLine($"Phrases: {session.Phrases.Count}");
        Console.WriteLine("Stop early: type q at a phrase prompt.");
        Console.WriteLine("Speak normally; do not over-enunciate.");
    }

    private static void PrintEnvironment(EnvironmentSnapshot environment, VoiceTestOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("Environment check");
        Console.WriteLine($"Microphone detected: {environment.MicrophoneDetected}");
        Console.WriteLine($"Selected input: {environment.SelectedInputDevice}");
        Console.WriteLine($"STT script exists: {environment.SttScriptExists}");
        Console.WriteLine($"Python detected: {environment.PythonExecutableDetected}");
        Console.WriteLine($"CUDA detected: {environment.CudaDetected?.ToString() ?? "unknown"} ({environment.CudaDetectionDetail})");
        Console.WriteLine($"Model/config: {options.Model}, device={options.DeviceType}, beam={options.BeamSize}, language={options.Language}, task={options.Task}, temperature={options.Temperature}");
        Console.WriteLine($"Mode: {options.Mode}");
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Merlin.VoiceTest.csproj")))
            {
                return directory.FullName;
            }

            var candidate = Path.Combine(directory.FullName, "Merlin.VoiceTest");
            if (File.Exists(Path.Combine(candidate, "Merlin.VoiceTest.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Retention cleanup is best-effort.
        }
    }
}
