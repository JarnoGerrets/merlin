namespace Merlin.Backend.Services;

public static class ToolSpeechTemplates
{
    public const string AppOpenSuccess = "Opening the app for you, sir.";
    public const string UrlOpenSuccess = "Opening the website for you, sir.";
    public const string GenericSuccess = "Done, sir.";

    public const string ConfirmationRequired = "I need your confirmation first.";
    public const string ConfirmationRequiredPolite = "Please confirm that first, sir.";

    public const string GenericFailure = "I couldn't complete that from here.";
    public const string OpenFailure = "I couldn't open that from here.";

    public const string AmbiguousGeneric = "I need one detail first.";
    public const string AmbiguousChoice = "Which one should I use, sir?";
    public const string BrowserFallbackConfirmation = "I couldn't find that app. Should I open it as a website instead?";
    public const string BrowserMappingRemoved = "I removed that browser mapping.";
    public const string BrowserMappingUpdated = "I updated that browser mapping.";
    public const string BrowserMappingEditPrompt = "Of course, sir. What should I change it to?";
    public const string BrowserMappingEditCancelled = "I cancelled that browser mapping edit.";

    public static IReadOnlyList<string> WakeResponses { get; } =
    [
        "I'm awake and ready.",
        "I'm here. What do you need?",
        "Ready when you are.",
        "I'm listening.",
        "Awake and online.",
        "I'm with you.",
        "Here and ready to help.",
        "I'm here, sir.",
        "Standing by.",
        "Yes. What can I do for you?"
    ];

    public static IReadOnlyCollection<string> CommonPhrases { get; } =
    [
        AppOpenSuccess,
        UrlOpenSuccess,
        "Opening it now.",
        GenericSuccess,
        ConfirmationRequired,
        ConfirmationRequiredPolite,
        GenericFailure,
        OpenFailure,
        AmbiguousGeneric,
        AmbiguousChoice,
        BrowserFallbackConfirmation,
        BrowserMappingRemoved,
        BrowserMappingUpdated,
        BrowserMappingEditPrompt,
        BrowserMappingEditCancelled,
        "Good question, sir. Let me gather my thoughts.",
        "Of course, sir. Let me look into that properly.",
        "I am checking that now.",
        "Of course. Let me check memory.",
        "I am still on it.",
        "This is taking a little longer than usual.",
        "Saved.",
        .. WakeResponses
    ];
}
