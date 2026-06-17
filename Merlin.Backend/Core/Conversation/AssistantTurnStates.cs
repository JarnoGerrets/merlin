namespace Merlin.Backend.Core.Conversation;

public static class AssistantTurnStates
{
    public const string Created = "created";
    public const string Thinking = "thinking";
    public const string StreamingResponse = "streaming_response";
    public const string GeneratingTts = "generating_tts";
    public const string Speaking = "speaking";
    public const string Paused = "paused";
    public const string Interrupted = "interrupted";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
