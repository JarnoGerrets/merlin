using Merlin.Backend.Configuration;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class RecentlyYieldedSpokenTurnStoreTests
{
    [Fact]
    public void TryGetFreshSnapshot_ReturnsRecordedSnapshotBeforeTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var store = CreateStore(ttlMs: 10000);

        store.Record(Snapshot(now.AddSeconds(-2)));

        var result = store.TryGetFreshSnapshot(now);

        Assert.NotNull(result);
        Assert.Equal("backend_voice:abc", result!.TurnId);
    }

    [Fact]
    public void TryGetFreshSnapshot_ExpiresSnapshotAfterTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var store = CreateStore(ttlMs: 1000);

        store.Record(Snapshot(now.AddSeconds(-2)));

        Assert.Null(store.TryGetFreshSnapshot(now));
    }

    private static RecentlyYieldedSpokenTurnStore CreateStore(int ttlMs) =>
        new(Options.Create(new InterruptionHandlingOptions
        {
            RecentlyYieldedTurnTtlMs = ttlMs
        }));

    private static RecentlyYieldedSpokenTurnSnapshot Snapshot(DateTimeOffset yieldedAtUtc) => new()
    {
        TurnId = "backend_voice:abc",
        CorrelationId = "backend_voice:abc",
        SpeechType = "FinalAnswer",
        ItemType = "FinalAnswer",
        YieldedAtUtc = yieldedAtUtc,
        YieldReason = "residual_speech_detected",
        YieldSource = "FloorYieldController",
        PlaybackWasCancelledByYieldFallback = true
    };
}
