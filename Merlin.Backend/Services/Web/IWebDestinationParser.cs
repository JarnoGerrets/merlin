namespace Merlin.Backend.Services.Web;

using Merlin.Backend.Services.Context.ActiveSurface;

public interface IWebDestinationParser
{
    WebDestinationCommand? TryParse(string text);

    WebDestinationCommand? TryParse(string text, ActiveSurfaceSnapshot? activeSurface);
}
