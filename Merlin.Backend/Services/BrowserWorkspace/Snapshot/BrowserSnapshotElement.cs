namespace Merlin.Backend.Services.BrowserWorkspace.Snapshot;

public sealed record BrowserSnapshotElement
{
    public string Id { get; init; } = "";

    public BrowserSnapshotElementType Type { get; init; }

    public string? Text { get; init; }

    public string? Label { get; init; }

    public string? AriaLabel { get; init; }

    public string? Title { get; init; }

    public string? DataTitleNoTooltip { get; init; }

    public string? DataTooltipTitle { get; init; }

    public string? Placeholder { get; init; }

    public string? Name { get; init; }

    public string? DomId { get; init; }

    public string? CssClass { get; init; }

    public string? Role { get; init; }

    public string? Href { get; init; }

    public string? ValuePreview { get; init; }

    public BrowserSnapshotRect Rect { get; init; } = new();

    public bool IsVisible { get; init; }

    public bool IsEnabled { get; init; }

    public bool IsInViewport { get; init; }

    public int Score { get; init; }
}
