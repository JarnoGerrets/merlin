namespace Merlin.Backend.Services.Vision;

public sealed class VisionPinchCalibrationResult
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public double PinchStartRatio { get; init; }

    public double PinchHoldRatio { get; init; }

    public double PinchReleaseRatio { get; init; }

    public int OpenSamples { get; init; }

    public int PinchSamples { get; init; }

    public int ReleaseSamples { get; init; }

    public string? CalibrationPath { get; init; }
}
