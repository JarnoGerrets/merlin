namespace Merlin.Backend.Services;

public sealed class LocalAIChatResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }
}
