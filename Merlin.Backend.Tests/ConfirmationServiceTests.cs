using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConfirmationServiceTests
{
    [Fact]
    public void Create_StoresPendingConfirmation()
    {
        var service = new ConfirmationService();

        var confirmation = service.Create(
            "open_application",
            "mspaint.exe",
            "Paint",
            "paint",
            "open paint",
            "open_application",
            "open paint",
            "Open Application");

        Assert.False(string.IsNullOrWhiteSpace(confirmation.ConfirmationId));
        Assert.Equal(1, service.PendingCount);
        Assert.Equal("Paint", service.GetLatestPending()?.DisplayName);
    }

    [Fact]
    public async Task PendingCount_RemovesExpiredConfirmations()
    {
        var service = new ConfirmationService(TimeSpan.FromMilliseconds(10));
        service.Create(
            "open_application",
            "mspaint.exe",
            "Paint",
            "paint",
            "open paint",
            "open_application",
            "open paint",
            "Open Application");

        await Task.Delay(30);

        Assert.Equal(0, service.PendingCount);
        Assert.Null(service.GetLatestPending());
    }
}
