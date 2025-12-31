namespace MiniCron.Core.Services;

/// <summary>
/// Abstraction for system clock to ease testing and timezone-aware evaluations.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// UTC now.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Current time in specified time zone.
    /// </summary>
    DateTime Now(TimeZoneInfo timeZone);
}
