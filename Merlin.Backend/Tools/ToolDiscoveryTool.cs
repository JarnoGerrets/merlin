using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Merlin.Backend.Tools;

public sealed class ToolDiscoveryTool : ITool
{
    private const string IntentName = "tool_discovery";
    private readonly IServiceProvider _serviceProvider;

    public ToolDiscoveryTool(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string Name => "Tool Discovery";

    public string Description => "Lists the tools Merlin can currently use.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "list tools",
        "show tools",
        "what can you do"
    ];

    public bool CanHandle(string command)
    {
        var normalizedCommand = command.Trim();

        return string.Equals(normalizedCommand, "list tools", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "show tools", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "what can you do", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var toolRegistry = _serviceProvider.GetRequiredService<ToolRegistry>();
        var availableTools = toolRegistry.GetTools()
            .Select(tool => new ToolMetadata
            {
                Name = tool.Name,
                Description = tool.Description,
                Examples = tool.Examples
            })
            .ToArray();

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = "Available tools listed.",
            ToolName = Name,
            Intent = IntentName,
            AvailableTools = availableTools
        });
    }
}
