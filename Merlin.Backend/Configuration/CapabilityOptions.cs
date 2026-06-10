using Merlin.Backend.Models;

namespace Merlin.Backend.Configuration;

public sealed class CapabilityOptions
{
    public List<CapabilityDomain> CapabilityDomains { get; set; } = [];

    public static CapabilityOptions CreateDefault()
    {
        return new CapabilityOptions
        {
            CapabilityDomains =
            [
                new CapabilityDomain
                {
                    Id = "application_launch",
                    Name = "Application Launch",
                    Description = "Open configured, trusted, or confirmed local applications.",
                    IsImplemented = true,
                    ImplementedIntent = "open_application",
                    SafetyLevel = "confirmation_required"
                },
                new CapabilityDomain
                {
                    Id = "url_opening",
                    Name = "URL Opening",
                    Description = "Open safe HTTP or HTTPS URLs.",
                    IsImplemented = true,
                    ImplementedIntent = "open_url",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "tool_discovery",
                    Name = "Tool Discovery",
                    Description = "List implemented tools and their examples.",
                    IsImplemented = true,
                    ImplementedIntent = "tool_discovery",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "diagnostics",
                    Name = "Diagnostics",
                    Description = "Report Merlin backend health, status, and runtime diagnostics.",
                    IsImplemented = true,
                    ImplementedIntent = "diagnostics",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "confirmation",
                    Name = "Confirmation",
                    Description = "Confirm and execute pending safe actions.",
                    IsImplemented = true,
                    ImplementedIntent = "confirmation",
                    SafetyLevel = "confirmation_required"
                },
                new CapabilityDomain
                {
                    Id = "general_conversation",
                    Name = "General Conversation",
                    Description = "Handle safe conversational interaction using the local AI model.",
                    IsImplemented = true,
                    ImplementedIntent = "general_conversation",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "system_time",
                    Name = "System Time",
                    Description = "Read the current local system time.",
                    IsImplemented = true,
                    ImplementedIntent = "system_resource_query",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "system_date",
                    Name = "System Date",
                    Description = "Read the current local system date.",
                    IsImplemented = true,
                    ImplementedIntent = "system_resource_query",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "system_timezone",
                    Name = "System Timezone",
                    Description = "Read the local system timezone.",
                    IsImplemented = true,
                    ImplementedIntent = "system_resource_query",
                    SafetyLevel = "safe"
                },
                new CapabilityDomain
                {
                    Id = "time",
                    Name = "Time",
                    Description = "Tell the current time, date, or timezone-aware time.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you're asking for the time, but I don't currently have a TimeTool.",
                    SafetyLevel = "missing"
                },
                new CapabilityDomain
                {
                    Id = "news",
                    Name = "News",
                    Description = "Retrieve current news, headlines, or news feeds.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you're asking for news, but I don't currently have a NewsTool or WebSearchTool.",
                    SafetyLevel = "missing"
                },
                new CapabilityDomain
                {
                    Id = "web_search",
                    Name = "Web Search",
                    Description = "Search the web or retrieve live/current information from the internet.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you want web search, but I don't currently have a WebSearchTool.",
                    SafetyLevel = "missing"
                },
                new CapabilityDomain
                {
                    Id = "email",
                    Name = "Email",
                    Description = "Read, search, or send email.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you want email access, but I don't currently have an EmailTool.",
                    SafetyLevel = "missing"
                },
                new CapabilityDomain
                {
                    Id = "calendar",
                    Name = "Calendar",
                    Description = "Read or manage calendar events.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you want calendar access, but I don't currently have a CalendarTool.",
                    SafetyLevel = "missing"
                },
                new CapabilityDomain
                {
                    Id = "file_access",
                    Name = "File Access",
                    Description = "Inspect folders, files, drives, desktop, downloads, or documents.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you're asking me to inspect files or folders, but I don't currently have a file access tool.",
                    SafetyLevel = "missing"
                },
                new CapabilityDomain
                {
                    Id = "system_settings",
                    Name = "System Settings",
                    Description = "Change operating system settings or security configuration.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you're asking for system settings control, but I don't currently have a safe SystemSettingsTool.",
                    SafetyLevel = "unsupported"
                },
                new CapabilityDomain
                {
                    Id = "software_installation",
                    Name = "Software Installation",
                    Description = "Install, download, update, or remove software.",
                    IsImplemented = false,
                    MissingMessage = "I understand that you're asking to install or update software, but Merlin does not support software installation.",
                    SafetyLevel = "unsupported"
                },
                new CapabilityDomain
                {
                    Id = "destructive_file_action",
                    Name = "Destructive File Action",
                    Description = "Delete, wipe, format, or destructively modify files or drives.",
                    IsImplemented = false,
                    MissingMessage = "I understand the request, but Merlin does not support destructive file actions.",
                    SafetyLevel = "unsupported"
                }
            ]
        };
    }
}
