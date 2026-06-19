using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.LiveUtterance;

public sealed partial class LiveUtteranceGate : ILiveUtteranceGate
{
    private static readonly string[] Fillers = ["uh", "um", "erm", "hmm", "well"];
    private static readonly string[] LeadingFillers = ["hey", "okay", "ok", "yo", "please", "merlin"];
    private static readonly string[] ShortControlPhrases = ["stop", "pause", "wait", "hold on", "hang on", "one second", "shut up", "be quiet", "quiet", "no stop", "no no", "no no no", "wait wait"];
    private static readonly string[] CancellationPhrases = ["cancel", "cancel that", "never mind", "nevermind", "forget it", "abort", "dont do that", "don't do that", "stop doing that"];
    private static readonly string[] ContinuationPhrases = ["continue", "go on", "resume", "keep going", "carry on"];
    private static readonly string[] StatusPhrases = ["what are you doing", "what are you working on", "did you hear me", "are you still there"];
    private static readonly string[] ReplacementPrefixes = ["sorry i meant ", "i meant ", "i mean ", "actually ", "no open ", "no ", "not ", "instead "];
    private static readonly string[] IncompletePhrases = ["yeah but", "but", "no no wait", "sorry i meant", "i meant", "i mean", "can you open", "open the", "but that means"];
    private static readonly HashSet<string> QuestionStarters = new(StringComparer.Ordinal)
    {
        "what", "who", "where", "when", "why", "how"
    };
    private static readonly HashSet<string> AuxiliaryQuestionStarters = new(StringComparer.Ordinal)
    {
        "can", "could", "would", "should", "is", "are", "do", "does", "did", "will"
    };
    private static readonly HashSet<string> QuestionSecondWords = new(StringComparer.Ordinal)
    {
        "is", "are", "am", "do", "does", "did", "can", "could", "would", "should", "will", "was", "were"
    };
    private static readonly HashSet<string> CommandVerbs = new(StringComparer.Ordinal)
    {
        "open", "search", "explain", "tell", "show", "read", "summarize", "play", "pause", "continue", "stop", "start", "launch", "find", "look"
    };
    private static readonly HashSet<string> VerbLikeWords = new(StringComparer.Ordinal)
    {
        "am", "is", "are", "was", "were", "be", "being", "been", "do", "does", "did", "mean", "means", "explain", "work", "works", "happen", "happens", "different", "correct", "know", "use", "uses", "open", "search", "tell", "show", "read", "summarize", "play", "pause", "continue", "stop", "need", "want", "help"
    };
    private static readonly HashSet<string> GarbageTokens = new(StringComparer.Ordinal)
    {
        "infant", "alrighty", "random", "sideways"
    };
    private static readonly HashSet<string> FillerTokens = new(StringComparer.Ordinal)
    {
        "uh", "um", "erm", "hmm", "well", "yeah", "okay", "ok", "alrighty"
    };
    private static readonly IReadOnlyDictionary<string, string> ContractionReplacements = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["what's"] = "what is",
        ["who's"] = "who is",
        ["where's"] = "where is",
        ["when's"] = "when is",
        ["why's"] = "why is",
        ["how's"] = "how is",
        ["that's"] = "that is",
        ["it's"] = "it is",
        ["isn't"] = "is not",
        ["aren't"] = "are not",
        ["doesn't"] = "does not",
        ["don't"] = "do not",
        ["didn't"] = "did not",
        ["can't"] = "can not",
        ["won't"] = "will not",
        ["i'm"] = "i am",
        ["you're"] = "you are"
    };

    private readonly ConcurrentDictionary<string, PendingFragment> _pendingFragments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<LiveUtteranceGate> _logger;
    private readonly LiveUtteranceGateOptions _options;

    public LiveUtteranceGate(
        ILogger<LiveUtteranceGate> logger,
        IOptions<LiveUtteranceGateOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value ?? new LiveUtteranceGateOptions();
    }

    public LiveUtteranceGateResult Evaluate(LiveUtteranceGateInput input)
    {
        var utterance = input.Utterance;
        var normalized = Normalize(utterance.Text);
        var key = FragmentKey(utterance);
        var combined = CombineWithPending(key, normalized);
        var result = EvaluateCore(input, combined, normalized);

        if (result.Decision is LiveUtteranceGateDecisionKind.HoldForMoreSpeech)
        {
            _pendingFragments[key] = new PendingFragment(combined, DateTimeOffset.UtcNow + (result.HoldWindow ?? TimeSpan.FromMilliseconds(_options.ActiveFlowHoldWindowMs)));
        }
        else
        {
            _pendingFragments.TryRemove(key, out _);
        }

        LogDecision(input, result);
        return result;
    }

    public UtteranceRouteDecision ToRouteDecision(UserUtterance utterance, LiveUtteranceGateResult result)
    {
        return result.Decision switch
        {
            LiveUtteranceGateDecisionKind.AcceptPlaybackControl => Decision(
                UtteranceRouteKind.PauseAndClarify,
                result.Confidence,
                result.Reason,
                utterance.StateWhenCaptured switch
                {
                    LiveAssistantTurnState.Speaking => "StopSpeechOnlyNoConfirmation",
                    LiveAssistantTurnState.AwaitingToolCommit or LiveAssistantTurnState.PlanningTool => "PauseAndConfirmCancel",
                    LiveAssistantTurnState.ExecutingTool => "TryCancelThenClarifyIfNeeded",
                    _ => "PauseActiveTurn"
                }),
            LiveUtteranceGateDecisionKind.AcceptCancellation => Decision(UtteranceRouteKind.CancelActiveTurn, result.Confidence, result.Reason, "CancelActiveTurn"),
            LiveUtteranceGateDecisionKind.AcceptReplacement => Decision(UtteranceRouteKind.ReplaceActiveTurn, result.Confidence, result.Reason, "CancelPendingCommandAndStartReplacement", result.ReplacementText),
            LiveUtteranceGateDecisionKind.AcceptCorrection => Decision(UtteranceRouteKind.ReplaceActiveTurn, result.Confidence, result.Reason, "CancelPendingCommandAndStartReplacement", result.ReplacementText),
            LiveUtteranceGateDecisionKind.AcceptContinuation => Decision(UtteranceRouteKind.AddToActiveTurn, result.Confidence, result.Reason, "ContinueActiveTurn"),
            LiveUtteranceGateDecisionKind.AcceptStatusQuestion => Decision(UtteranceRouteKind.StatusQuestion, result.Confidence, result.Reason, "AnswerStatus"),
            LiveUtteranceGateDecisionKind.HoldForMoreSpeech => Decision(UtteranceRouteKind.BackgroundOrNoOp, result.Confidence, result.Reason, "HoldForMoreSpeech"),
            LiveUtteranceGateDecisionKind.AskClarification => Decision(UtteranceRouteKind.PauseAndClarify, result.Confidence, result.Reason, "AskClarification"),
            LiveUtteranceGateDecisionKind.IgnoreAsWakewordLeak => Decision(UtteranceRouteKind.BackgroundOrNoOp, result.Confidence, result.Reason, "IgnoreWakewordLeak"),
            LiveUtteranceGateDecisionKind.IgnoreAsGarbageTranscript => Decision(UtteranceRouteKind.BackgroundOrNoOp, result.Confidence, result.Reason, "IgnoreGarbageTranscript"),
            LiveUtteranceGateDecisionKind.IgnoreAsNoise or LiveUtteranceGateDecisionKind.IgnoreAsEcho => Decision(UtteranceRouteKind.BackgroundOrNoOp, result.Confidence, result.Reason, "Ignore"),
            LiveUtteranceGateDecisionKind.AcceptNewRequest => Decision(UtteranceRouteKind.Unknown, result.Confidence, result.Reason, "RouteToCommandRouter"),
            _ => Decision(UtteranceRouteKind.Unknown, result.Confidence, result.Reason, "LogOnly")
        };
    }

    private LiveUtteranceGateResult EvaluateCore(LiveUtteranceGateInput input, string normalized, string currentNormalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result(LiveUtteranceGateDecisionKind.IgnoreAsNoise, 0.2, "Empty transcript.", normalized);
        }

        var activeFlow = IsActiveFlow(input);
        var openUrlFlow = IsOpenUrlFlow(input);
        var paused = input.Utterance.StateWhenCaptured is LiveAssistantTurnState.PausedByUser;
        var speaking = input.AssistantWasSpeaking || input.Utterance.StateWhenCaptured is LiveAssistantTurnState.Speaking;
        var strictness = GetStrictnessForSource(input, activeFlow, speaking);
        var stripped = StripLeadingFiller(normalized);
        var analysisText = string.IsNullOrWhiteSpace(stripped) ? normalized : stripped;
        var sourceContext = strictness.ToString();

        if (TryMatchEmbeddedPhrase(normalized, CancellationPhrases, out var cancellationPhrase))
        {
            LogEmbeddedControlPhraseMatched(normalized, cancellationPhrase);
            return Result(LiveUtteranceGateDecisionKind.AcceptCancellation, 0.96, "Matched cancellation phrase.", normalized, stripped, sourceContext, positiveSignals: ["cancellation_phrase"], shouldAffectPlayback: true);
        }

        if (IsFloorTakingCorrection(normalized))
        {
            var holdMs = paused ? _options.PausedClarificationHoldWindowMs : _options.ActiveFlowHoldWindowMs;
            return Result(
                LiveUtteranceGateDecisionKind.HoldForMoreSpeech,
                0.82,
                "Matched floor-taking correction phrase.",
                normalized,
                stripped,
                sourceContext,
                positiveSignals: ["floor_taking_correction"],
                holdWindow: TimeSpan.FromMilliseconds(holdMs));
        }

        if (TryMatchEmbeddedPhrase(normalized, ShortControlPhrases, out var controlPhrase)
            || TryMatchEmbeddedPhrase(analysisText, ShortControlPhrases, out controlPhrase)
            || ((normalized is "no" or "no no" || analysisText is "no" or "no no") && activeFlow))
        {
            if (!string.IsNullOrWhiteSpace(controlPhrase))
            {
                LogEmbeddedControlPhraseMatched(normalized, controlPhrase);
            }

            return Result(LiveUtteranceGateDecisionKind.AcceptPlaybackControl, 0.94, "Matched playback/control phrase.", normalized, stripped, sourceContext, positiveSignals: ["control_phrase"], shouldAffectPlayback: true);
        }

        if ((MatchesAny(normalized, ContinuationPhrases) || MatchesAny(analysisText, ContinuationPhrases)) && (paused || activeFlow))
        {
            return Result(LiveUtteranceGateDecisionKind.AcceptContinuation, 0.9, "Matched continuation phrase in active context.", normalized, stripped, sourceContext, positiveSignals: ["continuation_phrase", "active_context"], shouldAffectPlayback: true);
        }

        if (MatchesAny(normalized, StatusPhrases) || MatchesAny(analysisText, StatusPhrases))
        {
            return Result(LiveUtteranceGateDecisionKind.AcceptStatusQuestion, 0.82, "Matched status question.", normalized, stripped, sourceContext, positiveSignals: ["status_question"]);
        }

        if (TryExtractReplacement(normalized, openUrlFlow, out var replacement) || TryExtractReplacement(analysisText, openUrlFlow, out replacement))
        {
            return Result(LiveUtteranceGateDecisionKind.AcceptReplacement, 0.91, "Matched correction/replacement phrase.", normalized, stripped, sourceContext, positiveSignals: ["replacement_phrase"], replacementText: replacement);
        }

        if (openUrlFlow && IsSingleUsefulTarget(analysisText))
        {
            return Result(LiveUtteranceGateDecisionKind.AcceptReplacement, 0.86, "Single target accepted as replacement during active OpenUrl flow.", normalized, stripped, sourceContext, positiveSignals: ["single_open_url_target", "active_open_url_context"], replacementText: $"open {analysisText}");
        }

        if (IsIncomplete(normalized) || IsIncomplete(analysisText))
        {
            var holdMs = paused ? _options.PausedClarificationHoldWindowMs : activeFlow || speaking ? _options.ActiveFlowHoldWindowMs : _options.IdleHoldWindowMs;
            return Result(
                LiveUtteranceGateDecisionKind.HoldForMoreSpeech,
                0.72,
                "Transcript appears incomplete; holding for more speech.",
                normalized,
                stripped,
                sourceContext,
                negativeSignals: ["incomplete_phrase"],
                holdWindow: TimeSpan.FromMilliseconds(holdMs));
        }

        if (IsWakewordLeak(normalized) || IsWakewordLeak(analysisText))
        {
            return Result(LiveUtteranceGateDecisionKind.IgnoreAsWakewordLeak, 0.75, "Likely wakeword leakage or unrelated assistant invocation.", normalized, stripped, sourceContext, negativeSignals: ["wakeword_leak"]);
        }

        var analysis = AnalyzeGeneralRequest(analysisText);

        if (LooksMalformed(normalized) || LooksMalformed(analysisText) || analysis.IsWordSalad)
        {
            if (paused)
            {
                return Result(
                    LiveUtteranceGateDecisionKind.HoldForMoreSpeech,
                    0.68,
                    "Malformed transcript in paused context; holding before clarification.",
                    normalized,
                    stripped,
                    sourceContext,
                    positiveSignals: analysis.PositiveSignals,
                    negativeSignals: AddSignal(analysis.NegativeSignals, "malformed_transcript"),
                    holdWindow: TimeSpan.FromMilliseconds(_options.PausedClarificationHoldWindowMs),
                    clarificationPrompt: "Sorry, I didn't catch that.");
            }

            return Result(LiveUtteranceGateDecisionKind.IgnoreAsGarbageTranscript, 0.78, "Malformed transcript should not route to general conversation.", normalized, stripped, sourceContext, positiveSignals: analysis.PositiveSignals, negativeSignals: AddSignal(analysis.NegativeSignals, "malformed_transcript"));
        }

        if (input.IsIdleListening && analysis.IsCoherent)
        {
            return Result(
                LiveUtteranceGateDecisionKind.AcceptNewRequest,
                strictness is LiveUtteranceGateStrictness.IntentionalVoiceRequest ? 0.82 : 0.78,
                strictness is LiveUtteranceGateStrictness.IntentionalVoiceRequest
                    ? "Coherent intentional voice request."
                    : "Coherent idle voice request.",
                normalized,
                stripped,
                sourceContext,
                positiveSignals: analysis.PositiveSignals,
                negativeSignals: analysis.NegativeSignals,
                shouldCallDeepInfra: true,
                shouldRouteToCommandRouter: _options.RouteClearIdleRequestsToCommandRouter);
        }

        if (activeFlow && LooksLikeLikelyIntent(analysisText))
        {
            return Result(
                LiveUtteranceGateDecisionKind.AskClarification,
                0.66,
                "Likely user intent in active context but not clear enough to act.",
                normalized,
                stripped,
                sourceContext,
                positiveSignals: analysis.PositiveSignals,
                negativeSignals: analysis.NegativeSignals,
                clarificationPrompt: openUrlFlow ? "What should I use instead?" : "Sorry, I didn't catch that.");
        }

        if (strictness is LiveUtteranceGateStrictness.IntentionalVoiceRequest
            && LooksLikeSentenceLikeIntentionalRequest(analysisText, analysis))
        {
            return Result(
                LiveUtteranceGateDecisionKind.AcceptNewRequest,
                0.7,
                "Sentence-like intentional voice request accepted.",
                normalized,
                stripped,
                sourceContext,
                positiveSignals: AddSignal(analysis.PositiveSignals, "intentional_voice_source"),
                negativeSignals: analysis.NegativeSignals,
                shouldCallDeepInfra: true,
                shouldRouteToCommandRouter: _options.RouteClearIdleRequestsToCommandRouter);
        }

        return Result(LiveUtteranceGateDecisionKind.IgnoreAsGarbageTranscript, 0.62, "Unknown transcript blocked from default general conversation.", normalized, stripped, sourceContext, positiveSignals: analysis.PositiveSignals, negativeSignals: AddSignal(analysis.NegativeSignals, "no_coherent_request_shape"));
    }

    private string CombineWithPending(string key, string normalized)
    {
        if (!_pendingFragments.TryGetValue(key, out var pending))
        {
            return normalized;
        }

        if (pending.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            _pendingFragments.TryRemove(key, out _);
            return normalized;
        }

        return Normalize($"{pending.NormalizedText} {normalized}");
    }

    private static string FragmentKey(UserUtterance utterance)
    {
        return !string.IsNullOrWhiteSpace(utterance.CorrelationId)
            ? utterance.CorrelationId
            : !string.IsNullOrWhiteSpace(utterance.ActiveTurnId)
                ? utterance.ActiveTurnId
                : "idle";
    }

    private static bool IsActiveFlow(LiveUtteranceGateInput input)
    {
        return input.ActiveTurn is not null
            || !string.IsNullOrWhiteSpace(input.Utterance.ActiveTurnId)
            || input.Utterance.StateWhenCaptured is LiveAssistantTurnState.Speaking
                or LiveAssistantTurnState.PausedByUser
                or LiveAssistantTurnState.PlanningTool
                or LiveAssistantTurnState.AwaitingToolCommit
                or LiveAssistantTurnState.ExecutingTool
                or LiveAssistantTurnState.Interpreting
                or LiveAssistantTurnState.ProcessingTurn;
    }

    private static bool IsOpenUrlFlow(LiveUtteranceGateInput input)
    {
        return ContainsAny(input.PendingCommandDescription, "open url", "open facebook", "facebook", "browser")
            || ContainsAny(input.RecentToolName, "open url")
            || ContainsAny(input.RecentToolTarget, "facebook", "google", "url");
    }

    private static bool TryExtractReplacement(string normalized, bool openUrlFlow, out string replacement)
    {
        replacement = string.Empty;
        foreach (var prefix in ReplacementPrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = normalized[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            if (prefix is "not " && !openUrlFlow)
            {
                return false;
            }

            replacement = candidate.StartsWith("open ", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : $"open {candidate}";
            return true;
        }

        if (openUrlFlow && (normalized.EndsWith(" instead", StringComparison.Ordinal) || normalized.Contains(" instead of ", StringComparison.Ordinal)))
        {
            var candidate = normalized.Replace(" instead", string.Empty, StringComparison.Ordinal).Trim();
            replacement = candidate.StartsWith("open ", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : $"open {candidate}";
            return !string.IsNullOrWhiteSpace(candidate);
        }

        return false;
    }

    private static bool IsSingleUsefulTarget(string normalized)
    {
        return SingleWordRegex().IsMatch(normalized)
            && normalized.Length >= 2
            && normalized is not "uh" and not "um" and not "ok" and not "okay" and not "yes" and not "no";
    }

    private static bool IsIncomplete(string normalized)
    {
        return MatchesAny(normalized, Fillers)
            || IncompletePhrases.Any(phrase => string.Equals(normalized, phrase, StringComparison.Ordinal)
                || normalized.StartsWith($"{phrase} ", StringComparison.Ordinal) && normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4);
    }

    private static bool IsFloorTakingCorrection(string normalized)
    {
        return normalized.Contains("not what i meant", StringComparison.Ordinal)
            || normalized.Contains("that's not what i meant", StringComparison.Ordinal)
            || normalized.Contains("that is not what i meant", StringComparison.Ordinal)
            || normalized.Contains("not what i mean", StringComparison.Ordinal);
    }

    private static bool IsWakewordLeak(string normalized)
    {
        return normalized is "hey google" or "ok google" or "okay google" or "hey siri" or "alexa"
            || normalized.StartsWith("hey google ", StringComparison.Ordinal)
            || normalized.StartsWith("ok google ", StringComparison.Ordinal);
    }

    private static bool LooksMalformed(string normalized)
    {
        if (normalized is "hey its infant" or "hey it's infant" or "hey it is infant" or "its infant" or "it's infant" or "it is infant")
        {
            return true;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
        {
            return false;
        }

        return normalized.StartsWith("hey its ", StringComparison.Ordinal)
            || normalized.StartsWith("hey it s ", StringComparison.Ordinal)
            || normalized.StartsWith("hey it is ", StringComparison.Ordinal)
            || normalized.Contains(" infant", StringComparison.Ordinal)
            || words.Count(word => word.Length <= 2) >= Math.Max(2, words.Length - 1);
    }

    private static bool IsClearIdleRequest(string normalized)
    {
        return AnalyzeGeneralRequest(normalized).IsCoherent;
    }

    private static GeneralRequestAnalysis AnalyzeGeneralRequest(string normalized)
    {
        var positives = new List<string>();
        var negatives = new List<string>();
        var words = SplitWords(normalized);

        if (words.Length == 0)
        {
            negatives.Add("empty_after_normalization");
            return new GeneralRequestAnalysis(false, false, positives, negatives);
        }

        var wordSalad = LooksLikeWordSalad(words, normalized, negatives);
        var structuredQuestion = LooksLikeStructuredQuestion(words, normalized, positives, negatives);
        var structuredCommand = LooksLikeStructuredCommand(words, normalized, positives, negatives);

        if (!structuredQuestion && !structuredCommand && ContainsSentenceLikeSubjectVerb(words))
        {
            positives.Add("subject_verb_shape");
        }

        var coherent = !wordSalad
            && (structuredQuestion
                || structuredCommand
                || positives.Contains("subject_verb_shape", StringComparer.Ordinal));

        return new GeneralRequestAnalysis(coherent, wordSalad, positives, negatives);
    }

    private static bool LooksLikeLikelyIntent(string normalized)
    {
        var analysis = AnalyzeGeneralRequest(normalized);
        return SplitWords(normalized).Length >= 3
            && !LooksMalformed(normalized)
            && !analysis.IsWordSalad;
    }

    private static bool LooksLikeStructuredQuestion(string[] words, string normalized, ICollection<string> positives, ICollection<string> negatives)
    {
        if (words.Length < 3)
        {
            return false;
        }

        var first = words[0];
        if (QuestionStarters.Contains(first))
        {
            if (words.Length >= 4 && QuestionSecondWords.Contains(words[1]))
            {
                positives.Add("structured_wh_question");
                return true;
            }

            if (words.Length >= 4 && (words[1] is "do" or "does" or "did") && words.Any(VerbLikeWords.Contains))
            {
                positives.Add("structured_wh_question");
                return true;
            }

            negatives.Add("question_word_without_question_shape");
            return false;
        }

        if (AuxiliaryQuestionStarters.Contains(first))
        {
            var hasContent = words.Skip(1).Any(word => !FillerTokens.Contains(word) && word.Length > 1);
            if (hasContent && (words.Any(VerbLikeWords.Contains) || normalized.Contains(" you ", StringComparison.Ordinal)))
            {
                positives.Add("structured_auxiliary_question");
                return true;
            }

            negatives.Add("auxiliary_without_content");
        }

        return false;
    }

    private static bool LooksLikeStructuredCommand(string[] words, string normalized, ICollection<string> positives, ICollection<string> negatives)
    {
        if (words.Length < 2)
        {
            return false;
        }

        var first = words[0];
        if (first is "look" && words.ElementAtOrDefault(1) is "up")
        {
            positives.Add("structured_command");
            return words.Length >= 3;
        }

        if (first is "tell" && normalized.StartsWith("tell me ", StringComparison.Ordinal))
        {
            positives.Add("structured_command");
            return words.Length >= 3;
        }

        if (first is "search" && (words.ElementAtOrDefault(1) is "for" || words.Length >= 2))
        {
            positives.Add("structured_command");
            return words.Length >= 2;
        }

        if (!CommandVerbs.Contains(first))
        {
            return false;
        }

        var hasObject = words.Skip(1).Any(word => !FillerTokens.Contains(word) && !GarbageTokens.Contains(word));
        if (hasObject)
        {
            positives.Add("structured_command");
            return true;
        }

        negatives.Add("command_without_object");
        return false;
    }

    private static bool LooksLikeWordSalad(string[] words, string normalized, ICollection<string> negatives)
    {
        var garbageCount = words.Count(GarbageTokens.Contains);
        var fillerCount = words.Count(FillerTokens.Contains);

        if (garbageCount > 0)
        {
            negatives.Add("contains_garbage_token");
        }

        if (words.Length <= 3 && fillerCount >= Math.Max(1, words.Length - 1))
        {
            negatives.Add("mostly_filler");
            return true;
        }

        if (garbageCount > 0 && (QuestionStarters.Contains(words[0]) || normalized.StartsWith("it is ", StringComparison.Ordinal) || normalized.StartsWith("hey ", StringComparison.Ordinal)))
        {
            negatives.Add("question_word_followed_by_noun_soup");
            return true;
        }

        if (QuestionStarters.Contains(words[0]) && words.Length >= 3 && !QuestionSecondWords.Contains(words[1]))
        {
            negatives.Add("question_word_without_auxiliary");
            return true;
        }

        if (words.Length >= 3 && !words.Any(VerbLikeWords.Contains) && garbageCount > 0)
        {
            negatives.Add("no_action_or_verb_shape");
            return true;
        }

        return false;
    }

    private static bool ContainsSentenceLikeSubjectVerb(string[] words)
    {
        if (words.Length < 4)
        {
            return false;
        }

        return words.Any(VerbLikeWords.Contains)
            && words.Count(word => !FillerTokens.Contains(word) && !GarbageTokens.Contains(word)) >= 3;
    }

    private static bool LooksLikeSentenceLikeIntentionalRequest(string normalized, GeneralRequestAnalysis analysis)
    {
        var words = SplitWords(normalized);
        return words.Length >= 4
            && !analysis.IsWordSalad
            && !LooksMalformed(normalized)
            && ContainsSentenceLikeSubjectVerb(words);
    }

    private static string StripLeadingFiller(string normalized)
    {
        var words = SplitWords(normalized).ToList();
        while (words.Count > 0 && LeadingFillers.Contains(words[0], StringComparer.Ordinal))
        {
            words.RemoveAt(0);
        }

        return string.Join(' ', words);
    }

    private static string[] SplitWords(string normalized)
    {
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyList<string> AddSignal(IReadOnlyList<string> signals, string signal)
    {
        if (signals.Contains(signal, StringComparer.Ordinal))
        {
            return signals;
        }

        return signals.Concat([signal]).ToArray();
    }

    private static LiveUtteranceGateStrictness GetStrictnessForSource(LiveUtteranceGateInput input, bool activeFlow, bool speaking)
    {
        var source = input.Utterance.Source ?? string.Empty;
        if (string.Equals(source, "voice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "voice_stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "voice_stream_request", StringComparison.OrdinalIgnoreCase))
        {
            return LiveUtteranceGateStrictness.IntentionalVoiceRequest;
        }

        if (speaking)
        {
            return LiveUtteranceGateStrictness.BargeInDuringSpeaking;
        }

        if (activeFlow)
        {
            return LiveUtteranceGateStrictness.BargeInDuringActiveTurn;
        }

        if (string.Equals(source, "live_utterance_monitor", StringComparison.OrdinalIgnoreCase))
        {
            return LiveUtteranceGateStrictness.PassiveMonitor;
        }

        return input.IsIdleListening
            ? LiveUtteranceGateStrictness.IntentionalVoiceRequest
            : LiveUtteranceGateStrictness.PassiveMonitor;
    }

    private static bool MatchesAny(string normalized, IEnumerable<string> phrases)
    {
        return phrases.Any(phrase => string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryMatchEmbeddedPhrase(string normalized, IEnumerable<string> phrases, out string matchedPhrase)
    {
        foreach (var phrase in phrases.OrderByDescending(static phrase => phrase.Length))
        {
            if (string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase)
                || EmbeddedPhraseRegex(Regex.Escape(phrase)).IsMatch(normalized))
            {
                matchedPhrase = phrase;
                return true;
            }
        }

        matchedPhrase = string.Empty;
        return false;
    }

    private void LogEmbeddedControlPhraseMatched(string normalized, string phrase)
    {
        if (string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation(
            "EmbeddedControlPhraseMatched. NormalizedText: {NormalizedText}. Phrase: {Phrase}.",
            normalized,
            phrase);
    }

    private static bool ContainsAny(string? value, params string[] needles)
    {
        return !string.IsNullOrWhiteSpace(value)
            && needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        var lower = value.ToLowerInvariant()
            .Replace("â€™", "'", StringComparison.Ordinal)
            .Replace("â€˜", "'", StringComparison.Ordinal)
            .Replace('’', '\'')
            .Replace('‘', '\'');
        lower = NormalizeContractions(lower);
        lower = NonWordOrSpaceRegex().Replace(lower, " ");
        lower = WhitespaceRegex().Replace(lower, " ").Trim();
        return lower;
    }

    private static string NormalizeContractions(string value)
    {
        var normalized = value;
        foreach (var (contraction, replacement) in ContractionReplacements)
        {
            normalized = WholeWordRegex(Regex.Escape(contraction)).Replace(normalized, replacement);
        }

        return normalized;
    }

    private LiveUtteranceGateResult Result(
        LiveUtteranceGateDecisionKind decision,
        double confidence,
        string reason,
        string normalizedText,
        string? strippedText = null,
        string? sourceContext = null,
        IReadOnlyList<string>? positiveSignals = null,
        IReadOnlyList<string>? negativeSignals = null,
        TimeSpan? holdWindow = null,
        string? clarificationPrompt = null,
        bool shouldCallDeepInfra = false,
        bool shouldRouteToCommandRouter = false,
        bool shouldAffectPlayback = false,
        string? replacementText = null)
    {
        return new LiveUtteranceGateResult
        {
            Decision = decision,
            Confidence = confidence,
            Reason = reason,
            NormalizedText = normalizedText,
            StrippedText = strippedText,
            SourceContext = sourceContext,
            PositiveSignals = positiveSignals ?? Array.Empty<string>(),
            NegativeSignals = negativeSignals ?? Array.Empty<string>(),
            HoldWindow = holdWindow,
            ClarificationPrompt = clarificationPrompt,
            ShouldCallDeepInfra = shouldCallDeepInfra,
            ShouldRouteToCommandRouter = shouldRouteToCommandRouter,
            ShouldAffectPlayback = shouldAffectPlayback,
            ReplacementText = replacementText
        };
    }

    private void LogDecision(LiveUtteranceGateInput input, LiveUtteranceGateResult result)
    {
        _logger.LogInformation(
            "LiveUtteranceGateEvaluated. Text: {Text}. NormalizedText: {NormalizedText}. StrippedText: {StrippedText}. Source: {Source}. SourceContext: {SourceContext}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. AssistantWasSpeaking: {AssistantWasSpeaking}. PositiveSignals: {PositiveSignals}. NegativeSignals: {NegativeSignals}. Decision: {Decision}. Confidence: {Confidence}. Reason: {Reason}. ShouldCallDeepInfra: {ShouldCallDeepInfra}. ShouldRouteToCommandRouter: {ShouldRouteToCommandRouter}. HoldWindowMs: {HoldWindowMs}. ClarificationPrompt: {ClarificationPrompt}.",
            input.Utterance.Text,
            result.NormalizedText,
            string.Equals(result.StrippedText, result.NormalizedText, StringComparison.Ordinal) ? null : result.StrippedText,
            input.Utterance.Source,
            result.SourceContext,
            input.Utterance.ActiveTurnId,
            input.Utterance.CorrelationId,
            input.Utterance.StateWhenCaptured,
            input.AssistantWasSpeaking,
            string.Join(",", result.PositiveSignals),
            string.Join(",", result.NegativeSignals),
            result.Decision,
            result.Confidence,
            result.Reason,
            result.ShouldCallDeepInfra,
            result.ShouldRouteToCommandRouter,
            result.HoldWindow?.TotalMilliseconds,
            result.ClarificationPrompt);
    }

    private static UtteranceRouteDecision Decision(
        UtteranceRouteKind kind,
        double confidence,
        string reason,
        string action,
        string? replacementText = null)
    {
        return new UtteranceRouteDecision
        {
            Kind = kind,
            Confidence = confidence,
            Reason = reason,
            Action = action,
            ReplacementText = replacementText
        };
    }

    [GeneratedRegex("[^a-z0-9' ]+")]
    private static partial Regex NonWordOrSpaceRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex SingleWordRegex();

    private static Regex WholeWordRegex(string escapedPhrase)
    {
        return new Regex($@"\b{escapedPhrase}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static Regex EmbeddedPhraseRegex(string escapedPhrase)
    {
        return new Regex($@"(^|\s){escapedPhrase}(\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private enum LiveUtteranceGateStrictness
    {
        IntentionalVoiceRequest,
        BargeInDuringSpeaking,
        BargeInDuringActiveTurn,
        PassiveMonitor
    }

    private sealed record GeneralRequestAnalysis(
        bool IsCoherent,
        bool IsWordSalad,
        IReadOnlyList<string> PositiveSignals,
        IReadOnlyList<string> NegativeSignals);

    private sealed record PendingFragment(string NormalizedText, DateTimeOffset ExpiresAtUtc);
}
