using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

internal static class BargeInCaptureTiming
{
    public static int GetMaxCaptureMs(BargeInOptions options)
    {
        return Math.Max(
            0,
            Math.Max(
                Math.Max(options.TriggerCaptureMs, options.TriggerMaxCaptureMs),
                options.InterruptionCaptureMaxMs));
    }
}
