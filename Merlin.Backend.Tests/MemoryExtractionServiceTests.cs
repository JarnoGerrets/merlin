using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MemoryExtractionServiceTests
{
    [Fact]
    public void ExtractFromSummary_WhenPreferenceIsPresent_CreatesCandidate()
    {
        var service = new MemoryExtractionService(new FakeLongTermMemoryStore());

        var candidates = service.ExtractFromSummary(new ConversationSummary
        {
            SummaryId = "summary-1",
            Title = "Godot Preference",
            SummaryText = "Jarno prefers Godot.",
            Tags = ["merlin"],
            MessageCount = 1
        });

        var candidate = Assert.Single(candidates);
        Assert.Equal("preference", candidate.Category);
        Assert.Equal("ui_framework", candidate.Key);
        Assert.Equal("Godot", candidate.Value);
        Assert.Single(service.PendingCandidates);
    }

    [Fact]
    public void ExtractFromSummary_WhenArchitectureIsPresent_CreatesProjectCandidate()
    {
        var service = new MemoryExtractionService(new FakeLongTermMemoryStore());

        var candidates = service.ExtractFromSummary(new ConversationSummary
        {
            SummaryId = "summary-1",
            Title = "Architecture",
            SummaryText = "Merlin uses Godot frontend and .NET backend.",
            Tags = ["backend"],
            MessageCount = 1
        });

        Assert.Contains(candidates, candidate => candidate.Category == "project"
            && candidate.Value.Contains("Godot frontend and .NET backend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApproveCandidate_SavesMemoryPermanently()
    {
        var memoryStore = new FakeLongTermMemoryStore();
        var service = new MemoryExtractionService(memoryStore);
        var candidate = service.ExtractFromSummary(new ConversationSummary
        {
            SummaryId = "summary-1",
            Title = "Godot Preference",
            SummaryText = "Jarno prefers Godot.",
            Tags = ["merlin"],
            MessageCount = 1
        }).Single();

        var memory = service.ApproveCandidate(candidate.CandidateId);

        Assert.NotNull(memory);
        Assert.Equal("preference", memory.Category);
        Assert.Equal("ui_framework", memory.Key);
        Assert.Single(memoryStore.GetAll());
        Assert.Empty(service.PendingCandidates);
    }

    [Fact]
    public void RejectCandidate_RemovesCandidateWithoutSavingMemory()
    {
        var memoryStore = new FakeLongTermMemoryStore();
        var service = new MemoryExtractionService(memoryStore);
        var candidate = service.ExtractFromSummary(new ConversationSummary
        {
            SummaryId = "summary-1",
            Title = "Godot Preference",
            SummaryText = "Jarno prefers Godot.",
            Tags = ["merlin"],
            MessageCount = 1
        }).Single();

        var rejected = service.RejectCandidate(candidate.CandidateId);

        Assert.True(rejected);
        Assert.Empty(service.PendingCandidates);
        Assert.Empty(memoryStore.GetAll());
    }

    [Fact]
    public void ExtractFromTrustedCommand_CreatesOperationalCandidate()
    {
        var service = new MemoryExtractionService(new FakeLongTermMemoryStore());

        var candidates = service.ExtractFromTrustedCommand(new TrustedCommandMapping
        {
            OriginalCommand = "open paint",
            NormalizedCommand = "open paint",
            ToolName = "Open Application",
            Target = "mspaint.exe",
            DisplayName = "Paint",
            Intent = "open_application"
        });

        var candidate = Assert.Single(candidates);
        Assert.Equal("operational", candidate.Category);
        Assert.Contains("open paint", candidate.Value);
    }
}
