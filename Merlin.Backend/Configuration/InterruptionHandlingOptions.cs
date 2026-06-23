namespace Merlin.Backend.Configuration;

public sealed class InterruptionHandlingOptions
{
    public bool Enabled { get; set; } = false;

    public bool EnableLiveBargeInIntegration { get; set; } = false;

    public bool EnableLiveShadowMode { get; set; } = true;

    public bool EnableLivePlaybackActions { get; set; } = false;

    public bool EnableLiveRedirectRouting { get; set; } = false;

    public bool EnableLiveResponsiveFeedbackBridge { get; set; } = false;

    public bool EnableLiveModelCalls { get; set; } = false;

    public bool EnableLiveMinimalBehavior { get; set; } = false;

    public bool EnableLocalClassification { get; set; } = true;

    public bool EnableModelClassificationForAmbiguousCases { get; set; } = false;

    public bool EnableClarificationCalls { get; set; } = false;

    public bool EnableContinuationRecomposition { get; set; } = false;

    public bool EnableParallelContinuationRecomposition { get; set; } = false;

    public bool RecomposeAfterMeaningfulInterruption { get; set; } = true;

    public bool ResumeRawOnlyForBackchannels { get; set; } = true;

    public bool UseResponsiveFeedbackForBridgePhrases { get; set; } = true;

    public bool SuppressNormalProgressDuringInterruptionHandling { get; set; } = true;

    public int ClarificationMaxTokens { get; set; } = 90;

    public int ContinuationMaxTokens { get; set; } = 500;

    public int ClassificationMaxTokens { get; set; } = 120;

    public int MinimumInterruptionTranscriptChars { get; set; } = 2;

    public double MinimumInterruptionTranscriptConfidence { get; set; } = 0.55;

    public int MaxQueuedFollowUps { get; set; } = 3;

    public int MeaningfulInterruptionPauseTimeoutMs { get; set; } = 1200;

    public int ClarificationTimeoutMs { get; set; } = 5000;

    public int ContinuationTimeoutMs { get; set; } = 15000;

    public bool EnableDiagnosticsLogging { get; set; } = true;
}
