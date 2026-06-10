using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class ResponsePolisher : IResponsePolisher
{
    public Task<string> PolishMessageAsync(
        AssistantResponse response,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = response.ErrorCode switch
        {
            "MISSING_CAPABILITY" => GetMissingCapabilityMessage(response.OriginalMessage),
            "UNSUPPORTED_ACTION" => GetUnsupportedActionMessage(response.OriginalMessage),
            "UNKNOWN_INPUT" => "I couldn't understand that request.",
            "UNKNOWN_COMMAND" => "I couldn't determine a supported action from your request.",
            "BLOCKED_URL_SCHEME" => "I can only open HTTP and HTTPS links. Other URL schemes are blocked for safety.",
            "LOCAL_AI_UNAVAILABLE" when IsCurrentInformationQuestion(response.OriginalMessage) => GetCurrentInformationMessage(response.OriginalMessage),
            _ => response.Message
        };

        return Task.FromResult(message);
    }

    private static string GetMissingCapabilityMessage(string? message)
    {
        var normalized = message?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Contains("news"))
        {
            return "I understand that you're asking me to show a news feed, but I don't currently have a News Tool or Web Search Tool implemented.";
        }

        if (normalized.Contains("web") || normalized.Contains("internet"))
        {
            return "I understand that you want me to search the web, but I don't currently have a Web Search Tool implemented.";
        }

        if (normalized.Contains("folder") || normalized.Contains("file") || normalized.Contains("hard drive"))
        {
            return "I understand that you want me to inspect local files or folders, but I don't currently have a folder inspection or file search tool implemented.";
        }

        if (normalized.Contains("email") || normalized.Contains("mail"))
        {
            return "I understand that you want me to check email, but I don't currently have an Email Tool implemented.";
        }

        return "I understand what you're asking, but I don't currently have a tool that can do that.";
    }

    private static string GetUnsupportedActionMessage(string? message)
    {
        var normalized = message?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Contains("delete") || normalized.Contains("wipe") || normalized.Contains("disable"))
        {
            return "I understand the request, but Merlin does not support destructive or security-disabling actions.";
        }

        return "I understand the request, but Merlin does not support that action.";
    }

    private static bool IsCurrentInformationQuestion(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ToLowerInvariant();
        return normalized.Contains("time")
            || normalized.Contains("latest")
            || normalized.Contains("today")
            || normalized.Contains("current")
            || normalized.Contains("recent");
    }

    private static string GetCurrentInformationMessage(string? message)
    {
        var normalized = message?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("time"))
        {
            return "I don't have a time tool yet, so I can't reliably check the current time.";
        }

        return "I don't have web access or a dedicated tool for live information yet, so I shouldn't invent a current answer.";
    }
}
