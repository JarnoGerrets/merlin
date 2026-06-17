namespace Merlin.VoiceTest.Models;

public sealed class TestAttempt
{
    public string PhraseId { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public string ExpectedText { get; set; } = string.Empty;
    public string ActualTranscript { get; set; } = string.Empty;
    public string UserRating { get; set; } = string.Empty;
    public AudioDiagnostics AudioDiagnostics { get; set; } = new();
    public TranscriptionResult Transcription { get; set; } = new();
    public PhraseEvaluation Evaluation { get; set; } = new();
    public NormalizerPreview NormalizerPreview { get; set; } = new();
}

public sealed class NormalizerPreview
{
    public string RawTranscript { get; set; } = string.Empty;
    public string PreviewTranscript { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = [];
    public bool Changed { get; set; }
}
