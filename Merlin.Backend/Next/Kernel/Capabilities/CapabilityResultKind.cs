namespace Merlin.Backend.Next.Kernel.Capabilities;

public enum CapabilityResultKind
{
    NotHandled,
    Succeeded,
    Failed,
    RequiresConfirmation,
    Blocked
}
