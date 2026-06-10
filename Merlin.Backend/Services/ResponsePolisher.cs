using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class ResponsePolisher : IResponsePolisher
{
    private readonly CapabilityOptions _capabilityOptions;

    public ResponsePolisher(IOptions<CapabilityOptions> capabilityOptions)
    {
        _capabilityOptions = MergeWithDefaults(capabilityOptions.Value);
    }

    public Task<string> PolishMessageAsync(
        AssistantResponse response,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = response.ErrorCode switch
        {
            "MISSING_CAPABILITY" => GetConfiguredMessage(
                _capabilityOptions.MissingCapabilities,
                response.OriginalMessage,
                "I understand what you're asking, but I don't currently have a tool that can do that."),
            "UNSUPPORTED_ACTION" => GetConfiguredMessage(
                _capabilityOptions.UnsupportedActions,
                response.OriginalMessage,
                "I understand the request, but Merlin does not support that action."),
            "UNKNOWN_INPUT" => "I couldn't understand that request.",
            "UNKNOWN_COMMAND" => "I couldn't determine a supported action from your request.",
            "BLOCKED_URL_SCHEME" => "I can only open HTTP and HTTPS links. Other URL schemes are blocked for safety.",
            "LOCAL_AI_UNAVAILABLE" when IsCurrentInformationQuestion(response.OriginalMessage) => GetCurrentInformationMessage(response.OriginalMessage),
            _ => response.Message
        };

        return Task.FromResult(message);
    }

    private static string GetConfiguredMessage(
        IEnumerable<CapabilityRule> rules,
        string? message,
        string fallbackMessage)
    {
        var normalized = message?.Trim().ToLowerInvariant() ?? string.Empty;
        var rule = rules.FirstOrDefault(rule =>
            rule.Keywords.Any(keyword => ContainsWholePhrase(normalized, Normalize(keyword))));

        return string.IsNullOrWhiteSpace(rule?.Message)
            ? fallbackMessage
            : rule.Message;
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

    private static CapabilityOptions MergeWithDefaults(CapabilityOptions configuredOptions)
    {
        var defaults = CapabilityOptions.CreateDefault();

        if (configuredOptions.MissingCapabilities.Count == 0)
        {
            configuredOptions.MissingCapabilities = defaults.MissingCapabilities;
        }

        if (configuredOptions.UnsupportedActions.Count == 0)
        {
            configuredOptions.UnsupportedActions = defaults.UnsupportedActions;
        }

        return configuredOptions;
    }

    private static bool ContainsWholePhrase(string value, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        var afterIndex = index + phrase.Length;
        var afterIsBoundary = afterIndex >= value.Length || !char.IsLetterOrDigit(value[afterIndex]);

        return beforeIsBoundary && afterIsBoundary;
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
