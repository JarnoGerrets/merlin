namespace Merlin.Backend.Configuration;

public sealed class CapabilityOptions
{
    public List<CapabilityRule> MissingCapabilities { get; set; } = [];

    public List<CapabilityRule> UnsupportedActions { get; set; } = [];

    public static CapabilityOptions CreateDefault()
    {
        return new CapabilityOptions
        {
            MissingCapabilities =
            [
                new CapabilityRule
                {
                    Name = "Web Search",
                    Keywords =
                    [
                        "web search",
                        "search web",
                        "search the web",
                        "search the internet",
                        "internet",
                        "google something"
                    ],
                    Message = "I understand that you want web search, but I do not currently have a WebSearchTool."
                },
                new CapabilityRule
                {
                    Name = "News Feed",
                    Keywords = ["news", "newsfeed", "headlines"],
                    Message = "I understand that you're asking me to show a news feed, but I don't currently have a NewsTool or WebSearchTool."
                },
                new CapabilityRule
                {
                    Name = "File Inspection",
                    Keywords = ["folders", "folder", "files", "file", "hard drive", "desktop", "downloads"],
                    Message = "I understand that you want file inspection, but I do not currently have a file/folder inspection tool."
                },
                new CapabilityRule
                {
                    Name = "Email",
                    Keywords = ["email", "emails", "mail"],
                    Message = "I understand that you want me to check email, but I don't currently have an EmailTool."
                },
                new CapabilityRule
                {
                    Name = "Calendar",
                    Keywords = ["calendar", "schedule"],
                    Message = "I understand that you want calendar access, but I don't currently have a CalendarTool."
                },
                new CapabilityRule
                {
                    Name = "System Scanner",
                    Keywords = ["scanner", "scan"],
                    Message = "I understand that you want system scanning, but I don't currently have a safe diagnostics scanner tool."
                }
            ],
            UnsupportedActions =
            [
                new CapabilityRule
                {
                    Name = "Destructive File Actions",
                    Keywords = ["delete all files", "delete all my files", "delete files", "delete my files", "wipe drive", "wipe my hard drive", "format disk", "format drive"],
                    Message = "I cannot perform destructive system actions."
                },
                new CapabilityRule
                {
                    Name = "Security Bypass",
                    Keywords = ["disable windows security", "disable windows defender", "disable defender", "bypass confirmation", "bypass confirmations"],
                    Message = "I cannot disable security protections or bypass confirmation safeguards."
                }
            ]
        };
    }
}

public sealed class CapabilityRule
{
    public string Name { get; set; } = string.Empty;

    public List<string> Keywords { get; set; } = [];

    public string Message { get; set; } = string.Empty;
}
