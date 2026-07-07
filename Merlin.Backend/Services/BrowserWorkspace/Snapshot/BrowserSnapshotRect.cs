namespace Merlin.Backend.Services.BrowserWorkspace.Snapshot;

public sealed record BrowserSnapshotRect
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }
}
