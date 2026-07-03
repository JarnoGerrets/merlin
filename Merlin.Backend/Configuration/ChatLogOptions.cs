namespace Merlin.Backend.Configuration;

public sealed class ChatLogOptions
{
    public bool Enabled { get; init; } = true;

    public int DefaultWidth { get; init; } = 420;

    public int DefaultHeight { get; init; } = 720;

    public int MinWidth { get; init; } = 280;

    public int MinHeight { get; init; } = 220;

    public int MaxWidth { get; init; } = 900;

    public int MaxHeight { get; init; } = 1100;

    public string InitialAnchor { get; init; } = "left";

    public bool RememberPosition { get; init; } = true;

    public bool RememberSize { get; init; } = true;

    public int MaxMessages { get; init; } = 500;
}
