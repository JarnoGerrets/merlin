using Merlin.Backend.Models;

namespace Merlin.Backend.Tools;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    IReadOnlyCollection<string> Examples { get; }

    bool CanHandle(string command);

    Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default);

    Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(context.NormalizedCommand, cancellationToken);
    }
}
