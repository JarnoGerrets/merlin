using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ICapabilityClassifier
{
    bool MissingCapabilityDetectionEnabled { get; }

    int SupportedCapabilityCount { get; }

    IntentParseResult Classify(string message);
}
