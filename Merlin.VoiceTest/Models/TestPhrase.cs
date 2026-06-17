namespace Merlin.VoiceTest.Models;

public sealed class TestPhrase
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ExpectedText { get; set; } = string.Empty;
    public List<string> AcceptableAlternatives { get; set; } = [];
    public List<string> ImportantTerms { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public int RecommendedRecordingSeconds { get; set; } = 5;
}
