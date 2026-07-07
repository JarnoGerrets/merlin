using Merlin.Backend.Next.Kernel.Requests;

namespace Merlin.Backend.Next.Host;

public interface IMerlinNextShadowBridge
{
    void TryStartShadow(MerlinRequest request);
}
