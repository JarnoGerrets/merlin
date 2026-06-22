namespace Merlin.Backend.Services.SpeechPresence;

public sealed class SpeechPresenceOptions
{
    public bool Enabled { get; set; } = true;

    public bool EnableFloorYield { get; set; }

    public bool FloorYieldRequiresOfficialDecision { get; set; } = true;

    public int FloorYieldMinSustainedMs { get; set; } = 30;

    public bool FloorYieldLogEvidence { get; set; } = true;

    public double MaybeConfidenceThreshold { get; set; } = 0.55;

    public double YesConfidenceThreshold { get; set; } = 0.75;

    public double MinVadConfidence { get; set; } = 0.35;

    public double MinEchoReducedRms { get; set; } = 0.006;

    public double MinRawMicRms { get; set; } = 0.008;

    public double StrongSelfEchoCorrelationThreshold { get; set; } = 0.70;

    public double SelfEchoContaminatedCorrelationThreshold { get; set; } = 0.55;

    public double ClearNearEndVadConfidence { get; set; } = 0.80;

    public double ClearNearEndEchoReducedRms { get; set; } = 0.030;

    public double ClearNearEndRawMicRms { get; set; } = 0.050;

    public bool LogDecisions { get; set; } = true;

    public bool LogDecisionsToLogger { get; set; }

    public bool LogDecisionsToFile { get; set; } = true;

    public string DecisionLogFilePath { get; set; } = "speech-presence.tmp.log";

    public int DecisionLogFlushIntervalMs { get; set; } = 500;

    public int DecisionLogMaxQueueEntries { get; set; } = 5000;

    public int DecisionLogSampleEveryNFrames { get; set; } = 1;
}
