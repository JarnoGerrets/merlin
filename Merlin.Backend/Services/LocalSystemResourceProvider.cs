namespace Merlin.Backend.Services;

public sealed class LocalSystemResourceProvider : ISystemResourceProvider
{
    public DateTimeOffset GetCurrentLocalTime()
    {
        return DateTimeOffset.Now;
    }

    public DateOnly GetCurrentLocalDate()
    {
        return DateOnly.FromDateTime(DateTime.Now);
    }

    public TimeZoneInfo GetLocalTimeZone()
    {
        return TimeZoneInfo.Local;
    }
}
