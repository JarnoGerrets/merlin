using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IConversationSessionService
{
    ConversationSession CurrentSession { get; }

    ConversationSession CreateSession();

    void AddMessage(string role, string content);

    void AddUserMessage(string content);

    void AddAssistantMessage(string content);

    IReadOnlyList<ConversationMessage> GetRecentMessages(int maxMessages = 20);

    void UpdateRunningSummary(string summary);

    ConversationSummary? FinalizeCurrentSession();

    void ClearSession();
}
