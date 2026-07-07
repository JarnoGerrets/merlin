namespace Merlin.Backend.Services.Motion;

public sealed record MotionControlProfileResolution(
    IMotionControlProfile Profile,
    double Confidence,
    string Reason);
