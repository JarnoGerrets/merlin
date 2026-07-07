namespace Merlin.Backend.Services.Vision;

public sealed class VisionMotionRegionCalibrationResult
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public double ControlRegionLeft { get; init; }

    public double ControlRegionTop { get; init; }

    public double ControlRegionRight { get; init; }

    public double ControlRegionBottom { get; init; }

    public int TopLeftSamples { get; init; }

    public int TopRightSamples { get; init; }

    public int BottomRightSamples { get; init; }

    public int BottomLeftSamples { get; init; }

    public string? CalibrationPath { get; init; }
}
