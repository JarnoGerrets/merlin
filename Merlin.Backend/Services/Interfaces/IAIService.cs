namespace Merlin.Backend.Services;

public interface IAIService
{
    Task<string> InterpretAsync(string message, CancellationToken cancellationToken = default);
}
