using Merlin.Backend.Models;
using Merlin.Backend.Next.Kernel.Requests;
using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Next.Host;

public interface ILegacyMerlinRequestAdapter
{
    MerlinRequest FromAssistantRequest(
        AssistantRequest request,
        string requestId,
        string normalizedText,
        ActiveSurfaceSnapshot activeSurface,
        DateTimeOffset receivedAtUtc);
}
