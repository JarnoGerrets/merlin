using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class BrowserPageSafetyGuardTests
{
    private readonly BrowserPageSafetyGuard _guard = new();

    [Theory]
    [InlineData("Documentation", BrowserSnapshotElementType.Link)]
    [InlineData("Read more", BrowserSnapshotElementType.Link)]
    [InlineData("Next", BrowserSnapshotElementType.Button)]
    [InlineData("Accept cookies", BrowserSnapshotElementType.Button)]
    [InlineData("First webcam result", BrowserSnapshotElementType.Result)]
    public void Evaluate_WhenSafeNavigationAction_ReturnsAllow(
        string text,
        BrowserSnapshotElementType type)
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element(text, type)
        });

        Assert.Equal(BrowserPageSafetyLevel.Allow, decision.Level);
    }

    [Theory]
    [InlineData("Delete", BrowserPageSafetyRisk.DestructiveAction)]
    [InlineData("Remove", BrowserPageSafetyRisk.DestructiveAction)]
    [InlineData("Checkout", BrowserPageSafetyRisk.PurchaseOrPayment)]
    [InlineData("Buy now", BrowserPageSafetyRisk.PurchaseOrPayment)]
    [InlineData("Pay", BrowserPageSafetyRisk.PurchaseOrPayment)]
    [InlineData("Send", BrowserPageSafetyRisk.PersonalDataSubmission)]
    [InlineData("Submit", BrowserPageSafetyRisk.PersonalDataSubmission)]
    [InlineData("Unsubscribe", BrowserPageSafetyRisk.DestructiveAction)]
    [InlineData("Download installer", BrowserPageSafetyRisk.FileDownload)]
    public void Evaluate_WhenRiskyAction_ReturnsRequireConfirmation(
        string text,
        BrowserPageSafetyRisk expectedRisk)
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element(text, BrowserSnapshotElementType.Button)
        });

        Assert.Equal(BrowserPageSafetyLevel.RequireConfirmation, decision.Level);
        Assert.Contains(expectedRisk, decision.Risks);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("Credit card number")]
    [InlineData("CVV")]
    [InlineData("Verification code")]
    [InlineData("CAPTCHA")]
    public void Evaluate_WhenSensitiveField_ReturnsBlock(string label)
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.SearchCurrentPage,
            Element = Element(label, BrowserSnapshotElementType.Input) with
            {
                Label = label,
                Placeholder = label
            }
        });

        Assert.Equal(BrowserPageSafetyLevel.Block, decision.Level);
    }

    [Fact]
    public void Evaluate_WhenContinueButtonNearPayment_ReturnsRequireConfirmation()
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element("Continue", BrowserSnapshotElementType.Button),
            NearbyElements =
            [
                Element("Complete payment", BrowserSnapshotElementType.TextBlock)
            ]
        });

        Assert.Equal(BrowserPageSafetyLevel.RequireConfirmation, decision.Level);
        Assert.Contains(BrowserPageSafetyRisk.PurchaseOrPayment, decision.Risks);
    }

    [Fact]
    public void Evaluate_WhenSearchResultNearRiskyPageText_ReturnsAllow()
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element("Official music video", BrowserSnapshotElementType.Result) with
            {
                Href = "https://www.youtube.com/watch?v=test"
            },
            PageTitle = "Sign in to continue",
            NearbyElements =
            [
                Element("Login to your account", BrowserSnapshotElementType.TextBlock),
                Element("Checkout payment details", BrowserSnapshotElementType.TextBlock)
            ]
        });

        Assert.Equal(BrowserPageSafetyLevel.Allow, decision.Level);
    }

    [Fact]
    public void Evaluate_WhenSearchResultTextContainsRiskKeyword_ReturnsAllow()
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element("Buy now music video review", BrowserSnapshotElementType.Result) with
            {
                Href = "https://example.test/watch"
            }
        });

        Assert.Equal(BrowserPageSafetyLevel.Allow, decision.Level);
    }

    [Fact]
    public void Evaluate_WhenExecutableDownloadHref_ReturnsBlock()
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element("Download", BrowserSnapshotElementType.Link) with
            {
                Href = "https://example.test/setup.exe"
            }
        });

        Assert.Equal(BrowserPageSafetyLevel.Block, decision.Level);
        Assert.Contains(BrowserPageSafetyRisk.ExternalExecutable, decision.Risks);
    }

    [Fact]
    public void Evaluate_WhenSaveChangesOnAccountPage_ReturnsRequireConfirmation()
    {
        var decision = _guard.Evaluate(new BrowserPageSafetyContext
        {
            Action = BrowserPageAction.ClickVisibleElement,
            Element = Element("Save changes", BrowserSnapshotElementType.Button),
            PageTitle = "Account settings"
        });

        Assert.Equal(BrowserPageSafetyLevel.RequireConfirmation, decision.Level);
        Assert.Contains(BrowserPageSafetyRisk.AccountChange, decision.Risks);
    }

    private static BrowserSnapshotElement Element(
        string text,
        BrowserSnapshotElementType type) =>
        new()
        {
            Id = text.Replace(' ', '_').ToLowerInvariant(),
            Type = type,
            Text = text,
            IsVisible = true,
            IsEnabled = true,
            IsInViewport = true,
            Rect = new BrowserSnapshotRect
            {
                Width = 120,
                Height = 28
            }
        };
}
