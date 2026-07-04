namespace Merlin.Backend.Services.Vision;

public enum VisionHealthState
{
    Disabled,
    Idle,
    Starting,
    Ready,
    Tracking,
    Faulted,
    Stopped
}
