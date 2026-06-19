namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInDebugOptions
{
    public bool DebugOverlayEnabled { get; set; } = false;

    public int DebugOverlaySnapshotHz { get; set; } = 10;
}
