namespace Merlin.Backend.Services.BrowserWorkspace;

public sealed record BrowserWorkspaceStateChanged(
    bool Active,
    BrowserWorkspaceBounds? Bounds,
    string Reason);

public sealed record BrowserWorkspaceBounds(
    int X,
    int Y,
    int Width,
    int Height,
    bool IsMinimized,
    bool IsFocused);
