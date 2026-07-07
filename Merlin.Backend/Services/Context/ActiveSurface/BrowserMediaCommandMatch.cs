namespace Merlin.Backend.Services.Context.ActiveSurface;

public sealed record BrowserMediaCommandMatch(
    string Capability,
    double Confidence,
    string Reason);
