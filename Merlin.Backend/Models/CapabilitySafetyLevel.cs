namespace Merlin.Backend.Models;

public enum CapabilitySafetyLevel
{
    SafeReadonly,
    PrivateRead,
    ExternalRequest,
    RequiresConfirmation,
    Destructive,
    Privileged,
    Unsupported
}
