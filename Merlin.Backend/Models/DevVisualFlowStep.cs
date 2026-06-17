namespace Merlin.Backend.Models;

public sealed class DevVisualFlowStep
{
    public string State { get; init; } = "idle";

    public double DurationSeconds { get; init; } = 3.0;
}
