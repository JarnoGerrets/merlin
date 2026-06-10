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
            "MISSING_CAPABILITY" => GetDomainMessage(
                response.CapabilityId,
                "I understand what you're asking, but I don't currently have a tool that can do that."),
            "UNSUPPORTED_ACTION" => GetDomainMessage(
                response.CapabilityId,
                "I understand the request, but Merlin does not support that action."),
            "UNKNOWN_INPUT" => "I couldn't understand that request.",
            "UNKNOWN_COMMAND" => "I couldn't determine a supported action from your request.",
            "BLOCKED_URL_SCHEME" => "I can only open HTTP and HTTPS links. Other URL schemes are blocked for safety.",
            "LOCAL_AI_UNAVAILABLE" when IsCurrentInformationQuestion(response.OriginalMessage) => GetCurrentInformationMessage(response.OriginalMessage),
            _ => response.Message
        };

        return Task.FromResult(message);
    }

    private string GetDomainMessage(string? capabilityId, string fallbackMessage)
    {
        var domain = _capabilityOptions.CapabilityDomains.FirstOrDefault(item =>
            string.Equals(item.Id, capabilityId, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(domain?.MissingMessage)
            ? fallbackMessage
            : domain.MissingMessage;
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

        if (configuredOptions.CapabilityDomains.Count == 0)
        {
            configuredOptions.CapabilityDomains = defaults.CapabilityDomains;
        }

        return configuredOptions;
    }
}
