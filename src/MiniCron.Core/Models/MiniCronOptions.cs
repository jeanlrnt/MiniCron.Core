namespace MiniCron.Core.Models;

public enum CronGranularity
{
    Minute,
    Second
}

/// <summary>
/// Configuration options for MiniCron behavior.
/// </summary>
public class MiniCronOptions
{
    /// <summary>
    /// Time zone in which cron expressions are evaluated. Defaults to local time.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    /// <summary>
    /// Granularity for the scheduler: minute or second.
    /// </summary>
    public CronGranularity Granularity { get; set; } = CronGranularity.Minute;

    /// <summary>
    /// Maximum number of concurrently running jobs. Default is 10.
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Optional default timeout applied to jobs if they don't specify one.
    /// </summary>
    public TimeSpan? DefaultJobTimeout { get; set; } = null;

    /// <summary>
    /// If true, the hosted service will wait for in-flight jobs on shutdown.
    /// </summary>
    public bool WaitForJobsOnShutdown { get; set; } = true;
}
