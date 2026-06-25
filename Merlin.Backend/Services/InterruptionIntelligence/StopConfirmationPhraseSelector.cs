using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class StopConfirmationPhraseSelector : IStopConfirmationPhraseSelector
{
    public static readonly string[] DefaultPhrases =
    [
        "Got it, I'll stop.",
        "Okay, stopping.",
        "Understood, I'll be quiet.",
        "I'll shut up, sir."
    ];

    private readonly InterruptionHandlingOptions _options;
    private int _nextIndex = -1;

    public StopConfirmationPhraseSelector(IOptions<InterruptionHandlingOptions> options)
    {
        _options = options.Value;
    }

    public string SelectPhrase()
    {
        var phrases = (_options.StopConfirmationPhrases is { Length: > 0 }
                ? _options.StopConfirmationPhrases
                : DefaultPhrases)
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Select(phrase => phrase.Trim())
            .ToArray();
        if (phrases.Length == 0)
        {
            phrases = DefaultPhrases;
        }

        var index = Interlocked.Increment(ref _nextIndex);
        return phrases[Math.Abs(index % phrases.Length)];
    }
}
