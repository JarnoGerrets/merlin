using System.Globalization;
using System.Text.RegularExpressions;
using Merlin.Backend.Models;

namespace Merlin.Backend.Tools;

public sealed partial class DevVisualStateTool : ITool
{
    private const string IntentName = "dev_visual_state";
    private const double DefaultStateSeconds = 10.0;
    private const double DefaultFlowStepSeconds = 3.0;
    private static readonly string[] SupportedStates =
    [
        "idle",
        "listening",
        "thinking",
        "speaking",
        "tool",
        "executing",
        "executing_tool",
        "confirmation",
        "error"
    ];

    public string Name => "Dev Visual State";

    public string Description => "Triggers visual-only frontend states and state flows for development inspection.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "please activate thinking state for 30 seconds",
        "trigger speaking state for 10 seconds",
        "run dev flow thinking to speaking to error",
        "check visual flow listening to thinking to speaking to idle for 4 seconds each"
    ];

    public bool CanHandle(string command)
    {
        return TryParse(command, out _);
    }

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!TryParse(command, out var flow))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Message = "I could not parse that visual development command.",
                ErrorCode = "UNKNOWN_COMMAND",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            });
        }

        var summary = flow.Count == 1
            ? $"{flow[0].State} for {flow[0].DurationSeconds:N0} seconds"
            : string.Join(" -> ", flow.Select(step => $"{step.State} ({step.DurationSeconds:N0}s)"));

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = $"Dev visual flow queued: {summary}.",
            ToolName = Name,
            Intent = IntentName,
            CapabilityId = "dev_visual_state",
            CapabilityName = "Dev Visual State",
            ResponseType = "dev_visual",
            DevVisualFlow = flow
        });
    }

    internal static bool TryParse(string command, out IReadOnlyList<DevVisualFlowStep> flow)
    {
        flow = [];
        var normalized = Normalize(command);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var duration = ExtractDurationSeconds(normalized)
            ?? (LooksLikeFlowCommand(normalized) ? DefaultFlowStepSeconds : DefaultStateSeconds);

        if (TryParseFlow(normalized, duration, out var parsedFlow))
        {
            flow = parsedFlow;
            return true;
        }

        foreach (var state in SupportedStates)
        {
            if (!ContainsWholeWord(normalized, state.Replace('_', ' ')) && !ContainsWholeWord(normalized, state))
            {
                continue;
            }

            if (!LooksLikeDevVisualCommand(normalized))
            {
                continue;
            }

            flow =
            [
                new DevVisualFlowStep
                {
                    State = NormalizeState(state),
                    DurationSeconds = duration
                }
            ];
            return true;
        }

        return false;
    }

    private static bool TryParseFlow(string normalized, double duration, out IReadOnlyList<DevVisualFlowStep> flow)
    {
        flow = [];
        if (!LooksLikeFlowCommand(normalized))
        {
            return false;
        }

        var stateMatches = StateRegex().Matches(normalized);
        var states = stateMatches
            .Select(match => NormalizeState(match.Value))
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .DistinctBy(state => $"{state}:{Guid.NewGuid()}")
            .ToArray();

        if (states.Length < 2)
        {
            return false;
        }

        flow = states
            .Select(state => new DevVisualFlowStep
            {
                State = state,
                DurationSeconds = duration
            })
            .ToArray();
        return true;
    }

    private static bool LooksLikeDevVisualCommand(string normalized)
    {
        return ContainsWholeWord(normalized, "dev")
            || ContainsWholeWord(normalized, "visual")
            || ContainsWholeWord(normalized, "state")
            || ContainsWholeWord(normalized, "activate")
            || ContainsWholeWord(normalized, "trigger")
            || ContainsWholeWord(normalized, "inspect")
            || ContainsWholeWord(normalized, "show");
    }

    private static bool LooksLikeFlowCommand(string normalized)
    {
        return ContainsWholeWord(normalized, "flow")
            || ContainsWholeWord(normalized, "sequence")
            || normalized.Contains(" to ", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(" then ", StringComparison.OrdinalIgnoreCase);
    }

    private static double? ExtractDurationSeconds(string normalized)
    {
        var match = DurationRegex().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value;
        var seconds = unit.StartsWith("minute", StringComparison.OrdinalIgnoreCase)
            ? value * 60.0
            : value;
        return Math.Clamp(seconds, 0.25, 300.0);
    }

    private static string NormalizeState(string state)
    {
        return state.Trim().ToLowerInvariant().Replace(' ', '_') switch
        {
            "executing" or "executing_tool" => "tool",
            var value => value
        };
    }

    private static bool ContainsWholeWord(string value, string term)
    {
        return Regex.IsMatch(
            value,
            $@"(^|\W){Regex.Escape(term)}($|\W)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex(@"\b(?<value>\d+(?:\.\d+)?)\s*(?<unit>seconds?|secs?|s|minutes?|mins?|m)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"\b(idle|listening|thinking|speaking|tool|executing|executing tool|confirmation|error)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StateRegex();
}
