namespace Merlin.Backend.Services.Feedback;

[Flags]
public enum FeedbackOutputMode
{
    None = 0,
    Speech = 1,
    VisualState = 2,
    ActivityText = 4,
    Notification = 8
}
