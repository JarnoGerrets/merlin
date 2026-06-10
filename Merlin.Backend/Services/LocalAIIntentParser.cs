using System.Text.Json;
using System.Text.Json.Serialization;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public class LocalAIIntentParser : IIntentParser
{
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "open_application",
        "open_url",
        "tool_discovery",
        "diagnostics",
        "confirmation",
        "general_conversation",
        "unsupported_action",
        "missing_capability",
        "unknown_input",
        "unknown"
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILocalAIClient _localAIClient;
    private readonly ILocalAIHealthService _localAIHealthService;
    private readonly ILogger<LocalAIIntentParser> _logger;
    private readonly LocalAIOptions _options;
    private readonly IAssistantPolicyProvider _policyProvider;
    private readonly ToolRegistry _toolRegistry;

    public LocalAIIntentParser(
        ILocalAIClient localAIClient,
        IOptions<LocalAIOptions> options,
        ToolRegistry toolRegistry,
        IAssistantPolicyProvider policyProvider,
        ILogger<LocalAIIntentParser> logger,
        ILocalAIHealthService localAIHealthService)
    {
        _localAIClient = localAIClient;
        _localAIHealthService = localAIHealthService;
        _options = options.Value;
        _policyProvider = policyProvider;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public virtual async Task<IntentParseResult> ParseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Unknown(message);
        }

        try
        {
            var aiResponse = await _localAIClient.GenerateAsync(
                BuildPrompt(message),
                cancellationToken);

            _localAIHealthService.MarkAvailable(0);
            return ParseModelResponse(message, aiResponse);
        }
        catch (Exception exception)
        {
            _localAIHealthService.MarkUnavailable(exception.Message);
            _logger.LogWarning(exception, "Local AI intent parsing failed.");
            return Unknown(message);
        }
    }

    internal IntentParseResult ParseModelResponse(string originalMessage, string? aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
        {
            return Unknown(originalMessage);
        }

        try
        {
            var extractedJson = ExtractJsonObject(aiResponse);
            if (extractedJson is null)
            {
                return Unknown(originalMessage);
            }

            var modelResult = JsonSerializer.Deserialize<ModelIntentResult>(
                extractedJson,
                JsonSerializerOptions);

            if (modelResult is null
                || string.IsNullOrWhiteSpace(modelResult.Intent)
                || string.IsNullOrWhiteSpace(modelResult.NormalizedCommand)
                || !AllowedIntents.Contains(modelResult.Intent)
                || modelResult.Confidence < _options.MinimumConfidence)
            {
                return Unknown(originalMessage);
            }

            if (string.Equals(modelResult.Intent, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return Unknown(originalMessage);
            }

            if (string.Equals(modelResult.Intent, "general_conversation", StringComparison.OrdinalIgnoreCase))
            {
                modelResult = modelResult with
                {
                    NormalizedCommand = $"chat {originalMessage.Trim()}"
                };
            }
            else if (string.Equals(modelResult.Intent, "unsupported_action", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modelResult.Intent, "missing_capability", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modelResult.Intent, "unknown_input", StringComparison.OrdinalIgnoreCase))
            {
                return new IntentParseResult
                {
                    Intent = modelResult.Intent,
                    NormalizedCommand = originalMessage.Trim().ToLowerInvariant(),
                    Confidence = modelResult.Confidence,
                    OriginalMessage = originalMessage
                };
            }

            if (_toolRegistry.FindTool(modelResult.NormalizedCommand) is null)
            {
                return Unknown(originalMessage);
            }

            return new IntentParseResult
            {
                Intent = modelResult.Intent,
                NormalizedCommand = modelResult.NormalizedCommand,
                Confidence = modelResult.Confidence,
                OriginalMessage = originalMessage
            };
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Local AI returned invalid intent JSON.");
            return Unknown(originalMessage);
        }
    }

    internal static string? ExtractJsonObject(string response)
    {
        var trimmedResponse = response.Trim();

        if (trimmedResponse.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineBreak = trimmedResponse.IndexOf('\n');
            var closingFence = trimmedResponse.LastIndexOf("```", StringComparison.Ordinal);

            if (firstLineBreak >= 0 && closingFence > firstLineBreak)
            {
                trimmedResponse = trimmedResponse[firstLineBreak..closingFence].Trim();
            }
        }

        if (trimmedResponse.Length >= 2
            && trimmedResponse[0] == '`'
            && trimmedResponse[^1] == '`')
        {
            trimmedResponse = trimmedResponse[1..^1].Trim();
        }

        var startIndex = trimmedResponse.IndexOf('{');
        if (startIndex < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var isEscaped = false;

        for (var index = startIndex; index < trimmedResponse.Length; index++)
        {
            var character = trimmedResponse[index];

            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\' && inString)
            {
                isEscaped = true;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return trimmedResponse[startIndex..(index + 1)];
                }
            }
        }

        return null;
    }

    private static IntentParseResult Unknown(string originalMessage)
    {
        return new IntentParseResult
        {
            Intent = null,
            NormalizedCommand = originalMessage.Trim().ToLowerInvariant(),
            Confidence = 0,
            OriginalMessage = originalMessage
        };
    }

    internal string BuildPrompt(string message)
    {
        var toolMetadata = string.Join(
            Environment.NewLine,
            _toolRegistry.GetTools().Select(tool =>
                $"- {tool.Name}: {tool.Description} Examples: {string.Join(", ", tool.Examples)}"));
        var policy = _policyProvider.GetPolicyText();

        return $$"""
You are Merlin's local intent parser. Return only valid JSON. Do not include markdown.

Assistant policy:
{{policy}}

Allowed intents:
- open_application
- open_url
- tool_discovery
- diagnostics
- confirmation
- general_conversation
- unsupported_action
- missing_capability
- unknown_input
- unknown (internal fallback only when no supported classification applies)

Intent meanings:
- missing_capability: The user asks for a reasonable capability Merlin does not currently have, such as web search, news feed, email, calendar, folder/file inspection, or live/current information without a dedicated tool.
- unsupported_action: The user asks for something intentionally unsafe or disallowed, such as deleting files, wiping disks, disabling security, or bypassing confirmations.
- unknown_input: The request is not understandable.
- general_conversation: Safe conversation or a question that does not require a tool.

Available tools:
{{toolMetadata}}

Return this exact shape:
{
  "intent": "open_application|open_url|tool_discovery|diagnostics|confirmation|general_conversation|unsupported_action|missing_capability|unknown_input|unknown",
  "normalizedCommand": "normalized command for an existing tool",
  "confidence": 0.0
}

Normalization examples:
- "could you open notepad for me" -> {"intent":"open_application","normalizedCommand":"open notepad","confidence":0.85}
- "take me to google.com" -> {"intent":"open_url","normalizedCommand":"open google.com","confidence":0.85}
- "what tools do you have" -> {"intent":"tool_discovery","normalizedCommand":"list tools","confidence":0.85}
- "show status" -> {"intent":"diagnostics","normalizedCommand":"show status","confidence":0.85}
- "confirm" -> {"intent":"confirmation","normalizedCommand":"confirm","confidence":0.85}
- "choose 1" -> {"intent":"confirmation","normalizedCommand":"choose 1","confidence":0.85}
- "tell me a joke" -> {"intent":"general_conversation","normalizedCommand":"tell me a joke","confidence":0.9}
- "search my hard drive" -> {"intent":"missing_capability","normalizedCommand":"search my hard drive","confidence":0.9}
- "delete my files" -> {"intent":"unsupported_action","normalizedCommand":"delete my files","confidence":0.9}
- unclear input -> {"intent":"unknown_input","normalizedCommand":"original unclear input","confidence":0.8}
- unsupported or unrecognized requests -> {"intent":"unknown","normalizedCommand":"","confidence":0.0}

User message:
{{message}}
""";
    }

    private sealed record ModelIntentResult
    {
        [JsonPropertyName("intent")]
        public string? Intent { get; init; }

        [JsonPropertyName("normalizedCommand")]
        public string? NormalizedCommand { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }
}
