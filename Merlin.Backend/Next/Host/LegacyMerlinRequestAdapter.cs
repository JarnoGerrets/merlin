using Merlin.Backend.Models;
using Merlin.Backend.Next.Kernel.Requests;
using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Next.Host;

public sealed class LegacyMerlinRequestAdapter : ILegacyMerlinRequestAdapter
{
    public MerlinRequest FromAssistantRequest(
        AssistantRequest request,
        string requestId,
        string normalizedText,
        ActiveSurfaceSnapshot activeSurface,
        DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(activeSurface);

        var source = string.IsNullOrWhiteSpace(request.InteractionSource)
            ? "unknown"
            : request.InteractionSource;

        var metadata = new Dictionary<string, string?>
        {
            ["raw_text"] = request.Message,
            ["client_mode"] = request.ClientMode,
            ["capture_id"] = request.CaptureId,
            ["active_surface_kind"] = activeSurface.Kind.ToString(),
            ["active_surface_source"] = activeSurface.Source.ToString(),
            ["active_surface_confidence"] = activeSurface.Confidence.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        };

        return new MerlinRequest(
            RequestId: requestId,
            UserText: normalizedText,
            Source: source,
            SourceSessionId: request.CaptureId,
            RequestedSurfaceId: activeSurface.SurfaceId,
            CreatedAt: receivedAtUtc,
            Metadata: metadata);
    }
}
