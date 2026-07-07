using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Vision;
using Microsoft.Extensions.DependencyInjection;

namespace Merlin.Backend.Services.Motion;

public sealed class MotionControlModeService : IMotionControlModeService
{
    private readonly IActiveSurfaceService _activeSurfaceService;
    private readonly IMotionControlProfileRegistry _profileRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MotionControlModeService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MotionControlModeSnapshot _current;
    private IMotionControlProfile? _activeProfile;
    private bool _trackingStartedByMotionMode;

    public MotionControlModeService(
        IActiveSurfaceService activeSurfaceService,
        IMotionControlProfileRegistry profileRegistry,
        IServiceProvider serviceProvider,
        ILogger<MotionControlModeService> logger)
    {
        _activeSurfaceService = activeSurfaceService;
        _profileRegistry = profileRegistry;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _current = MotionControlModeSnapshot.Disabled(activeSurfaceService.Current, "startup");
        _activeSurfaceService.ActiveSurfaceChanged += OnActiveSurfaceChangedFromServiceAsync;
    }

    public event Func<VisionGestureEvent, CancellationToken, Task>? DashboardGestureForwarded;

    public MotionControlModeSnapshot Current => _current;

    public bool IsEnabled => _current.IsEnabled;

    public async Task<MotionControlModeSnapshot> EnableAsync(
        string reason,
        MotionControlProfileOverride? profileOverride = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var surface = await _activeSurfaceService.GetCurrentAsync(cancellationToken);
            _logger.LogInformation(
                "MotionControlEnableRequested Reason: {Reason}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. ActiveSurfaceSource: {ActiveSurfaceSource}. ActiveSurfaceConfidence: {ActiveSurfaceConfidence}.",
                reason,
                surface.Kind,
                surface.SurfaceId,
                surface.Source,
                surface.Confidence);

            if (_current.IsEnabled)
            {
                await SwitchProfileLockedAsync(surface, reason, profileOverride, cancellationToken);
                return _current;
            }

            _current = _current with
            {
                State = MotionControlModeState.Enabling,
                UpdatedUtc = DateTimeOffset.UtcNow,
                Reason = reason
            };

            var resolution = _profileRegistry.Resolve(surface, profileOverride);
            var profile = await ActivateResolvedProfileLockedAsync(resolution, surface, reason, cancellationToken);
            if (ShouldStartTracking(profile))
            {
                await StartTrackingIfNeededLockedAsync(cancellationToken);
            }

            var now = DateTimeOffset.UtcNow;
            _activeProfile = profile;
            _current = new MotionControlModeSnapshot(
                MotionControlModeState.Enabled,
                IsEnabled: true,
                profile.Descriptor.ProfileId,
                profile.Descriptor.DisplayName,
                surface,
                EnabledUtc: now,
                UpdatedUtc: now,
                Reason: reason);

            _logger.LogInformation(
                "MotionControlEnabled ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
                profile.Descriptor.ProfileId,
                surface.Kind,
                surface.SurfaceId,
                reason);
            return _current;
        }
        catch (Exception exception)
        {
            _current = _current with
            {
                State = MotionControlModeState.Faulted,
                UpdatedUtc = DateTimeOffset.UtcNow,
                Reason = reason
            };
            _logger.LogWarning(exception, "MotionControlEnableFailed Reason: {Reason}.", reason);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MotionControlModeSnapshot> DisableAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation(
                "MotionControlDisableRequested ProfileId: {ProfileId}. Reason: {Reason}.",
                _activeProfile?.Descriptor.ProfileId,
                reason);

            if (!_current.IsEnabled && _activeProfile is null)
            {
                _current = MotionControlModeSnapshot.Disabled(_activeSurfaceService.Current, reason);
                return _current;
            }

            _current = _current with
            {
                State = MotionControlModeState.Disabling,
                UpdatedUtc = DateTimeOffset.UtcNow,
                Reason = reason
            };

            if (_activeProfile is not null)
            {
                await DeactivateProfileLockedAsync(_activeProfile, reason, cancellationToken);
                _activeProfile = null;
            }

            if (_trackingStartedByMotionMode)
            {
                await StopTrackingLockedAsync(cancellationToken);
            }

            _current = MotionControlModeSnapshot.Disabled(_activeSurfaceService.Current, reason);
            _logger.LogInformation("MotionControlDisabled Reason: {Reason}.", reason);
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task HandleGestureAsync(
        VisionGestureEvent gestureEvent,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_current.IsEnabled || _activeProfile is null)
            {
                _logger.LogDebug(
                    "MotionGestureRejected GestureType: {GestureType}. PointerId: {PointerId}. Reason: motion_disabled.",
                    gestureEvent.Type,
                    gestureEvent.PointerId);
                return;
            }

            var context = new MotionControlGestureContext(
                gestureEvent,
                _current.ActiveSurface,
                _current,
                DateTimeOffset.UtcNow);
            await _activeProfile.HandleGestureAsync(context, cancellationToken);
            _logger.LogDebug(
                "MotionGestureDispatched ProfileId: {ProfileId}. GestureType: {GestureType}. PointerId: {PointerId}.",
                _activeProfile.Descriptor.ProfileId,
                gestureEvent.Type,
                gestureEvent.PointerId);

            if (string.Equals(_activeProfile.Descriptor.ProfileId, MotionControlProfileId.Dashboard, StringComparison.OrdinalIgnoreCase))
            {
                var handlers = DashboardGestureForwarded;
                if (handlers is not null)
                {
                    await handlers.Invoke(gestureEvent, cancellationToken);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task OnActiveSurfaceChangedAsync(
        ActiveSurfaceSnapshot activeSurface,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_current.IsEnabled)
            {
                return;
            }

            await SwitchProfileLockedAsync(activeSurface, reason, profileOverride: null, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task OnActiveSurfaceChangedFromServiceAsync(
        ActiveSurfaceSnapshot surface,
        string reason,
        CancellationToken cancellationToken) =>
        OnActiveSurfaceChangedAsync(surface, reason, cancellationToken);

    private async Task SwitchProfileLockedAsync(
        ActiveSurfaceSnapshot surface,
        string reason,
        MotionControlProfileOverride? profileOverride,
        CancellationToken cancellationToken)
    {
        var resolution = _profileRegistry.Resolve(surface, profileOverride);
        var nextProfile = resolution.Profile;
        if (_activeProfile is not null
            && string.Equals(_activeProfile.Descriptor.ProfileId, nextProfile.Descriptor.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            await _activeProfile.OnActiveSurfaceChangedAsync(surface, cancellationToken);
            _current = _current with
            {
                ActiveSurface = surface,
                UpdatedUtc = DateTimeOffset.UtcNow,
                Reason = reason
            };
            _logger.LogDebug(
                "MotionProfileChangeSkipped ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
                _activeProfile.Descriptor.ProfileId,
                surface.Kind,
                surface.SurfaceId,
                reason);
            return;
        }

        var oldProfile = _activeProfile;
        _current = _current with
        {
            State = MotionControlModeState.SwitchingProfile,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Reason = reason
        };

        if (oldProfile is not null)
        {
            await DeactivateProfileLockedAsync(oldProfile, "active_surface_changed", cancellationToken);
        }

        var activated = await ActivateResolvedProfileLockedAsync(resolution, surface, reason, cancellationToken);
        if (ShouldStartTracking(activated))
        {
            await StartTrackingIfNeededLockedAsync(cancellationToken);
        }
        else if (_trackingStartedByMotionMode)
        {
            await StopTrackingLockedAsync(cancellationToken);
        }

        _activeProfile = activated;
        _current = _current with
        {
            State = MotionControlModeState.Enabled,
            IsEnabled = true,
            ActiveProfileId = activated.Descriptor.ProfileId,
            ActiveProfileDisplayName = activated.Descriptor.DisplayName,
            ActiveSurface = surface,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Reason = reason
        };

        _logger.LogInformation(
            "MotionProfileChanged OldProfileId: {OldProfileId}. NewProfileId: {NewProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
            oldProfile?.Descriptor.ProfileId,
            activated.Descriptor.ProfileId,
            surface.Kind,
            surface.SurfaceId,
            reason);
    }

    private async Task<IMotionControlProfile> ActivateResolvedProfileLockedAsync(
        MotionControlProfileResolution resolution,
        ActiveSurfaceSnapshot surface,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await resolution.Profile.ActivateAsync(
                new MotionControlProfileActivationContext(surface, reason, DateTimeOffset.UtcNow),
                cancellationToken);
            return resolution.Profile;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "MotionProfileActivationFailed ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
                resolution.Profile.Descriptor.ProfileId,
                surface.Kind,
                surface.SurfaceId,
                reason);
            var fallback = _profileRegistry.Resolve(
                KnownSurfaces.Unknown(DateTimeOffset.UtcNow),
                new MotionControlProfileOverride(MotionControlProfileId.Neutral, "activation_failed")).Profile;
            await fallback.ActivateAsync(
                new MotionControlProfileActivationContext(surface, "activation_failed", DateTimeOffset.UtcNow),
                cancellationToken);
            return fallback;
        }
    }

    private async Task DeactivateProfileLockedAsync(
        IMotionControlProfile profile,
        string reason,
        CancellationToken cancellationToken)
    {
        await profile.DeactivateAsync(reason, cancellationToken);
    }

    private async Task StartTrackingIfNeededLockedAsync(CancellationToken cancellationToken)
    {
        if (_trackingStartedByMotionMode)
        {
            return;
        }

        var sidecar = _serviceProvider.GetService<IVisionSidecarHost>();
        if (sidecar is null)
        {
            _logger.LogWarning("MotionControlTrackingStartSkipped Reason: sidecar_unavailable.");
            return;
        }

        await sidecar.StartTrackingAsync(cancellationToken);
        _trackingStartedByMotionMode = true;
    }

    private async Task StopTrackingLockedAsync(CancellationToken cancellationToken)
    {
        var sidecar = _serviceProvider.GetService<IVisionSidecarHost>();
        if (sidecar is not null)
        {
            await sidecar.StopTrackingAsync(cancellationToken);
        }

        _trackingStartedByMotionMode = false;
    }

    private static bool ShouldStartTracking(IMotionControlProfile profile) =>
        !string.Equals(profile.Descriptor.ProfileId, MotionControlProfileId.Neutral, StringComparison.OrdinalIgnoreCase);
}
