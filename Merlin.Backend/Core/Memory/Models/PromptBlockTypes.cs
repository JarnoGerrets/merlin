namespace Merlin.Backend.Core.Memory.Models;

public static class PromptBlockTypes
{
    public const string SystemIdentity = "system_identity";
    public const string RuntimeRules = "runtime_rules";

    public const string ResponsePreferences = "response_preferences";
    public const string CodingPreferences = "coding_preferences";
    public const string MerlinBehaviorPreferences = "merlin_behavior_preferences";
    public const string WorkflowPreferences = "workflow_preferences";
    public const string PersonalFacts = "personal_facts";

    public const string ProjectContext = "project_context";
    public const string SessionMemory = "session_memory";
    public const string TopicMemory = "topic_memory";

    public const string RelevantLongTermMemory = "relevant_long_term_memory";
    public const string RelevantMediumMemory = "relevant_medium_memory";
    public const string UserPreferencesMemory = "user_preferences_memory";
    public const string RetrievalNotes = "retrieval_notes";

    public const string CurrentUserMessage = "current_user_message";
}
