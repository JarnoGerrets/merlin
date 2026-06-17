using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Acknowledgement;

public sealed class AcknowledgementPolicy : IAcknowledgementPolicy
{
    private readonly IAcknowledgementPhraseLibrary _phraseLibrary;
    private readonly AcknowledgementSpeechOptions _options;

    public AcknowledgementPolicy(
        IAcknowledgementPhraseLibrary phraseLibrary,
        IOptions<AcknowledgementSpeechOptions> options)
    {
        _phraseLibrary = phraseLibrary;
        _options = options.Value;
    }

    public AcknowledgementDecision Decide(AcknowledgementContext context)
    {
        var timings = Timings(context);
        if (!_options.Enabled)
        {
            return Skip("Acknowledgement speech is disabled.", timings.ProgressState);
        }

        if (_options.VoiceModeOnly && !context.IsVoiceMode)
        {
            return Skip("Request is not voice mode.", timings.ProgressState);
        }

        if (context.IsMemorySave)
        {
            return Skip("Explicit memory save returns its own short acknowledgement.", RequestProgressState.WaitingOnMemory);
        }

        if (IsConfirmationFlow(context))
        {
            return Skip("Confirmation flow returns its own action acknowledgement.", timings.ProgressState);
        }

        if (context.IsExpectedFastLocalTool)
        {
            return Skip("Expected fast local tool.", timings.ProgressState);
        }

        if (context.IsMemorySearch)
        {
            return Speak(AcknowledgementCategory.MemorySearch, "Memory search may take noticeable time.", RequestProgressState.WaitingOnMemory);
        }

        if (context.WillUseDeepInfra)
        {
            var category = LooksTechnical(context.NormalizedText)
                ? AcknowledgementCategory.DeepTechnicalArchitecture
                : LooksRecommendation(context.NormalizedText)
                    ? AcknowledgementCategory.ResearchRecommendation
                    : AcknowledgementCategory.GeneralReasoning;

            return Speak(category, "Request will use DeepInfra.", RequestProgressState.WaitingOnDeepInfra);
        }

        if (context.WillUseExternalTool)
        {
            return Speak(AcknowledgementCategory.LocalSystemTool, "Tool request may take noticeable time.", RequestProgressState.WaitingOnTool);
        }

        return Skip("Request is not expected to exceed acknowledgement latency threshold.", timings.ProgressState);

        AcknowledgementDecision Speak(
            AcknowledgementCategory category,
            string reason,
            RequestProgressState progressState)
        {
            var cooldown = TimeSpan.FromSeconds(Math.Max(0, _options.PhraseCooldownSeconds));
            var phrase = _phraseLibrary.Select(category, context.Now, cooldown);
            return new AcknowledgementDecision
            {
                ShouldSpeakInitialAcknowledgement = true,
                PhraseId = phrase.Id,
                PhraseText = phrase.Text,
                InitialCategory = category,
                FirstProgressAfter = timings.First,
                SecondProgressAfter = timings.Second,
                LongWaitProgressAfter = timings.LongWait,
                MaxProgressUpdates = Math.Max(0, _options.MaxProgressUpdates),
                ProgressState = progressState,
                Reason = reason
            };
        }

        AcknowledgementDecision Skip(string reason, RequestProgressState progressState)
        {
            return AcknowledgementDecision.Skipped(
                reason,
                timings.First,
                timings.Second,
                timings.LongWait,
                Math.Max(0, _options.MaxProgressUpdates),
                progressState);
        }
    }

    private ProgressTimings Timings(AcknowledgementContext context)
    {
        var progressState = context.IsMemorySearch
            ? RequestProgressState.WaitingOnMemory
            : context.WillUseDeepInfra
                ? RequestProgressState.WaitingOnDeepInfra
                : context.WillUseExternalTool
                    ? RequestProgressState.WaitingOnTool
                    : RequestProgressState.StillWorking;

        return new ProgressTimings(
            TimeSpan.FromMilliseconds(Math.Max(1, _options.FirstProgressAfterMs)),
            TimeSpan.FromMilliseconds(Math.Max(1, _options.SecondProgressAfterMs)),
            TimeSpan.FromMilliseconds(Math.Max(1, _options.LongWaitProgressAfterMs)),
            progressState);
    }

    private static bool LooksTechnical(string text)
    {
        return ContainsAny(text, "architecture", "tradeoff", "tradeoffs", "sqlite", "database", "technical", "system design", "local-first");
    }

    private static bool LooksRecommendation(string text)
    {
        return ContainsAny(text, "recommend", "best", "which", "should i buy", "options", "compare", "laptop", "gpu", "car");
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsConfirmationFlow(AcknowledgementContext context)
    {
        return string.Equals(context.IntentDomain, "confirmation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.Capability, "confirmation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.NormalizedText, "confirm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.NormalizedText, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.NormalizedText, "approve", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProgressTimings(
        TimeSpan First,
        TimeSpan Second,
        TimeSpan LongWait,
        RequestProgressState ProgressState);
}
