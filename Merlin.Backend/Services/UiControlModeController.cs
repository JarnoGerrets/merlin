using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public enum UiControlModeState
{
    Off,
    Starting,
    Active,
    Stopping,
    Faulted
}

public enum UiControlModeCommandAction
{
    Start,
    Stop
}

public sealed class UiControlModeController
{
    private readonly ILogger<UiControlModeController> _logger;
    private readonly object _gate = new();

    public UiControlModeController(ILogger<UiControlModeController> logger)
    {
        _logger = logger;
    }

    public UiControlModeState State { get; private set; } = UiControlModeState.Off;

    public UiControlModeState Start()
    {
        lock (_gate)
        {
            if (State is UiControlModeState.Active or UiControlModeState.Starting)
            {
                return State;
            }

            State = UiControlModeState.Starting;
            State = UiControlModeState.Active;
            _logger.LogInformation("UiControlModeStarted");
            return State;
        }
    }

    public UiControlModeState Stop()
    {
        lock (_gate)
        {
            if (State is UiControlModeState.Off or UiControlModeState.Stopping)
            {
                return State;
            }

            State = UiControlModeState.Stopping;
            State = UiControlModeState.Off;
            _logger.LogInformation("UiControlModeStopped");
            return State;
        }
    }
}

public static class UiControlModeCommandMatcher
{
    private static readonly string[] StartPhrases =
    [
        "let me control the ui",
        "let me control ui",
        "start ui control",
        "enable ui control",
        "gesture mode",
        "start gesture mode",
        "edit the ui",
        "let me edit the ui"
    ];

    private static readonly string[] StopPhrases =
    [
        "i am done with the ui",
        "im done with the ui",
        "i'm done with the ui",
        "stop ui control",
        "stop gesture mode",
        "disable ui control",
        "exit gesture mode",
        "close ui control",
        "cancel ui control",
        "done controlling"
    ];

    public static bool TryMatch(string? message, out UiControlModeCommandAction action)
    {
        var normalized = SpokenCommandNormalizer.Normalize(message).CommandText;
        if (StartPhrases.Any(phrase => string.Equals(normalized, phrase, StringComparison.Ordinal)))
        {
            action = UiControlModeCommandAction.Start;
            return true;
        }

        if (StopPhrases.Any(phrase => string.Equals(normalized, phrase, StringComparison.Ordinal)))
        {
            action = UiControlModeCommandAction.Stop;
            return true;
        }

        var tokens = Tokenize(normalized);
        if (tokens.Count == 0 || LooksLikeQuestionOrDiscussion(tokens, normalized))
        {
            action = UiControlModeCommandAction.Start;
            return false;
        }

        if (HasUiControlTarget(tokens, normalized) && HasStopIntent(tokens, normalized))
        {
            action = UiControlModeCommandAction.Stop;
            return true;
        }

        if (HasUiControlTarget(tokens, normalized) && HasStartIntent(tokens, normalized))
        {
            action = UiControlModeCommandAction.Start;
            return true;
        }

        action = UiControlModeCommandAction.Start;
        return false;
    }

    public static IntentParseResult ToIntentParseResult(
        UiControlModeCommandAction action,
        string originalMessage)
    {
        return new IntentParseResult
        {
            Intent = action == UiControlModeCommandAction.Start
                ? "ui_control_mode_start"
                : "ui_control_mode_stop",
            NormalizedCommand = action == UiControlModeCommandAction.Start
                ? "ui control mode start"
                : "ui control mode stop",
            Confidence = 1.0,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(UiControlModeCommandMatcher),
            CapabilityId = "ui_control_mode",
            CapabilityName = "UI Control Mode"
        };
    }

    private static IReadOnlyList<string> Tokenize(string commandText)
    {
        return commandText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken)
            .ToArray();
    }

    private static string NormalizeToken(string token)
    {
        return token switch
        {
            "controlling" => "control",
            "controlled" => "control",
            "controls" => "control",
            "gestures" => "gesture",
            _ => token
        };
    }

    private static bool LooksLikeQuestionOrDiscussion(IReadOnlyList<string> tokens, string commandText)
    {
        if (StartsWith(tokens, "what")
            || StartsWith(tokens, "why")
            || StartsWith(tokens, "how")
            || StartsWith(tokens, "explain")
            || StartsWith(tokens, "describe")
            || StartsWith(tokens, "should")
            || StartsWith(tokens, "does")
            || StartsWith(tokens, "could", "it")
            || StartsWith(tokens, "would", "it")
            || StartsWith(tokens, "can", "it")
            || StartsWith(tokens, "do", "you", "think"))
        {
            return true;
        }

        return ContainsPhrase(commandText, "tell me about")
            || ContainsPhrase(commandText, "do you think")
            || ContainsPhrase(commandText, "could it")
            || ContainsPhrase(commandText, "would it")
            || ContainsPhrase(commandText, "can it")
            || tokens.Contains("should", StringComparer.Ordinal)
            || tokens.Contains("explain", StringComparer.Ordinal)
            || tokens.Contains("describe", StringComparer.Ordinal);
    }

    private static bool HasUiControlTarget(IReadOnlyList<string> tokens, string commandText)
    {
        var hasUi = tokens.Contains("ui", StringComparer.Ordinal);
        var hasControl = tokens.Contains("control", StringComparer.Ordinal);
        var hasMode = tokens.Contains("mode", StringComparer.Ordinal);
        var hasGesture = tokens.Contains("gesture", StringComparer.Ordinal);

        return (hasUi && (hasControl || hasMode || hasGesture))
            || (hasGesture && (hasMode || hasControl))
            || ContainsPhrase(commandText, "ui control")
            || ContainsPhrase(commandText, "gesture mode");
    }

    private static bool HasStartIntent(IReadOnlyList<string> tokens, string commandText)
    {
        return tokens.Contains("start", StringComparer.Ordinal)
            || tokens.Contains("enable", StringComparer.Ordinal)
            || tokens.Contains("activate", StringComparer.Ordinal)
            || tokens.Contains("enter", StringComparer.Ordinal)
            || tokens.Contains("give", StringComparer.Ordinal)
            || tokens.Contains("let", StringComparer.Ordinal)
            || tokens.Contains("take", StringComparer.Ordinal)
            || tokens.Contains("use", StringComparer.Ordinal)
            || tokens.Contains("switch", StringComparer.Ordinal)
            || tokens.Contains("want", StringComparer.Ordinal)
            || ContainsPhrase(commandText, "turn on");
    }

    private static bool HasStopIntent(IReadOnlyList<string> tokens, string commandText)
    {
        return tokens.Contains("stop", StringComparer.Ordinal)
            || tokens.Contains("disable", StringComparer.Ordinal)
            || tokens.Contains("exit", StringComparer.Ordinal)
            || tokens.Contains("cancel", StringComparer.Ordinal)
            || tokens.Contains("done", StringComparer.Ordinal)
            || tokens.Contains("close", StringComparer.Ordinal)
            || tokens.Contains("leave", StringComparer.Ordinal)
            || ContainsPhrase(commandText, "turn off");
    }

    private static bool StartsWith(IReadOnlyList<string> tokens, params string[] prefix)
    {
        if (tokens.Count < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (!string.Equals(tokens[index], prefix[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsPhrase(string commandText, string phrase)
    {
        return string.Equals(commandText, phrase, StringComparison.Ordinal)
            || commandText.StartsWith($"{phrase} ", StringComparison.Ordinal)
            || commandText.EndsWith($" {phrase}", StringComparison.Ordinal)
            || commandText.Contains($" {phrase} ", StringComparison.Ordinal);
    }
}
