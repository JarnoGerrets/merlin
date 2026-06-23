using System.Text.Json;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class AnswerRecomposerJsonParser
{
    public ClarificationResult ParseClarificationResult(string modelOutput)
    {
        using var document = ParseDocument(modelOutput);
        var root = document.RootElement;
        var replyText = ReadString(root, "replyText");
        if (string.IsNullOrWhiteSpace(replyText))
        {
            throw new InvalidOperationException("Clarification result did not include a non-empty replyText.");
        }

        return new ClarificationResult
        {
            ReplyText = replyText,
            ClarificationContext = ReadString(root, "clarificationContext"),
            ShouldRecomposeContinuation = ReadBool(root, "shouldRecomposeContinuation", defaultValue: true),
            UserQuestionAnswered = ReadBool(root, "userQuestionAnswered", defaultValue: true)
        };
    }

    public ContinuationRecompositionResult ParseContinuationRecompositionResult(string modelOutput)
    {
        using var document = ParseDocument(modelOutput);
        var root = document.RootElement;
        var continuationText = ReadString(root, "continuationText");
        if (string.IsNullOrWhiteSpace(continuationText))
        {
            throw new InvalidOperationException("Continuation recomposition result did not include a non-empty continuationText.");
        }

        return new ContinuationRecompositionResult
        {
            ContinuationText = continuationText,
            IncludedClarificationContext = ReadBool(root, "includedClarificationContext", defaultValue: false),
            AvoidedRepeatingSpokenContent = ReadBool(root, "avoidedRepeatingSpokenContent", defaultValue: false)
        };
    }

    private static JsonDocument ParseDocument(string modelOutput)
    {
        var json = ExtractJsonObject(modelOutput);
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Model output did not contain valid JSON.", exception);
        }
    }

    internal static string ExtractJsonObject(string? modelOutput)
    {
        if (string.IsNullOrWhiteSpace(modelOutput))
        {
            throw new InvalidOperationException("Model output was empty.");
        }

        var text = modelOutput.Trim();
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            throw new InvalidOperationException("Model output did not contain a JSON object.");
        }

        return text[firstBrace..(lastBrace + 1)];
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return string.Empty;
        }

        return property.ValueKind is JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : property.ToString().Trim();
    }

    private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }
}
