namespace Merlin.Backend.Services;

public interface ILocalAIChatService
{
    Task<LocalAIChatResult> GenerateResponseAsync(
        string message,
        CancellationToken cancellationToken = default);
}
