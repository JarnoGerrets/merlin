using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class SpeechPolicyService : ISpeechPolicyService
{
    private const string SourceVoice = "voice";
    private const string SourceVoiceStream = "voice_stream";
    private const string SourceVoiceCorrection = "voice_correction";
    private const string SourceBackendIdleVoice = "backend_idle_voice";
    private const string ModeOrb = "orb";

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
            var shouldSpeak = IsVoiceSource(interactionSource)
                && clientMode == ModeOrb
                && ShouldSpeakResponse(response);

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

    private static bool IsVoiceSource(string interactionSource)
    {
        return interactionSource == SourceVoice
            || interactionSource == SourceVoiceStream
            || interactionSource == SourceVoiceCorrection
            || interactionSource == SourceBackendIdleVoice;
    }

    private static bool ShouldSpeakResponse(AssistantResponse response)
    {
        if (response.SuppressSpeech)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.Message))
        {
            return false;
        }

        if (string.Equals(response.ResponseType, "dev_visual", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return response.Success
            || string.Equals(response.ResponseType, "confirmation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "limitation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "safety", StringComparison.OrdinalIgnoreCase);
    }
}
