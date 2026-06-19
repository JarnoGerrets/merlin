using System.Text.RegularExpressions;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed partial class CorrectionRequestBuilder : ICorrectionRequestBuilder
{
    private static readonly string[] DirectActionPrefixes =
    [
        "open ",
        "launch ",
        "start ",
        "go to ",
        "browse ",
        "visit ",
        "pull up ",
        "search ",
        "look up ",
        "find ",
        "show ",
        "what ",
        "why ",
        "how ",
        "explain ",
        "tell me ",
        "delete ",
        "install ",
        "change ",
        "set "
    ];

    private static readonly string[] ContextualMarkers =
    [
        "i meant ",
        "no i meant ",
        "no i mean ",
        "not ",
        "actually use ",
        "instead use ",
        "with ",
        "that one",
        "the other one"
    ];

    public CorrectionRequestBuildResult Build(CorrectionRequestBuildInput input)
    {
        if (string.IsNullOrWhiteSpace(input.OriginalCorrelationId))
        {
            throw new ArgumentException("Original correlation id is required.", nameof(input));
        }

        var correctionText = NormalizeWhitespace(input.CorrectionText);
        var cleanedCorrection = CleanCorrectionText(correctionText);
        var previousMessage = NormalizeWhitespace(input.PreviousRequest?.Message);
        var strategy = SelectStrategy(cleanedCorrection, previousMessage);
        var correctedMessage = strategy == "contextual"
            ? BuildContextualMessage(previousMessage!, cleanedCorrection)
            : cleanedCorrection;

        if (string.IsNullOrWhiteSpace(correctedMessage))
        {
            correctedMessage = correctionText;
        }

        if (string.IsNullOrWhiteSpace(correctedMessage))
        {
            correctedMessage = "I need to correct my previous request.";
        }

        var newCorrelationId = $"{input.OriginalCorrelationId.Trim()}:correction:{Guid.NewGuid():N}";
        var previousRequest = input.PreviousRequest;
        var request = new AssistantRequest
        {
            Message = correctedMessage,
            CorrelationId = newCorrelationId,
            SpeakResponse = previousRequest?.SpeakResponse,
            InteractionSource = "voice_correction",
            ClientMode = previousRequest?.ClientMode,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };

        return new CorrectionRequestBuildResult(
            request,
            strategy,
            string.IsNullOrWhiteSpace(previousMessage) ? null : previousMessage,
            correctionText,
            input.OriginalCorrelationId.Trim(),
            newCorrelationId);
    }

    private static string SelectStrategy(string cleanedCorrection, string? previousMessage)
    {
        if (string.IsNullOrWhiteSpace(previousMessage))
        {
            return "direct";
        }

        var normalized = cleanedCorrection.ToLowerInvariant();
        if (DirectActionPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return "direct";
        }

        if (ContextualMarkers.Any(marker => normalized.StartsWith(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return "contextual";
        }

        return "direct";
    }

    private static string BuildContextualMessage(string previousMessage, string correctionText)
    {
        return string.Join(
            Environment.NewLine,
            "Correct my previous request using this correction.",
            string.Empty,
            "Previous request:",
            previousMessage,
            string.Empty,
            "Correction:",
            correctionText);
    }

    private static string CleanCorrectionText(string value)
    {
        var cleaned = NormalizeWhitespace(value)
            .Trim()
            .TrimEnd('.', '!', '?', ';', ':', ',');

        foreach (var prefix in new[]
        {
            "no, ",
            "no ",
            "actually, ",
            "actually ",
            "wait, ",
            "wait "
        })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
                break;
            }
        }

        if (cleaned.EndsWith(" instead", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^" instead".Length].Trim();
        }

        return cleaned;
    }

    private static string NormalizeWhitespace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ");
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
