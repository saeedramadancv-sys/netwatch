namespace NetWatch.Infrastructure.Monitoring;

/// <summary>
/// Tuning for the background monitoring loop, bound from the <c>Monitoring</c>
/// configuration section.
/// </summary>
public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>
    /// Turns the scheduler off entirely. Integration tests and the migration container
    /// run with this false so they do not start probing the network.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often the scheduler looks for due probes. This is the resolution of the
    /// system, not the check frequency: a probe with a 60s interval still runs every
    /// 60s, it is simply noticed within this many seconds of becoming due.
    /// </summary>
    public int TickSeconds { get; set; } = 5;

    /// <summary>
    /// Upper bound on probes running at the same time. Each in-flight check holds a
    /// socket and a database scope, so this is the knob that keeps a thousand monitored
    /// devices from exhausting the connection pool.
    /// </summary>
    public int MaxConcurrentChecks { get; set; } = 20;

    /// <summary>
    /// Cap on how many due probes a single tick will start. Prevents a thundering herd
    /// after downtime, when every probe in the system is overdue at once.
    /// </summary>
    public int MaxChecksPerTick { get; set; } = 200;

    /// <summary>How long raw check results are kept before the retention job deletes them.</summary>
    public int ResultRetentionDays { get; set; } = 30;

    /// <summary>How often the retention job runs.</summary>
    public int RetentionSweepHours { get; set; } = 6;

    /// <summary>Rows deleted per statement, so retention never holds a long lock.</summary>
    public int RetentionBatchSize { get; set; } = 5_000;
}
