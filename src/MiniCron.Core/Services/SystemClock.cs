namespace MiniCron.Core.Services;

public class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now(TimeZoneInfo? timeZone)
    {
        return timeZone == null ? DateTime.Now : TimeZoneInfo.ConvertTime(DateTime.UtcNow, timeZone);
    }
}
