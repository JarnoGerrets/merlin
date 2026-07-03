using Merlin.Backend.Configuration;
using Merlin.Backend.Services.StreamingResponses;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class StreamingResponseAssemblerTests
{
    public static TheoryData<string[], string> AdjacentDeltaCases => new()
    {
        { ["t", "ending"], "tending" },
        { ["cult", "ivating"], "cultivating" },
        { ["it", "'", "s"], "it's" },
        { ["ask", "ed"], "asked" },
        { ["st", "irs"], "stirs" },
        { ["stream", "ing"], "streaming" },
        { ["Hello", ",", " world", "!"], "Hello, world!" },
        { ["The answer is", " tending", " toward this."], "The answer is tending toward this." },
        { ["The answer is", " tending"], "The answer is tending" }
    };

    [Theory]
    [MemberData(nameof(AdjacentDeltaCases))]
    public void Append_PreservesProviderDeltaAdjacency(string[] deltas, string expected)
    {
        var assembler = CreateAssembler();

        foreach (var delta in deltas)
        {
            assembler.Append(new ModelTextDelta(delta));
        }

        Assert.Equal(expected, assembler.UncommittedText);
    }

    [Fact]
    public void DrainReadySegments_DoesNotReleaseDanglingTinyProviderChunks()
    {
        var assembler = CreateAssembler();

        foreach (var delta in new[] { "The", " best", " way", " is", " to" })
        {
            assembler.Append(new ModelTextDelta(delta));
        }

        Assert.Empty(assembler.DrainReadySegments(isFinal: false));
        Assert.Equal("The best way is to", assembler.UncommittedText);
    }

    [Fact]
    public void DrainReadySegments_ReleasesCompleteSentence()
    {
        var assembler = CreateAssembler(new StreamingResponseOptions { FirstSegmentMinWords = 5 });

        assembler.Append(new ModelTextDelta("The best way is to split safely."));
        var segments = assembler.DrainReadySegments(isFinal: false);

        var segment = Assert.Single(segments);
        Assert.Equal("The best way is to split safely.", segment.Text);
        Assert.Equal(SpeakableBoundaryKind.Sentence, segment.BoundaryKind);
    }

    [Fact]
    public void DrainReadySegments_PreventsDanglingConnectorAtClauseBoundary()
    {
        var assembler = CreateAssembler(new StreamingResponseOptions
        {
            FirstSegmentMinWords = 3,
            LaterSegmentMinWords = 3,
            PreferredSentenceMaxChars = 20,
            HardBufferMaxChars = 80
        });

        assembler.Append(new ModelTextDelta("This means that, and"));

        Assert.Empty(assembler.DrainReadySegments(isFinal: false));
    }

    [Fact]
    public void DrainReadySegments_ReleasesLongBufferAtLastSafeBoundaryOnly()
    {
        var assembler = CreateAssembler(new StreamingResponseOptions
        {
            FirstSegmentMinWords = 4,
            LaterSegmentMinWords = 4,
            PreferredSentenceMaxChars = 500,
            HardBufferMaxChars = 90
        });

        assembler.Append(new ModelTextDelta("The safest approach is to buffer model text locally, release only stable clauses, and keep unsafe tails uncommitted until more text arrives"));

        var segment = Assert.Single(assembler.DrainReadySegments(isFinal: false));
        Assert.Equal("The safest approach is to buffer model text locally, release only stable clauses,", segment.Text);
        Assert.Equal(SpeakableBoundaryKind.ForcedLongBufferFlush, segment.BoundaryKind);
        Assert.Equal("and keep unsafe tails uncommitted until more text arrives", assembler.UncommittedText);
    }

    [Fact]
    public void DrainReadySegments_FinalFlushReleasesRemainingTail()
    {
        var assembler = CreateAssembler();
        assembler.Append(new ModelTextDelta("Much faster"));

        var segment = Assert.Single(assembler.DrainReadySegments(isFinal: true));

        Assert.Equal("Much faster", segment.Text);
        Assert.True(segment.IsFinalSegment);
        Assert.Equal(SpeakableBoundaryKind.FinalFlush, segment.BoundaryKind);
    }

    [Fact]
    public void DrainReadySegments_MultipleSentencesInOneDeltaProduceMultipleSegments()
    {
        var assembler = CreateAssembler(new StreamingResponseOptions
        {
            FirstSegmentMinWords = 2,
            LaterSegmentMinWords = 2
        });

        assembler.Append(new ModelTextDelta("Streaming helps. Raw chunks are unsafe. Use a segmenter."));
        var segments = assembler.DrainReadySegments(isFinal: false);

        Assert.Equal(
            new[] { "Streaming helps.", "Raw chunks are unsafe.", "Use a segmenter." },
            segments.Select(segment => segment.Text).ToArray());
    }

    private static StreamingResponseAssembler CreateAssembler(StreamingResponseOptions? options = null)
    {
        return new StreamingResponseAssembler(options ?? new StreamingResponseOptions());
    }
}
