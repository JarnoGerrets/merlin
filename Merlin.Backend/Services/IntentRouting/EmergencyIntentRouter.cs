using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class EmergencyIntentRouter
{
    private static readonly string[] StopPhrases =
    [
        "stop",
        "shut up",
        "stop talking",
        "pause",
        "abort"
    ];

    private static readonly string[] CancelPhrases =
    [
        "cancel",
        "never mind",
        "nevermind",
        "forget it"
    ];

    public RouteDecision? TryRoute(NormalizedInput input)
    {
        if (StopPhrases.Any(phrase => string.Equals(input.Text, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return RouteDecision.Tool(
                "assistant.stop",
                IntentDomain.ConversationControl,
                1.0,
                "Emergency conversation stop command matched.");
        }

        if (CancelPhrases.Any(phrase => string.Equals(input.Text, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return RouteDecision.Tool(
                "assistant.cancel",
                IntentDomain.ConversationControl,
                1.0,
                "Emergency conversation cancel command matched.");
        }

        return null;
    }
}
