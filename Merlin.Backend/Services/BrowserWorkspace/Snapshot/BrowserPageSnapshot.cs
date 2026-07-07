namespace Merlin.Backend.Services.BrowserWorkspace.Snapshot;

public sealed record BrowserPageSnapshot
{
    public string? SnapshotId { get; init; }

    public string? Url { get; init; }

    public string? Title { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; }

    public long? PageVersion { get; init; }

    public bool IsStale { get; init; }

    public bool IsLoading { get; init; }

    public IReadOnlyList<BrowserSnapshotElement> Inputs { get; init; } = [];

    public IReadOnlyList<BrowserSnapshotElement> SearchFields { get; init; } = [];

    public IReadOnlyList<BrowserSnapshotElement> Buttons { get; init; } = [];

    public IReadOnlyList<BrowserSnapshotElement> Links { get; init; } = [];

    public IReadOnlyList<BrowserSnapshotElement> Headings { get; init; } = [];

    public IReadOnlyList<BrowserSnapshotElement> Results { get; init; } = [];

    public IReadOnlyList<BrowserSnapshotElement> TextBlocks { get; init; } = [];

    public int TotalElementCount { get; init; }

    public bool IsTruncated { get; init; }

    public string? Error { get; init; }
}
