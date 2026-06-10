using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Tests;

internal static class TestApplicationLaunchOptions
{
    public static IOptions<ApplicationLaunchOptions> Create()
    {
        return Options.Create(new ApplicationLaunchOptions
        {
            Applications = new Dictionary<string, ApplicationLaunchTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["notepad"] = new ApplicationLaunchTarget
                {
                    DisplayName = "Notepad",
                    ExecutableOrUrl = "notepad.exe",
                    Aliases = ["notepad"]
                },
                ["calculator"] = new ApplicationLaunchTarget
                {
                    DisplayName = "Calculator",
                    ExecutableOrUrl = "calc.exe",
                    Aliases = ["calculator", "calc"]
                },
                ["browser"] = new ApplicationLaunchTarget
                {
                    DisplayName = "Browser",
                    ExecutableOrUrl = "https://www.google.com",
                    Aliases = ["browser", "web browser"]
                },
                ["vscode"] = new ApplicationLaunchTarget
                {
                    DisplayName = "VS Code",
                    ExecutableOrUrl = "code",
                    Aliases = ["vscode", "vs code", "visual studio code"]
                }
            }
        });
    }
}
