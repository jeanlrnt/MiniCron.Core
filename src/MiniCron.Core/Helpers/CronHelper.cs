namespace MiniCron.Core.Helpers;

public static class CronHelper
{
    /// <summary>
    /// Verify if a given DateTime matches the provided cron expression.
    /// </summary>
    /// <param name="cronExpression">
    /// A cron expression in the format "min hour day month weekday" (e.g., "*/5 * * * *").
    /// </param>
    /// <param name="time">
    /// The DateTime to check against the cron expression.
    /// </param>
    /// <returns></returns>
    public static bool IsDue(string cronExpression, DateTime time)
    {
        var parts = cronExpression.Split(' ');
        if (parts.Length != 5) return false;

        return CheckField(parts[0], time.Minute) &&
               CheckField(parts[1], time.Hour) &&
               CheckField(parts[2], time.Day) &&
               CheckField(parts[3], time.Month) &&
               CheckField(parts[4], (int)time.DayOfWeek);
    }

    /// <summary>
    /// Check if a single field of the cron expression matches the given value.
    /// </summary>
    /// <param name="field">
    /// A single field from the cron expression (minute, hour, day, month, or weekday).
    /// </param>
    /// <param name="value">
    /// The corresponding value from the DateTime to check.
    /// </param>
    /// <returns></returns>
    private static bool CheckField(string field, int value)
    {
        if (field == "*") return true;

        // Pas : "*/5 
        if (field.Contains('/'))
        {
            var split = field.Split('/');
            if (int.TryParse(split[1], out int step))
                return value % step == 0;
        }

        // List : "1,2,3"
        if (field.Contains(','))
        {
            return field.Split(',').Select(int.Parse).Contains(value);
        }

        // Interval : "9-17"
        if (field.Contains('-'))
        {
            var split = field.Split('-');
            if (int.TryParse(split[0], out int start) && int.TryParse(split[1], out int end))
                return value >= start && value <= end;
        }

        // Exact : "42"
        return int.TryParse(field, out int exact) && exact == value;
    }
}