using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion;

public interface IMotionControlProfileRegistry
{
    MotionControlProfileResolution Resolve(
        ActiveSurfaceSnapshot activeSurface,
        MotionControlProfileOverride? profileOverride = null);

    IReadOnlyList<MotionControlProfileDescriptor> ListProfiles();
}
