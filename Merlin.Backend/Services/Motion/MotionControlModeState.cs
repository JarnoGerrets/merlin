namespace Merlin.Backend.Services.Motion;

public enum MotionControlModeState
{
    Disabled = 0,
    Enabling = 1,
    Enabled = 2,
    SwitchingProfile = 3,
    Disabling = 4,
    Faulted = 5
}
