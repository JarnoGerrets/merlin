using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class WakeMerlinTool : ITool
{
    private const string CommandName = "wake_merlin";

    public string Name => "Wake Merlin";

    public string Description => "Answers lightweight wake and availability checks without calling an LLM.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "are you awake",
        "hey Merlin are you awake",
        "are you there"
    ];

    public bool CanHandle(string command)
    {
        return string.Equals(command.Trim(), CommandName, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildResult());
    }

    public Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildResult());
    }

    private static ToolResult BuildResult()
    {
        var response = ToolSpeechTemplates.WakeResponses[Random.Shared.Next(ToolSpeechTemplates.WakeResponses.Count)];
        return new ToolResult
        {
            Success = true,
            Message = response,
            SpokenText = response,
            SpeechCacheKey = "wake.merlin.random",
            PreferPhraseCache = true,
            IsReplayableSpeech = true,
            ToolName = "Wake Merlin",
            Intent = "wake_merlin",
            CapabilityId = "wake_merlin",
            CapabilityName = "Wake Merlin",
            ResponseType = "assistant"
        };
    }
}
