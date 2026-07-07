using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion;

public sealed class MotionControlProfileRegistry : IMotionControlProfileRegistry
{
    private readonly IReadOnlyList<IMotionControlProfile> _profiles;
    private readonly ILogger<MotionControlProfileRegistry> _logger;

    public MotionControlProfileRegistry(
        IEnumerable<IMotionControlProfile> profiles,
        ILogger<MotionControlProfileRegistry> logger)
    {
        _profiles = profiles
            .OrderByDescending(profile => profile.Descriptor.Priority)
            .ToArray();
        _logger = logger;
    }

    public MotionControlProfileResolution Resolve(
        ActiveSurfaceSnapshot activeSurface,
        MotionControlProfileOverride? profileOverride = null)
    {
        if (profileOverride is not null && !string.IsNullOrWhiteSpace(profileOverride.ProfileId))
        {
            var overridden = _profiles.FirstOrDefault(profile =>
                string.Equals(profile.Descriptor.ProfileId, profileOverride.ProfileId, StringComparison.OrdinalIgnoreCase));
            if (overridden is not null)
            {
                LogSelected(overridden, activeSurface, $"override:{profileOverride.Reason}");
                return new MotionControlProfileResolution(overridden, 1.0, $"override:{profileOverride.Reason}");
            }
        }

        var profile = _profiles.FirstOrDefault(item => item.CanHandle(activeSurface))
            ?? _profiles.First(item => string.Equals(item.Descriptor.ProfileId, MotionControlProfileId.Neutral, StringComparison.OrdinalIgnoreCase));
        var reason = activeSurface.Kind switch
        {
            ActiveSurfaceKind.Dashboard => "surface_dashboard",
            ActiveSurfaceKind.BrowserWorkspace => "surface_browser_workspace",
            _ => "surface_unknown"
        };
        LogSelected(profile, activeSurface, reason);
        return new MotionControlProfileResolution(profile, activeSurface.Confidence, reason);
    }

    public IReadOnlyList<MotionControlProfileDescriptor> ListProfiles() =>
        _profiles.Select(profile => profile.Descriptor).ToArray();

    private void LogSelected(
        IMotionControlProfile profile,
        ActiveSurfaceSnapshot surface,
        string reason)
    {
        _logger.LogInformation(
            "MotionProfileSelected ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. ActiveSurfaceSource: {ActiveSurfaceSource}. ActiveSurfaceConfidence: {ActiveSurfaceConfidence}. Reason: {Reason}.",
            profile.Descriptor.ProfileId,
            surface.Kind,
            surface.SurfaceId,
            surface.Source,
            surface.Confidence,
            reason);
    }
}
