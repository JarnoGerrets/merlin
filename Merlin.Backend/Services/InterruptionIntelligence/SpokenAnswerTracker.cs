using System.Collections.Concurrent;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class SpokenAnswerTracker : ISpokenAnswerTracker
{
    private readonly ConcurrentDictionary<string, SpokenAnswerState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private readonly SpokenSentenceSegmenter _sentenceSegmenter = new();

    public SpokenAnswerState? GetState(string turnId)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return null;
        }

        return _states.TryGetValue(turnId.Trim(), out var state)
            ? state
            : null;
    }

    public SpokenAnswerState StartAnswer(
        string turnId,
        string correlationId,
        string originalUserQuestion,
        string? originalAssistantDraft = null,
        string? currentTopicLabel = null)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new ArgumentException("Turn id is required.", nameof(turnId));
        }

        var state = new SpokenAnswerState
        {
            TurnId = turnId.Trim(),
            CorrelationId = correlationId?.Trim() ?? string.Empty,
            OriginalUserQuestion = originalUserQuestion?.Trim() ?? string.Empty,
            OriginalAssistantDraft = NormalizeOptional(originalAssistantDraft),
            CurrentTopicLabel = NormalizeOptional(currentTopicLabel),
            CanRecompose = !string.IsNullOrWhiteSpace(originalUserQuestion),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        lock (_syncRoot)
        {
            _states[state.TurnId] = state;
        }

        return state;
    }

    public SpokenAnswerState AppendSpokenText(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null)
    {
        return Mutate(turnId, state =>
        {
            var spokenSoFar = JoinText(state.SpokenSoFar, text);
            return RebuildFromSpokenText(state, spokenSoFar, playbackPosition);
        });
    }

    public SpokenAnswerState MarkChunkStarted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null)
    {
        return Mutate(turnId, state =>
        {
            var chunk = SpokenSentenceSegmenter.NormalizeSpacing(text);
            return Clone(
                state,
                currentPartialSentence: chunk,
                playbackPosition: playbackPosition ?? state.PlaybackPosition,
                updatedAtUtc: DateTimeOffset.UtcNow);
        });
    }

    public SpokenAnswerState MarkChunkCompleted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null)
    {
        return Mutate(turnId, state =>
        {
            var chunk = SpokenSentenceSegmenter.NormalizeSpacing(text);
            var spokenSoFar = EndsWithChunk(state.SpokenSoFar, chunk)
                ? state.SpokenSoFar
                : JoinText(state.SpokenSoFar, chunk);
            return RebuildFromSpokenText(state, spokenSoFar, playbackPosition);
        });
    }

    public SpokenAnswerCheckpoint CreateCheckpoint(
        string turnId,
        bool discardCurrentPartialSentence = true)
    {
        var normalizedTurnId = RequireTurnId(turnId);
        lock (_syncRoot)
        {
            if (!_states.TryGetValue(normalizedTurnId, out var state))
            {
                throw new InvalidOperationException($"No spoken answer state exists for turn '{normalizedTurnId}'.");
            }

            var safePrefix = discardCurrentPartialSentence
                ? SafePrefixFromCompletedSentences(state.SpokenSoFar)
                : state.SpokenSoFar;
            var discardedPartial = discardCurrentPartialSentence
                ? state.CurrentPartialSentence
                : string.Empty;

            return new SpokenAnswerCheckpoint
            {
                TurnId = state.TurnId,
                CorrelationId = state.CorrelationId,
                OriginalUserQuestion = state.OriginalUserQuestion,
                SafeSpokenPrefix = safePrefix,
                LastCompletedSentence = state.LastCompletedSentence,
                DiscardedPartialSentence = discardedPartial,
                CurrentTopicLabel = state.CurrentTopicLabel,
                PlaybackPosition = state.PlaybackPosition,
                OriginalPlanOrIntent = state.OriginalAssistantDraft
            };
        }
    }

    public void Clear(string turnId)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        lock (_syncRoot)
        {
            _states.TryRemove(turnId.Trim(), out _);
        }
    }

    private SpokenAnswerState Mutate(string turnId, Func<SpokenAnswerState, SpokenAnswerState> mutation)
    {
        var normalizedTurnId = RequireTurnId(turnId);
        lock (_syncRoot)
        {
            if (!_states.TryGetValue(normalizedTurnId, out var state))
            {
                throw new InvalidOperationException($"No spoken answer state exists for turn '{normalizedTurnId}'.");
            }

            var updated = mutation(state);
            _states[normalizedTurnId] = updated;
            return updated;
        }
    }

    private SpokenAnswerState RebuildFromSpokenText(
        SpokenAnswerState state,
        string spokenSoFar,
        TimeSpan? playbackPosition)
    {
        var segmentation = _sentenceSegmenter.Segment(spokenSoFar);
        return Clone(
            state,
            spokenSoFar: spokenSoFar,
            lastCompletedSentence: segmentation.LastCompletedSentence,
            currentPartialSentence: segmentation.CurrentPartialSentence,
            unspokenRemainder: CalculateUnspokenRemainder(state.OriginalAssistantDraft, spokenSoFar),
            playbackPosition: playbackPosition ?? state.PlaybackPosition,
            updatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static SpokenAnswerState Clone(
        SpokenAnswerState state,
        string? spokenSoFar = null,
        string? lastCompletedSentence = null,
        string? currentPartialSentence = null,
        string? unspokenRemainder = null,
        TimeSpan? playbackPosition = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return new SpokenAnswerState
        {
            TurnId = state.TurnId,
            CorrelationId = state.CorrelationId,
            OriginalUserQuestion = state.OriginalUserQuestion,
            OriginalAssistantDraft = state.OriginalAssistantDraft,
            SpokenSoFar = spokenSoFar ?? state.SpokenSoFar,
            LastCompletedSentence = lastCompletedSentence ?? state.LastCompletedSentence,
            CurrentPartialSentence = currentPartialSentence ?? state.CurrentPartialSentence,
            UnspokenRemainder = unspokenRemainder ?? state.UnspokenRemainder,
            CurrentTopicLabel = state.CurrentTopicLabel,
            PlaybackPosition = playbackPosition ?? state.PlaybackPosition,
            CanRecompose = state.CanRecompose,
            UpdatedAtUtc = updatedAtUtc ?? state.UpdatedAtUtc
        };
    }

    private string SafePrefixFromCompletedSentences(string spokenSoFar)
    {
        var segmentation = _sentenceSegmenter.Segment(spokenSoFar);
        return string.Join(' ', segmentation.CompletedSentences).Trim();
    }

    private static string CalculateUnspokenRemainder(string? originalAssistantDraft, string spokenSoFar)
    {
        if (string.IsNullOrWhiteSpace(originalAssistantDraft) || string.IsNullOrWhiteSpace(spokenSoFar))
        {
            return string.Empty;
        }

        var draft = SpokenSentenceSegmenter.NormalizeSpacing(originalAssistantDraft);
        var spoken = SpokenSentenceSegmenter.NormalizeSpacing(spokenSoFar);
        var index = draft.IndexOf(spoken, StringComparison.Ordinal);
        if (index < 0)
        {
            return string.Empty;
        }

        return draft[(index + spoken.Length)..].Trim();
    }

    private static string JoinText(string existing, string addition)
    {
        var normalizedAddition = SpokenSentenceSegmenter.NormalizeSpacing(addition);
        if (string.IsNullOrWhiteSpace(normalizedAddition))
        {
            return SpokenSentenceSegmenter.NormalizeSpacing(existing);
        }

        var normalizedExisting = SpokenSentenceSegmenter.NormalizeSpacing(existing);
        if (string.IsNullOrWhiteSpace(normalizedExisting))
        {
            return normalizedAddition;
        }

        return $"{normalizedExisting} {normalizedAddition}".Trim();
    }

    private static bool EndsWithChunk(string spokenSoFar, string chunk)
    {
        if (string.IsNullOrWhiteSpace(spokenSoFar) || string.IsNullOrWhiteSpace(chunk))
        {
            return false;
        }

        return SpokenSentenceSegmenter.NormalizeSpacing(spokenSoFar)
            .EndsWith(SpokenSentenceSegmenter.NormalizeSpacing(chunk), StringComparison.Ordinal);
    }

    private static string RequireTurnId(string turnId)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new ArgumentException("Turn id is required.", nameof(turnId));
        }

        return turnId.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = SpokenSentenceSegmenter.NormalizeSpacing(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
