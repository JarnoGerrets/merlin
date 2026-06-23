namespace Merlin.Backend.Models;

public sealed class DiagnosticsInfo
{
    public string BackendVersion { get; init; } = string.Empty;

    public string Uptime { get; init; } = string.Empty;

    public bool LocalAiEnabled { get; init; }

    public bool LocalAiAvailable { get; init; }

    public bool ChatToolEnabled { get; init; }

    public string LocalAiProvider { get; init; } = string.Empty;

    public string LocalAiModel { get; init; } = string.Empty;

    public DateTimeOffset? LocalAiLastWarmupUtc { get; init; }

    public string? LocalAiLastError { get; init; }

    public long? LocalAiLastLatencyMs { get; init; }

    public int RegisteredToolCount { get; init; }

    public IReadOnlyCollection<string> RegisteredTools { get; init; } = [];

    public int ActiveWebSocketConnections { get; init; }

    public string LastIntentParserUsed { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public DateTimeOffset CurrentTimeUtc { get; init; }

    public long TotalRequestsProcessed { get; init; }

    public long TotalSuccessfulToolExecutions { get; init; }

    public long TotalFailedToolExecutions { get; init; }

    public int PendingConfirmations { get; init; }

    public string ConfirmationExpiryDuration { get; init; } = string.Empty;

    public string ResolverStatus { get; init; } = string.Empty;

    public int TrustedApplicationCount { get; init; }

    public int TrustedUrlCount { get; init; }

    public int TrustedCommandCount { get; init; }

    public bool TrustedCommandRoutingEnabled { get; init; }

    public bool TrustedCommandMappingsQuarantined { get; init; }

    public string LastApplicationResolutionStatus { get; init; } = string.Empty;

    public string ConversationSessionId { get; init; } = string.Empty;

    public int ConversationMessageCount { get; init; }

    public int ConversationSummaryLength { get; init; }

    public DateTimeOffset ConversationSessionCreatedUtc { get; init; }

    public int ConversationSummaryCount { get; init; }

    public DateTimeOffset? LastConversationSummaryDate { get; init; }

    public bool ConversationSummaryStoreHealthy { get; init; }

    public int MemoryCount { get; init; }

    public string MemoryMode { get; init; } = string.Empty;

    public bool CoreDatabaseAvailable { get; init; }

    public bool CoreMemoryHealthy { get; init; }

    public bool RequireCoreMemoryForConversation { get; init; }

    public int CoreMemoryCount { get; init; }

    public int ActiveProfileFactCount { get; init; }

    public int ConceptCount { get; init; }

    public int LegacyJsonMemoryCount { get; init; }

    public bool LegacyJsonEnabled { get; init; }

    public bool DegradedFallbackEnabled { get; init; }

    public int MemoryCandidateCount { get; init; }

    public bool MemoryStoreHealthy { get; init; }

    public int SupportedCapabilityCount { get; init; }

    public bool MissingCapabilityDetectionEnabled { get; init; }

    public int CapabilityDomainCount { get; init; }

    public int ImplementedCapabilityCount { get; init; }

    public int MissingCapabilityCount { get; init; }

    public int UnsupportedCapabilityCount { get; init; }

    public bool SystemResourceProviderEnabled { get; init; }
}
