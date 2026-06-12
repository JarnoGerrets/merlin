namespace Merlin.Backend.Services;

public interface IVoiceWarmupService
{
    Task WarmupAsync(CancellationToken cancellationToken, bool force = false);
}
