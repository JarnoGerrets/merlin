namespace Merlin.Backend.Configuration;

public sealed class ApplicationLaunchOptions
{
    public Dictionary<string, ApplicationLaunchTarget> Applications { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
