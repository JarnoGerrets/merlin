using System.Reflection;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class BrowserWorkspaceScoringTests
{
    [Fact]
    public void CommonActionScoring_UsesYoutubeTooltipMetadataForPause()
    {
        var snapshot = new BrowserPageSnapshot
        {
            Buttons =
            [
                Element("button_1", BrowserSnapshotElementType.Button) with
                {
                    AriaLabel = "Afspelen (k)",
                    DataTitleNoTooltip = "Pauzeren",
                    DataTooltipTitle = "Pauzeren (k)",
                    CssClass = "ytp-play-button ytp-button",
                    Rect = new BrowserSnapshotRect { Width = 48, Height = 36 }
                },
                Element("button_2", BrowserSnapshotElementType.Button) with
                {
                    Text = "Advertentie",
                    CssClass = "ad-button",
                    Rect = new BrowserSnapshotRect { Width = 160, Height = 40 }
                }
            ]
        };

        var candidates = InvokeCandidateBuilder("BuildCommonActionCandidates", snapshot, "pause_video");

        Assert.NotEmpty(candidates);
        Assert.Equal("button_1", CandidateElement(candidates[0]).Id);
    }

    [Fact]
    public void CommonActionScoring_UsesYoutubeSkipAdClassAndText()
    {
        var snapshot = new BrowserPageSnapshot
        {
            Buttons =
            [
                Element("button_1", BrowserSnapshotElementType.Button) with
                {
                    Text = "Overslaan",
                    DomId = "skip-button:2",
                    CssClass = "ytp-skip-ad-button",
                    Rect = new BrowserSnapshotRect { Width = 150, Height = 40 }
                }
            ]
        };

        var candidates = InvokeCandidateBuilder("BuildCommonActionCandidates", snapshot, "skip_ad");

        Assert.NotEmpty(candidates);
        Assert.Equal("button_1", CandidateElement(candidates[0]).Id);
    }

    [Fact]
    public void ClickScoring_DoesNotMatchUnrelatedVisibleAdLink()
    {
        var snapshot = new BrowserPageSnapshot
        {
            Links =
            [
                Element("link_1", BrowserSnapshotElementType.Link) with
                {
                    Text = "Schaalbare narrowcasting",
                    Href = "https://www.googleadservices.com/pagead/aclk",
                    Rect = new BrowserSnapshotRect { Width = 150, Height = 36 }
                }
            ]
        };

        var candidates = InvokeCandidateBuilder("BuildClickCandidates", snapshot, "pause", null, null);

        Assert.Empty(candidates);
    }

    private static BrowserSnapshotElement Element(string id, BrowserSnapshotElementType type) => new()
    {
        Id = id,
        Type = type,
        IsVisible = true,
        IsEnabled = true,
        IsInViewport = true
    };

    private static IReadOnlyList<object> InvokeCandidateBuilder(string methodName, params object?[] parameters)
    {
        var method = typeof(BrowserWorkspaceService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, parameters);
        Assert.NotNull(result);
        return ((System.Collections.IEnumerable)result).Cast<object>().ToArray();
    }

    private static BrowserSnapshotElement CandidateElement(object candidate)
    {
        var property = candidate.GetType().GetProperty("Element");
        Assert.NotNull(property);

        var value = property.GetValue(candidate);
        Assert.IsType<BrowserSnapshotElement>(value);
        return (BrowserSnapshotElement)value;
    }
}
