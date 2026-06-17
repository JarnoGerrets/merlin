namespace Merlin.Backend.Core.Conversation;

public static class PromptTypes
{
    public const string Normal = "normal";
    public const string Correction = "correction";
    public const string Clarification = "clarification";
    public const string Continuation = "continuation";
    public const string Summary = "summary";
    public const string MemoryWrite = "memory_write";
}
