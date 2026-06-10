namespace Merlin.Backend.Configuration;

public sealed class ApplicationLaunchTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public string ExecutableOrUrl { get; set; } = string.Empty;

    public List<string> Aliases { get; set; } = [];
}
