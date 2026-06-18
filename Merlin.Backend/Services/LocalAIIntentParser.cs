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
        "system_resource_query",
        "web_search",
        "general_conversation",
        "unsupported_action",
        "missing_capability",
        "unknown_input"
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILocalAIClient _localAIClient;
    private readonly ILocalAIHealthService _localAIHealthService;
    private readonly ILogger<LocalAIIntentParser> _logger;
    private readonly CapabilityOptions _capabilityOptions;
    private readonly LocalAIOptions _options;
    private readonly IAssistantPolicyProvider _policyProvider;
    private readonly ToolRegistry _toolRegistry;

    public LocalAIIntentParser(
        ILocalAIClient localAIClient,
        IOptions<LocalAIOptions> options,
        IOptions<CapabilityOptions> capabilityOptions,
        ToolRegistry toolRegistry,
        IAssistantPolicyProvider policyProvider,
        ILogger<LocalAIIntentParser> logger,
        ILocalAIHealthService localAIHealthService)
    {
        _localAIClient = localAIClient;
        _localAIHealthService = localAIHealthService;
        _options = options.Value;
        _capabilityOptions = MergeWithDefaults(capabilityOptions.Value);
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

            var capability = string.IsNullOrWhiteSpace(modelResult.CapabilityId)
                ? FindCapabilityByIntent(modelResult.Intent)
                : FindCapabilityById(modelResult.CapabilityId);

            if (!IsCapabilityValidForIntent(modelResult.Intent, capability))
            {
                return UnknownInput(originalMessage);
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
                    NormalizedCommand = modelResult.NormalizedCommand,
                    Confidence = modelResult.Confidence,
                    OriginalMessage = originalMessage,
                    CapabilityId = capability?.Id,
                    CapabilityName = capability?.Name
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
                OriginalMessage = originalMessage,
                CapabilityId = capability?.Id,
                CapabilityName = capability?.Name
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

    private static IntentParseResult UnknownInput(string originalMessage)
    {
        return new IntentParseResult
        {
            Intent = "unknown_input",
            NormalizedCommand = originalMessage.Trim().ToLowerInvariant(),
            Confidence = 0.8,
            OriginalMessage = originalMessage
        };
    }

    internal string BuildPrompt(string message)
    {
        var toolMetadata = string.Join(
            Environment.NewLine,
            _toolRegistry.GetTools().Select(tool =>
                $"- {tool.Name}: {tool.Description} Examples: {string.Join(", ", tool.Examples)}"));
        var capabilityDomains = string.Join(
            Environment.NewLine,
            _capabilityOptions.CapabilityDomains.Select(domain =>
                $"- {domain.Id} ({domain.Name}): implemented={domain.IsImplemented}, intent={domain.ImplementedIntent ?? "none"}, safety={domain.SafetyLevel}. {domain.Description}"));
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
- system_resource_query
- web_search
- general_conversation
- unsupported_action
- missing_capability
- unknown_input

Intent meanings:
- web_search: The user explicitly asks to search the public web and only needs a result list.
- missing_capability: The user asks for a reasonable capability Merlin does not currently have, such as source-aware web research, news feed, email, calendar, folder/file inspection, or live/current information without a dedicated tool.
- unsupported_action: The user asks for something intentionally unsafe or disallowed, such as deleting files, wiping disks, disabling security, or bypassing confirmations.
- unknown_input: The request is not understandable.
- general_conversation: Safe conversation or a question that does not require a tool.

Available tools:
{{toolMetadata}}

Capability domains:
{{capabilityDomains}}

Return this exact shape:
{
  "intent": "open_application|open_url|tool_discovery|diagnostics|confirmation|system_resource_query|web_search|general_conversation|unsupported_action|missing_capability|unknown_input",
  "normalizedCommand": "normalized command for an existing tool",
  "capabilityId": "one configured capability domain id or null",
  "confidence": 0.0
}

Normalization examples:
- "could you open notepad for me" -> {"intent":"open_application","normalizedCommand":"open notepad","capabilityId":"application_launch","confidence":0.85}
- "take me to google.com" -> {"intent":"open_url","normalizedCommand":"open google.com","capabilityId":"url_opening","confidence":0.85}
- "what tools do you have" -> {"intent":"tool_discovery","normalizedCommand":"list tools","capabilityId":"tool_discovery","confidence":0.85}
- "show status" -> {"intent":"diagnostics","normalizedCommand":"show status","capabilityId":"diagnostics","confidence":0.85}
- "confirm" -> {"intent":"confirmation","normalizedCommand":"confirm","capabilityId":"confirmation","confidence":0.85}
- "what time is it" -> {"intent":"system_resource_query","normalizedCommand":"system resource current_time","capabilityId":"system_time","confidence":0.9}
- "what is today's date" -> {"intent":"system_resource_query","normalizedCommand":"system resource current_date","capabilityId":"system_date","confidence":0.9}
- "what timezone am I in" -> {"intent":"system_resource_query","normalizedCommand":"system resource timezone","capabilityId":"system_timezone","confidence":0.9}
- "search the web for chatterbox turbo latency" -> {"intent":"web_search","normalizedCommand":"web_search chatterbox turbo latency","capabilityId":"web_search","confidence":0.9}
- "tell me a joke" -> {"intent":"general_conversation","normalizedCommand":"tell me a joke","capabilityId":"general_conversation","confidence":0.9}
- "can you pull up the newsfeed" -> {"intent":"missing_capability","normalizedCommand":"can you pull up the newsfeed","capabilityId":"news","confidence":0.9}
- "delete all my files" -> {"intent":"unsupported_action","normalizedCommand":"delete all my files","capabilityId":"destructive_file_action","confidence":0.95}
- unclear input -> {"intent":"unknown_input","normalizedCommand":"original unclear input","capabilityId":null,"confidence":0.8}
- unrecognized unclear requests -> {"intent":"unknown_input","normalizedCommand":"original unclear input","capabilityId":null,"confidence":0.8}

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

        [JsonPropertyName("capabilityId")]
        public string? CapabilityId { get; init; }
    }

    private CapabilityDomain? FindCapabilityById(string capabilityId)
    {
        return _capabilityOptions.CapabilityDomains.FirstOrDefault(domain =>
            string.Equals(domain.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
    }

    private CapabilityDomain? FindCapabilityByIntent(string? intent)
    {
        return _capabilityOptions.CapabilityDomains.FirstOrDefault(domain =>
            string.Equals(domain.ImplementedIntent, intent, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCapabilityValidForIntent(string intent, CapabilityDomain? capability)
    {
        if (string.Equals(intent, "unknown_input", StringComparison.OrdinalIgnoreCase))
        {
            return capability is null;
        }

        if (capability is null)
        {
            return false;
        }

        if (string.Equals(intent, "missing_capability", StringComparison.OrdinalIgnoreCase))
        {
            return !capability.IsImplemented
                && string.Equals(capability.SafetyLevel, "missing", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(intent, "unsupported_action", StringComparison.OrdinalIgnoreCase))
        {
            return !capability.IsImplemented
                && string.Equals(capability.SafetyLevel, "unsupported", StringComparison.OrdinalIgnoreCase);
        }

        return capability.IsImplemented
            && string.Equals(capability.ImplementedIntent, intent, StringComparison.OrdinalIgnoreCase);
    }

    private static CapabilityOptions MergeWithDefaults(CapabilityOptions configuredOptions)
    {
        if (configuredOptions.CapabilityDomains.Count == 0)
        {
            configuredOptions.CapabilityDomains = CapabilityOptions.CreateDefault().CapabilityDomains;
        }

        return configuredOptions;
    }
}
