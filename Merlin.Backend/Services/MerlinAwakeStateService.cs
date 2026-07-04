using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class MerlinAwakeStateService
{
    public static readonly TimeSpan DefaultAwakeTimeout = TimeSpan.FromMinutes(10);

    private static readonly string[][] WakePhraseTokenSequences =
    [
        ["are", "you", "awake"],
        ["you", "awake"],
        ["are", "you", "there"],
        ["you", "there"],
        ["are", "you", "listening"],
        ["you", "listening"],
        ["wake", "up"],
        ["wake", "up", "merlin"],
        ["merlin", "wake", "up"]
    ];

    private static readonly string[][] SleepPhraseTokenSequences =
    [
        ["go", "to", "sleep"],
        ["go", "sleep"],
        ["sleep"],
        ["sleep", "now"],
        ["go", "back", "to", "sleep"],
        ["back", "to", "sleep"],
        ["you", "can", "sleep"],
        ["you", "can", "sleep", "now"],
        ["you", "can", "go", "to", "sleep"],
        ["stand", "down"],
        ["standby"],
        ["stand", "by"]
    ];

    private static readonly HashSet<string> LeadingFillers = new(StringComparer.Ordinal)
    {
        "hey",
        "hi",
        "hello",
        "okay",
        "ok",
        "yo"
    };

    private readonly AssistantUiStateBroadcaster? _assistantUiStateBroadcaster;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _awakeTimeout;
    private readonly object _syncRoot = new();
    private DateTimeOffset? _lastActivityUtc;
    private bool _isAwake;

    public MerlinAwakeStateService(
        AssistantUiStateBroadcaster? assistantUiStateBroadcaster = null,
        TimeProvider? timeProvider = null,
        TimeSpan? awakeTimeout = null)
    {
        _assistantUiStateBroadcaster = assistantUiStateBroadcaster;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _awakeTimeout = awakeTimeout ?? DefaultAwakeTimeout;
    }

    public bool IsAwake
    {
        get
        {
            lock (_syncRoot)
            {
                ExpireIfIdleLocked(GetUtcNow());
                return _isAwake;
            }
        }
    }

    public MerlinAwakeGateResult EvaluateVoiceActivity(
        string? transcript,
        string? correlationId = null,
        string? turnId = null)
    {
        var now = GetUtcNow();
        var isWakePhrase = IsWakePhrase(transcript);
        var isSleepPhrase = IsSleepPhrase(transcript);
        bool shouldBroadcastAwake;
        bool shouldBroadcastSleeping;
        var sleepingBroadcastReason = "awake_timeout_expired";
        bool sleepAccepted;

        lock (_syncRoot)
        {
            shouldBroadcastSleeping = ExpireIfIdleLocked(now);
            if (isWakePhrase)
            {
                shouldBroadcastAwake = !_isAwake;
                _isAwake = true;
                _lastActivityUtc = now;
                sleepAccepted = false;
            }
            else if (isSleepPhrase)
            {
                shouldBroadcastAwake = false;
                shouldBroadcastSleeping = _isAwake;
                sleepingBroadcastReason = "sleep_phrase_accepted";
                sleepAccepted = true;
                _isAwake = false;
                _lastActivityUtc = null;
            }
            else
            {
                shouldBroadcastAwake = false;
                sleepAccepted = false;
            }
        }

        if (shouldBroadcastSleeping)
        {
            _ = BroadcastSleepingAsync(correlationId, turnId, sleepingBroadcastReason);
        }

        if (shouldBroadcastAwake)
        {
            _ = BroadcastAwakeAsync(correlationId, turnId);
        }

        if (isWakePhrase)
        {
            return new MerlinAwakeGateResult(
                MerlinAwakeGateDecision.WakePhraseAccepted,
                true,
                true,
                false,
                "wake_phrase_accepted");
        }

        if (isSleepPhrase)
        {
            return new MerlinAwakeGateResult(
                MerlinAwakeGateDecision.SleepPhraseAccepted,
                sleepAccepted,
                false,
                true,
                "sleep_phrase_accepted");
        }

        return IsAwake
            ? new MerlinAwakeGateResult(
                MerlinAwakeGateDecision.AwakeActivityAccepted,
                true,
                false,
                false,
                "merlin_awake")
            : new MerlinAwakeGateResult(
                MerlinAwakeGateDecision.IgnoredWhileSleeping,
                false,
                false,
                false,
                "merlin_sleeping");
    }

    public void TouchActivity()
    {
        lock (_syncRoot)
        {
            if (_isAwake)
            {
                _lastActivityUtc = GetUtcNow();
            }
        }
    }

    public bool SleepIfExpired(string? correlationId = null, string? turnId = null)
    {
        bool expired;
        lock (_syncRoot)
        {
            expired = ExpireIfIdleLocked(GetUtcNow());
        }

        if (expired)
        {
            _ = BroadcastSleepingAsync(correlationId, turnId, "awake_timeout_expired");
        }

        return expired;
    }

    public static bool IsWakePhrase(string? transcript)
    {
        var tokens = NormalizeInvocationTokens(transcript);
        return WakePhraseTokenSequences.Any(sequence => TokensEqual(tokens, sequence));
    }

    public static bool IsSleepPhrase(string? transcript)
    {
        var tokens = NormalizeInvocationTokens(transcript);
        return SleepPhraseTokenSequences.Any(sequence => TokensEqual(tokens, sequence));
    }

    private static List<string> NormalizeInvocationTokens(string? transcript)
    {
        var tokens = Tokenize(transcript);
        while (tokens.Count > 0 && LeadingFillers.Contains(tokens[0]))
        {
            tokens.RemoveAt(0);
        }

        if (tokens.Count > 0 && string.Equals(tokens[0], "merlin", StringComparison.Ordinal))
        {
            tokens.RemoveAt(0);
        }

        while (tokens.Count > 0 && LeadingFillers.Contains(tokens[0]))
        {
            tokens.RemoveAt(0);
        }

        if (tokens.Count > 0 && string.Equals(tokens[^1], "merlin", StringComparison.Ordinal))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        return tokens;
    }

    private DateTimeOffset GetUtcNow()
    {
        return _timeProvider.GetUtcNow();
    }

    private bool ExpireIfIdleLocked(DateTimeOffset now)
    {
        if (!_isAwake || _lastActivityUtc is null)
        {
            return false;
        }

        if (now - _lastActivityUtc.Value < _awakeTimeout)
        {
            return false;
        }

        _isAwake = false;
        _lastActivityUtc = null;
        return true;
    }

    private Task BroadcastAwakeAsync(string? correlationId, string? turnId)
    {
        return _assistantUiStateBroadcaster?.EmitImmediateAsync(
            AssistantUiStateEvent.Create(
                "idle",
                "wake_phrase_accepted",
                correlationId,
                turnId,
                overlayState: "none"),
            nameof(MerlinAwakeStateService)) ?? Task.CompletedTask;
    }

    private Task BroadcastSleepingAsync(string? correlationId, string? turnId, string reason)
    {
        return _assistantUiStateBroadcaster?.EmitImmediateAsync(
            AssistantUiStateEvent.Create(
                "sleeping",
                reason,
                correlationId,
                turnId,
                overlayState: "none"),
            nameof(MerlinAwakeStateService)) ?? Task.CompletedTask;
    }

    private static List<string> Tokenize(string? text)
    {
        var normalized = new string((text ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character == '\''
                ? character
                : ' ')
            .ToArray());

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static bool TokensEqual(IReadOnlyList<string> tokens, IReadOnlyList<string> phrase)
    {
        if (tokens.Count != phrase.Count)
        {
            return false;
        }

        for (var index = 0; index < phrase.Count; index++)
        {
            if (!string.Equals(tokens[index], phrase[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record MerlinAwakeGateResult(
    MerlinAwakeGateDecision Decision,
    bool ShouldAllow,
    bool IsWakePhrase,
    bool IsSleepPhrase,
    string Reason);

public enum MerlinAwakeGateDecision
{
    WakePhraseAccepted,
    AwakeActivityAccepted,
    SleepPhraseAccepted,
    IgnoredWhileSleeping
}
