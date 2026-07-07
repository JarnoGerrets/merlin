namespace Merlin.Backend.Services.Context.ActiveSurface;

public interface IBrowserMediaCommandNormalizer
{
    BrowserMediaCommandMatch? TryMatchExplicit(string normalizedText);

    BrowserMediaCommandMatch? TryMatchAmbiguous(
        string normalizedText,
        ActiveSurfaceSnapshot activeSurface);
}
