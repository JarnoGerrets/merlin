using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;

namespace Merlin.Backend.Services.LiveUtterance;

public sealed class LiveUtteranceGateInput
{
    public required UserUtterance Utterance { get; init; }

    public LiveAssistantTurn? ActiveTurn { get; init; }

    public required string CurrentSystemState { get; init; }

    public required bool AssistantWasSpeaking { get; init; }

    public required bool IsIdleListening { get; init; }

    public string? PendingCommandDescription { get; init; }

    public string? RecentToolName { get; init; }

    public string? RecentToolTarget { get; init; }

    public IReadOnlyList<string> RecentTranscripts { get; init; } = Array.Empty<string>();

    public double? SttConfidence { get; init; }

    public double? AudioSpeechConfidence { get; init; }
}

public enum LiveUtteranceGateDecisionKind
{
    AcceptPlaybackControl,
    AcceptCancellation,
    AcceptReplacement,
    AcceptCorrection,
    AcceptContinuation,
    AcceptNewRequest,
    AcceptStatusQuestion,
    HoldForMoreSpeech,
    AskClarification,
    IgnoreAsNoise,
    IgnoreAsEcho,
    IgnoreAsWakewordLeak,
    IgnoreAsGarbageTranscript,
    Unknown
}

public sealed class LiveUtteranceGateResult
{
    public required LiveUtteranceGateDecisionKind Decision { get; init; }

    public required double Confidence { get; init; }

    public required string Reason { get; init; }

    public string? NormalizedText { get; init; }

    public string? StrippedText { get; init; }

    public string? SourceContext { get; init; }

    public IReadOnlyList<string> PositiveSignals { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NegativeSignals { get; init; } = Array.Empty<string>();

    public TimeSpan? HoldWindow { get; init; }

    public string? ClarificationPrompt { get; init; }

    public bool ShouldCallDeepInfra { get; init; }

    public bool ShouldRouteToCommandRouter { get; init; }

    public bool ShouldAffectPlayback { get; init; }

    public string? ReplacementText { get; init; }
}
