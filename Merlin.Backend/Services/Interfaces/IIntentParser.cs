using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IIntentParser
{
    Task<IntentParseResult> ParseAsync(
        string message,
        CancellationToken cancellationToken = default);
}
