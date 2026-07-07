namespace Merlin.Backend.Configuration;

public sealed class BrowserWorkspaceOptions
{
    public bool Enabled { get; init; } = true;

    public string? HostExecutablePath { get; init; }

    public string StartUrl { get; init; } = "about:blank";

    public bool OpenUrlsInsideWorkspaceWhenActive { get; init; } = true;

    public string SearchEngineUrlTemplate { get; init; } = "https://www.google.com/search?q={query}";

    public int SnapshotTimeoutMs { get; init; } = 5000;

    public int SnapshotFreshnessMs { get; init; } = 3000;

    public int PageActionSettleDelayMs { get; init; } = 500;

    public int PageActionSettleTimeoutMs { get; init; } = 3000;

    public bool EnablePageActionRetry { get; init; } = true;

    public bool EnableCommonSafeActions { get; init; } = true;

    public bool EnableYoutubeSkipAdCommand { get; init; } = true;

    public BrowserWorkspaceSnapshotOptions Snapshot { get; init; } = new();
}

public sealed class BrowserWorkspaceSnapshotOptions
{
    public int MaxInputs { get; init; } = 50;

    public int MaxSearchFields { get; init; } = 10;

    public int MaxButtons { get; init; } = 75;

    public int MaxLinks { get; init; } = 100;

    public int MaxHeadings { get; init; } = 50;

    public int MaxResults { get; init; } = 30;

    public int MaxTextBlocks { get; init; } = 20;

    public int MaxElementTextLength { get; init; } = 300;
}
