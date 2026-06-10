namespace Merlin.Backend.Services;

public interface ISystemResourceProvider
{
    DateTimeOffset GetCurrentLocalTime();

    DateOnly GetCurrentLocalDate();

    TimeZoneInfo GetLocalTimeZone();
}
