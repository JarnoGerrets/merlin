using Merlin.Backend.Next.Kernel.Requests;

namespace Merlin.Backend.Next.Kernel.Runtime;

public interface IMerlinNextRuntime
{
    Task<MerlinNextShadowTrace> RunShadowAsync(
        MerlinRequest request,
        CancellationToken cancellationToken = default);
}
