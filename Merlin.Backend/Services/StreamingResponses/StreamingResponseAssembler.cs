using System.Text;
using System.Text.RegularExpressions;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.StreamingResponses;

public sealed partial class StreamingResponseAssembler
{
    private static readonly HashSet<string> BadEndingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "but", "because", "that", "to", "of", "for",
        "with", "without", "as", "if", "when", "while", "where",
        "which", "who", "into", "from", "by", "so", "then", "than", "about"
    };

    private readonly StreamingResponseOptions _options;
    private readonly ILogger<StreamingResponseAssembler>? _logger;
    private readonly StringBuilder _buffer = new();
    private int _nextSequenceNumber;

    public StreamingResponseAssembler(IOptions<StreamingResponseOptions> options)
        : this(options.Value, logger: null)
    {
    }

    public StreamingResponseAssembler(StreamingResponseOptions options, ILogger<StreamingResponseAssembler>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public int DanglingCandidateRejectedCount { get; private set; }

    public string UncommittedText => _buffer.ToString();

    public void Append(ModelTextDelta delta)
    {
        if (string.IsNullOrEmpty(delta.Text))
        {
            return;
        }

        var text = NormalizeDeltaWhitespace(delta.Text);
        _logger?.LogDebug(
            "StreamingDeltaReceived. Length: {Length}. StartsWithWhitespace: {StartsWithWhitespace}. EndsWithWhitespace: {EndsWithWhitespace}.",
            text.Length,
            text.Length > 0 && char.IsWhiteSpace(text[0]),
            text.Length > 0 && char.IsWhiteSpace(text[^1]));

        _buffer.Append(text);
        _logger?.LogDebug(
            "StreamingTextAppended. BufferLength: {BufferLength}. InsertedSeparator: false.",
            _buffer.Length);
    }

    public IReadOnlyList<SpeakableTextSegment> DrainReadySegments(bool isFinal)
    {
        var ready = new List<SpeakableTextSegment>();

        while (_buffer.Length > 0)
        {
            var boundary = FindBoundary(_buffer.ToString(), isFinal);
            if (boundary is null)
            {
                break;
            }

            var candidate = _buffer.ToString(0, boundary.Value.EndIndex).Trim();
            var tail = _buffer.ToString(boundary.Value.EndIndex, _buffer.Length - boundary.Value.EndIndex);

            if (!IsSpeakable(candidate, isFinal, boundary.Value.Kind))
            {
                break;
            }

            ready.Add(new SpeakableTextSegment(
                candidate,
                _nextSequenceNumber++,
                IsFinalSegment: isFinal && string.IsNullOrWhiteSpace(tail),
                WasForcedFlush: boundary.Value.WasForced,
                BoundaryKind: boundary.Value.Kind));

            _buffer.Clear();
            _buffer.Append(tail.TrimStart());
        }

        if (isFinal && _buffer.Length > 0)
        {
            var finalText = _buffer.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalText))
            {
                ready.Add(new SpeakableTextSegment(
                    finalText,
                    _nextSequenceNumber++,
                    IsFinalSegment: true,
                    BoundaryKind: SpeakableBoundaryKind.FinalFlush));
            }

            _buffer.Clear();
        }

        return ready;
    }

    public void Clear()
    {
        _buffer.Clear();
    }

    private Boundary? FindBoundary(string text, bool isFinal)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var sentence = FindFirstSentenceBoundary(text);
        if (sentence is not null)
        {
            var candidate = text[..sentence.Value.EndIndex].Trim();
            if (WordCount(candidate) >= MinimumWords())
            {
                return sentence;
            }
        }

        var paragraph = FindFirstParagraphBoundary(text);
        if (paragraph is not null)
        {
            var candidate = text[..paragraph.Value.EndIndex].Trim();
            if (WordCount(candidate) >= MinimumWords())
            {
                return paragraph;
            }
        }

        if (_options.AllowClauseBoundaries
            && (text.Length >= _options.PreferredSentenceMaxChars || (_nextSequenceNumber > 0 && text.Length >= _options.PreferredSentenceMaxChars / 2)))
        {
            var clause = FindLastClauseBoundary(text);
            if (clause is not null)
            {
                var candidate = text[..clause.Value.EndIndex].Trim();
                if (WordCount(candidate) >= MinimumWords() && !EndsWithDanglingWord(candidate))
                {
                    return clause;
                }
            }
        }

        if (text.Length > _options.HardBufferMaxChars)
        {
            var forced = FindLastSafeBoundary(text);
            if (forced is not null)
            {
                return forced.Value with
                {
                    Kind = SpeakableBoundaryKind.ForcedLongBufferFlush,
                    WasForced = true
                };
            }
        }

        if (isFinal)
        {
            return new Boundary(text.Length, SpeakableBoundaryKind.FinalFlush, WasForced: false);
        }

        return null;
    }

    private bool IsSpeakable(string candidate, bool isFinal, SpeakableBoundaryKind boundaryKind)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (!isFinal && WordCount(candidate) < MinimumWords())
        {
            return false;
        }

        if (!isFinal && EndsWithDanglingWord(candidate))
        {
            DanglingCandidateRejectedCount++;
            _logger?.LogDebug(
                "StreamingSegmentRejectedPartialWord. Preview: {Preview}. Reason: dangling_connector.",
                Preview(candidate));
            return false;
        }

        return boundaryKind is not SpeakableBoundaryKind.Unknown;
    }

    private int MinimumWords() => _nextSequenceNumber == 0
        ? Math.Max(1, _options.FirstSegmentMinWords)
        : Math.Max(1, _options.LaterSegmentMinWords);

    private static Boundary? FindFirstSentenceBoundary(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('.' or '?' or '!'))
            {
                continue;
            }

            if (index + 1 < text.Length && char.IsLetterOrDigit(text[index + 1]))
            {
                continue;
            }

            var end = index + 1;
            while (end < text.Length && (text[end] is '"' or '\'' or ')' or ']' || char.IsWhiteSpace(text[end])))
            {
                if (text[end] is '\n')
                {
                    break;
                }

                end++;
            }

            return new Boundary(end, SpeakableBoundaryKind.Sentence, WasForced: false);
        }

        return null;
    }

    private static Boundary? FindFirstParagraphBoundary(string text)
    {
        var index = text.IndexOf("\n\n", StringComparison.Ordinal);
        return index < 0
            ? null
            : new Boundary(index + 2, SpeakableBoundaryKind.Paragraph, WasForced: false);
    }

    private static Boundary? FindLastClauseBoundary(string text)
    {
        for (var index = Math.Min(text.Length - 1, text.Length); index >= 0; index--)
        {
            if (text[index] is ',' or ';' or ':')
            {
                return new Boundary(index + 1, SpeakableBoundaryKind.Clause, WasForced: false);
            }
        }

        return null;
    }

    private static Boundary? FindLastSafeBoundary(string text)
    {
        var sentence = LastBoundary(text, ".?!");
        if (sentence is not null)
        {
            return sentence.Value with { Kind = SpeakableBoundaryKind.Sentence };
        }

        var paragraphIndex = text.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paragraphIndex >= 0)
        {
            return new Boundary(paragraphIndex + 2, SpeakableBoundaryKind.Paragraph, WasForced: false);
        }

        var clause = LastBoundary(text, ",;:");
        return clause is null ? null : clause.Value with { Kind = SpeakableBoundaryKind.Clause };
    }

    private static Boundary? LastBoundary(string text, string punctuation)
    {
        for (var index = text.Length - 1; index >= 0; index--)
        {
            if (punctuation.Contains(text[index], StringComparison.Ordinal))
            {
                return new Boundary(index + 1, SpeakableBoundaryKind.Unknown, WasForced: false);
            }
        }

        return null;
    }

    private static bool EndsWithDanglingWord(string text)
    {
        var matches = WordRegex().Matches(text);
        if (matches.Count == 0)
        {
            return true;
        }

        return BadEndingWords.Contains(matches[^1].Value);
    }

    private static int WordCount(string text) => WordRegex().Matches(text).Count;

    private static string NormalizeDeltaWhitespace(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string Preview(string text)
        => text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 77), "...");

    private readonly record struct Boundary(int EndIndex, SpeakableBoundaryKind Kind, bool WasForced);

    [GeneratedRegex(@"\b[\w']+\b", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
