using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class PromptRendererTests
{
    private readonly PromptRenderer _renderer = new();

    [Fact]
    public void Render_OrdersBlocksBySortOrder()
    {
        var prompt = _renderer.Render(
        [
            Block(PromptBlockTypes.CurrentUserMessage, "CURRENT USER MESSAGE:", "\"hello\"", 1000, required: true),
            Block(PromptBlockTypes.SystemIdentity, "SYSTEM:", "system text", 100, required: true)
        ]);

        Assert.True(prompt.IndexOf("SYSTEM:", StringComparison.Ordinal) <
            prompt.IndexOf("CURRENT USER MESSAGE:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_SkipsEmptyOptionalBlocks()
    {
        var prompt = _renderer.Render(
        [
            Block(PromptBlockTypes.SystemIdentity, "SYSTEM:", "system text", 100, required: true),
            Block(PromptBlockTypes.RetrievalNotes, "RETRIEVAL NOTES:", "", 700, required: false),
            Block(PromptBlockTypes.CurrentUserMessage, "CURRENT USER MESSAGE:", "\"hello\"", 1000, required: true)
        ]);

        Assert.DoesNotContain("RETRIEVAL NOTES:", prompt);
    }

    [Fact]
    public void Render_SkipsRetrievalNotesByDefault()
    {
        var prompt = _renderer.Render(
        [
            Block(PromptBlockTypes.SystemIdentity, "SYSTEM:", "system text", 100, required: true),
            Block(PromptBlockTypes.RetrievalNotes, "RETRIEVAL NOTES:", "- abc123: Matched keyword: pr4", 700),
            Block(PromptBlockTypes.CurrentUserMessage, "CURRENT USER MESSAGE:", "\"hello\"", 1000, required: true)
        ]);

        Assert.DoesNotContain("RETRIEVAL NOTES:", prompt);
        Assert.DoesNotContain("abc123", prompt);
        Assert.Contains("SYSTEM:", prompt);
        Assert.Contains("CURRENT USER MESSAGE:", prompt);
    }

    [Fact]
    public void Render_RendersRetrievalNotesWhenOptionEnabled()
    {
        var renderer = new PromptRenderer(Options.Create(new CoreMemoryOptions
        {
            IncludeRetrievalNotesInPrompt = true
        }));

        var prompt = renderer.Render(
        [
            Block(PromptBlockTypes.SystemIdentity, "SYSTEM:", "system text", 100, required: true),
            Block(PromptBlockTypes.RetrievalNotes, "RETRIEVAL NOTES:", "- abc123: Matched keyword: pr4", 700),
            Block(PromptBlockTypes.CurrentUserMessage, "CURRENT USER MESSAGE:", "\"hello\"", 1000, required: true)
        ]);

        Assert.Contains("RETRIEVAL NOTES:", prompt);
        Assert.Contains("abc123", prompt);
    }

    [Fact]
    public void Render_RendersRequiredBlocksEvenWhenContentIsEmpty()
    {
        var prompt = _renderer.Render(
        [
            Block(PromptBlockTypes.SystemIdentity, "SYSTEM:", "", 100, required: true)
        ]);

        Assert.Contains("SYSTEM:", prompt);
    }

    [Fact]
    public void Render_KeepsProfileFactHeadingsStable()
    {
        var prompt = _renderer.Render(
        [
            Block(PromptBlockTypes.ResponsePreferences, "RESPONSE PREFERENCES:", "- Jarno prefers concise responses.", 200)
        ]);

        Assert.Contains("USER PROFILE FACTS:", prompt);
        Assert.Contains("RESPONSE PREFERENCES:", prompt);
        Assert.Contains("- Jarno prefers concise responses.", prompt);
    }

    [Fact]
    public void Render_PreservesCurrentUserMessageBlock()
    {
        var prompt = _renderer.Render(
        [
            Block(PromptBlockTypes.CurrentUserMessage, "CURRENT USER MESSAGE:", "\"What now?\"", 1000, required: true)
        ]);

        Assert.Contains("CURRENT USER MESSAGE:", prompt);
        Assert.Contains("\"What now?\"", prompt);
    }

    private static PromptBlock Block(
        string type,
        string title,
        string content,
        int sortOrder,
        bool required = false) =>
        new()
        {
            Type = type,
            Title = title,
            Content = content,
            SortOrder = sortOrder,
            Required = required
        };
}
