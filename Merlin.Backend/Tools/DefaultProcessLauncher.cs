using System.Diagnostics;

namespace Merlin.Backend.Tools;

public sealed class DefaultProcessLauncher : IProcessLauncher
{
    public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Process.Start returned null.");
        }

        return Task.CompletedTask;
    }
}
