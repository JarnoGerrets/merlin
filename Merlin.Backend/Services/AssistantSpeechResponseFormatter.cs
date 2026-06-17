using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class AssistantSpeechResponseFormatter : IAssistantResponsePresentationFormatter
{
    private const int ToolSuccessMaxChars = 90;
    private const int ConfirmationMaxChars = 100;
    private const int FailureMaxChars = 120;
    private const int AmbiguousMaxChars = 120;
    private readonly ILogger<AssistantSpeechResponseFormatter> _logger;

    public AssistantSpeechResponseFormatter(ILogger<AssistantSpeechResponseFormatter> logger)
    {
        _logger = logger;
    }

    public AssistantResponsePresentation? Format(AssistantResponse response)
    {
        var presentation = SelectPresentation(response);
        if (presentation is null)
        {
            return null;
        }

        _logger.LogInformation(
            "Speech formatter selected. Category={Category}. SpokenChars={SpokenChars}. DisplayChars={DisplayChars}. CacheKey={CacheKey}. Replayable={Replayable}.",
            presentation.CacheKey,
            presentation.SpokenText.Length,
            presentation.DisplayText.Length,
            presentation.CacheKey,
            presentation.IsReplayable);

        return presentation;
    }

    private static AssistantResponsePresentation? SelectPresentation(AssistantResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.SpokenText))
        {
            return null;
        }

        if (IsDiagnosticsOrDiscovery(response) || IsGeneralConversation(response) || IsSystemResource(response) || IsWakeResponse(response))
        {
            return null;
        }

        if (string.Equals(response.ResponseType, "dev_visual", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var displayText = string.IsNullOrWhiteSpace(response.Message)
            ? string.Empty
            : response.Message.Trim();

        if (IsAmbiguous(response))
        {
            return BuildPresentation(
                ToolSpeechTemplates.AmbiguousGeneric,
                displayText,
                "tool.ambiguous.generic",
                AmbiguousMaxChars,
                ToolSpeechTemplates.AmbiguousGeneric);
        }

        if (IsConfirmationRequired(response))
        {
            return BuildPresentation(
                ToolSpeechTemplates.ConfirmationRequired,
                displayText,
                "tool.confirmation.required.generic",
                ConfirmationMaxChars,
                ToolSpeechTemplates.ConfirmationRequired);
        }

        if (response.Success)
        {
            if (IsOpenApplication(response))
            {
                return BuildPresentation(
                    ToolSpeechTemplates.AppOpenSuccess,
                    displayText,
                    "tool.app.open.success.generic",
                    ToolSuccessMaxChars,
                    ToolSpeechTemplates.GenericSuccess);
            }

            if (IsOpenUrl(response))
            {
                return BuildPresentation(
                    ToolSpeechTemplates.UrlOpenSuccess,
                    displayText,
                    "tool.url.open.success.generic",
                    ToolSuccessMaxChars,
                    ToolSpeechTemplates.GenericSuccess);
            }

            if (IsConfirmation(response))
            {
                return BuildPresentation(
                    ToolSpeechTemplates.AppOpenSuccess,
                    displayText,
                    "tool.confirmation.success.generic",
                    ToolSuccessMaxChars,
                    ToolSpeechTemplates.GenericSuccess);
            }

            if (IsToolResponse(response))
            {
                return BuildPresentation(
                    ToolSpeechTemplates.GenericSuccess,
                    displayText,
                    "tool.success.generic",
                    ToolSuccessMaxChars,
                    ToolSpeechTemplates.GenericSuccess);
            }
        }

        if (!response.Success && IsToolResponse(response))
        {
            var spoken = IsOpenApplication(response) || IsOpenUrl(response)
                ? ToolSpeechTemplates.OpenFailure
                : ToolSpeechTemplates.GenericFailure;
            var cacheKey = IsOpenApplication(response) || IsOpenUrl(response)
                ? "tool.open.failure.generic"
                : "tool.failure.generic";
            return BuildPresentation(
                spoken,
                displayText,
                cacheKey,
                FailureMaxChars,
                ToolSpeechTemplates.GenericFailure);
        }

        return null;
    }

    internal static AssistantResponsePresentation BuildPresentation(
        string spokenText,
        string displayText,
        string cacheKey,
        int maxChars,
        string fallback)
    {
        var finalSpokenText = spokenText.Length <= maxChars
            ? spokenText
            : fallback;
        return new AssistantResponsePresentation(
            finalSpokenText,
            displayText,
            cacheKey);
    }

    private static bool IsToolResponse(AssistantResponse response)
    {
        return !string.IsNullOrWhiteSpace(response.ToolName)
            && !string.Equals(response.ToolName, "General Conversation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenApplication(AssistantResponse response)
    {
        return string.Equals(response.Intent, "open_application", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ToolName, "Open Application", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenUrl(AssistantResponse response)
    {
        return string.Equals(response.Intent, "open_url", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ToolName, "Open URL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfirmation(AssistantResponse response)
    {
        return string.Equals(response.Intent, "confirmation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ToolName, "Confirmation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "confirmation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfirmationRequired(AssistantResponse response)
    {
        return string.Equals(response.ErrorCode, "CONFIRMATION_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || IsConfirmation(response) && response.Confirmation is not null;
    }

    private static bool IsAmbiguous(AssistantResponse response)
    {
        return string.Equals(response.ErrorCode, "AMBIGUOUS_APPLICATION", StringComparison.OrdinalIgnoreCase)
            || response.ApplicationCandidates is { Count: > 1 };
    }

    private static bool IsDiagnosticsOrDiscovery(AssistantResponse response)
    {
        return response.Diagnostics is not null
            || response.AvailableTools is not null
            || string.Equals(response.Intent, "diagnostics", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.Intent, "tool_discovery", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneralConversation(AssistantResponse response)
    {
        return string.Equals(response.Intent, "general_conversation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ToolName, "General Conversation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemResource(AssistantResponse response)
    {
        return string.Equals(response.Intent, "system_resource_query", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWakeResponse(AssistantResponse response)
    {
        return string.Equals(response.Intent, "wake_merlin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ToolName, "Wake Merlin", StringComparison.OrdinalIgnoreCase);
    }
}
