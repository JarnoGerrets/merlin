using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class CapabilitySafetyClassifier : ICapabilitySafetyClassifier
{
    public CapabilitySafetyLevel Classify(CapabilityRouteResult route)
    {
        return route.RecommendedCapability switch
        {
            "web_search" or "web_research" => CapabilitySafetyLevel.ExternalRequest,
            "file_access" or "email" or "calendar" or "memory_lookup" => CapabilitySafetyLevel.PrivateRead,
            "codex_research" => route.RequiresExternalInfo
                ? CapabilitySafetyLevel.ExternalRequest
                : CapabilitySafetyLevel.SafeReadonly,
            "codex_implementation" => CapabilitySafetyLevel.RequiresConfirmation,
            "software_installation" => CapabilitySafetyLevel.Privileged,
            "destructive_file_action" or "destructive_file_actions" => CapabilitySafetyLevel.Destructive,
            "system_settings" => CapabilitySafetyLevel.Privileged,
            _ => CapabilitySafetyLevel.SafeReadonly
        };
    }
}
