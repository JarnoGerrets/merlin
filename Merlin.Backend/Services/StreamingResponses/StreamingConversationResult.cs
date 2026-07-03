namespace Merlin.Backend.Services.StreamingResponses;

public sealed class StreamingConversationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public bool SegmentedSpeechStarted { get; init; }

    public int SpeechSegmentsGenerated { get; init; }

    public bool FallbackUsed { get; init; }
}
