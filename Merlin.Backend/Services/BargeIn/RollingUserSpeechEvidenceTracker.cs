using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

internal sealed class RollingUserSpeechEvidenceTracker
{
    private readonly Queue<Sample> _samples = new();

    public RollingUserSpeechEvidenceSnapshot Observe(
        DateTimeOffset timestamp,
        int durationMs,
        double? userSpeechScore,
        BargeInOptions options)
    {
        var sampleDurationMs = Math.Max(1, durationMs);
        if (userSpeechScore is > 0.0)
        {
            _samples.Enqueue(new Sample(timestamp, sampleDurationMs, Math.Clamp(userSpeechScore.Value, 0.0, 1.0)));
        }

        return Compute(timestamp, options);
    }

    public RollingUserSpeechEvidenceSnapshot Compute(DateTimeOffset timestamp, BargeInOptions options)
    {
        var windowMs = Math.Max(1, options.FloorYieldEvidenceWindowMs);
        var highScoreThreshold = Math.Clamp(options.FloorYieldHighScoreThreshold, 0.0, 1.0);
        var requiredHighScoreMs = Math.Max(1, options.FloorYieldRequiredHighScoreMs);
        var averageScoreThreshold = Math.Clamp(options.FloorYieldAverageScoreThreshold, 0.0, 1.0);
        var recentHighFrameWindowMs = Math.Max(1, options.FloorYieldRecentHighFrameWindowMs);
        var cutoff = timestamp.AddMilliseconds(-windowMs);

        while (_samples.Count > 0 && _samples.Peek().Timestamp < cutoff)
        {
            _samples.Dequeue();
        }

        var highScoreMs = 0;
        var observedMs = 0;
        var weightedScore = 0.0;
        var recentHighCutoff = timestamp.AddMilliseconds(-recentHighFrameWindowMs);
        var recentHighFramePresent = !options.FloorYieldRequireRecentHighFrame;

        foreach (var sample in _samples)
        {
            observedMs += sample.DurationMs;
            weightedScore += sample.Score * sample.DurationMs;
            if (sample.Score >= highScoreThreshold)
            {
                highScoreMs += sample.DurationMs;
                if (sample.Timestamp >= recentHighCutoff)
                {
                    recentHighFramePresent = true;
                }
            }
        }

        var averageScore = observedMs > 0 ? weightedScore / observedMs : 0.0;
        var highScoreCriterionMet = highScoreMs >= requiredHighScoreMs;
        var averageCriterionMet = observedMs >= requiredHighScoreMs && averageScore >= averageScoreThreshold;
        var shouldYield = recentHighFramePresent && (highScoreCriterionMet || averageCriterionMet);
        var reason = shouldYield
            ? highScoreCriterionMet
                ? "high_score_duration_met"
                : "average_score_threshold_met"
            : !recentHighFramePresent
                ? "recent_high_frame_missing"
                : "rolling_evidence_below_threshold";

        return new RollingUserSpeechEvidenceSnapshot
        {
            WindowMs = windowMs,
            HighScoreThreshold = highScoreThreshold,
            HighScoreMsInWindow = highScoreMs,
            RequiredHighScoreMs = requiredHighScoreMs,
            AverageScore = averageScore,
            AverageScoreThreshold = averageScoreThreshold,
            RecentHighFramePresent = recentHighFramePresent,
            RecentHighFrameWindowMs = recentHighFrameWindowMs,
            ObservedMsInWindow = observedMs,
            ShouldYield = shouldYield,
            DecisionReason = reason
        };
    }

    public RollingUserSpeechEvidenceSnapshot Reset(DateTimeOffset timestamp, BargeInOptions options)
    {
        _samples.Clear();
        return Compute(timestamp, options);
    }

    private sealed record Sample(DateTimeOffset Timestamp, int DurationMs, double Score);
}

internal sealed record RollingUserSpeechEvidenceSnapshot
{
    public required int WindowMs { get; init; }

    public required double HighScoreThreshold { get; init; }

    public required int HighScoreMsInWindow { get; init; }

    public required int RequiredHighScoreMs { get; init; }

    public required double AverageScore { get; init; }

    public required double AverageScoreThreshold { get; init; }

    public required bool RecentHighFramePresent { get; init; }

    public required int RecentHighFrameWindowMs { get; init; }

    public required int ObservedMsInWindow { get; init; }

    public required bool ShouldYield { get; init; }

    public required string DecisionReason { get; init; }
}
