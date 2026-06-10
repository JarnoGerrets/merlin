namespace Merlin.Backend.Services;

public interface ILocalAIClient
{
    Task<string?> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
