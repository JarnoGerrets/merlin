using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IIntentFallbackClassifier
{
    IntentParseResult Classify(string message);
}
