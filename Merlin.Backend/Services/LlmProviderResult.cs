namespace Merlin.Backend.Services;

public sealed class LlmProviderResult
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public bool Retryable { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static LlmProviderResult Succeeded(string message)
    {
        return new LlmProviderResult
        {
            Success = true,
            Message = message
        };
    }

    public static LlmProviderResult Failed(string? errorCode, string? errorMessage, bool retryable)
    {
        return new LlmProviderResult
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Retryable = retryable
        };
    }
}
