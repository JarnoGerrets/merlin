using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class SystemResourceTool : ITool
{
    private const string IntentName = "system_resource_query";
    private const string ToolName = "System Resource";
    private readonly ISystemResourceProvider _systemResourceProvider;

    public SystemResourceTool(ISystemResourceProvider systemResourceProvider)
    {
        _systemResourceProvider = systemResourceProvider;
    }

    public string Name => ToolName;

    public string Description => "Provides safe read-only local system resources such as time, date, and timezone.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "what time is it",
        "what is today's date",
        "what timezone am I in"
    ];

    public bool CanHandle(string command)
    {
        return TryGetResourceKind(command, out _);
    }

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetResourceKind(command, out var resourceKind))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Message = "Unknown system resource.",
                ErrorCode = "UNKNOWN_COMMAND",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            });
        }

        var result = resourceKind switch
        {
            "current_time" => CurrentTimeResult(),
            "current_date" => CurrentDateResult(),
            "timezone" => TimeZoneResult(),
            _ => throw new InvalidOperationException("Unsupported system resource kind.")
        };

        return Task.FromResult(result);
    }

    private ToolResult CurrentTimeResult()
    {
        var currentTime = _systemResourceProvider.GetCurrentLocalTime();
        return Success(
            $"The current local time is {currentTime:HH:mm:ss}.",
            "system_time",
            "System Time");
    }

    private ToolResult CurrentDateResult()
    {
        var currentDate = _systemResourceProvider.GetCurrentLocalDate();
        return Success(
            $"Today's local date is {currentDate:dd-MM-yyyy}.",
            "system_date",
            "System Date");
    }

    private ToolResult TimeZoneResult()
    {
        var timeZone = _systemResourceProvider.GetLocalTimeZone();
        return Success(
            $"Your local timezone is {timeZone.DisplayName} ({timeZone.Id}).",
            "system_timezone",
            "System Timezone");
    }

    private static ToolResult Success(
        string message,
        string capabilityId,
        string capabilityName)
    {
        return new ToolResult
        {
            Success = true,
            Message = message,
            ToolName = ToolName,
            Intent = IntentName,
            CapabilityId = capabilityId,
            CapabilityName = capabilityName,
            ResponseType = "assistant"
        };
    }

    private static bool TryGetResourceKind(string command, out string resourceKind)
    {
        resourceKind = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var normalizedCommand = Normalize(command);
        if (!normalizedCommand.StartsWith("system resource ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resourceKind = normalizedCommand["system resource ".Length..].Trim();
        return resourceKind is "current_time" or "current_date" or "timezone";
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
