using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion.Profiles;

public sealed class NeutralMotionProfile : IMotionControlProfile
{
    private readonly ILogger<NeutralMotionProfile> _logger;

    public NeutralMotionProfile(ILogger<NeutralMotionProfile> logger)
    {
        _logger = logger;
    }

    public MotionControlProfileDescriptor Descriptor { get; } = new(
        MotionControlProfileId.Neutral,
        "Neutral",
        ActiveSurfaceKind.Unknown,
        Priority: -100,
        Capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MotionControlProfileCapabilities.SafeNoop
        });

    public bool CanHandle(ActiveSurfaceSnapshot surface) =>
        surface.Kind is ActiveSurfaceKind.Unknown;

    public Task ActivateAsync(
        MotionControlProfileActivationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "MotionProfileActivated ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
            Descriptor.ProfileId,
            context.ActiveSurface.Kind,
            context.ActiveSurface.SurfaceId,
            context.Reason);
        return Task.CompletedTask;
    }

    public Task DeactivateAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "MotionProfileDeactivated ProfileId: {ProfileId}. Reason: {Reason}.",
            Descriptor.ProfileId,
            reason);
        return Task.CompletedTask;
    }

    public Task HandleGestureAsync(
        MotionControlGestureContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "MotionGestureRejected ProfileId: {ProfileId}. GestureType: {GestureType}. PointerId: {PointerId}. Reason: neutral_profile.",
            Descriptor.ProfileId,
            context.GestureEvent.Type,
            context.GestureEvent.PointerId);
        return Task.CompletedTask;
    }

    public Task OnActiveSurfaceChangedAsync(
        ActiveSurfaceSnapshot surface,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
