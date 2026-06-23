using Merlin.Backend.Services.InterruptionIntelligence;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpokenAnswerTrackerTests
{
    [Fact]
    public void StartAnswer_CreatesInitialState()
    {
        var tracker = new SpokenAnswerTracker();

        var state = tracker.StartAnswer(
            "turn-1",
            "correlation-1",
            "Why does pool water look blue?",
            currentTopicLabel: "pool color");

        Assert.Equal("turn-1", state.TurnId);
        Assert.Equal("correlation-1", state.CorrelationId);
        Assert.Equal("Why does pool water look blue?", state.OriginalUserQuestion);
        Assert.Equal("pool color", state.CurrentTopicLabel);
        Assert.True(state.CanRecompose);
        Assert.Equal(string.Empty, state.SpokenSoFar);
        Assert.Equal(string.Empty, state.LastCompletedSentence);
        Assert.Equal(string.Empty, state.CurrentPartialSentence);
        Assert.Equal(string.Empty, state.UnspokenRemainder);
    }

    [Fact]
    public void AppendSpokenText_TracksCompletedAndPartialSentence()
    {
        var tracker = CreateStartedTracker();

        var state = tracker.AppendSpokenText(
            "turn-1",
            "Pool water can look blue for several reasons. Due to the color of the pool li");

        Assert.Equal("Pool water can look blue for several reasons.", state.LastCompletedSentence);
        Assert.Equal("Due to the color of the pool li", state.CurrentPartialSentence);
    }

    [Fact]
    public void AppendSpokenText_TracksMultipleCompletedSentences()
    {
        var tracker = CreateStartedTracker();

        var state = tracker.AppendSpokenText("turn-1", "Hello! How are you? I am fi");

        Assert.Equal("How are you?", state.LastCompletedSentence);
        Assert.Equal("I am fi", state.CurrentPartialSentence);
    }

    [Fact]
    public void CreateCheckpoint_WhenDiscardingPartial_UsesSafeCompletedPrefix()
    {
        var tracker = CreateStartedTracker();
        tracker.AppendSpokenText(
            "turn-1",
            "Pool water can look blue for several reasons. Due to the color of the pool li");

        var checkpoint = tracker.CreateCheckpoint("turn-1", discardCurrentPartialSentence: true);

        Assert.Equal("Pool water can look blue for several reasons.", checkpoint.SafeSpokenPrefix);
        Assert.Equal("Pool water can look blue for several reasons.", checkpoint.LastCompletedSentence);
        Assert.Equal("Due to the color of the pool li", checkpoint.DiscardedPartialSentence);
    }

    [Fact]
    public void CreateCheckpoint_WhenNoCompletedSentence_DiscardsFullPartial()
    {
        var tracker = CreateStartedTracker();
        tracker.AppendSpokenText("turn-1", "Due to the color of the pool li");

        var checkpoint = tracker.CreateCheckpoint("turn-1", discardCurrentPartialSentence: true);

        Assert.Equal(string.Empty, checkpoint.SafeSpokenPrefix);
        Assert.Equal(string.Empty, checkpoint.LastCompletedSentence);
        Assert.Equal("Due to the color of the pool li", checkpoint.DiscardedPartialSentence);
    }

    [Fact]
    public void CreateCheckpoint_WhenNotDiscardingPartial_PreservesFullSpokenPrefix()
    {
        var tracker = CreateStartedTracker();
        tracker.AppendSpokenText(
            "turn-1",
            "Pool water can look blue for several reasons. Due to the color of the pool li");

        var checkpoint = tracker.CreateCheckpoint("turn-1", discardCurrentPartialSentence: false);

        Assert.Equal(
            "Pool water can look blue for several reasons. Due to the color of the pool li",
            checkpoint.SafeSpokenPrefix);
        Assert.Equal(string.Empty, checkpoint.DiscardedPartialSentence);
    }

    [Fact]
    public void MarkChunkStarted_SetsCurrentPartialWithoutAppendingSpokenText()
    {
        var tracker = CreateStartedTracker();

        var state = tracker.MarkChunkStarted("turn-1", "Due to the color of the pool li");

        Assert.Equal(string.Empty, state.SpokenSoFar);
        Assert.Equal("Due to the color of the pool li", state.CurrentPartialSentence);
    }

    [Fact]
    public void MarkChunkCompleted_AppendsChunkAndClearsPartialAtBoundary()
    {
        var tracker = CreateStartedTracker();

        var state = tracker.MarkChunkCompleted("turn-1", "Pool water can look blue for several reasons.");

        Assert.Equal("Pool water can look blue for several reasons.", state.SpokenSoFar);
        Assert.Equal("Pool water can look blue for several reasons.", state.LastCompletedSentence);
        Assert.Equal(string.Empty, state.CurrentPartialSentence);
    }

    [Fact]
    public void MarkChunkCompleted_DoesNotDuplicateAlreadyAppendedChunk()
    {
        var tracker = CreateStartedTracker();
        tracker.AppendSpokenText("turn-1", "Pool water can look blue for several reasons.");

        var state = tracker.MarkChunkCompleted("turn-1", "Pool water can look blue for several reasons.");

        Assert.Equal("Pool water can look blue for several reasons.", state.SpokenSoFar);
    }

    [Fact]
    public void AppendSpokenText_UpdatesUnspokenRemainder_WhenDraftContainsSpokenPrefix()
    {
        var tracker = new SpokenAnswerTracker();
        tracker.StartAnswer(
            "turn-1",
            "correlation-1",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons. Due to the liner, it appears blue.");

        var state = tracker.AppendSpokenText("turn-1", "Pool water can look blue for several reasons.");

        Assert.Equal("Due to the liner, it appears blue.", state.UnspokenRemainder);
    }

    [Fact]
    public void Clear_RemovesState()
    {
        var tracker = CreateStartedTracker();

        tracker.Clear("turn-1");

        Assert.Null(tracker.GetState("turn-1"));
    }

    [Fact]
    public void GetState_WhenMissing_ReturnsNull()
    {
        var tracker = new SpokenAnswerTracker();

        Assert.Null(tracker.GetState("missing"));
    }

    [Fact]
    public void CreateCheckpoint_WhenMissingTurn_Throws()
    {
        var tracker = new SpokenAnswerTracker();

        Assert.Throws<InvalidOperationException>(() => tracker.CreateCheckpoint("missing"));
    }

    [Fact]
    public void Tracker_AllowsBasicConcurrentAppendAccess()
    {
        var tracker = CreateStartedTracker();

        Parallel.For(
            0,
            20,
            index => tracker.AppendSpokenText("turn-1", $"Sentence {index}."));

        var state = tracker.GetState("turn-1");

        Assert.NotNull(state);
        Assert.Contains("Sentence", state!.SpokenSoFar);
    }

    [Theory]
    [InlineData("Hello.", "Hello.", "")]
    [InlineData("Hello. This is par", "Hello.", "This is par")]
    [InlineData("Hello! How are you? I am fi", "How are you?", "I am fi")]
    public void SentenceSegmenter_SplitsCompletedAndPartialSentences(
        string text,
        string expectedLastCompleted,
        string expectedPartial)
    {
        var segmenter = new SpokenSentenceSegmenter();

        var segmentation = segmenter.Segment(text);

        Assert.Equal(expectedLastCompleted, segmentation.LastCompletedSentence);
        Assert.Equal(expectedPartial, segmentation.CurrentPartialSentence);
    }

    private static SpokenAnswerTracker CreateStartedTracker()
    {
        var tracker = new SpokenAnswerTracker();
        tracker.StartAnswer("turn-1", "correlation-1", "Why does pool water look blue?");
        return tracker;
    }
}
