namespace Merlin.VoiceTest.Models;

public sealed class PhraseEvaluation
{
    public bool ExactMatchAfterNormalization { get; set; }
    public double WordErrorRate { get; set; }
    public double CharacterErrorRate { get; set; }
    public List<string> MissingImportantTerms { get; set; } = [];
    public List<string> SubstitutedImportantTerms { get; set; } = [];
    public List<string> SuspectedConfusionPairs { get; set; } = [];
    public int TranscriptLength { get; set; }
    public string NormalizedExpected { get; set; } = string.Empty;
    public string NormalizedActual { get; set; } = string.Empty;
}
