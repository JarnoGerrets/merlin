namespace Merlin.Backend.Services;

public sealed class DummyAIService : IAIService
{
    public Task<string> InterpretAsync(string message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(message.Trim());
    }
}
