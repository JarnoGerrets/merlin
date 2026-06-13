using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class SpeechPolicyService : ISpeechPolicyService
{
    private const string SourceVoice = "voice";
    private const string ModeOrb = "orb";
    private const string ModeChat = "chat";

    public SpeechPolicyDecision Decide(AssistantRequest? request, AssistantResponse response)
    {
        if (request is null || string.IsNullOrWhiteSpace(response.Message))
        {
            return new SpeechPolicyDecision();
        }

        var interactionSource = Normalize(request.InteractionSource, "unknown");
        var clientMode = Normalize(request.ClientMode, "api");
        var hasNewContext = !string.IsNullOrWhiteSpace(request.InteractionSource)
            || !string.IsNullOrWhiteSpace(request.ClientMode);

        if (hasNewContext)
        {
            var shouldSpeak = interactionSource == SourceVoice
                && clientMode == ModeOrb
                && response.Success;

            return new SpeechPolicyDecision
            {
                ShouldSpeak = shouldSpeak,
                ShouldQueue = shouldSpeak
            };
        }

        if (request.SpeakResponse.HasValue)
        {
            return new SpeechPolicyDecision
            {
                ShouldSpeak = request.SpeakResponse.Value,
                ShouldQueue = request.SpeakResponse.Value,
                UsedLegacySpeakResponseFallback = true
            };
        }

        return new SpeechPolicyDecision();
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
    }
}
