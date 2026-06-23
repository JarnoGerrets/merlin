using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class TrustedCommandIntentParser : IIntentParser
{
    // Trusted command mappings are quarantined. This parser remains available
    // for direct diagnostics/tests, but is not wired into active routing by default.
    private readonly ITrustedCommandStore _trustedCommandStore;

    public TrustedCommandIntentParser(ITrustedCommandStore trustedCommandStore)
    {
        _trustedCommandStore = trustedCommandStore;
    }

    public Task<IntentParseResult> ParseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mapping = _trustedCommandStore.FindByCommand(message);
        if (mapping is null)
        {
            return Task.FromResult(new IntentParseResult
            {
                OriginalMessage = message
            });
        }

        return Task.FromResult(new IntentParseResult
        {
            Intent = mapping.Intent,
            NormalizedCommand = mapping.NormalizedCommand,
            Confidence = 1.0,
            OriginalMessage = message,
            ParserUsed = nameof(TrustedCommandIntentParser)
        });
    }
}
