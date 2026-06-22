namespace Merlin.Backend.Configuration;

public sealed class CoreMemoryOptions
{
    public bool IncludeRetrievalNotesInPrompt { get; set; } = false;

    public bool RequireCoreMemoryForConversation { get; set; } = true;
}
