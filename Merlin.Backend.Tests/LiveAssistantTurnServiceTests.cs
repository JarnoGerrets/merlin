using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LiveAssistantTurnServiceTests
{
    [Fact]
    public void BeginTurn_RegistersActiveTurn()
    {
        var service = CreateService();

        var turn = service.BeginTurn("conversation-1", "correlation-1");

        Assert.True(service.IsActive("correlation-1"));
        Assert.True(service.ShouldEmit("correlation-1"));
        Assert.Equal("correlation-1", turn.AssistantTurnId);
    }

    [Fact]
    public async Task CancelTurn_CancelsTokenAndSuppressesEmit()
    {
        var service = CreateService();
        var turn = service.BeginTurn("conversation-1", "correlation-1");

        var cancelled = await service.CancelTurnAsync(
            "correlation-1",
            LiveAssistantTurnCancelReason.UserHardStop);

        Assert.True(cancelled);
        Assert.True(turn.CancellationToken.IsCancellationRequested);
        Assert.True(service.IsCancelled("correlation-1"));
        Assert.False(service.ShouldEmit("correlation-1"));
    }

    [Fact]
    public async Task CancelTurn_IsIdempotent()
    {
        var service = CreateService();
        service.BeginTurn("conversation-1", "correlation-1");

        var first = await service.CancelTurnAsync(
            "correlation-1",
            LiveAssistantTurnCancelReason.UserHardStop);
        var second = await service.CancelTurnAsync(
            "correlation-1",
            LiveAssistantTurnCancelReason.UserHardStop);

        Assert.True(first);
        Assert.False(second);
        Assert.True(service.IsCancelled("correlation-1"));
    }

    [Fact]
    public void CompleteTurn_RemovesTurn()
    {
        var service = CreateService();
        service.BeginTurn("conversation-1", "correlation-1");

        service.CompleteTurn("correlation-1");

        Assert.False(service.IsActive("correlation-1"));
        Assert.False(service.IsCancelled("correlation-1"));
        Assert.False(service.ShouldEmit("correlation-1"));
    }

    [Fact]
    public async Task CancelUnknownTurn_DoesNotThrow()
    {
        var service = CreateService();

        var cancelled = await service.CancelTurnAsync(
            "missing",
            LiveAssistantTurnCancelReason.UserHardStop);

        Assert.False(cancelled);
    }

    [Fact]
    public async Task CancellingOneCorrelationId_DoesNotCancelAnother()
    {
        var service = CreateService();
        var first = service.BeginTurn("conversation-1", "correlation-1");
        var second = service.BeginTurn("conversation-1", "correlation-2");

        await service.CancelTurnAsync(
            "correlation-1",
            LiveAssistantTurnCancelReason.UserHardStop);

        Assert.True(first.CancellationToken.IsCancellationRequested);
        Assert.False(second.CancellationToken.IsCancellationRequested);
        Assert.False(service.ShouldEmit("correlation-1"));
        Assert.True(service.ShouldEmit("correlation-2"));
    }

    [Fact]
    public async Task NewTurnAfterCancellation_CanCompleteNormally()
    {
        var service = CreateService();
        service.BeginTurn("conversation-1", "correlation-1");
        await service.CancelTurnAsync(
            "correlation-1",
            LiveAssistantTurnCancelReason.UserHardStop);
        service.CompleteTurn("correlation-1");

        service.BeginTurn("conversation-1", "correlation-1");

        Assert.True(service.ShouldEmit("correlation-1"));
        service.CompleteTurn("correlation-1");
        Assert.False(service.IsActive("correlation-1"));
    }

    private static LiveAssistantTurnService CreateService()
    {
        return new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance);
    }
}
