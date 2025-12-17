namespace MiniCron.Core.Helpers;

public static class CronHelper
{
    /// <summary>
    /// Validates a cron expression and throws an exception if it is invalid.
    /// </summary>
    /// <param name="cronExpression">
    /// A cron expression in the format "min hour day month weekday" (e.g., "*/5 * * * *").
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when cronExpression is null or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when the cron expression format is invalid.</exception>
    public static void ValidateCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentNullException(nameof(cronExpression), "Cron expression cannot be null or empty.");
        }

        var parts = cronExpression.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            throw new ArgumentException(
                $"Cron expression must have exactly 5 fields (min hour day month weekday), but got {parts.Length} field(s): '{cronExpression}'",
                nameof(cronExpression));
        }

        // Validate each field
        ValidateField(parts[0], "minute", 0, 59, cronExpression);
        ValidateField(parts[1], "hour", 0, 23, cronExpression);
        ValidateField(parts[2], "day", 1, 31, cronExpression);
        ValidateField(parts[3], "month", 1, 12, cronExpression);
        ValidateField(parts[4], "weekday", 0, 6, cronExpression);
    }

    /// <summary>
    /// Validates a single field of a cron expression.
    /// </summary>
    private static void ValidateField(string field, string fieldName, int minValue, int maxValue, string fullExpression)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException($"The {fieldName} field cannot be empty in cron expression: '{fullExpression}'");
        }

        // Wildcard is always valid
        if (field == "*") return;

        // Step syntax: "*/n"
        if (field.Contains('/'))
        {
            var split = field.Split('/');
            if (split.Length != 2)
            {
                throw new ArgumentException(
                    $"Invalid step syntax in {fieldName} field '{field}'. Expected format: '*/n' in cron expression: '{fullExpression}'");
            }

            if (split[0] != "*")
            {
                throw new ArgumentException(
                    $"Invalid step syntax in {fieldName} field '{field}'. Step syntax must start with '*' (e.g., '*/5') in cron expression: '{fullExpression}'");
            }

            if (!int.TryParse(split[1], out int step))
            {
                throw new ArgumentException(
                    $"Invalid step value in {fieldName} field '{field}'. Step must be a valid integer in cron expression: '{fullExpression}'");
            }

            if (step <= 0)
            {
                throw new ArgumentException(
                    $"Invalid step value in {fieldName} field '{field}'. Step must be greater than zero in cron expression: '{fullExpression}'");
            }

            var maxStep = maxValue - minValue + 1;
            if (step > maxStep)
            {
                throw new ArgumentException(
                    $"Invalid step value in {fieldName} field '{field}'. Step must be less than or equal to {maxStep} for the allowed range {minValue}-{maxValue} in cron expression: '{fullExpression}'");
            }
            return;
        }

        // List: "1,2,3"
        if (field.Contains(','))
        {
            var values = field.Split(',');
            foreach (var value in values)
            {
                var trimmedValue = value.Trim();

                if (string.IsNullOrEmpty(trimmedValue))
                {
                    throw new ArgumentException(
                        $"Empty value in {fieldName} field list is not allowed in cron expression: '{fullExpression}'");
                }

                if (!int.TryParse(trimmedValue, out int intValue))
                {
                    throw new ArgumentException(
                        $"Invalid list value '{trimmedValue}' in {fieldName} field. All values must be integers in cron expression: '{fullExpression}'");
                }
                if (intValue < minValue || intValue > maxValue)
                {
                    throw new ArgumentException(
                        $"Value '{intValue}' in {fieldName} field is out of range. Expected {minValue}-{maxValue} in cron expression: '{fullExpression}'");
                }
            }
            return;
        }

        // Range: "9-17"
        if (field.Contains('-'))
        {
            var split = field.Split('-');
            if (split.Length != 2)
            {
                throw new ArgumentException(
                    $"Invalid range syntax in {fieldName} field '{field}'. Expected format: 'start-end' in cron expression: '{fullExpression}'");
            }

            if (!int.TryParse(split[0], out int start) || !int.TryParse(split[1], out int end))
            {
                throw new ArgumentException(
                    $"Invalid range values in {fieldName} field '{field}'. Both start and end must be integers in cron expression: '{fullExpression}'");
            }

            if (start < minValue || start > maxValue || end < minValue || end > maxValue)
            {
                throw new ArgumentException(
                    $"Range values in {fieldName} field '{field}' are out of range. Expected {minValue}-{maxValue} in cron expression: '{fullExpression}'");
            }

            if (start > end)
            {
                throw new ArgumentException(
                    $"Invalid range in {fieldName} field '{field}'. Start value must be less than or equal to end value in cron expression: '{fullExpression}'");
            }

            return;
        }

        // Exact value: "42"
        if (!int.TryParse(field, out int exactValue))
        {
            throw new ArgumentException(
                $"Invalid value '{field}' in {fieldName} field. Expected an integer, wildcard (*), list (e.g., 1,2,3), range (e.g., 9-17), or step (e.g., */5) in cron expression: '{fullExpression}'");
        }

        if (exactValue < minValue || exactValue > maxValue)
        {
            throw new ArgumentException(
                $"Value '{exactValue}' in {fieldName} field is out of range. Expected {minValue}-{maxValue} in cron expression: '{fullExpression}'");
        }
    }

    /// <summary>
    /// Verify if a given DateTime matches the provided cron expression.
    /// </summary>
    /// <param name="cronExpression">
    /// A cron expression in the format "min hour day month weekday" (e.g., "*/5 * * * *").
    /// </param>
    /// <param name="time">
    /// The DateTime to check against the cron expression.
    /// </param>
    /// <returns>
    /// true if the DateTime matches the cron expression; false if the expression is invalid or does not match.
    /// Note: This method returns false for invalid cron expressions without throwing exceptions.
    /// For validation with detailed error messages, use <see cref="ValidateCronExpression"/> before calling this method.
    /// </returns>
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
    /// <returns>true if the field value matches the cron expression field, false otherwise.</returns>
    private static bool CheckField(string field, int value)
    {
        if (field == "*") return true;

        // Step syntax: "*/5" means every 5 units 
        if (field.Contains('/'))
        {
            var split = field.Split('/');
            if (split.Length == 2 && split[0] == "*" && int.TryParse(split[1], out int step) && step > 0)
                return value % step == 0;
        }

        // List : "1,2,3"
        if (field.Contains(','))
        {
            return field.Split(',')
                .Any(s => int.TryParse(s, out int num) && num == value);
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
