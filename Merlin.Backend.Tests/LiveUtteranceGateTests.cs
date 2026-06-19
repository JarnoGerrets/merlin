using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.LiveUtterance;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LiveUtteranceGateTests
{
    private readonly LiveUtteranceGate _gate = new(
        NullLogger<LiveUtteranceGate>.Instance,
        Options.Create(new LiveUtteranceGateOptions()));

    [Fact]
    public void StopDuringSpeaking_IsPlaybackControl()
    {
        var result = Evaluate("stop", LiveAssistantTurnState.Speaking, assistantWasSpeaking: true);

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptPlaybackControl, result.Decision);
        Assert.True(result.ShouldAffectPlayback);
        Assert.False(result.ShouldRouteToCommandRouter);
    }

    [Fact]
    public void CancelThatDuringActiveFlow_IsCancellation()
    {
        var result = Evaluate("cancel that", LiveAssistantTurnState.AwaitingToolCommit, pendingCommand: "Open URL: open facebook");

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptCancellation, result.Decision);
        Assert.False(result.ShouldRouteToCommandRouter);
    }

    [Fact]
    public void SorryIMeantGoogleDuringOpenUrl_IsReplacement()
    {
        var result = Evaluate("sorry I meant Google", LiveAssistantTurnState.AwaitingToolCommit, pendingCommand: "Open URL: open facebook");

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptReplacement, result.Decision);
        Assert.Equal("open google", result.ReplacementText);
    }

    [Fact]
    public void SingleGoogleDuringOpenUrlFacebook_IsReplacement()
    {
        var result = Evaluate("Google", LiveAssistantTurnState.AwaitingToolCommit, pendingCommand: "Open URL: open facebook");

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptReplacement, result.Decision);
        Assert.Equal("open google", result.ReplacementText);
    }

    [Fact]
    public void GoogleWhileIdle_IsNotBlindCommand()
    {
        var result = Evaluate("Google", LiveAssistantTurnState.IdleListening, activeTurn: false);

        Assert.NotEqual(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.False(result.ShouldRouteToCommandRouter);
        Assert.False(result.ShouldCallDeepInfra);
    }

    [Fact]
    public void HeyItsInfant_DoesNotCallDeepInfra()
    {
        var result = Evaluate("hey its infant", LiveAssistantTurnState.IdleListening, activeTurn: false);

        Assert.Equal(LiveUtteranceGateDecisionKind.IgnoreAsGarbageTranscript, result.Decision);
        Assert.False(result.ShouldCallDeepInfra);
        Assert.False(result.ShouldRouteToCommandRouter);
    }

    [Fact]
    public void AreYouAwakeWhileIdle_RoutesToCommandRouter()
    {
        var result = Evaluate("are you awake?", LiveAssistantTurnState.IdleListening, activeTurn: false);

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.True(result.ShouldRouteToCommandRouter);
    }

    [Theory]
    [InlineData("What's the meaning of life?")]
    [InlineData("What is the meaning of life?")]
    [InlineData("How's that different?")]
    [InlineData("Why doesn't that work?")]
    [InlineData("Can you explain graphs?")]
    [InlineData("Tell me about causation.")]
    [InlineData("Is that correct?")]
    public void CoherentVoiceStreamQuestionsAndRequests_RouteToCommandRouter(string text)
    {
        var result = Evaluate(text, LiveAssistantTurnState.IdleListening, activeTurn: false, source: "voice_stream");

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.True(result.ShouldRouteToCommandRouter);
        Assert.True(result.ShouldCallDeepInfra);
    }

    [Theory]
    [InlineData("Hey what's the weather?")]
    [InlineData("please tell me about the Roman empire")]
    [InlineData("yo can you explain why this happens?")]
    public void LeadingFillerDoesNotBlockCoherentVoiceStreamRequest(string text)
    {
        var result = Evaluate(text, LiveAssistantTurnState.IdleListening, activeTurn: false, source: "voice_stream");

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.True(result.ShouldRouteToCommandRouter);
    }

    [Theory]
    [InlineData("Hey what infant alrighty")]
    [InlineData("What infant alrighty")]
    [InlineData("Uh")]
    [InlineData("Okay alrighty thing")]
    public void IncoherentVoiceStreamFragments_DoNotRouteToGeneralConversation(string text)
    {
        var result = Evaluate(text, LiveAssistantTurnState.IdleListening, activeTurn: false, source: "voice_stream");

        Assert.NotEqual(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.False(result.ShouldRouteToCommandRouter);
        Assert.False(result.ShouldCallDeepInfra);
    }

    [Fact]
    public void MalformedAuxiliaryFragmentWhileIdle_DoesNotRoute()
    {
        var result = Evaluate("are uh infant", LiveAssistantTurnState.IdleListening, activeTurn: false);

        Assert.NotEqual(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.False(result.ShouldRouteToCommandRouter);
    }

    [Fact]
    public void YeahButDuringSpeaking_HoldsForMoreSpeech()
    {
        var result = Evaluate("yeah but", LiveAssistantTurnState.Speaking, assistantWasSpeaking: true);

        Assert.Equal(LiveUtteranceGateDecisionKind.HoldForMoreSpeech, result.Decision);
        Assert.NotNull(result.HoldWindow);
        Assert.False(result.ShouldRouteToCommandRouter);
    }

    [Fact]
    public void ContinueDuringPausedState_AcceptsContinuation()
    {
        var result = Evaluate("continue", LiveAssistantTurnState.PausedByUser, pendingCommand: "Open URL: open facebook");

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptContinuation, result.Decision);
        Assert.True(result.ShouldAffectPlayback);
    }

    [Fact]
    public void UnknownMalformedText_DoesNotDefaultToGeneralConversation()
    {
        var result = Evaluate("blue maybe sideways the", LiveAssistantTurnState.IdleListening, activeTurn: false);

        Assert.NotEqual(LiveUtteranceGateDecisionKind.AcceptNewRequest, result.Decision);
        Assert.False(result.ShouldCallDeepInfra);
        Assert.False(result.ShouldRouteToCommandRouter);
    }

    [Fact]
    public void ShortValidPhrase_IsNotRejectedForLength()
    {
        var result = Evaluate("wait", LiveAssistantTurnState.Speaking, assistantWasSpeaking: true);

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptPlaybackControl, result.Decision);
    }

    [Theory]
    [InlineData("And shut up.")]
    [InlineData("no shut up")]
    [InlineData("please shut up")]
    [InlineData("no stop")]
    public void EmbeddedHardStop_IsPlaybackControl(string text)
    {
        var result = Evaluate(text, LiveAssistantTurnState.Speaking, assistantWasSpeaking: true);

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptPlaybackControl, result.Decision);
        Assert.False(result.ShouldCallDeepInfra);
    }

    [Theory]
    [InlineData("wait wait")]
    [InlineData("no no no")]
    [InlineData("hold on")]
    public void FloorTakingControlPhrases_ArePlaybackControl(string text)
    {
        var result = Evaluate(text, LiveAssistantTurnState.Speaking, assistantWasSpeaking: true);

        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptPlaybackControl, result.Decision);
        Assert.False(result.ShouldCallDeepInfra);
    }

    [Theory]
    [InlineData("wait, that's not what I meant")]
    [InlineData("that's not what I meant")]
    public void NotWhatIMeant_TakesFloor(string text)
    {
        var result = Evaluate(text, LiveAssistantTurnState.Speaking, assistantWasSpeaking: true);

        Assert.Equal(LiveUtteranceGateDecisionKind.HoldForMoreSpeech, result.Decision);
        Assert.False(result.ShouldCallDeepInfra);
    }

    [Fact]
    public void HeldCorrectionCombinesWithNextTranscript()
    {
        var first = Evaluate("sorry I meant", LiveAssistantTurnState.AwaitingToolCommit, pendingCommand: "Open URL: open facebook");
        var second = Evaluate("Google", LiveAssistantTurnState.AwaitingToolCommit, pendingCommand: "Open URL: open facebook");

        Assert.Equal(LiveUtteranceGateDecisionKind.HoldForMoreSpeech, first.Decision);
        Assert.Equal(LiveUtteranceGateDecisionKind.AcceptReplacement, second.Decision);
        Assert.Equal("open google", second.ReplacementText);
    }

    private LiveUtteranceGateResult Evaluate(
        string text,
        LiveAssistantTurnState state,
        bool assistantWasSpeaking = false,
        bool activeTurn = true,
        string? pendingCommand = null,
        string source = "test")
    {
        var turn = activeTurn
            ? new LiveAssistantTurn
            {
                ConversationId = "default",
                CorrelationId = "test-correlation",
                AssistantTurnId = "test-turn",
                StartedAt = DateTimeOffset.UtcNow,
                CancellationTokenSource = new CancellationTokenSource()
            }
            : null;
        turn?.UpdateState(state, pendingCommand);
        var utterance = new UserUtterance
        {
            Text = text,
            TimestampUtc = DateTimeOffset.UtcNow,
            ActiveTurnId = turn?.AssistantTurnId,
            CorrelationId = turn?.CorrelationId ?? "idle-test",
            StateWhenCaptured = state,
            AssistantWasSpeaking = assistantWasSpeaking,
            Source = source,
            Confidence = 0.9
        };

        return _gate.Evaluate(new LiveUtteranceGateInput
        {
            Utterance = utterance,
            ActiveTurn = turn,
            CurrentSystemState = state.ToString(),
            AssistantWasSpeaking = assistantWasSpeaking,
            IsIdleListening = !activeTurn || state is LiveAssistantTurnState.IdleListening,
            PendingCommandDescription = pendingCommand,
            SttConfidence = utterance.Confidence,
            AudioSpeechConfidence = utterance.Confidence
        });
    }
}
