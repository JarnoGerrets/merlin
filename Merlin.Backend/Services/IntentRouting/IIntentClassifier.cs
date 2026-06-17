using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public interface IIntentClassifier
{
    Task<IntentClassificationResult> ClassifyAsync(
        NormalizedInput input,
        IReadOnlyList<CapabilityCandidate> candidates,
        CancellationToken cancellationToken = default);
}
