using Merlin.Backend.Tools;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class EditBrowserMappingToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTargetIsDomainSuffix_UpdatesExistingMapping()
    {
        var store = new FakeTrustedUrlStore();
        store.SaveMapping("terminal", "https://terminal.com", "terminal.com");
        var tool = new EditBrowserMappingTool(store);

        var result = await tool.ExecuteAsync("edit browser mapping terminal to .co.uk");

        Assert.True(result.Success);
        Assert.Equal("tool.browser.mapping.updated", result.SpeechCacheKey);
        Assert.Equal("https://terminal.co.uk", store.FindByAlias("terminal")?.Url);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetIsDomain_UpdatesExistingMapping()
    {
        var store = new FakeTrustedUrlStore();
        store.SaveMapping("terminal", "https://terminal.com", "terminal.com");
        var tool = new EditBrowserMappingTool(store);

        var result = await tool.ExecuteAsync("change terminal browser mapping to terminal.nl");

        Assert.True(result.Success);
        Assert.Equal("https://terminal.nl", store.FindByAlias("terminal")?.Url);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetIsMissing_CreatesPendingInteraction()
    {
        var store = new FakeTrustedUrlStore();
        store.SaveMapping("terminal", "https://terminal.com", "terminal.com");
        var pendingInteractions = new PendingInteractionService();
        var tool = new EditBrowserMappingTool(store, pendingInteractions);

        var result = await tool.ExecuteAsync("edit browser mapping terminal");

        Assert.True(result.Success);
        Assert.Equal("tool.browser.mapping.edit.prompt", result.SpeechCacheKey);
        var pending = pendingInteractions.GetLatestPending(PendingInteractionTypes.BrowserMappingEdit);
        Assert.NotNull(pending);
        Assert.Equal("terminal", pending.Context["alias"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPendingEditIsCancelled_ClearsPendingInteraction()
    {
        var store = new FakeTrustedUrlStore();
        var pendingInteractions = new PendingInteractionService();
        pendingInteractions.Create(
            PendingInteractionTypes.BrowserMappingEdit,
            "What should I change terminal to?",
            new Dictionary<string, string> { ["alias"] = "terminal" },
            "edit browser mapping terminal");
        var tool = new EditBrowserMappingTool(store, pendingInteractions);

        var result = await tool.ExecuteAsync("cancel browser mapping edit");

        Assert.True(result.Success);
        Assert.Equal(0, pendingInteractions.PendingCount);
    }
}
