using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion;

public sealed record MotionControlModeSnapshot(
    MotionControlModeState State,
    bool IsEnabled,
    string? ActiveProfileId,
    string? ActiveProfileDisplayName,
    ActiveSurfaceSnapshot ActiveSurface,
    DateTimeOffset? EnabledUtc,
    DateTimeOffset UpdatedUtc,
    string Reason)
{
    public static MotionControlModeSnapshot Disabled(ActiveSurfaceSnapshot activeSurface, string reason) =>
        new(
            MotionControlModeState.Disabled,
            IsEnabled: false,
            ActiveProfileId: null,
            ActiveProfileDisplayName: null,
            ActiveSurface: activeSurface,
            EnabledUtc: null,
            UpdatedUtc: DateTimeOffset.UtcNow,
            Reason: reason);
}
