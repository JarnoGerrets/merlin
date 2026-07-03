namespace Merlin.Backend.Configuration;

public sealed class StreamingResponseOptions
{
    public bool Enabled { get; set; } = true;

    public bool UseDeepInfraStreaming { get; set; } = true;

    public bool UseSegmentedTts { get; set; } = true;

    public bool FallbackToFullResponse { get; set; } = true;

    public int FirstSegmentMinWords { get; set; } = 8;

    public int LaterSegmentMinWords { get; set; } = 5;

    public int PreferredSentenceMaxChars { get; set; } = 220;

    public int HardBufferMaxChars { get; set; } = 320;

    public int TinyFinalTailMaxWords { get; set; } = 4;

    public bool AllowClauseBoundaries { get; set; } = true;

    public bool MergeTinyFinalTail { get; set; } = true;

    public int MaxPendingTtsSegments { get; set; } = 5;

    public int PrebufferSegments { get; set; } = 1;

    public int MaxReadyAudioSegments { get; set; } = 2;

    public bool SkipCodeBlocksInSpeech { get; set; } = true;

    public bool DebugLogSegmentBoundaries { get; set; } = true;
}
