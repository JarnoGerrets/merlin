namespace Merlin.VoiceTest.Models;

public sealed class TestSessionResult
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string ReportDirectory { get; set; } = string.Empty;
    public string RecordingDirectory { get; set; } = string.Empty;
    public VoiceTestOptions Config { get; set; } = new();
    public EnvironmentSnapshot Environment { get; set; } = new();
    public List<TestPhrase> Phrases { get; set; } = [];
    public List<TestAttempt> Attempts { get; set; } = [];
    public List<string> SkippedPhraseIds { get; set; } = [];
}

public sealed class EnvironmentSnapshot
{
    public string MachineName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
    public string OsVersion { get; set; } = Environment.OSVersion.ToString();
    public string DotNetVersion { get; set; } = Environment.Version.ToString();
    public string SelectedInputDevice { get; set; } = string.Empty;
    public List<string> InputDevices { get; set; } = [];
    public bool MicrophoneDetected { get; set; }
    public bool SttScriptExists { get; set; }
    public bool PythonExecutableDetected { get; set; }
    public bool? CudaDetected { get; set; }
    public string CudaDetectionDetail { get; set; } = string.Empty;
}
