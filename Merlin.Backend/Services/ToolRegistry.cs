using Merlin.Backend.Tools;

namespace Merlin.Backend.Services;

public sealed class ToolRegistry
{
    private readonly IReadOnlyCollection<ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToArray();
    }

    public ITool? FindTool(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        return _tools.FirstOrDefault(tool => tool.CanHandle(command));
    }

    public IReadOnlyCollection<ITool> GetTools()
    {
        return _tools;
    }
}
