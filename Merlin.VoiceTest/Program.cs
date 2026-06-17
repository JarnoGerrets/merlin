using System.Text.Json;
using Merlin.VoiceTest.Models;
using Merlin.VoiceTest.Services;

var options = LoadOptions(args);
var audioCapture = new AudioCaptureService();
var transcription = new WhisperTranscriptionService();
var runner = new TestSessionRunner(
    audioCapture,
    new VoiceActivityRecorder(audioCapture),
    transcription,
    new PhraseEvaluator(),
    new TranscriptNormalizerPreview(),
    new ReportWriter());

try
{
    var session = await runner.RunAsync(options, CancellationToken.None);
    Console.WriteLine();
    Console.WriteLine("VoiceTest complete.");
    Console.WriteLine($"Reports saved to: {session.ReportDirectory}");
    Console.WriteLine($"Recordings saved to: {session.RecordingDirectory}");
    Console.WriteLine($"Attempts: {session.Attempts.Count}");
    Console.WriteLine($"Exact matches: {session.Attempts.Count(a => a.Evaluation.ExactMatchAfterNormalization)}");
}
catch (Exception ex)
{
    Console.Error.WriteLine("VoiceTest failed.");
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}

static VoiceTestOptions LoadOptions(string[] args)
{
    var options = new VoiceTestOptions();
    var appSettingsPath = Path.Combine(FindProjectRoot(), "appsettings.json");
    if (File.Exists(appSettingsPath))
    {
        var json = File.ReadAllText(appSettingsPath);
        options = JsonSerializer.Deserialize<VoiceTestOptions>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? options;
    }

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = arg[2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[++i]
            : "true";
        ApplyOption(options, key, value);
    }

    return options;
}

static void ApplyOption(VoiceTestOptions options, string key, string value)
{
    switch (key.ToLowerInvariant())
    {
        case "phrases":
            options.Phrases = value;
            break;
        case "max-phrases":
            options.MaxPhrases = int.TryParse(value, out var maxPhrases) ? maxPhrases : options.MaxPhrases;
            break;
        case "mode":
            options.Mode = value;
            break;
        case "recording-seconds":
            options.RecordingSeconds = int.TryParse(value, out var recordingSeconds) ? recordingSeconds : options.RecordingSeconds;
            break;
        case "output":
            options.Output = value;
            break;
        case "keep-audio":
            options.KeepAudio = bool.TryParse(value, out var keepAudio) ? keepAudio : options.KeepAudio;
            break;
        case "device":
            options.Device = value;
            break;
        case "model":
            options.Model = value;
            break;
        case "beam-size":
            options.BeamSize = int.TryParse(value, out var beamSize) ? beamSize : options.BeamSize;
            break;
        case "device-type":
            options.DeviceType = value;
            break;
        case "python":
            options.PythonExecutable = value;
            break;
        case "compute-type":
            options.ComputeType = value;
            break;
        case "language":
            options.Language = value;
            break;
        case "dry-run":
            Console.WriteLine(JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(0);
            break;
    }
}

static string FindProjectRoot()
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
