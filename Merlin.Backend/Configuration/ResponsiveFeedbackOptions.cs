namespace Merlin.Backend.Configuration;

public sealed class ResponsiveFeedbackOptions
{
    public bool Enabled { get; set; } = true;

    public bool EnableSpeechFeedback { get; set; } = true;

    public bool EnableVisualFeedback { get; set; } = false;

    public bool UseCardSelectorForImmediateFeedback { get; set; } = true;

    public bool UseCardSelectorForProgressFeedback { get; set; } = false;

    public bool UseCardSelectorForInterruptionBridgeFeedback { get; set; } = true;

    public int ImmediateFeedbackDelayMs { get; set; } = 150;

    public int MinimumMsBeforeGenericFeedback { get; set; } = 250;

    public double MinimumSelectionScore { get; set; } = 0.5;

    public int GlobalCooldownMs { get; set; } = 1500;

    public int DefaultCardCooldownSeconds { get; set; } = 60;

    public int SameTextCooldownSeconds { get; set; } = 120;

    public int MaxImmediateFeedbackPerTurn { get; set; } = 1;

    public int MaxInterruptionBridgeFeedbackPerInterruption { get; set; } = 1;

    public bool PreferTaskAwareFeedback { get; set; } = true;

    public bool SuppressIfMainResponseReady { get; set; } = true;

    public bool SuppressFinalSuccessSpeechAfterImmediateFeedback { get; set; } = true;

    public int MainResponseReadyStateRetentionSeconds { get; set; } = 120;

    public bool SuppressNormalProgressDuringInterruptionHandling { get; set; } = true;

    public bool UseStableCardIdAsSpeechCacheKey { get; set; } = true;

    public bool MarkFeedbackAsReplayableSpeech { get; set; } = true;

    public bool EnableDiagnosticsLogging { get; set; } = false;

    public string? CardsFilePath { get; set; }
}
