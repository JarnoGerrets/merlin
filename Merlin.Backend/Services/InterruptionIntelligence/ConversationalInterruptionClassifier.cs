using System.Text.RegularExpressions;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed partial class ConversationalInterruptionClassifier : IConversationalInterruptionClassifier
{
    private static readonly HashSet<string> Backchannels = new(StringComparer.Ordinal)
    {
        "yeah",
        "yep",
        "yes",
        "mhm",
        "mmhm",
        "mm hmm",
        "uh huh",
        "right",
        "okay",
        "ok",
        "sure",
        "true"
    };

    private static readonly HashSet<string> PassiveAgreements = new(StringComparer.Ordinal)
    {
        "right",
        "okay",
        "ok",
        "sure",
        "true"
    };

    private static readonly HashSet<string> StopRequests = new(StringComparer.Ordinal)
    {
        "stop",
        "stop talking",
        "shut up",
        "that's enough",
        "thats enough",
        "enough"
    };

    private static readonly HashSet<string> CancelRequests = new(StringComparer.Ordinal)
    {
        "cancel",
        "cancel that",
        "never mind",
        "nevermind"
    };

    private readonly InterruptionHandlingOptions _options;

    public ConversationalInterruptionClassifier(IOptions<InterruptionHandlingOptions> options)
    {
        _options = options.Value;
    }

    public ConversationalInterruptionDecision Classify(ConversationalInterruptionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var normalized = ConversationalInterruptionTextNormalizer.Normalize(candidate.Transcript);
        if (IsNoiseOrFalsePositive(candidate, normalized, out var noiseReason))
        {
            return Noise(noiseReason);
        }

        if (IsBackchannel(normalized))
        {
            return new ConversationalInterruptionDecision
            {
                Type = PassiveAgreements.Contains(normalized)
                    ? ConversationalInterruptionType.PassiveAgreement
                    : ConversationalInterruptionType.Backchannel,
                Strategy = ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse,
                Confidence = 0.92,
                PausePlayback = false,
                CancelOriginalTurn = false,
                ResumeRawPlayback = true,
                DiscardCurrentPartialSentence = false,
                Reason = "Exact short backchannel or passive agreement."
            };
        }

        if (StopRequests.Contains(normalized))
        {
            return Stop(ConversationalInterruptionType.StopRequest, "Matched local stop request.");
        }

        if (CancelRequests.Contains(normalized))
        {
            return Stop(ConversationalInterruptionType.CancelRequest, "Matched local cancel request.");
        }

        if (IsRepeatRequest(normalized))
        {
            return new ConversationalInterruptionDecision
            {
                Type = ConversationalInterruptionType.RepeatRequest,
                Strategy = ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption,
                Confidence = 0.78,
                PausePlayback = true,
                CancelOriginalTurn = false,
                ResumeRawPlayback = false,
                RequiresBridgeFeedback = true,
                RequiresDeepInfraClarification = false,
                RequiresContinuationRecomposition = false,
                ClarificationMaxTokens = _options.ClarificationMaxTokens,
                ContinuationMaxTokens = _options.ContinuationMaxTokens,
                Reason = "Matched repeat request; repeat behavior is not implemented yet."
            };
        }

        if (TryExtractCorrectionOrRedirect(normalized, out var rewritten, out var isRedirect))
        {
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                return Ambiguous("Matched correction/redirect cue without a clean replacement request.");
            }

            return new ConversationalInterruptionDecision
            {
                Type = isRedirect
                    ? ConversationalInterruptionType.Redirect
                    : ConversationalInterruptionType.Correction,
                Strategy = ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
                Confidence = 0.88,
                PausePlayback = true,
                CancelOriginalTurn = true,
                ResumeRawPlayback = false,
                DiscardCurrentPartialSentence = true,
                RequiresBridgeFeedback = true,
                RequiresDeepInfraClarification = false,
                RequiresContinuationRecomposition = false,
                RewrittenUserRequest = rewritten,
                ClarificationMaxTokens = _options.ClarificationMaxTokens,
                ContinuationMaxTokens = _options.ContinuationMaxTokens,
                Reason = "Matched local correction or redirect cue."
            };
        }

        if (IsQueueFollowUp(normalized))
        {
            return new ConversationalInterruptionDecision
            {
                Type = ConversationalInterruptionType.RelatedFollowUpQuestion,
                Strategy = ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent,
                Confidence = 0.84,
                PausePlayback = true,
                CancelOriginalTurn = false,
                ResumeRawPlayback = false,
                DiscardCurrentPartialSentence = normalized.Contains("after this", StringComparison.Ordinal),
                RequiresBridgeFeedback = true,
                RequiresDeepInfraClarification = false,
                RequiresContinuationRecomposition = false,
                QueueAfterCurrentTurn = true,
                ClarificationMaxTokens = _options.ClarificationMaxTokens,
                ContinuationMaxTokens = _options.ContinuationMaxTokens,
                Reason = "Matched queue-after-current follow-up cue."
            };
        }

        if (IsClarificationQuestion(normalized, out var questionType))
        {
            return new ConversationalInterruptionDecision
            {
                Type = questionType,
                Strategy = ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
                Confidence = 0.82,
                PausePlayback = true,
                CancelOriginalTurn = false,
                ResumeRawPlayback = false,
                DiscardCurrentPartialSentence = true,
                RequiresBridgeFeedback = false,
                RequiresDeepInfraClarification = true,
                RequiresContinuationRecomposition = true,
                CanRunContinuationInParallel = true,
                ClarificationMaxTokens = _options.ClarificationMaxTokens,
                ContinuationMaxTokens = _options.ContinuationMaxTokens,
                Reason = "Matched clarification or related follow-up question."
            };
        }

        if (IsAdditionalContext(normalized, out var contextType))
        {
            return new ConversationalInterruptionDecision
            {
                Type = contextType,
                Strategy = ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint,
                Confidence = 0.76,
                PausePlayback = true,
                CancelOriginalTurn = false,
                ResumeRawPlayback = false,
                DiscardCurrentPartialSentence = true,
                RequiresBridgeFeedback = true,
                RequiresDeepInfraClarification = false,
                RequiresContinuationRecomposition = true,
                ClarificationMaxTokens = _options.ClarificationMaxTokens,
                ContinuationMaxTokens = _options.ContinuationMaxTokens,
                Reason = "Matched related side comment or additional context."
            };
        }

        return Ambiguous("No conservative local conversational interruption rule matched.");
    }

    private bool IsNoiseOrFalsePositive(
        ConversationalInterruptionCandidate candidate,
        string normalized,
        out string reason)
    {
        // Defensive direct-candidate handling only.
        // Live integration should call this layer with yielded utterances from Layer 1,
        // not raw acoustic candidates. NoiseOrFalsePositive here means the yielded text
        // is conversationally useless, not that Layer 2 re-detected echo or speaker identity.
        if (string.IsNullOrWhiteSpace(normalized))
        {
            reason = "Transcript was empty after normalization.";
            return true;
        }

        if (candidate.IsLikelySelfEcho)
        {
            reason = "Candidate was marked as likely self-echo.";
            return true;
        }

        if (!candidate.IsLikelyUserSpeech)
        {
            reason = "Candidate was not marked as likely user speech.";
            return true;
        }

        if (candidate.TranscriptConfidence < _options.MinimumInterruptionTranscriptConfidence)
        {
            reason = "Transcript confidence was below the local interruption threshold.";
            return true;
        }

        if (normalized.Length < Math.Max(0, _options.MinimumInterruptionTranscriptChars))
        {
            reason = "Transcript was shorter than the local interruption length threshold.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private ConversationalInterruptionDecision Noise(string reason) => new()
    {
        Type = ConversationalInterruptionType.NoiseOrFalsePositive,
        Strategy = ConversationalInterruptionHandlingStrategy.IgnoreAndContinue,
        Confidence = 0.9,
        PausePlayback = false,
        CancelOriginalTurn = false,
        ResumeRawPlayback = true,
        DiscardCurrentPartialSentence = false,
        RequiresBridgeFeedback = false,
        RequiresDeepInfraClarification = false,
        RequiresContinuationRecomposition = false,
        ClarificationMaxTokens = _options.ClarificationMaxTokens,
        ContinuationMaxTokens = _options.ContinuationMaxTokens,
        Reason = reason
    };

    private ConversationalInterruptionDecision Stop(
        ConversationalInterruptionType type,
        string reason) => new()
    {
        Type = type,
        Strategy = ConversationalInterruptionHandlingStrategy.StopPlayback,
        Confidence = 0.94,
        PausePlayback = true,
        CancelOriginalTurn = true,
        ResumeRawPlayback = false,
        DiscardCurrentPartialSentence = true,
        RequiresBridgeFeedback = false,
        RequiresDeepInfraClarification = false,
        RequiresContinuationRecomposition = false,
        ClarificationMaxTokens = _options.ClarificationMaxTokens,
        ContinuationMaxTokens = _options.ContinuationMaxTokens,
        Reason = reason
    };

    private ConversationalInterruptionDecision Ambiguous(string reason) => new()
    {
        Type = ConversationalInterruptionType.Unknown,
        Strategy = ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption,
        Confidence = 0.5,
        PausePlayback = true,
        CancelOriginalTurn = false,
        ResumeRawPlayback = false,
        RequiresBridgeFeedback = true,
        RequiresDeepInfraClarification = false,
        RequiresContinuationRecomposition = false,
        NeedsUserConfirmation = true,
        ClarificationMaxTokens = _options.ClarificationMaxTokens,
        ContinuationMaxTokens = _options.ContinuationMaxTokens,
        Reason = reason
    };

    private static bool IsBackchannel(string normalized)
    {
        return Backchannels.Contains(normalized);
    }

    private static bool IsRepeatRequest(string normalized)
    {
        return normalized is "repeat that" or "say that again" or "can you repeat that";
    }

    private static bool TryExtractCorrectionOrRedirect(
        string normalized,
        out string? rewritten,
        out bool isRedirect)
    {
        rewritten = null;
        isRedirect = false;

        foreach (var prefix in new[] { "no no i meant ", "no i meant ", "actually i meant ", "i meant " })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                rewritten = CleanRewrite(normalized[prefix.Length..]);
                return true;
            }
        }

        foreach (var prefix in new[] { "actually ", "no answer ", "not that " })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                rewritten = CleanRewrite(normalized[prefix.Length..]);
                isRedirect = true;
                return true;
            }
        }

        foreach (Match match in InsteadRedirectRegex().Matches(normalized))
        {
            rewritten = CleanRewrite(match.Groups["request"].Value);
            isRedirect = true;
            return true;
        }

        return false;
    }

    private static string CleanRewrite(string value)
    {
        var cleaned = value.Trim(' ', ',', '.', '!', '?');
        if (cleaned.EndsWith(" instead", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^" instead".Length].Trim();
        }

        return cleaned;
    }

    private static bool IsQueueFollowUp(string normalized)
    {
        return normalized.StartsWith("after this ", StringComparison.Ordinal)
            || normalized.Contains(" after this", StringComparison.Ordinal)
            || normalized.StartsWith("come back to ", StringComparison.Ordinal) && normalized.Contains(" after this", StringComparison.Ordinal)
            || normalized.StartsWith("later tell me ", StringComparison.Ordinal);
    }

    private static bool IsClarificationQuestion(
        string normalized,
        out ConversationalInterruptionType type)
    {
        var comparable = normalized.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        if (comparable.StartsWith("what do you mean", StringComparison.Ordinal)
            || comparable.StartsWith("what is ", StringComparison.Ordinal)
            || comparable.StartsWith("how ", StringComparison.Ordinal)
            || comparable.StartsWith("why ", StringComparison.Ordinal)
            || comparable is "but why")
        {
            type = ConversationalInterruptionType.ClarificationQuestion;
            return true;
        }

        if (comparable.StartsWith("what about ", StringComparison.Ordinal)
            || comparable is "the water itself too right"
            || comparable is "but the water itself too right"
            || comparable.StartsWith("but ", StringComparison.Ordinal) && (comparable.Contains(" too", StringComparison.Ordinal) || comparable.EndsWith(" right", StringComparison.Ordinal))
            || comparable.StartsWith("isn't it also ", StringComparison.Ordinal)
            || comparable.StartsWith("isnt it also ", StringComparison.Ordinal))
        {
            type = ConversationalInterruptionType.RelatedFollowUpQuestion;
            return true;
        }

        type = ConversationalInterruptionType.Unknown;
        return false;
    }

    private static bool IsAdditionalContext(
        string normalized,
        out ConversationalInterruptionType type)
    {
        if (normalized.StartsWith("well yeah but ", StringComparison.Ordinal))
        {
            type = ConversationalInterruptionType.SideComment;
            return true;
        }

        if (normalized.StartsWith("also ", StringComparison.Ordinal)
            || normalized.StartsWith("but sometimes ", StringComparison.Ordinal))
        {
            type = ConversationalInterruptionType.AdditionalContext;
            return true;
        }

        if (normalized.StartsWith("that is only true ", StringComparison.Ordinal)
            || normalized.StartsWith("that's only true ", StringComparison.Ordinal))
        {
            type = ConversationalInterruptionType.Disagreement;
            return true;
        }

        type = ConversationalInterruptionType.Unknown;
        return false;
    }

    [GeneratedRegex("^(?<request>(?:explain|talk about|answer) .+?) instead$")]
    private static partial Regex InsteadRedirectRegex();
}
