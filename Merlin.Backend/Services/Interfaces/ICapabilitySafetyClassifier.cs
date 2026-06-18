using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ICapabilitySafetyClassifier
{
    CapabilitySafetyLevel Classify(CapabilityRouteResult route);
}
