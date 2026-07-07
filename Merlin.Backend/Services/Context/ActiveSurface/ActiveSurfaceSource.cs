namespace Merlin.Backend.Services.Context.ActiveSurface;

public enum ActiveSurfaceSource
{
    StartupDefault = 0,
    FrontendState = 1,
    FrontendFocus = 2,
    BrowserWorkspace = 3,
    UserCommand = 4,
    TimeoutFallback = 5,
    Unknown = 99
}
