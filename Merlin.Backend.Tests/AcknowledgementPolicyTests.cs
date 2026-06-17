using Merlin.Backend.Configuration;
using Merlin.Backend.Services.Acknowledgement;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AcknowledgementPolicyTests
{
    [Fact]
    public void Decide_WhenDeepInfraGeneralRequest_SelectsAcknowledgement()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(Context(willUseDeepInfra: true));

        Assert.True(decision.ShouldSpeakInitialAcknowledgement);
        Assert.Equal(AcknowledgementCategory.GeneralReasoning, decision.InitialCategory);
        Assert.False(string.IsNullOrWhiteSpace(decision.PhraseText));
    }

    [Fact]
    public void Decide_WhenFastLocalTimeRequest_SkipsAcknowledgement()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(Context(
            normalizedText: "system resource current_time",
            capability: "system_time",
            isExpectedFastLocalTool: true));

        Assert.False(decision.ShouldSpeakInitialAcknowledgement);
        Assert.Equal("Expected fast local tool.", decision.Reason);
    }

    [Fact]
    public void Decide_WhenConfirmationFlow_SkipsAcknowledgement()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(Context(
            userText: "yes",
            normalizedText: "confirm",
            capability: "confirmation",
            intentDomain: "confirmation",
            willUseExternalTool: true));

        Assert.False(decision.ShouldSpeakInitialAcknowledgement);
        Assert.Equal("Confirmation flow returns its own action acknowledgement.", decision.Reason);
    }

    [Fact]
    public void Decide_WhenExplicitMemorySave_SkipsSeparateAcknowledgement()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(Context(isMemorySave: true, willUseDeepInfra: true));

        Assert.False(decision.ShouldSpeakInitialAcknowledgement);
        Assert.Contains("memory save", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_WhenMemorySearch_SelectsMemoryAcknowledgement()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(Context(isMemorySearch: true));

        Assert.True(decision.ShouldSpeakInitialAcknowledgement);
        Assert.Equal(AcknowledgementCategory.MemorySearch, decision.InitialCategory);
        Assert.Contains("memory", decision.PhraseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_DoesNotUseSamePhraseTwiceInARow()
    {
        var policy = CreatePolicy(new AcknowledgementSpeechOptions
        {
            PhraseCooldownSeconds = 600
        });

        var first = policy.Decide(Context(willUseDeepInfra: true));
        var second = policy.Decide(Context(willUseDeepInfra: true, now: DateTimeOffset.UtcNow.AddSeconds(1)));

        Assert.NotEqual(first.PhraseId, second.PhraseId);
    }

    [Fact]
    public void PhraseLibrary_DoesNotExposeImplementationDetailsInSpokenPhrases()
    {
        var options = Options.Create(new AcknowledgementSpeechOptions());
        var library = new AcknowledgementPhraseLibrary(options);
        var bannedTerms = new[]
        {
            "model",
            "llm",
            "deepinfra",
            "api",
            "backend",
            "provider",
            "token",
            "inference",
            "http",
            "request",
            "reasoning response"
        };

        foreach (var phrase in library.CommonPhrases)
        {
            foreach (var bannedTerm in bannedTerms)
            {
                Assert.DoesNotContain(bannedTerm, phrase, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void PhraseLibrary_WhenWaitingOnDeepInfra_UsesNaturalProgressPhrase()
    {
        var options = Options.Create(new AcknowledgementSpeechOptions());
        var library = new AcknowledgementPhraseLibrary(options);

        var phrase = library.SelectProgress(
            RequestProgressState.WaitingOnDeepInfra,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        Assert.DoesNotContain("model", phrase.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeepInfra", phrase.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LLM", phrase.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", phrase.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning response", phrase.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            phrase.Text.StartsWith("I ", StringComparison.OrdinalIgnoreCase)
            || phrase.Text.StartsWith("Still ", StringComparison.OrdinalIgnoreCase)
            || phrase.Text.StartsWith("This ", StringComparison.OrdinalIgnoreCase)
            || phrase.Text.StartsWith("Almost ", StringComparison.OrdinalIgnoreCase));
    }

    private static AcknowledgementPolicy CreatePolicy(AcknowledgementSpeechOptions? options = null)
    {
        var configured = options ?? new AcknowledgementSpeechOptions();
        var optionsWrapper = Options.Create(configured);
        return new AcknowledgementPolicy(
            new AcknowledgementPhraseLibrary(optionsWrapper),
            optionsWrapper);
    }

    private static AcknowledgementContext Context(
        string userText = "Why do people fear change?",
        string normalizedText = "chat why do people fear change?",
        string intentDomain = "general_conversation",
        string? capability = "general_conversation",
        bool isVoiceMode = true,
        bool willUseDeepInfra = false,
        bool willUseExternalTool = false,
        bool isExpectedFastLocalTool = false,
        bool isMemorySave = false,
        bool isMemorySearch = false,
        DateTimeOffset? now = null)
    {
        return new AcknowledgementContext
        {
            UserText = userText,
            NormalizedText = normalizedText,
            IntentDomain = intentDomain,
            Capability = capability,
            IsVoiceMode = isVoiceMode,
            WillUseDeepInfra = willUseDeepInfra,
            WillUseExternalTool = willUseExternalTool,
            IsExpectedFastLocalTool = isExpectedFastLocalTool,
            IsMemorySave = isMemorySave,
            IsMemorySearch = isMemorySearch,
            Now = now ?? DateTimeOffset.UtcNow
        };
    }
}
