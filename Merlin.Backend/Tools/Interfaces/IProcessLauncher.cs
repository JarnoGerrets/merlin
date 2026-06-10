namespace Merlin.Backend.Tools;

public interface IProcessLauncher
{
    Task LaunchAsync(string target, CancellationToken cancellationToken = default);
}
